using System;
using UnityEngine;

namespace Hotfix.DroneFlight
{
    /// <summary>无人机可切换视角。</summary>
    internal enum DroneCameraMode
    {
        Gimbal,
        ThirdPerson,
        Orbit,
        FixedForward,
        Belly,
        HarpoonAim
    }

    /// <summary>
    /// 复用一个无人机 Camera，在多个观察位之间切换，不修改机体物理状态。
    /// </summary>
    public sealed class DroneCameraRig : MonoBehaviour
    {
        [SerializeField] private Camera outputCamera;
        [SerializeField] private Transform droneBody;
        [SerializeField] private Transform gimbalYaw;
        [SerializeField] private Transform gimbalPitch;
        [SerializeField] private Transform gimbalOpticalBody;
        [SerializeField] private Transform fixedForwardMount;
        [SerializeField] private Transform bellyMount;
        [SerializeField] private Vector3 thirdPersonOffset = new(0f, 0.85f, -2.2f);
        [SerializeField] private float thirdPersonLookAheadSeconds = 0.18f;
        [SerializeField] private float orbitDistanceMeters = 2.5f;
        [SerializeField] private float transitionSeconds = 0.35f;
        [SerializeField] private float followSmoothTimeSeconds = 0.16f;
        [SerializeField] private float rotationSharpness = 12f;
        [SerializeField] private float collisionRadiusMeters = 0.18f;
        [SerializeField] private float collisionMinimumDistanceMeters = 0.55f;
        [SerializeField] private float collisionBufferMeters = 0.1f;
        [SerializeField] private float gimbalPitchMinimum = -90f;
        [SerializeField] private float gimbalPitchMaximum = 30f;
        [SerializeField] private float minimumFieldOfView = 20f;
        [SerializeField] private float maximumFieldOfView = 80f;

        private DroneCameraMode mode = DroneCameraMode.ThirdPerson;
        private float gimbalPitchDegrees;
        private float gimbalYawDegrees;
        private float orbitYawDegrees;
        private float orbitPitchDegrees = 20f;
        private DroneCameraMode savedMode = DroneCameraMode.ThirdPerson;
        private Rigidbody bodyRigidbody;
        private Vector3 followVelocity;
        private Vector3 transitionStartPosition;
        private Quaternion transitionStartRotation;
        private float transitionStartFieldOfView;
        private float transitionElapsed;
        private bool isTransitioning;
        private readonly float[] modeFieldOfViews = { 65f, 60f, 60f, 75f, 60f, 55f };

        /// <summary>唯一的无人机画面输出 Camera。</summary>
        internal Camera OutputCamera => outputCamera;

        /// <summary>当前无人机视角。</summary>
        internal DroneCameraMode Mode => mode;

        /// <summary>当前云台水平角，单位度。</summary>
        internal float GimbalYawDegrees => gimbalYawDegrees;

        /// <summary>当前云台俯仰角，单位度。</summary>
        internal float GimbalPitchDegrees => gimbalPitchDegrees;

        /// <summary>当前正式镜头模型的世界空间光轴方向。</summary>
        internal Vector3 GimbalOpticalForward => ResolveGimbalOpticalForward();

        /// <summary>当前镜头视场角，单位度。</summary>
        internal float FieldOfView => outputCamera != null ? outputCamera.fieldOfView : 0f;

        private void LateUpdate()
        {
            if (outputCamera == null || droneBody == null)
            {
                return;
            }

            UpdateGimbalTransforms();
            CalculatePose(out var targetPosition, out var targetRotation, out var focusPoint);
            targetPosition = ResolveObstruction(targetPosition, focusPoint);
            var targetFieldOfView = GetModeFieldOfView(mode);
            ApplyNearClipPlane();

            if (isTransitioning)
            {
                transitionElapsed += Mathf.Max(0f, Time.unscaledDeltaTime);
                var normalized = Mathf.Clamp01(transitionElapsed / Mathf.Max(0.01f, transitionSeconds));
                var eased = normalized * normalized * (3f - 2f * normalized);
                outputCamera.transform.position = Vector3.Lerp(transitionStartPosition, targetPosition, eased);
                outputCamera.transform.rotation = Quaternion.Slerp(transitionStartRotation, targetRotation, eased);
                outputCamera.fieldOfView = Mathf.Lerp(transitionStartFieldOfView, targetFieldOfView, eased);
                if (normalized >= 1f)
                {
                    isTransitioning = false;
                    followVelocity = Vector3.zero;
                }
                return;
            }

            var smoothTime = mode is DroneCameraMode.ThirdPerson or DroneCameraMode.Orbit
                ? followSmoothTimeSeconds
                : 0.06f;
            outputCamera.transform.position = Vector3.SmoothDamp(
                outputCamera.transform.position,
                targetPosition,
                ref followVelocity,
                Mathf.Max(0.01f, smoothTime),
                Mathf.Infinity,
                Time.unscaledDeltaTime);
            var blend = 1f - Mathf.Exp(-Mathf.Max(0.01f, rotationSharpness) * Time.unscaledDeltaTime);
            outputCamera.transform.rotation = Quaternion.Slerp(outputCamera.transform.rotation, targetRotation, blend);
            outputCamera.fieldOfView = Mathf.Lerp(outputCamera.fieldOfView, targetFieldOfView, blend);
        }

