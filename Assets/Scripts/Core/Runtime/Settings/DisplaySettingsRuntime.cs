using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Core.Runtime.Rendering;
using Core.Runtime.Rendering.Streamline;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Core.Runtime.Settings
{
    /// 显示与画质的单一应用入口；DLSS 必须先释放，才能替换管线副本。
    public static class DisplaySettingsRuntime
    {
        private static readonly FieldInfo RendererDataField = typeof(UniversalRenderPipelineAsset)
            .GetField("m_RendererDataList", BindingFlags.NonPublic | BindingFlags.Instance);

        private static GraphicsPreferences current = new();
        private static Camera camera;
        private static Camera uiCamera;
        private static AntialiasingMode authoredAntialiasing;
        private static RenderPipelineAsset authoredGraphicsPipeline;
        private static RenderPipelineAsset authoredQualityPipeline;
        private static UniversalRenderPipelineAsset authoredPipeline;
        private static PipelineCopy runtimePipeline;
        private static int authoredMipmapLimit;
        private static float authoredLodBias;
        private static int authoredVSync;
        private static int authoredFrameLimit;
        private static bool initialized;
        private static bool applying;

        /// 当前实际原生抗锯齿；DLSS 请求与实际模式继续读取 StreamlineRuntime。
        public static GraphicsAaMode EffectiveAntialiasing { get; private set; }

        /// 当前原生抗锯齿的降级原因；空表示按要求应用。
        public static string Status { get; private set; }

        /// 当前平台是否提供桌面窗口和显示模式设置。
        public static bool SupportsDesktopDisplay => !Application.isMobilePlatform;
        /// 当前显示器提供的原生模式；页面合并相同尺寸，保留当前窗口尺寸作为当前项。
        public static Resolution[] AvailableResolutions => Screen.resolutions;

        /// 显示设置或相机实际状态已更新。
        public static event Action Changed;

        /// 获取当前显示尺寸和应用偏好的独立快照，用于显示确认与回滚。
        public static GraphicsPreferences Capture()
        {
            var snapshot = current.Copy();
            snapshot.Width = Screen.width;
            snapshot.Height = Screen.height;
            snapshot.WindowMode = (int)Screen.fullScreenMode;
            snapshot.VSync = QualitySettings.vSyncCount > 0 ? 1 : 0;
            snapshot.FrameLimit = Application.targetFrameRate;
            return snapshot;
        }

        /// <summary>检测候选是否改变桌面显示尺寸或窗口模式。</summary>
        /// <param name="previous">改变前的实际快照。</param>
        /// <param name="next">待应用候选；零尺寸或 -1 模式保留现状。</param>
        /// <returns>需要限时显示确认时为 true。</returns>
        public static bool RequiresConfirmation(GraphicsPreferences previous, GraphicsPreferences next)
        {
            if (previous == null || next == null)
            {
                throw new ArgumentNullException(previous == null ? nameof(previous) : nameof(next));
            }

            return SupportsDesktopDisplay && (next.Width > 0 && (next.Width != previous.Width || next.Height != previous.Height)
                || next.WindowMode >= 0 && next.WindowMode != previous.WindowMode);
        }

        /// <summary>原子应用画质副本并重建 DLSS；取消或异常恢复应用前状态。</summary>
        /// <param name="preferences">已校验的显示与画质候选；不保存偏好。</param>
        /// <param name="presets">当前 SO 配置的档位；Current 使用平台作者配置。</param>
        /// <param name="cancellationToken">取消请求；恢复过程不受取消令牌影响。</param>
        /// <returns>完成整个运行时应用的任务；能力不足保留请求并明确降级。</returns>
        public static async UniTask ApplyAsync(GraphicsPreferences preferences, IReadOnlyList<GraphicsQualityPreset> presets,
            CancellationToken cancellationToken = default)
        {
            if (preferences == null)
            {
                throw new ArgumentNullException(nameof(preferences));
            }

            preferences.Validate();
            var mode = StreamlineRuntime.DecodePreference(preferences.DlssMode);
            if (preferences.DlssMode != 0 && !mode.HasValue)
            {
                throw new ArgumentException("DLSS 档位无效。", nameof(preferences));
            }

            var preset = ResolvePreset(preferences.Quality, presets);
            cancellationToken.ThrowIfCancellationRequested();
            if (applying)
            {
                throw new InvalidOperationException("显示设置正在应用，请等待上一请求完成。");
            }

            applying = true;
            PipelineCopy candidate = null;
            PipelineCopy previousPipeline = runtimePipeline;
            var previous = Capture();
            int previousMipmap = QualitySettings.globalTextureMipmapLimit;
            float previousLod = QualitySettings.lodBias;
            try
            {
                await StreamlineRuntime.ApplyModeAsync(null, cancellationToken);
                if (StreamlineRuntime.RequiresRestart)
                {
                    throw new InvalidOperationException(StreamlineRuntime.Status);
                }

                Initialize();
                candidate = new PipelineCopy(authoredPipeline);
                if (preset != null)
                {
                    candidate.Asset.renderScale = preset.RenderScale;
                    candidate.Asset.shadowDistance = preset.ShadowDistance;
                    candidate.Asset.msaaSampleCount = preset.MsaaSamples;
                }

                // DLSS 会话以独立 RT 和 camera.allowMSAA 禁用采样；基础管线保留原生回退所需 MSAA。
                if (preferences.Antialiasing == GraphicsAaMode.Taa)
                {
                    candidate.Asset.msaaSampleCount = 1;
                }

                SetPipeline(candidate.Asset);
                QualitySettings.globalTextureMipmapLimit = preset?.TextureMipmapLimit ?? authoredMipmapLimit;
                QualitySettings.lodBias = preset?.LodBias ?? authoredLodBias;
                ApplyWindow(preferences);
                QualitySettings.vSyncCount = preferences.VSync < 0 ? authoredVSync : preferences.VSync;
                Application.targetFrameRate = SupportsDesktopDisplay && QualitySettings.vSyncCount > 0 ? -1
                    : preferences.FrameLimit < 0 ? authoredFrameLimit : preferences.FrameLimit == 0 ? -1 : preferences.FrameLimit;

                ApplyAntialiasing(preferences.Antialiasing);
                await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, cancellationToken);
                await StreamlineRuntime.ApplyModeAsync(mode, cancellationToken);
                if (StreamlineRuntime.RequiresRestart)
                {
                    throw new InvalidOperationException(StreamlineRuntime.Status);
                }

                current = preferences.Copy();
                runtimePipeline = candidate;
                candidate = null;
                previousPipeline?.Dispose();
                PublishChanged();
            }
            catch (Exception failure)
            {
                try
                {
                    StreamlineRuntime.DetachCamera();
                    if (initialized)
                    {
                        if (previousPipeline != null)
                        {
                            SetPipeline(previousPipeline.Asset);
                        }
                        else
                        {
                            GraphicsSettings.defaultRenderPipeline = authoredGraphicsPipeline;
                            QualitySettings.renderPipeline = authoredQualityPipeline;
                        }
                    }

                    QualitySettings.globalTextureMipmapLimit = previousMipmap;
                    QualitySettings.lodBias = previousLod;
                    QualitySettings.vSyncCount = previous.VSync;
                    Application.targetFrameRate = previous.FrameLimit;
                    ApplyWindow(previous);
                    ApplyAntialiasing(current.Antialiasing);
                    if (camera != null && uiCamera != null)
                    {
                        StreamlineRuntime.BindCamera(camera, uiCamera);
                    }

                    await StreamlineRuntime.ApplyModeAsync(StreamlineRuntime.DecodePreference(current.DlssMode));
                }
                catch (Exception restoreFailure)
                {
                    throw new AggregateException("显示设置应用失败，恢复也未完成。", failure, restoreFailure);
                }

                throw;
            }
            finally
            {
                candidate?.Dispose();
                applying = false;
            }
        }

        /// <summary>公共 UI Camera Stack 绑定完成后接入当前世界相机。</summary>
        /// <param name="worldCamera">当前世界相机；空表示解绑并恢复作者状态。</param>
        /// <param name="overlayCamera">公共 Overlay 相机；世界相机非空时必须提供。</param>
        public static void BindCamera(Camera worldCamera, Camera overlayCamera)
        {
            if (worldCamera != null && overlayCamera == null)
            {
                throw new ArgumentNullException(nameof(overlayCamera));
            }

            bool changedCamera = camera != worldCamera;
            StreamlineRuntime.DetachCamera();
            if (changedCamera)
            {
                PostProcessRuntime.BindCamera(null);
                if (camera != null)
                {
                    camera.GetUniversalAdditionalCameraData().antialiasing = authoredAntialiasing;
                }
            }

            camera = worldCamera;
            uiCamera = overlayCamera;
            if (worldCamera != null)
            {
                if (changedCamera)
                {
                    authoredAntialiasing = camera.GetUniversalAdditionalCameraData().antialiasing;
                }
                ApplyAntialiasing(current.Antialiasing);
                PostProcessRuntime.BindCamera(worldCamera);
                StreamlineRuntime.BindCamera(worldCamera, overlayCamera);
            }

            PublishChanged();
        }

        private static void PublishChanged()
        {
            if (Changed == null)
            {
                return;
            }

            foreach (Action callback in Changed.GetInvocationList())
            {
                try
                {
                    callback();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

        /// <summary>查询当前相机能否使用指定原生抗锯齿。</summary>
        /// <param name="mode">用户请求的原生抗锯齿。</param>
        /// <param name="reason">当前配置不能使用时的原因。</param>
        /// <returns>当前相机可使用时为 true；没有相机时保留请求等待绑定。</returns>
        public static bool CanUseAntialiasing(GraphicsAaMode mode, out string reason)
        {
            reason = null;
            if (mode == GraphicsAaMode.Taa && camera != null)
            {
                if (camera.GetUniversalAdditionalCameraData().cameraStack.Count > 0)
                {
                    reason = "当前公共 UI 相机栈不支持原生 TAA，使用 SMAA。";
                }
                else if (camera.orthographic || camera.stereoEnabled)
                {
                    reason = "当前正交或 XR 相机不支持原生 TAA，使用 SMAA。";
                }
            }

            return reason == null;
        }

        /// 退出公共服务时恢复作者管线和相机配置，释放所有运行时副本。
        public static void Shutdown()
        {
            StreamlineRuntime.ApplyModeAsync(null).Forget();
            BindCamera(null, null);
            if (initialized)
            {
                GraphicsSettings.defaultRenderPipeline = authoredGraphicsPipeline;
                QualitySettings.renderPipeline = authoredQualityPipeline;
                QualitySettings.globalTextureMipmapLimit = authoredMipmapLimit;
                QualitySettings.lodBias = authoredLodBias;
                QualitySettings.vSyncCount = authoredVSync;
                Application.targetFrameRate = authoredFrameLimit;
            }

            runtimePipeline?.Dispose();
            runtimePipeline = null;
            initialized = false;
            authoredPipeline = null;
            current = new GraphicsPreferences();
            Status = null;
            EffectiveAntialiasing = GraphicsAaMode.CameraDefault;
        }

        private static void Initialize()
        {
            if (initialized)
            {
                return;
            }

            authoredPipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (authoredPipeline == null)
            {
                throw new InvalidOperationException("公共画质设置需要 URP 管线。");
            }

            authoredGraphicsPipeline = GraphicsSettings.defaultRenderPipeline;
            authoredQualityPipeline = QualitySettings.renderPipeline;
            authoredMipmapLimit = QualitySettings.globalTextureMipmapLimit;
            authoredLodBias = QualitySettings.lodBias;
            authoredVSync = QualitySettings.vSyncCount;
            authoredFrameLimit = Application.targetFrameRate;
            initialized = true;
        }

        private static GraphicsQualityPreset ResolvePreset(GraphicsQualityLevel level, IReadOnlyList<GraphicsQualityPreset> presets)
        {
            if (level == GraphicsQualityLevel.Current)
            {
                return null;
            }

            GraphicsQualityPreset found = null;
            if (presets != null)
            {
                foreach (var item in presets)
                {
                    if (item == null || item.Level != level)
                    {
                        continue;
                    }

                    if (found != null)
                    {
                        throw new ArgumentException("画质档位重复。", nameof(presets));
                    }

                    item.Validate();
                    found = item;
                }
            }

            return found != null ? found : throw new ArgumentException("缺少选定档位的 SO 画质预设。", nameof(presets));
        }

        private static void SetPipeline(UniversalRenderPipelineAsset asset)
        {
            GraphicsSettings.defaultRenderPipeline = asset;
            QualitySettings.renderPipeline = asset;
        }

        private static void ApplyWindow(GraphicsPreferences preferences)
        {
            if (!SupportsDesktopDisplay || preferences.Width == 0 && preferences.WindowMode < 0)
            {
                return;
            }

            int width = preferences.Width > 0 ? preferences.Width : Screen.width;
            int height = preferences.Height > 0 ? preferences.Height : Screen.height;
            var mode = preferences.WindowMode >= 0 ? (FullScreenMode)preferences.WindowMode : Screen.fullScreenMode;
            Screen.SetResolution(width, height, mode);
        }

        private static void ApplyAntialiasing(GraphicsAaMode requested)
        {
            var effective = CanUseAntialiasing(requested, out string reason) ? requested : GraphicsAaMode.Smaa;
            Status = reason;
            EffectiveAntialiasing = effective;
            if (camera == null)
            {
                return;
            }

            var data = camera.GetUniversalAdditionalCameraData();
            switch (effective)
            {
                case GraphicsAaMode.CameraDefault:
                    data.antialiasing = authoredAntialiasing;
                    break;
                case GraphicsAaMode.Off:
                    data.antialiasing = AntialiasingMode.None;
                    break;
                case GraphicsAaMode.Fxaa:
                    data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
                    break;
                case GraphicsAaMode.Smaa:
                    data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                    break;
                case GraphicsAaMode.Taa:
                    data.antialiasing = AntialiasingMode.TemporalAntiAliasing;
                    break;
            }
        }

        private sealed class PipelineCopy : IDisposable
        {
            private readonly List<Object> owned = new();

            internal UniversalRenderPipelineAsset Asset { get; private set; }

            internal PipelineCopy(UniversalRenderPipelineAsset source)
            {
                if (RendererDataField == null)
                {
                    throw new NotSupportedException("当前 URP 缺少 RendererData 副本接入字段，未修改制作资产。");
                }

                try
                {
                    Asset = Object.Instantiate(source);
                    Asset.hideFlags = HideFlags.HideAndDontSave;
                    var renderers = new ScriptableRendererData[source.rendererDataList.Length];
                    for (int index = 0; index < renderers.Length; index++)
                    {
                        var template = source.rendererDataList[index];
                        if (template == null)
                        {
                            continue;
                        }

                        var renderer = Object.Instantiate(template);
                        renderer.hideFlags = HideFlags.HideAndDontSave;
                        renderers[index] = renderer;
                        owned.Add(renderer);
                        renderer.rendererFeatures.Clear();
                        foreach (var feature in template.rendererFeatures)
                        {
                            // Demo 的临时 Feature 由其宿主在新管线上重装，不能复制旧会话的纸印状态。
                            if (feature == null || (feature.hideFlags & HideFlags.DontSave) != 0)
                            {
                                continue;
                            }

                            var copy = Object.Instantiate(feature);
                            copy.hideFlags = HideFlags.HideAndDontSave;
                            renderer.rendererFeatures.Add(copy);
                            owned.Add(copy);
                        }

                        var filter = ScriptableObject.CreateInstance<PostProcessSettingsFeature>();
                        filter.hideFlags = HideFlags.HideAndDontSave;
                        renderer.rendererFeatures.Add(filter);
                        owned.Add(filter);
                        renderer.SetDirty();
                    }

                    // URP 17.3 只公开只读列表；仅替换本次管线副本中的固定序列化字段。
                    RendererDataField.SetValue(Asset, renderers);
                }
                catch
                {
                    Dispose();
                    throw;
                }
            }

            public void Dispose()
            {
                CoreUtils.Destroy(Asset);
                Asset = null;
                foreach (var value in owned)
                {
                    CoreUtils.Destroy(value);
                }

                owned.Clear();
            }
        }
    }
}
