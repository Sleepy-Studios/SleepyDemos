using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Core.Runtime.Rendering.Streamline
{
    /// DLSS 质量模式，对应锁定 SDK 的数值；实际支持须查询推荐设置。
    public enum StreamlineDlssMode : uint
    {
        Performance = 1,
        Balanced = 2,
        Quality = 3,
        UltraPerformance = 4,
        Dlaa = 6
    }

    /// SDK 返回的输入尺寸建议与范围，查询成功不表示图像处理已启用。
    public struct StreamlineDlssOptimalSettings
    {
        /// 对应的质量模式。
        public StreamlineDlssMode Mode;
        /// 查询时的输出尺寸。
        public Vector2Int OutputSize;
        /// SDK 推荐输入尺寸。
        public Vector2Int OptimalSize;
        /// SDK 允许的最小输入尺寸。
        public Vector2Int MinimumSize;
        /// SDK 允许的最大输入尺寸。
        public Vector2Int MaximumSize;

        /// <summary>把 SDK 推荐尺寸转换为 URP 17.3 的统一缩放比例，并校验截断后的输入尺寸。</summary>
        /// <param name="renderScale">应用到独立 URP 配置的比例；本方法不修改管线资产。</param>
        /// <param name="inputSize">按 URP 截断规则计算的实际输入尺寸。</param>
        /// <param name="reason">SDK 范围无效或无法用统一比例表达的原因。</param>
        /// <returns>是否得到处于 SDK 范围内的输入；宿主仍须检查实际相机捕获尺寸。</returns>
        public bool TryGetUrpRenderScale(out float renderScale, out Vector2Int inputSize, out string reason)
        {
            renderScale = 1;
            inputSize = default;
            reason = null;
            if (OutputSize.x <= 0 || OutputSize.y <= 0 || MinimumSize.x <= 0 || MinimumSize.y <= 0 ||
                OptimalSize.x < MinimumSize.x || OptimalSize.y < MinimumSize.y ||
                OptimalSize.x > MaximumSize.x || OptimalSize.y > MaximumSize.y)
            {
                reason = "SDK 推荐输入尺寸或允许范围无效。";
                return false;
            }
            double lower = Math.Max(0.1, Math.Max(MinimumSize.x / (double)OutputSize.x, MinimumSize.y / (double)OutputSize.y));
            double upper = Math.Min(1.0000001, Math.Min((MaximumSize.x + 1.0) / OutputSize.x, (MaximumSize.y + 1.0) / OutputSize.y));
            if (lower >= upper)
            {
                reason = "SDK 输入范围不能由 URP 的统一 renderScale 表达。";
                return false;
            }
            double optimalLower = Math.Max(OptimalSize.x / (double)OutputSize.x, OptimalSize.y / (double)OutputSize.y);
            double optimalUpper = Math.Min((OptimalSize.x + 1.0) / OutputSize.x, (OptimalSize.y + 1.0) / OutputSize.y);
            // Prefer exact integer dimensions when one scalar can represent both axes.
            double candidate = optimalLower < optimalUpper ? (optimalLower + optimalUpper) * 0.5 : optimalLower;
            if (candidate < lower || candidate >= upper) candidate = (lower + upper) * 0.5;
            renderScale = Mode == StreamlineDlssMode.Dlaa ? 1 : Mathf.Min(1, (float)candidate);
            inputSize = new Vector2Int(Mathf.Max(1, (int)(OutputSize.x * renderScale)), Mathf.Max(1, (int)(OutputSize.y * renderScale)));
            if (inputSize.x < MinimumSize.x || inputSize.y < MinimumSize.y || inputSize.x > MaximumSize.x || inputSize.y > MaximumSize.y ||
                (Mode == StreamlineDlssMode.Dlaa && inputSize != OutputSize))
            {
                reason = "URP 取整后的输入尺寸超出 SDK 允许范围。";
                inputSize = default;
                return false;
            }
            return true;
        }
    }

    /// 单帧原生桥接数据。纹理必须保持有效至提交后的 GPU fence 完成。
    [StructLayout(LayoutKind.Sequential)]
    public struct StreamlineDlssFrame
    {
        /// 相机历史标识，不同相机不能共用。
        public uint Viewport;
        /// 当前验证会话内单调递增的帧编号。
        public uint FrameIndex;
        /// SDK 的质量或 DLAA 模式。
        public StreamlineDlssMode Mode;
        /// 输入有效区域宽度。
        public uint InputWidth;
        /// 输入有效区域高度。
        public uint InputHeight;
        /// 输出有效区域宽度。
        public uint OutputWidth;
        /// 输出有效区域高度。
        public uint OutputHeight;
        /// 非零表示本帧重置历史。
        public uint Reset;
        /// 非零表示反向深度。
        public uint DepthInverted;
        /// 输入像素空间的水平 jitter。
        public float JitterX;
        /// 输入像素空间的垂直 jitter。
        public float JitterY;
        /// 将水平运动转换到 current-to-previous UV 位移的比例。
        public float MotionScaleX;
        /// 将垂直运动转换到 current-to-previous UV 位移的比例。
        public float MotionScaleY;
        /// 近裁面距离。
        public float NearPlane;
        /// 远裁面距离。
        public float FarPlane;
        /// 垂直视野角度，弧度。
        public float VerticalFov;
        /// 输出宽高比。
        public float AspectRatio;
        // Unity 矩阵内存按列排列，直接拷贝得到 SL 行向量约定的转置矩阵；不要重复 transpose。
        /// 不含 jitter 的 GPU 投影矩阵。
        public Matrix4x4 CameraViewToClip;
        /// 投影矩阵的逆矩阵。
        public Matrix4x4 ClipToCameraView;
        /// Unity 列向量约定下，从当前裁剪空间到上一帧裁剪空间的矩阵。
        public Matrix4x4 ClipToPreviousClip;
        /// ClipToPreviousClip 的逆矩阵。
        public Matrix4x4 PreviousClipToClip;
        /// 相机世界坐标。
        public Vector3 CameraPosition;
        /// 相机世界上向量。
        public Vector3 CameraUp;
        /// 相机世界右向量。
        public Vector3 CameraRight;
        /// 相机世界前向量。
        public Vector3 CameraForward;
        /// 线性 HDR 颜色的原生资源指针。
        public IntPtr Color;
        /// 与运动矢量同一相机、同一帧的深度资源。
        public IntPtr Depth;
        /// 包含相机运动的二维运动矢量资源，不含 jitter 位移。
        public IntPtr Motion;
        /// 可写 UAV 输出资源，不能与 Color 相同。
        public IntPtr Output;
    }
}
