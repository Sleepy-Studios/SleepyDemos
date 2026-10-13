using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.Universal.Internal;

namespace Hotfix.KagitKart
{
    /// 原作纸印后处理宿主；只给本 Demo 的世界相机临时安装原生全屏 Pass。
    public sealed class KagitKartPostProcess : MonoBehaviour
    {
        private readonly Dictionary<UniversalRendererData, Installation> installations = new();
        private Camera worldCamera;
        private Material material;
        private RenderPipelineAsset pipeline;
        private bool compatibilityMode;
        private UniversalAdditionalCameraData cameraData;
        private CameraOverrideOption previousColorOption;
        private bool ownsColorOption;
        private readonly List<ScreenSpaceAmbientOcclusion> ambientFeatures = new();
        private readonly List<ScreenSpaceAmbientOcclusion> temporarilyDisabledAmbient = new();

        private sealed class Installation
        {
            public KagitKartPostFeature feature;
            public CopyDepthMode copyDepthMode;
        }

        /// <summary>绑定世界 Base 相机并克隆纸印材质；重新绑定会先恢复上次安装。</summary>
        /// <param name="camera">本 Demo 的透视世界相机；其它相机不会执行纸印 Pass。</param>
        /// <param name="template">原作参数与纸纤维纹理所在材质；不会修改该资产。</param>
        public void Initialize(Camera camera, Material template)
        {
            if (camera == null || camera.orthographic || template == null)
            {
                throw new ArgumentException("纸印后处理需要透视世界相机和原作材质。");
            }
            Release();
            worldCamera = camera;
            material = new Material(template) { hideFlags = HideFlags.HideAndDontSave };
            if (isActiveAndEnabled)
            {
                SetPipeline(GraphicsSettings.currentRenderPipeline);
            }
        }

        /// <summary>沿用环境材质已有的线性墨色，避免再次执行 sRGB 解码。</summary>
        /// <param name="linearInk">当前环境 Paper 材质的 uInk 数值。</param>
        public void SetInk(Color linearInk)
        {
            if (material != null)
            {
                material.SetVector("uInk", linearInk);
            }
        }

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += BeginCamera;
            RenderPipelineManager.endCameraRendering += EndCamera;
            if (material != null)
            {
                SetPipeline(GraphicsSettings.currentRenderPipeline);
            }
        }

        private void Update()
        {
            bool compatible = GraphicsSettings.TryGetRenderPipelineSettings<RenderGraphSettings>(out var settings) && settings.enableRenderCompatibilityMode;
            if (material != null && (pipeline != GraphicsSettings.currentRenderPipeline || compatible != compatibilityMode))
            {
                SetPipeline(GraphicsSettings.currentRenderPipeline);
            }
        }

        private void SetPipeline(RenderPipelineAsset next)
        {
            Uninstall();
            pipeline = next;
            compatibilityMode = GraphicsSettings.TryGetRenderPipelineSettings<RenderGraphSettings>(out var settings) && settings.enableRenderCompatibilityMode;
            if (next is not UniversalRenderPipelineAsset universal)
            {
                return;
            }
            if (compatibilityMode)
            {
                Debug.LogWarning("KagitKart 原作纸印仅支持 RenderGraph；兼容模式下已关闭，避免透明幽灵深度缺失。", this);
                return;
            }
            foreach (var data in universal.rendererDataList)
            {
                if (data is not UniversalRendererData renderer || installations.ContainsKey(renderer))
                {
                    continue;
                }
                foreach (var existing in renderer.rendererFeatures)
                {
                    if (existing is ScreenSpaceAmbientOcclusion ambient && !ambientFeatures.Contains(ambient))
                    {
                        ambientFeatures.Add(ambient);
                    }
                }
                var feature = ScriptableObject.CreateInstance<KagitKartPostFeature>();
                feature.name = "KagitKart 原作纸印";
                feature.hideFlags = HideFlags.HideAndDontSave;
                feature.worldCamera = worldCamera;
                feature.passMaterial = material;
                feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingPostProcessing;
                feature.fetchColorBuffer = true;
                feature.requirements = ScriptableRenderPassInput.Depth;
                installations.Add(renderer, new Installation { feature = feature, copyDepthMode = renderer.copyDepthMode });
                renderer.copyDepthMode = CopyDepthMode.AfterTransparents;
                renderer.rendererFeatures.Add(feature);
                renderer.SetDirty();
            }
        }

        private void BeginCamera(ScriptableRenderContext context, Camera camera)
        {
            if (camera != worldCamera) return;
            cameraData = worldCamera.GetUniversalAdditionalCameraData();
            previousColorOption = cameraData.requiresColorOption;
            cameraData.requiresColorOption = CameraOverrideOption.Off;
            ownsColorOption = true;
            // 纸印材质不读取 AO；只在世界相机的原生渲染作用域停用，不影响共享 Renderer 的其它相机。
            foreach (var feature in ambientFeatures)
            {
                if (feature != null && feature.isActive)
                {
                    temporarilyDisabledAmbient.Add(feature);
                    feature.SetActive(false);
                }
            }
        }

