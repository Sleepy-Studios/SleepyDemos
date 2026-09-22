using System;
using UnityEngine;

namespace Core.Runtime.Rendering.Streamline
{
    /// 管理单个相机的非抖动变换历史；停用、切镜或场景卸载时由宿主重置。
    public sealed class StreamlineCameraHistory
    {
        private Camera previousCamera;
        private Matrix4x4 previousViewProjection;
        private Matrix4x4 previousProjection;
        private Vector4 previousDimensions;
        private StreamlineDlssMode previousMode;
        private uint previousFrame;
        private uint previousViewport;
        private bool initialized;

        /// 丢弃历史，下一次提交将通知 SDK 重置。
        public void Reset()
        {
            initialized = false;
            previousCamera = null;
        }

        /// <summary>填充相机常量并推进历史；提交失败时调用 Reset，避免沿用未消费的历史。</summary>
        /// <param name="frame">已设置 viewport、帧编号、模式、尺寸与资源的帧；已有 Reset 请求会保留。</param>
        /// <param name="camera">本帧透视相机。</param>
        /// <param name="projection">与输入深度一致、不含 jitter 的 GPU 投影。</param>
        /// <param name="jitter">本帧输入像素空间抖动。</param>
        public void Apply(ref StreamlineDlssFrame frame, Camera camera, Matrix4x4 projection, Vector2 jitter)
        {
            if (camera == null) throw new ArgumentNullException(nameof(camera));
            if (camera.orthographic) throw new ArgumentException("当前 DLSS 相机路径只支持透视相机。", nameof(camera));
            for (int index = 0; index < 16; index++)
                if (float.IsNaN(projection[index]) || float.IsInfinity(projection[index]))
                    throw new ArgumentException("投影包含非有限值。", nameof(projection));
            if (Mathf.Abs(projection.determinant) < 0.00000001f)
                throw new ArgumentException("投影必须可逆。", nameof(projection));
            if (frame.InputWidth == 0 || frame.InputHeight == 0 || frame.OutputWidth == 0 || frame.OutputHeight == 0)
                throw new ArgumentException("输入输出尺寸必须为正数。", nameof(frame));

            Vector4 dimensions = new Vector4(frame.InputWidth, frame.InputHeight, frame.OutputWidth, frame.OutputHeight);
            bool reset = !initialized || frame.Reset != 0 || camera != previousCamera ||
                frame.Viewport != previousViewport || frame.FrameIndex != unchecked(previousFrame + 1) ||
                dimensions != previousDimensions || frame.Mode != previousMode || projection != previousProjection;
            Matrix4x4 viewProjection = projection * camera.worldToCameraMatrix;
            frame.Reset = reset ? 1u : 0u;
            frame.CameraViewToClip = projection;
            frame.ClipToCameraView = projection.inverse;
            frame.ClipToPreviousClip = reset ? Matrix4x4.identity : previousViewProjection * viewProjection.inverse;
            frame.PreviousClipToClip = frame.ClipToPreviousClip.inverse;
            frame.JitterX = jitter.x;
            frame.JitterY = jitter.y;
            frame.DepthInverted = SystemInfo.usesReversedZBuffer ? 1u : 0u;
            frame.MotionScaleX = -1;
            frame.MotionScaleY = -1;
            frame.NearPlane = camera.nearClipPlane;
            frame.FarPlane = camera.farClipPlane;
            frame.VerticalFov = camera.fieldOfView * Mathf.Deg2Rad;
            frame.AspectRatio = camera.aspect;
            frame.CameraPosition = camera.transform.position;
            frame.CameraUp = camera.transform.up;
            frame.CameraRight = camera.transform.right;
            frame.CameraForward = camera.transform.forward;

            previousCamera = camera;
            previousViewProjection = viewProjection;
            previousProjection = projection;
            previousDimensions = dimensions;
            previousMode = frame.Mode;
            previousFrame = frame.FrameIndex;
            previousViewport = frame.Viewport;
            initialized = true;
        }
    }
}
