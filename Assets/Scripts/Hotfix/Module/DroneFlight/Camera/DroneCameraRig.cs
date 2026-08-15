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
        [SerializeField] private Transform fixedForwardMount;
        [SerializeField] private Transform bellyMount;
        [SerializeField] private float followSharpness = 10f;
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

        /// <summary>唯一的无人机画面输出 Camera。</summary>
        internal Camera OutputCamera => outputCamera;

        /// <summary>当前无人机视角。</summary>
        internal DroneCameraMode Mode => mode;

        /// <summary>当前云台水平角，单位度。</summary>
        internal float GimbalYawDegrees => gimbalYawDegrees;

        /// <summary>当前云台俯仰角，单位度。</summary>
        internal float GimbalPitchDegrees => gimbalPitchDegrees;

        /// <summary>当前镜头视场角，单位度。</summary>
        internal float FieldOfView => outputCamera != null ? outputCamera.fieldOfView : 0f;

        private void LateUpdate()
        {
            if (outputCamera == null || droneBody == null)
            {
                return;
            }

            UpdateGimbalTransforms();
            CalculatePose(out var targetPosition, out var targetRotation);
            var blend = 1f - Mathf.Exp(-Mathf.Max(0.01f, followSharpness) * Time.unscaledDeltaTime);
            outputCamera.transform.position = Vector3.Lerp(outputCamera.transform.position, targetPosition, blend);
            outputCamera.transform.rotation = Quaternion.Slerp(outputCamera.transform.rotation, targetRotation, blend);
        }

        /// <summary>
        /// 切换视角，不创建或销毁 Camera。
        /// </summary>
        /// <param name="cameraMode">目标视角。</param>
        internal void SetMode(DroneCameraMode cameraMode)
        {
            mode = cameraMode;
        }

        /// <summary>保存当前视角并切换到机腹向下瞄准。</summary>
        internal void EnterHarpoonAim()
        {
            if (mode != DroneCameraMode.HarpoonAim)
            {
                savedMode = mode;
            }

            mode = DroneCameraMode.HarpoonAim;
        }

        /// 恢复进入渔叉瞄准前的视角。
        internal void ExitHarpoonAim()
        {
            if (mode == DroneCameraMode.HarpoonAim)
            {
                mode = savedMode == DroneCameraMode.HarpoonAim ? DroneCameraMode.ThirdPerson : savedMode;
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
            Transform belly)
        {
            outputCamera = camera;
            droneBody = body;
            gimbalYaw = yaw;
            gimbalPitch = pitch;
            fixedForwardMount = forward;
            bellyMount = belly;
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

        private void CalculatePose(out Vector3 position, out Quaternion rotation)
        {
            switch (mode)
            {
                case DroneCameraMode.HarpoonAim:
                    var aimMount = bellyMount != null ? bellyMount : droneBody;
                    position = aimMount.position;
                    rotation = aimMount.rotation;
                    break;
                case DroneCameraMode.ThirdPerson:
                    position = droneBody.TransformPoint(0f, 1.2f, -3f);
                    rotation = Quaternion.LookRotation(droneBody.position - position + Vector3.up * 0.2f, Vector3.up);
                    break;
                case DroneCameraMode.Orbit:
                    var orbitRotation = Quaternion.Euler(orbitPitchDegrees, orbitYawDegrees, 0f);
                    position = droneBody.position + orbitRotation * new Vector3(0f, 0f, -3f);
                    rotation = Quaternion.LookRotation(droneBody.position - position, Vector3.up);
                    break;
                case DroneCameraMode.FixedForward when fixedForwardMount != null:
                    position = fixedForwardMount.position;
                    rotation = fixedForwardMount.rotation;
                    break;
                case DroneCameraMode.Belly when bellyMount != null:
                    position = bellyMount.position;
                    rotation = bellyMount.rotation;
                    break;
                default:
                    var mount = gimbalPitch != null ? gimbalPitch : droneBody;
                    position = mount.position;
                    rotation = mount.rotation;
                    break;
            }
        }
    }
}