        /// <summary>
        /// 切换视角，不创建或销毁 Camera。
        /// </summary>
        /// <param name="cameraMode">目标视角。</param>
        internal void SetMode(DroneCameraMode cameraMode)
        {
            if (mode == cameraMode)
            {
                return;
            }

            BeginTransition();
            mode = cameraMode;
        }

        /// <summary>保存当前视角并切换到机腹向下瞄准。</summary>
        internal void EnterHarpoonAim()
        {
            if (mode != DroneCameraMode.HarpoonAim)
            {
                savedMode = mode;
            }

            SetMode(DroneCameraMode.HarpoonAim);
        }

        /// 恢复进入渔叉瞄准前的视角。
        internal void ExitHarpoonAim()
        {
            if (mode == DroneCameraMode.HarpoonAim)
            {
                SetMode(savedMode == DroneCameraMode.HarpoonAim ? DroneCameraMode.ThirdPerson : savedMode);
            }
        }

        /// <summary>
        /// 更新云台或自由环绕输入。
        /// </summary>
        /// <param name="yaw">水平归一化输入。</param>
        /// <param name="pitch">垂直归一化输入。</param>
        /// <param name="deltaTime">非缩放时间步。</param>
        internal void ApplyLookInput(float yaw, float pitch, float deltaTime)
        {
            if (!float.IsFinite(deltaTime) || deltaTime <= 0f)
            {
                return;
            }

            if (mode == DroneCameraMode.Orbit)
            {
                orbitYawDegrees += yaw * 90f * deltaTime;
                orbitPitchDegrees = Mathf.Clamp(orbitPitchDegrees - pitch * 60f * deltaTime, -10f, 75f);
                return;
            }

            gimbalYawDegrees = Mathf.Clamp(gimbalYawDegrees + yaw * 60f * deltaTime, -120f, 120f);
            gimbalPitchDegrees = Mathf.Clamp(
                gimbalPitchDegrees - pitch * 60f * deltaTime,
                gimbalPitchMinimum,
                gimbalPitchMaximum);
        }

        /// <summary>
        /// 调整无人机镜头 FOV。
        /// </summary>
        /// <param name="delta">正数放大视野，负数缩小视野。</param>
        internal void AdjustFieldOfView(float delta)
        {
            if (outputCamera == null || !float.IsFinite(delta))
            {
                return;
            }

            outputCamera.fieldOfView = Mathf.Clamp(
                outputCamera.fieldOfView + delta,
                minimumFieldOfView,
                maximumFieldOfView);
            modeFieldOfViews[(int)mode] = outputCamera.fieldOfView;
        }

        /// <summary>
        /// 由场景装配器或测试夹具绑定唯一 Camera 与机体节点。
        /// </summary>
        internal void Configure(
            Camera camera,
            Transform body,
            Transform yaw,
            Transform pitch,
            Transform forward,
            Transform belly,
            Transform opticalBody = null)
        {
            outputCamera = camera;
            droneBody = body;
            gimbalYaw = yaw;
            gimbalPitch = pitch;
            gimbalOpticalBody = opticalBody;
            fixedForwardMount = forward;
            bellyMount = belly;
            bodyRigidbody = body != null ? body.GetComponent<Rigidbody>() : null;
            if (outputCamera != null && droneBody != null)
            {
                CalculatePose(out var position, out var rotation, out _);
                outputCamera.transform.SetPositionAndRotation(position, rotation);
                outputCamera.fieldOfView = GetModeFieldOfView(mode);
                ApplyNearClipPlane();
            }
        }

        private void UpdateGimbalTransforms()
        {
            if (gimbalYaw != null)
            {
                gimbalYaw.localRotation = Quaternion.Euler(0f, gimbalYawDegrees, 0f);
            }

            if (gimbalPitch != null)
            {
                gimbalPitch.localRotation = Quaternion.Euler(gimbalPitchDegrees, 0f, 0f);
            }
        }