        private void EndCamera(ScriptableRenderContext context, Camera camera)
        {
            if (camera == worldCamera) RestoreCameraSettings();
        }

        private void RestoreCameraSettings()
        {
            if (ownsColorOption && cameraData != null) cameraData.requiresColorOption = previousColorOption;
            ownsColorOption = false;
            cameraData = null;
            foreach (var feature in temporarilyDisabledAmbient)
            {
                if (feature != null) feature.SetActive(true);
            }
            temporarilyDisabledAmbient.Clear();
        }

        private void Uninstall()
        {
            RestoreCameraSettings();
            ambientFeatures.Clear();
            foreach (var entry in installations)
            {
                var feature = entry.Value.feature;
                feature.SetActive(false);
                feature.worldCamera = null;
                if (entry.Key != null)
                {
                    entry.Key.rendererFeatures.Remove(feature);
                    entry.Key.copyDepthMode = entry.Value.copyDepthMode;
                    entry.Key.SetDirty();
                }
                feature.Dispose();
                CoreUtils.Destroy(feature);
            }
            installations.Clear();
            pipeline = null;
        }

        private void Release()
        {
            Uninstall();
            CoreUtils.Destroy(material);
            material = null;
            worldCamera = null;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= BeginCamera;
            RenderPipelineManager.endCameraRendering -= EndCamera;
            Uninstall();
        }

        private void OnDestroy() => Release();
    }

    internal sealed class KagitKartPostFeature : FullScreenPassRendererFeature
    {
        internal Camera worldCamera;
        private FinalDepthPass finalDepth;

        public override void Create()
        {
            finalDepth?.Dispose();
            finalDepth = new FinalDepthPass();
            base.Create();
        }

        /// <summary>严格过滤本 Demo 的世界 Base 相机，并从本次实际内部渲染尺寸刷新整像素采样。</summary>
        /// <param name="renderer">当前相机使用的 URP Renderer。</param>
        /// <param name="renderingData">当前相机和目标纹理信息；Overlay、预览及其它相机直接跳过。</param>
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            ref var cameraData = ref renderingData.cameraData;
            if (worldCamera == null || cameraData.camera != worldCamera || cameraData.renderType != CameraRenderType.Base
                || cameraData.cameraType != CameraType.Game || passMaterial == null)
            {
                return;
            }
            var descriptor = cameraData.cameraTargetDescriptor;
            float width = Mathf.Max(1, descriptor.width);
            float height = Mathf.Max(1, descriptor.height);
            float thick = Mathf.Max(1, Mathf.Floor(passMaterial.GetFloat("uOutlinePx") * height / 720f + 0.5f));
            passMaterial.SetVector("uRes", new Vector4(width, height, 1f / width, 1f / height));
            passMaterial.SetFloat("uThick", thick);
            passMaterial.SetFloat("uPR", height / Mathf.Max(1, worldCamera.pixelHeight));
            // SSAO 等早期深度用户会抢先复制；仅改 AfterTransparents 不能保证幽灵写入最终深度。
            renderer.EnqueuePass(finalDepth);
            base.AddRenderPasses(renderer, ref renderingData);
        }

        protected override void Dispose(bool disposing)
        {
            finalDepth?.Dispose();
            finalDepth = null;
            base.Dispose(disposing);
        }

        private sealed class FinalDepthPass : ScriptableRenderPass
        {
            private CopyDepthPass copy;
            private bool copiesToDepth;

            internal FinalDepthPass()
            {
                renderPassEvent = (RenderPassEvent)((int)RenderPassEvent.BeforeRenderingPostProcessing - 1);
            }

            /// <summary>将透明物体写入的最终深度复制回 URP 已声明的采样纹理。</summary>
            /// <param name="graph">本帧 RenderGraph；复制依赖和 MSAA 解析沿用官方 CopyDepthPass。</param>
            /// <param name="frameData">当前相机的纹理与渲染信息。</param>
            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                if (resources.activeDepthTexture == resources.cameraDepthTexture)
                {
                    return;
                }
                bool toDepth = graph.GetTextureDesc(resources.cameraDepthTexture).depthBufferBits != DepthBits.None;
                if (copy == null || toDepth != copiesToDepth)
                {
                    copy?.Dispose();
                    copiesToDepth = toDepth;
                    var shader = GraphicsSettings.GetRenderPipelineSettings<UniversalRendererResources>().copyDepthPS;
                    copy = new CopyDepthPass(renderPassEvent, shader, copyToDepth: toDepth, customPassName: "KagitKart Final Depth");
                }
                copy.Render(graph, frameData, resources.cameraDepthTexture, resources.activeDepthTexture, true, "KagitKart Final Depth");
            }

            internal void Dispose()
            {
                copy?.Dispose();
                copy = null;
            }
        }
    }
}
