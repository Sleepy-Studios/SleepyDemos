using Core.Runtime;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hotfix.DroneFlight
{
    /// <summary>F 直接进入第三人称控制，Escape 返回场景等待视角。</summary>
    public sealed class DroneRemoteControllerExperience : MonoBehaviour
    {
        [SerializeField] private Camera playerCamera;
        [SerializeField] private DroneCameraRig droneCameraRig;
        [SerializeField] private DronePlayerInput flightInput;
        [SerializeField] private DroneFlightController flightController;
        [SerializeField] private DroneHookInput mechanismInput;

        private readonly DroneControlSession session = new();

        /// 当前控制会话状态。
        internal DroneControlSessionState State => session.State;

        /// 供运行诊断读取的状态名。
        public string CurrentStateName => session.State.ToString();

        private void Awake()
        {
            ApplyWaiting();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.fKey.wasPressedThisFrame)
            {
                Activate();
            }
            else if (keyboard.escapeKey.wasPressedThisFrame)
            {
                ReturnToWaiting();
            }
        }

        /// 立即进入第三人称控制；不会自动解锁或起飞。
        internal void Activate()
        {
            session.Activate();
            droneCameraRig?.SetMode(DroneCameraMode.ThirdPerson);
            var droneCamera = droneCameraRig != null ? droneCameraRig.OutputCamera : null;
            if (droneCamera != null)
            {
                droneCamera.targetTexture = null;
                droneCamera.enabled = true;
            }

            if (playerCamera != null)
            {
                playerCamera.enabled = false;
            }

            if (flightInput != null)
            {
                flightInput.enabled = true;
            }
            if (mechanismInput != null)
            {
                mechanismInput.enabled = true;
            }

            BindUiCameraIfReady(droneCamera);
        }

        /// 返回 Waiting，并锁定电机和清空飞行输入。
        internal void ReturnToWaiting()
        {
            session.ReturnToWaiting();
            ApplyWaiting();
        }

        internal void Configure(
            Camera player,
            DroneCameraRig rig,
            DronePlayerInput input,
            DroneFlightController controller = null,
            DroneHookInput mechanisms = null)
        {
            playerCamera = player;
            droneCameraRig = rig;
            flightInput = input;
            flightController = controller;
            mechanismInput = mechanisms;
            ApplyWaiting();
        }

        private void ApplyWaiting()
        {
            flightController?.SetArmed(false);
            flightInput?.ResetBufferedInput();
            if (flightInput != null)
            {
                flightInput.enabled = false;
            }
            if (mechanismInput != null)
            {
                mechanismInput.enabled = false;
            }

            var droneCamera = droneCameraRig != null ? droneCameraRig.OutputCamera : null;
            if (droneCamera != null)
            {
                droneCamera.targetTexture = null;
                droneCamera.enabled = false;
            }

            if (playerCamera != null)
            {
                playerCamera.enabled = true;
            }

            BindUiCameraIfReady(playerCamera);
        }

        private static void BindUiCameraIfReady(Camera baseCamera)
        {
            if (baseCamera == null)
            {
                return;
            }

            var uiRoot = UIRootManager.Instance;
            if (uiRoot.Root != null && uiRoot.UICamera != null && uiRoot.BaseCamera != baseCamera)
            {
                uiRoot.BindToBaseCamera(baseCamera);
            }
        }
    }
}
