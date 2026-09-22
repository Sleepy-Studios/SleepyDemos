using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Core.Runtime.Rendering.Streamline
{
    /// 已接入验证的 DLSS 模式，对应锁定 SDK 的数值。
    public enum StreamlineDlssMode : uint
    {
        Quality = 3,
        Dlaa = 6
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
