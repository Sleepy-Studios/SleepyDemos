using UnityEngine;

namespace Hotfix.DroneFlight.Adapters.SleepyDemos
{
    /// <summary>保持相机世界坐标不变，只平滑旋转和收窄视场角追踪无人机。</summary>
    [RequireComponent(typeof(Camera))]
    public sealed class DroneCinematicCameraTracker : MonoBehaviour
    {
        [SerializeField, InspectorName("捕鱼任务配置")]
        [Tooltip("集中管理固定机位追踪和捕鱼演出参数。")]
        private DroneFishingMissionConfig config;

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
                var rotationSharpness = config != null ? config.CameraRotationSharpness : 3.5f;
                var rotationBlend = 1f - Mathf.Exp(-rotationSharpness * Time.unscaledDeltaTime);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(direction, Vector3.up),
                    rotationBlend);
            }

            var fieldOfViewSharpness = config != null ? config.FieldOfViewSharpness : 2.5f;
            var trackingFieldOfView = config != null ? config.TrackingFieldOfView : 35f;
            var fovBlend = 1f - Mathf.Exp(-fieldOfViewSharpness * Time.unscaledDeltaTime);
            outputCamera.fieldOfView = Mathf.Lerp(outputCamera.fieldOfView, trackingFieldOfView, fovBlend);
        }

        internal void CaptureInitialPose()
        {
            // Camera 由 RequireComponent 保证并在组合阶段缓存，不是 UI 子节点查找。
            outputCamera = GetComponent<Camera>();
            initialPosition = transform.position;
            initialRotation = transform.rotation;
            initialFieldOfView = outputCamera != null ? outputCamera.fieldOfView : 60f;
            hasCapturedInitialPose = true;
        }

        /// 由场景构建器统一写入捕鱼演出配置。
        internal void Configure(DroneFishingMissionConfig missionConfig)
        {
            config = missionConfig;
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
