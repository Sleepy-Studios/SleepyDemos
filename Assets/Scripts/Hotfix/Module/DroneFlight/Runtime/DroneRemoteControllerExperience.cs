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
        [SerializeField] private DroneEquipmentInput equipmentInput;

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

            // 必须先关闭旧监听器，再启用新监听器，避免同一帧短暂出现两个 AudioListener。
            if (playerCamera != null)
            {
                var oldListener = playerCamera.GetComponent<AudioListener>();
                if (oldListener != null)
                {
                    oldListener.enabled = false;
                }
                playerCamera.enabled = false;
            }

            if (droneCamera != null)
            {
                droneCamera.targetTexture = null;
                droneCamera.enabled = true;
            }

            var droneListener = droneCamera != null ? droneCamera.GetComponent<AudioListener>() : null;
            if (droneListener != null)
            {
                droneListener.enabled = true;
            }

            if (flightInput != null)
            {
                flightInput.enabled = true;
            }
            if (equipmentInput != null)
            {
                equipmentInput.enabled = true;
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
            DroneEquipmentInput equipment = null)
        {
            playerCamera = player;
            droneCameraRig = rig;
            flightInput = input;
            flightController = controller;
            equipmentInput = equipment;
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
            if (equipmentInput != null)
            {
                equipmentInput.enabled = false;
            }

            var droneCamera = droneCameraRig != null ? droneCameraRig.OutputCamera : null;
            if (droneCamera != null)
            {
                var listener = droneCamera.GetComponent<AudioListener>();
                if (listener != null)
                {
                    listener.enabled = false;
                }
                droneCamera.targetTexture = null;
                droneCamera.enabled = false;
            }

            if (playerCamera != null)
            {
                playerCamera.enabled = true;
                var listener = playerCamera.GetComponent<AudioListener>();
                if (listener != null)
                {
                    listener.enabled = true;
                }
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
