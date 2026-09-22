using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace Core.Runtime.Rendering.Streamline
{
    /// 捕获指定验证相机的 URP 输入，可显式将处理结果交回后处理链。
    public sealed class StreamlineCaptureFeature : ScriptableRendererFeature
    {
        /// 当前显式创建的 Editor 验证会话；为空时不录入任何 Pass。
        public static CaptureSession Session { get; set; }

        private CapturePass capturePass;
        private PresentationPass presentationPass;

        /// 创建输入捕获 Pass。
        public override void Create()
        {
            capturePass = new CapturePass();
            presentationPass = new PresentationPass();
        }

        /// <summary>仅为当前验证相机添加捕获 Pass。</summary>
        /// <param name="renderer">当前相机的 URP Renderer。</param>
        /// <param name="renderingData">用于匹配相机的帧数据。</param>
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.camera == StreamlineCameraSession.PresentationCamera)
            {
                renderer.EnqueuePass(presentationPass);
                return;
            }
            if (Session == null || !Session.IsCapturing || Session.Camera != renderingData.cameraData.camera) return;
            renderer.EnqueuePass(capturePass);
        }

        /// 输入纹理由调用者持有；本对象只管理 RTHandle 包装，并在 GPU 完成后释放包装。
        public sealed class CaptureSession : IDisposable
        {
            /// 当前验证相机。
            public Camera Camera { get; }
            /// 是否接受新的捕获；停止不会释放 GPU 资源。
            public bool IsCapturing { get; private set; } = true;
            /// 实际记录的捕获帧数。
            public int CapturedFrames { get; internal set; }
            /// URP 相机本帧的内部渲染宽度。
            public int InputWidth { get; internal set; }
            /// URP 相机本帧的内部渲染高度。
            public int InputHeight { get; internal set; }
            /// 本帧实际的 GPU 投影矩阵，不含 jitter。
            public Matrix4x4 Projection { get; internal set; }
            /// 从 URP 实际 GPU 投影矩阵恢复的输入像素 jitter。
            public Vector2 Jitter { get; internal set; }
            /// 捕获时相机的抗锯齿、后处理和采样配置。
            public string InputConfiguration { get; internal set; }
            /// 输入复制后、同一命令缓冲内的验证回调；不得在此释放输入。
            public Action<CommandBuffer, CaptureSession> AfterCapture { get; set; }
            /// 将输出交给当前验证相机的后处理；仅支持无相机栈、线性缩放的离屏路径。
            public bool PublishToCamera { get; set; }
            /// 本帧是否已经把输出交给 URP。
            public bool PublishedToCamera { get; internal set; }
            /// 输入不可用时的诊断。
            public string Error { get; internal set; }
            internal readonly RTHandle Color;
            internal readonly RTHandle Depth;
            internal readonly RTHandle Motion;
            internal readonly RTHandle Output;
            private bool disposed;

            /// <summary>绑定输入捕获目标；尺寸应匹配相机内部渲染尺寸。</summary>
            /// <param name="camera">只捕获此相机。</param>
            /// <param name="color">线性 HDR 颜色目标。</param>
            /// <param name="depth">R32 浮点深度目标。</param>
            /// <param name="motion">RG 浮点运动矢量目标。</param>
            /// <param name="output">回调写入的 DLSS 输出目标。</param>
            public CaptureSession(Camera camera, RenderTexture color, RenderTexture depth, RenderTexture motion, RenderTexture output)
            {
                Camera = camera;
                Color = RTHandles.Alloc(color);
                Depth = RTHandles.Alloc(depth);
                Motion = RTHandles.Alloc(motion);
                Output = RTHandles.Alloc(output);
            }

            /// 停止录入与尚未执行的捕获回调；已提交的 GPU 工作仍须等待完成。
            public void StopCapture()
            {
                IsCapturing = false;
                AfterCapture = null;
            }

            /// 在 GPU 不再使用捕获资源后释放包装，不销毁调用者的 RenderTexture。
            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                StopCapture();
                if (ReferenceEquals(Session, this)) Session = null;
                Color.Release();
                Depth.Release();
                Motion.Release();
                Output.Release();
            }
        }

        private sealed class PresentationPass : ScriptableRenderPass
        {
            public PresentationPass()
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
                requiresIntermediateTexture = true;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var source = StreamlineCameraSession.PresentationHandle;
                if (source == null) return;
                var resources = frameData.Get<UniversalResourceData>();
                renderGraph.AddBlitPass(renderGraph.ImportTexture(source), resources.activeColorTexture,
                    Vector2.one, Vector2.zero, passName: "Streamline presentation");
            }
        }

        private sealed class CapturePass : ScriptableRenderPass
        {
            private sealed class PassData
            {
                public TextureHandle Color;
                public TextureHandle Depth;
                public TextureHandle Motion;
                public TextureHandle ColorTarget;
                public TextureHandle DepthTarget;
                public TextureHandle MotionTarget;
                public TextureHandle OutputTarget;
                public CaptureSession Session;
                public bool PublishToCamera;
                public Vector2Int OutputSize;
            }

            public CapturePass()
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
                requiresIntermediateTexture = true;
                ConfigureInput(ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Motion);
            }

            /// <summary>声明输入和外部捕获目标的依赖，并记录 GPU 复制。</summary>
            /// <param name="renderGraph">当前相机的 RenderGraph。</param>
            /// <param name="frameData">当前相机的颜色、深度及运动矢量数据。</param>
            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                CaptureSession session = Session;
                if (session == null || !session.IsCapturing) return;
                UniversalCameraData camera = frameData.Get<UniversalCameraData>();
                if (camera.camera != session.Camera) return;
                UniversalResourceData resources = frameData.Get<UniversalResourceData>();
                if (!resources.activeColorTexture.IsValid() || !resources.cameraDepthTexture.IsValid() || !resources.motionVectorColor.IsValid())
                {
                    session.Error = "URP 没有提供完整的颜色、深度、运动矢量输入。";
                    return;
                }
                session.InputWidth = camera.cameraTargetDescriptor.width;
                session.InputHeight = camera.cameraTargetDescriptor.height;
                // 此验证入口只支持自有、无相机栈的离屏相机。RenderGraph 模式下旧的
                // GetGPUProjectionMatrix(NoJitter) 是返回 default 的兼容占位，不能用于帧参数。
                Matrix4x4 unjittered = session.Camera.projectionMatrix;
                session.Projection = GL.GetGPUProjectionMatrix(unjittered, true);
                var additional = session.Camera.GetUniversalAdditionalCameraData();
                session.PublishedToCamera = false;
                var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                if (session.PublishToCamera && (session.Camera.targetTexture == null || camera.renderType != CameraRenderType.Base ||
                    additional.cameraStack.Count != 0 || session.Camera.orthographic || session.AfterCapture == null ||
                    pipeline == null || pipeline.upscalingFilter != UpscalingFilterSelection.Linear))
                {
                    session.Error = "当前输出验证需要无相机栈的离屏透视相机、处理回调与 Linear 缩放配置。";
                    return;
                }
                session.InputConfiguration = $"AA={camera.antialiasing}, post={camera.postProcessEnabled}, MSAA={camera.cameraTargetDescriptor.msaaSamples}, dynamic={session.Camera.allowDynamicResolution}, jitterScale={additional.taaSettings.jitterScale}, frame={Time.frameCount}, tonemap={VolumeManager.instance.stack.GetComponent<Tonemapping>()?.mode.value}, exposure={VolumeManager.instance.stack.GetComponent<ColorAdjustments>()?.postExposure.value}";
                Matrix4x4 jitterTransform = camera.GetProjectionMatrix() * unjittered.inverse;
                // 与 URP CalculateJitterMatrix 和 Unity DLSSIUpscaler 的像素偏移约定保持一致。
                session.Jitter = new Vector2(jitterTransform.m03 * session.InputWidth * 0.5f,
                    jitterTransform.m13 * session.InputHeight * 0.5f);
                using (var builder = renderGraph.AddUnsafePass<PassData>("Streamline validation inputs", out var data))
                {
                    data.Color = resources.activeColorTexture;
                    data.Depth = resources.cameraDepthTexture;
                    data.Motion = resources.motionVectorColor;
                    data.ColorTarget = renderGraph.ImportTexture(session.Color);
                    data.DepthTarget = renderGraph.ImportTexture(session.Depth);
                    data.MotionTarget = renderGraph.ImportTexture(session.Motion);
                    data.OutputTarget = renderGraph.ImportTexture(session.Output);
                    data.Session = session;
                    data.PublishToCamera = session.PublishToCamera;
                    data.OutputSize = new Vector2Int(session.Output.rt.width, session.Output.rt.height);
                    builder.UseTexture(data.Color, AccessFlags.Read);
                    builder.UseTexture(data.Depth, AccessFlags.Read);
                    builder.UseTexture(data.Motion, AccessFlags.Read);
                    builder.UseTexture(data.ColorTarget, AccessFlags.ReadWrite);
                    builder.UseTexture(data.DepthTarget, AccessFlags.ReadWrite);
                    builder.UseTexture(data.MotionTarget, AccessFlags.ReadWrite);
                    builder.UseTexture(data.OutputTarget, AccessFlags.Write);
                    builder.AllowPassCulling(false);
                    if (data.PublishToCamera) builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc((PassData pass, UnsafeGraphContext context) =>
                    {
                        if (!pass.Session.IsCapturing) return;
                        CommandBuffer commands = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                        Blitter.BlitCameraTexture(commands, pass.Color, pass.ColorTarget);
                        Blitter.BlitCameraTexture(commands, pass.Depth, pass.DepthTarget);
                        Blitter.BlitCameraTexture(commands, pass.Motion, pass.MotionTarget);
                        pass.Session.AfterCapture?.Invoke(commands, pass.Session);
                        if (pass.PublishToCamera)
                            commands.SetGlobalVector("_ScreenSize", new Vector4(pass.OutputSize.x, pass.OutputSize.y, 1f / pass.OutputSize.x, 1f / pass.OutputSize.y));
                        pass.Session.CapturedFrames++;
                    });
                    if (session.PublishToCamera)
                    {
                        resources.cameraColor = data.OutputTarget;
                        camera.cameraTargetDescriptor.width = data.OutputSize.x;
                        camera.cameraTargetDescriptor.height = data.OutputSize.y;
                        camera.scaledWidth = data.OutputSize.x;
                        camera.scaledHeight = data.OutputSize.y;
                        camera.renderScale = 1f;
                        // 本帧的 jitter 已经用于场景渲染；只关闭后续 TAA，保留相机下一帧的配置。
                        camera.antialiasing = AntialiasingMode.None;
                        session.PublishedToCamera = true;
                    }
                }
            }
        }
    }
}
