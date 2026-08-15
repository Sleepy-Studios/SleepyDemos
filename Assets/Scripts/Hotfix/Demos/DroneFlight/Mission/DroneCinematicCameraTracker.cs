using UnityEngine;

namespace Hotfix.DroneFlight
{
    /// <summary>保持相机世界坐标不变，只平滑旋转和收窄视场角追踪无人机。</summary>
    [RequireComponent(typeof(Camera))]
    public sealed class DroneCinematicCameraTracker : MonoBehaviour
    {
        [SerializeField, Range(10f, 80f)] private float trackingFieldOfView = 35f;
        [SerializeField, Min(0.1f)] private float rotationSharpness = 3.5f;
        [SerializeField, Min(0.1f)] private float fieldOfViewSharpness = 2.5f;

        private Camera outputCamera;
        private Vector3 initialPosition;
        private Quaternion initialRotation;
        private float initialFieldOfView;
        private Transform target;
        private bool isTracking;
        private bool hasCapturedInitialPose;

        /// 是否已进入固定机位追踪阶段。
        internal bool IsTracking => isTracking;

        private void Awake()
        {
            EnsureInitialPoseCaptured();
        }

        private void LateUpdate()
        {
            if (!isTracking || target == null || outputCamera == null)
            {
                return;
            }

            transform.position = initialPosition;
            var direction = target.position - initialPosition;
            if (direction.sqrMagnitude > 0.0001f)
            {
                var rotationBlend = 1f - Mathf.Exp(-rotationSharpness * Time.unscaledDeltaTime);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(direction, Vector3.up),
                    rotationBlend);
            }

            var fovBlend = 1f - Mathf.Exp(-fieldOfViewSharpness * Time.unscaledDeltaTime);
            outputCamera.fieldOfView = Mathf.Lerp(outputCamera.fieldOfView, trackingFieldOfView, fovBlend);
        }

        internal void CaptureInitialPose()
        {
            outputCamera = GetComponent<Camera>();
            initialPosition = transform.position;
            initialRotation = transform.rotation;
            initialFieldOfView = outputCamera != null ? outputCamera.fieldOfView : 60f;
            hasCapturedInitialPose = true;
        }

        internal void BeginTracking(Transform trackingTarget)
        {
            target = trackingTarget;
            isTracking = target != null;
        }

        internal void ResetTracking()
        {
            EnsureInitialPoseCaptured();
            isTracking = false;
            target = null;
            transform.SetPositionAndRotation(initialPosition, initialRotation);
            if (outputCamera != null)
            {
                outputCamera.fieldOfView = initialFieldOfView;
            }
        }

        private void EnsureInitialPoseCaptured()
        {
            if (!hasCapturedInitialPose)
            {
                CaptureInitialPose();
            }
        }
    }
}
