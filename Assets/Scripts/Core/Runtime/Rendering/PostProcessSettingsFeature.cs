using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Core.Runtime.Rendering
{
    /// 在 URP 已评估场景 Volume 后过滤世界相机参数，不增加全屏绘制 Pass。
    public sealed class PostProcessSettingsFeature : ScriptableRendererFeature
    {
        /// 参数由公共服务持有，本 Feature 不分配渲染资源。
        public override void Create() { }

        /// <summary>仅处理已绑定的世界相机，UI 和其它相机不参与。</summary>
        /// <param name="renderer">当前相机 Renderer。</param>
        /// <param name="renderingData">URP 本帧相机数据。</param>
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!PostProcessRuntime.ApplyToStack(renderingData.cameraData.camera))
            {
                return;
            }
            // URP 在 AddRenderPasses 前已根据作者 Volume 判断深度需求；补上本帧临时景深。
            // 此 ref 对应真实 UniversalCameraData，不修改相机偏好或制作管线。
            if (renderingData.cameraData.postProcessEnabled
                && VolumeManager.instance.stack.GetComponent<DepthOfField>()?.IsActive() == true)
            {
                renderingData.cameraData.postProcessingRequiresDepthTexture = true;
            }
        }
    }
}
