using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Core.Runtime.Rendering.Streamline
{
    /// 正式启动注册的 DLSS 入口；当前体验版本只启用 Windows Editor 后端。
    public static class StreamlineRuntime
    {
        /// 启动入口是否已注册。
        public static bool IsInitialized { get; private set; }
        /// 上次清理未能确认完成，须重启 Editor 后再创建会话。
        public static bool RequiresRestart { get; internal set; }
        /// 当前体验版本可尝试的图形后端，硬件支持仍由 SDK 查询决定。
        public static bool IsBackendSupported => Application.platform == RuntimePlatform.WindowsEditor &&
            (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D12 || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Vulkan);
        internal static StreamlineCameraSession ActiveSession;

        /// 注册入口，不在启动时改变相机或启用效果。
        public static void Initialize()
        {
            IsInitialized = true;
        }
    }

    /// 场景持有的离屏世界相机会话；UI 使用全尺寸输出，独立于低分辨率世界输入。
    public sealed class StreamlineCameraSession : IDisposable
    {
        private const uint Viewport = 900;
        private readonly Camera camera;
        private readonly UniversalAdditionalCameraData cameraData;
        private readonly RenderPipelineAsset originalGraphicsPipeline;
        private readonly RenderPipelineAsset originalQualityPipeline;
        private readonly RenderTexture originalTarget;
        private readonly AntialiasingMode originalAntialiasing;
        private readonly bool originalPostProcessing;
        private readonly bool originalEnabled;
        private readonly List<RenderTexture> textures = new List<RenderTexture>();
        private readonly StreamlineCameraHistory history = new StreamlineCameraHistory();
        private StreamlineCaptureFeature.CaptureSession capture;
        private bool sdkTouched;
        private bool disposed;
        private uint frameIndex;
        private ulong lastRequest;
        private ulong firstRequest;
        private ulong inspectedRequest;
        private RenderTexture worldTarget;

        /// 实际生效的 DLSS 模式；null 表示关闭。
        public StreamlineDlssMode? Mode { get; private set; }
        /// 输出纹理，不拥有它的 UI 不得释放。
        public RenderTexture OutputTexture { get; private set; }
        /// 实际输入尺寸。
        public Vector2Int InputSize { get; private set; }
        /// 显示输出尺寸。
        public Vector2Int OutputSize { get; private set; }
        /// 切换期间不接受新的设置。
        public bool IsBusy { get; private set; }
        /// 当前错误；效果失败时回退到关闭。
        public string Error { get; private set; }
        /// 已录入的 DLSS 帧数。
        public int CapturedFrames => capture?.CapturedFrames ?? 0;
        /// 当前模式是否已收到成功的原生评估结果。
        public bool HasEvaluatedFrame { get; private set; }
        /// 当前目标纹理是否已完成至少一次相机渲染录入。
        public bool HasRenderedFrame { get; private set; }
        /// 纹理、设置或诊断变化。
        public event Action Changed;

        /// <summary>创建场景相机会话，使用带 StreamlineCaptureFeature 的独立管线配置。</summary>
        /// <param name="worldCamera">不属于 UI Camera Stack 的离屏世界相机。</param>
        /// <param name="pipeline">宿主提供的管线，renderScale 必须为 1，避免降低 UI 分辨率。</param>
        public StreamlineCameraSession(Camera worldCamera, UniversalRenderPipelineAsset pipeline)
        {
            if (!StreamlineRuntime.IsInitialized) throw new InvalidOperationException("DLSS 运行时尚未注册。");
            if (StreamlineRuntime.RequiresRestart) throw new InvalidOperationException("上次 DLSS 清理失败，请重启 Editor。");
            if (StreamlineRuntime.ActiveSession != null) throw new InvalidOperationException("已有 DLSS 相机会话。");
            if (worldCamera == null || pipeline == null) throw new ArgumentNullException();
            if (Mathf.Abs(pipeline.renderScale - 1) > 0.0001f) throw new ArgumentException("体验管线必须保持原生 UI 比例。", nameof(pipeline));
            camera = worldCamera;
            cameraData = worldCamera.GetUniversalAdditionalCameraData();
            if (cameraData.renderType != CameraRenderType.Base || cameraData.cameraStack.Count != 0)
                throw new ArgumentException("世界相机不能使用 Overlay Camera Stack。", nameof(worldCamera));
            originalGraphicsPipeline = GraphicsSettings.defaultRenderPipeline;
            originalQualityPipeline = QualitySettings.renderPipeline;
            originalTarget = camera.targetTexture;
            originalAntialiasing = cameraData.antialiasing;
            originalPostProcessing = cameraData.renderPostProcessing;
            originalEnabled = camera.enabled;
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            cameraData.SetRenderer(0);
            cameraData.requiresDepthTexture = true;
            cameraData.renderPostProcessing = true;
            RenderPipelineManager.endCameraRendering += OnCameraRendered;
            StreamlineRuntime.ActiveSession = this;
        }

        /// <summary>切换效果或输出尺寸；查询失败时恢复原生分辨率关闭路径。</summary>
        /// <param name="mode">null 为关闭，其余为 SDK 质量档。</param>
        /// <param name="outputSize">UI 显示所需的输出像素尺寸。</param>
        /// <returns>请求是否生效；失败原因见 Error。</returns>
        public async UniTask<bool> ApplyAsync(StreamlineDlssMode? mode, Vector2Int outputSize)
        {
            if (disposed || IsBusy) return false;
            if (outputSize.x <= 0 || outputSize.y <= 0) throw new ArgumentOutOfRangeException(nameof(outputSize));
            IsBusy = true;
            Error = null;
            Changed?.Invoke();
            bool oldResourcesReleased = false;
            try
            {
                StopProducing();
                await WaitForGpuAsync();
                if (disposed) return false;
                if (sdkTouched)
                {
                    using (var commands = new CommandBuffer { name = "Release DLSS scene viewport" })
                    {
                        if (!StreamlineDlssValidation.TryReleaseViewport(commands, Viewport, out string reason)) throw new InvalidOperationException(reason);
                        Graphics.ExecuteCommandBuffer(commands);
                    }
                    await WaitForGpuAsync();
                    if (disposed) return false;
                    RequireReport("ViewportReleased");
                }
                ReleaseTextures();
                oldResourcesReleased = true;
                OutputSize = outputSize;
                InputSize = outputSize;
                if (mode.HasValue)
                {
                    if (!StreamlineRuntime.IsBackendSupported) throw new InvalidOperationException("当前后端不支持此 DLSS 体验版本，请使用 Windows Editor 的 D3D12 或 Vulkan。");
                    ulong request;
                    using (var commands = new CommandBuffer { name = "Query DLSS scene settings" })
                    {
                        if (!StreamlineDlssValidation.TryEnqueueOptimalSettings(commands, mode.Value, outputSize, out request, out string reason)) throw new InvalidOperationException(reason);
                        Graphics.ExecuteCommandBuffer(commands);
                        sdkTouched = true;
                    }
                    await WaitForGpuAsync();
                    if (disposed) return false;
                    if (!StreamlineDlssValidation.TryGetOptimalSettings(request, out var settings, out string queryError)) throw new InvalidOperationException(queryError);
                    InputSize = settings.OptimalSize;
                }
                ConfigureTargets(mode);
                Mode = mode;
                return true;
            }
            catch (Exception exception)
            {
                Error = exception.Message;
                if (!disposed)
                {
                    StopProducing();
                    // No evaluation is submitted while configuring targets. Old work was drained above.
                    if (oldResourcesReleased && capture == null)
                    {
                        ReleaseTextures();
                        OutputSize = outputSize;
                        InputSize = outputSize;
                        ConfigureTargets(null);
                        Mode = null;
                    }
                }
                return false;
            }
            finally
            {
                IsBusy = false;
                Changed?.Invoke();
            }
        }

        private void ConfigureTargets(StreamlineDlssMode? mode)
        {
            history.Reset();
            firstRequest = lastRequest = inspectedRequest = 0;
            HasEvaluatedFrame = false;
            HasRenderedFrame = false;
            worldTarget = CreateTexture(InputSize, GraphicsFormat.R16G16B16A16_SFloat, 24);
            camera.targetTexture = worldTarget;
            camera.aspect = OutputSize.x / (float)OutputSize.y;
            cameraData.antialiasing = mode.HasValue ? AntialiasingMode.TemporalAntiAliasing : AntialiasingMode.None;
            cameraData.resetHistory = true;
            if (mode.HasValue)
            {
                var color = CreateTexture(InputSize, GraphicsFormat.R16G16B16A16_SFloat);
                var depth = CreateTexture(InputSize, GraphicsFormat.R32_SFloat);
                var motion = CreateTexture(InputSize, GraphicsFormat.R16G16_SFloat);
                var output = CreateTexture(OutputSize, GraphicsFormat.R16G16B16A16_SFloat);
                var colorPointer = color.GetNativeTexturePtr();
                var depthPointer = depth.GetNativeTexturePtr();
                var motionPointer = motion.GetNativeTexturePtr();
                var outputPointer = output.GetNativeTexturePtr();
                capture = new StreamlineCaptureFeature.CaptureSession(camera, color, depth, motion, output);
                capture.AfterCapture = (commands, session) =>
                {
                    if (session.InputWidth != InputSize.x || session.InputHeight != InputSize.y)
                    {
                        Error = "实际世界输入尺寸与 DLSS 配置不一致。";
                        session.StopCapture();
                        return;
                    }
                    var frame = new StreamlineDlssFrame
                    {
                        Viewport = Viewport, FrameIndex = frameIndex++, Mode = mode.Value,
                        InputWidth = (uint)InputSize.x, InputHeight = (uint)InputSize.y,
                        OutputWidth = (uint)OutputSize.x, OutputHeight = (uint)OutputSize.y,
                        Color = colorPointer, Depth = depthPointer,
                        Motion = motionPointer, Output = outputPointer
                    };
                    history.Apply(ref frame, camera, session.Projection, session.Jitter);
                    if (!StreamlineDlssValidation.TryEnqueue(commands, frame, out lastRequest, out string reason))
                    {
                        Error = reason;
                        session.StopCapture();
                    }
                    else if (firstRequest == 0) firstRequest = lastRequest;
                };
                StreamlineCaptureFeature.Session = capture;
                OutputTexture = output;
            }
            else OutputTexture = worldTarget;
            camera.enabled = true;
        }

        /// 由宿主 Update 调用，读取实际执行失败并回退。
        public void Tick()
        {
            if (disposed || IsBusy || !Mode.HasValue) return;
            if (!string.IsNullOrEmpty(capture?.Error)) Error = capture.Error;
            if (lastRequest != 0 && lastRequest != inspectedRequest && StreamlineDlssValidation.TryGetReport(out string json, out _))
            {
                var report = JsonUtility.FromJson<NativeReport>(json);
                if (firstRequest != 0 && report.requestId >= firstRequest && report.requestId <= lastRequest && report.requestId > inspectedRequest)
                {
                    inspectedRequest = report.requestId;
                    if (report.result != 0) Error = "DLSS 执行失败：" + report.stage + " (" + report.result + ")";
                    else if (report.state == "Evaluated" && !HasEvaluatedFrame)
                    {
                        HasEvaluatedFrame = true;
                        Changed?.Invoke();
                    }
                }
            }
            if (!string.IsNullOrEmpty(Error)) FallbackAsync(Error).Forget();
        }

        private async UniTaskVoid FallbackAsync(string error)
        {
            await ApplyAsync(null, OutputSize);
            Error = error;
            Changed?.Invoke();
        }

        /// 相机瞬移等情况下丢弃历史。
        public void ResetHistory()
        {
            history.Reset();
            if (cameraData != null) cameraData.resetHistory = true;
        }

        private void StopProducing()
        {
            capture?.StopCapture();
            if (camera != null) camera.enabled = false;
        }

        private void OnCameraRendered(ScriptableRenderContext context, Camera renderedCamera)
        {
            if (disposed || renderedCamera != camera || worldTarget == null || HasRenderedFrame) return;
            HasRenderedFrame = true;
            Changed?.Invoke();
        }

        private RenderTexture CreateTexture(Vector2Int size, GraphicsFormat format, int depthBits = 0)
        {
            var texture = new RenderTexture(new RenderTextureDescriptor(size.x, size.y)
            {
                graphicsFormat = format, depthBufferBits = depthBits, msaaSamples = 1,
                enableRandomWrite = true, sRGB = false
            }) { name = "Streamline scene " + format, hideFlags = HideFlags.DontSave };
            textures.Add(texture);
            if (!texture.Create()) throw new InvalidOperationException("无法创建 DLSS 场景纹理。");
            return texture;
        }

        private void ReleaseTextures()
        {
            OutputTexture = null;
            Changed?.Invoke();
            if (camera != null) camera.targetTexture = null;
            capture?.Dispose();
            capture = null;
            foreach (var texture in textures) { texture.Release(); Object.Destroy(texture); }
            textures.Clear();
            worldTarget = null;
        }

        private static async UniTask WaitForGpuAsync()
        {
            bool done = false, failed = false;
            using (var commands = new CommandBuffer { name = "Complete Streamline scene work" })
            {
                commands.RequestAsyncReadback(Texture2D.blackTexture, 0, request => { failed = request.hasError; done = true; });
                Graphics.ExecuteCommandBuffer(commands);
            }
            double deadline = Time.realtimeSinceStartupAsDouble + 30;
            while (!done && Time.realtimeSinceStartupAsDouble < deadline) await UniTask.Yield();
            if (!done || failed) throw new InvalidOperationException("GPU 完成确认失败，不能继续释放资源。");
        }

        private static void CompleteGpuSynchronously()
        {
            bool done = false, failed = false;
            using (var commands = new CommandBuffer { name = "Drain Streamline scene work" })
            {
                commands.RequestAsyncReadback(Texture2D.blackTexture, 0, request => { failed = request.hasError; done = true; });
                Graphics.ExecuteCommandBuffer(commands);
            }
            AsyncGPUReadback.WaitAllRequests();
            if (!done || failed) throw new InvalidOperationException("同步 GPU 完成确认失败。");
        }

        [Serializable]
        private sealed class NativeReport
        {
            public ulong requestId;
            public int result;
            public string state;
            public string stage;
        }

        private static void RequireReport(string expected)
        {
            if (!StreamlineDlssValidation.TryGetReport(out string json, out string reason)) throw new InvalidOperationException(reason);
            var report = JsonUtility.FromJson<NativeReport>(json);
            if (report.state != expected || report.result != 0) throw new InvalidOperationException(json);
        }

        /// 停止并同步排空 GPU 后释放；场景退出前调用，OnDestroy 可作兜底。
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            RenderPipelineManager.endCameraRendering -= OnCameraRendered;
            StopProducing();
            bool cleanupSucceeded = false;
            try
            {
                CompleteGpuSynchronously();
                if (sdkTouched)
                {
                    using (var commands = new CommandBuffer { name = "End Streamline scene session" })
                    {
                        if (!StreamlineDlssValidation.TryEndSession(commands, out string reason)) throw new InvalidOperationException(reason);
                        Graphics.ExecuteCommandBuffer(commands);
                    }
                    CompleteGpuSynchronously();
                    RequireReport("SessionEnded");
                }
                ReleaseTextures();
                cleanupSucceeded = true;
            }
            catch (Exception exception)
            {
                // Keep native-referenced textures alive if cleanup could not be confirmed.
                Debug.LogError("[Streamline] 场景清理失败，需重启 Editor：" + exception.Message);
                StreamlineRuntime.RequiresRestart = true;
            }
            finally
            {
                if (camera != null)
                {
                    camera.targetTexture = originalTarget;
                    cameraData.antialiasing = originalAntialiasing;
                    cameraData.renderPostProcessing = originalPostProcessing;
                    camera.enabled = originalEnabled;
                }
                GraphicsSettings.defaultRenderPipeline = originalGraphicsPipeline;
                QualitySettings.renderPipeline = originalQualityPipeline;
                // A failed session remains rooted so Unity textures cannot be collected while native references may exist.
                if (cleanupSucceeded && ReferenceEquals(StreamlineRuntime.ActiveSession, this)) StreamlineRuntime.ActiveSession = null;
            }
        }
    }
}
