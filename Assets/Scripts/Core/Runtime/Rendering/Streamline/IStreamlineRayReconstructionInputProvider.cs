using UnityEngine;

namespace Core.Runtime.Rendering.Streamline
{
    /// 光追渲染器提供的单帧 RR 输入；纹理由提供者拥有，本接口不启用 RR。
    public struct StreamlineRayReconstructionInputs
    {
        /// 输入分辨率的线性 HDR 含噪颜色，不能用已降噪画面冒充光追信号。
        public RenderTexture NoisyColor;
        /// 输入分辨率的硬件深度，与运动矢量使用同一相机和帧。
        public RenderTexture Depth;
        /// 包含相机及动态物体运动的屏幕空间运动矢量。
        public RenderTexture MotionVectors;
        /// 将运动矢量纹理值转换到 Streamline 归一化坐标的比例。
        public Vector2 MotionVectorScale;
        /// 线性空间的漫反射反照率，输入分辨率。
        public RenderTexture DiffuseAlbedo;
        /// 线性空间的镜面反射反照率，输入分辨率。
        public RenderTexture SpecularAlbedo;
        /// 世界空间单位法线 RGB 与粗糙度 Alpha，输入分辨率浮点纹理。
        public RenderTexture NormalRoughness;
        /// 反射运动矢量；与 SpecularHitDistance 至少提供一种。
        public RenderTexture SpecularMotionVectors;
        /// 从主表面到镜面光追命中点的世界空间距离。
        public RenderTexture SpecularHitDistance;
        /// 当前相机不含 jitter 的世界到视图矩阵。
        public Matrix4x4 WorldToView;
        /// 当前相机不含 jitter 的 GPU 投影矩阵。
        public Matrix4x4 ViewToClip;
        /// 上一帧不含 jitter 的世界到裁剪空间矩阵。
        public Matrix4x4 PreviousWorldToClip;
        /// 当前输入分辨率下的像素 jitter。
        public Vector2 Jitter;
        /// 相机跳变、尺寸变化或历史失效时为 true。
        public bool ResetHistory;
    }

    /// RR 输入扩展点；当前没有消费此接口的 RR 执行器。
    public interface IStreamlineRayReconstructionInputProvider
    {
        /// <summary>
        /// 获取指定相机当前帧的真实光追输入。实现方负责保证纹理在 GPU 消费完成前有效。
        /// </summary>
        /// <param name="camera">本帧被重建的相机，不能返回其他相机的历史数据。</param>
        /// <param name="inputs">成功时返回同一帧、同一输入尺寸的纹理与相机参数。</param>
        /// <param name="reason">未生成所需光追信号时的原因。</param>
        /// <returns>是否具备完整输入；不表示硬件支持或 RR 已启用。</returns>
        bool TryGetInputs(Camera camera, out StreamlineRayReconstructionInputs inputs, out string reason);
    }
}
