using Core.Runtime;
using Hotfix.DroneFlight;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hotfix.DroneFlight.Adapters.SleepyDemos
{
    /// <summary>F 直接进入第三人称控制，Escape 返回场景等待视角。</summary>
    public sealed class DroneRemoteControllerExperience : MonoBehaviour, IDroneControlSession
    {
        // 以下引用均由场景适配器在生成无人机时注入，不属于 Prefab 配置。
        private Camera playerCamera;
        private DroneCameraRig droneCameraRig;
        private DronePlayerInput flightInput;
        private DroneFlightController flightController;
        private DroneEquipmentInput equipmentInput;

        private readonly DroneControlSession session = new();

        /// 当前控制会话状态。
        internal DroneControlSessionState State => session.State;

        /// 当前是否正在控制无人机。
        public bool IsActive => session.State == DroneControlSessionState.Active;

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
        public void Activate()
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

            // AudioListener 是相机实例上的可选同对象组件，不属于固定 UI Prefab 节点。
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
        public void ReturnToWaiting()
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
                equipmentInput.ResetTransientState();
                equipmentInput.enabled = false;
            }

            var droneCamera = droneCameraRig != null ? droneCameraRig.OutputCamera : null;
            if (droneCamera != null)
            {
                // 相机可能由不同机型运行时提供，因此按同对象可选组件恢复监听状态。
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
                // 玩家相机是场景外部对象，AudioListener 并非所有场景都强制存在。
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