        private void CalculatePose(out Vector3 position, out Quaternion rotation, out Vector3 focusPoint)
        {
            focusPoint = droneBody.position;
            switch (mode)
            {
                case DroneCameraMode.HarpoonAim:
                    var aimMount = bellyMount != null ? bellyMount : droneBody;
                    position = aimMount.position;
                    rotation = CalculateDownwardRotation();
                    break;
                case DroneCameraMode.ThirdPerson:
                    var yawRotation = Quaternion.Euler(0f, droneBody.eulerAngles.y, 0f);
                    var lookAhead = bodyRigidbody != null
                        ? bodyRigidbody.linearVelocity * thirdPersonLookAheadSeconds
                        : Vector3.zero;
                    focusPoint = droneBody.position + Vector3.up * 0.1f + lookAhead;
                    position = droneBody.position + yawRotation * thirdPersonOffset + lookAhead;
                    rotation = Quaternion.LookRotation(focusPoint - position, Vector3.up);
                    break;
                case DroneCameraMode.Orbit:
                    var orbitRotation = Quaternion.Euler(
                        orbitPitchDegrees,
                        orbitYawDegrees + droneBody.eulerAngles.y,
                        0f);
                    focusPoint = droneBody.position + Vector3.up * 0.08f;
                    position = focusPoint + orbitRotation * new Vector3(0f, 0f, -orbitDistanceMeters);
                    rotation = Quaternion.LookRotation(focusPoint - position, Vector3.up);
                    break;
                case DroneCameraMode.FixedForward when fixedForwardMount != null:
                    position = fixedForwardMount.position;
                    rotation = fixedForwardMount.rotation;
                    break;
                case DroneCameraMode.Belly when bellyMount != null:
                    position = bellyMount.position;
                    rotation = CalculateDownwardRotation();
                    break;
                default:
                    var mount = gimbalOpticalBody != null
                        ? gimbalOpticalBody
                        : gimbalPitch != null
                            ? gimbalPitch
                            : droneBody;
                    position = mount.position;
                    var forward = ResolveGimbalOpticalForward();
                    rotation = CalculateHorizonStableRotation(forward);
                    break;
            }
        }

        private Vector3 ResolveGimbalOpticalForward()
        {
            return gimbalOpticalBody != null
                ? gimbalOpticalBody.TransformDirection(DroneFlightModelContract.GimbalOpticalAxis).normalized
                : gimbalPitch != null
                    ? gimbalPitch.forward
                    : droneBody != null
                        ? droneBody.forward
                        : Vector3.forward;
        }

        private Quaternion CalculateHorizonStableRotation(Vector3 forward)
        {
            var screenUp = Vector3.ProjectOnPlane(Vector3.up, forward).normalized;
            if (screenUp.sqrMagnitude < 0.001f)
            {
                screenUp = Vector3.ProjectOnPlane(droneBody.forward, forward).normalized;
            }
            if (screenUp.sqrMagnitude < 0.001f)
            {
                screenUp = Vector3.ProjectOnPlane(droneBody.right, forward).normalized;
            }
            return Quaternion.LookRotation(forward, screenUp);
        }

        private Quaternion CalculateDownwardRotation()
        {
            var screenUp = Vector3.ProjectOnPlane(droneBody.forward, Vector3.down).normalized;
            if (screenUp.sqrMagnitude < 0.001f)
            {
                screenUp = Vector3.forward;
            }
            return Quaternion.LookRotation(Vector3.down, screenUp);
        }

        private Vector3 ResolveObstruction(Vector3 desiredPosition, Vector3 focusPoint)
        {
            if (mode is not DroneCameraMode.ThirdPerson and not DroneCameraMode.Orbit)
            {
                return desiredPosition;
            }

            var delta = desiredPosition - focusPoint;
            var distance = delta.magnitude;
            if (distance <= 0.0001f)
            {
                return desiredPosition;
            }

            var hits = Physics.SphereCastAll(
                focusPoint,
                Mathf.Max(0.01f, collisionRadiusMeters),
                delta / distance,
                distance,
                ~0,
                QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            foreach (var hit in hits)
            {
                if (hit.collider == null || hit.transform.IsChildOf(droneBody))
                {
                    continue;
                }

                var resolvedDistance = Mathf.Clamp(
                    hit.distance - collisionBufferMeters,
                    collisionMinimumDistanceMeters,
                    distance);
                return focusPoint + delta.normalized * resolvedDistance;
            }

            return desiredPosition;
        }

        private void BeginTransition()
        {
            if (outputCamera == null)
            {
                return;
            }

            transitionStartPosition = outputCamera.transform.position;
            transitionStartRotation = outputCamera.transform.rotation;
            transitionStartFieldOfView = outputCamera.fieldOfView;
            transitionElapsed = 0f;
            isTransitioning = true;
            followVelocity = Vector3.zero;
        }

        private float GetModeFieldOfView(DroneCameraMode cameraMode)
        {
            var index = Mathf.Clamp((int)cameraMode, 0, modeFieldOfViews.Length - 1);
            return Mathf.Clamp(modeFieldOfViews[index], minimumFieldOfView, maximumFieldOfView);
        }

        private void ApplyNearClipPlane()
        {
            if (outputCamera == null)
            {
                return;
            }

            outputCamera.nearClipPlane = mode is DroneCameraMode.FixedForward
                or DroneCameraMode.Belly
                or DroneCameraMode.HarpoonAim
                or DroneCameraMode.Gimbal
                ? 0.02f
                : 0.08f;
        }
    }
}
