using UnityEngine;
using UnityEngine.InputSystem;

namespace Hotfix.DroneFlight
{
    /// <summary>处理腹部装备和起落架的玩家输入。</summary>
    public sealed class DroneEquipmentInput : MonoBehaviour
    {
        private DroneEquipmentHost equipmentHost;
        private DroneLandingGearController landingGear;
        private IDroneControlSession controlSession;

        internal void Configure(
            DroneEquipmentHost host,
            DroneLandingGearController gear = null,
            IDroneControlSession session = null)
        {
            equipmentHost = host;
            landingGear = gear;
            controlSession = session;
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null
                || (controlSession != null && !controlSession.IsActive))
            {
                return;
            }

            if (keyboard.lKey.wasPressedThisFrame)
            {
                landingGear?.Toggle();
            }

            if (keyboard.hKey.wasPressedThisFrame)
            {
                equipmentHost?.PrimaryAction();
            }

            if (equipmentHost == null)
            {
                return;
            }

            if (equipmentHost.Kind == DroneEquipmentKind.None)
            {
                equipmentHost.SetLineInput(0f);
                return;
            }

            if (equipmentHost.Kind == DroneEquipmentKind.Grapple)
            {
                equipmentHost.SetLineInput(ReadLineInput(keyboard));
                return;
            }

            if (keyboard.vKey.wasPressedThisFrame)
            {
                equipmentHost.ToggleAimMode();
            }

            var mouse = Mouse.current;
            if (mouse != null && Screen.width > 0 && Screen.height > 0)
            {
                var position = mouse.position.ReadValue();
                equipmentHost.SetAimViewportPosition(new Vector2(
                    Mathf.Clamp01(position.x / Screen.width),
                    Mathf.Clamp01(position.y / Screen.height)));
            }

            equipmentHost.SetLineInput(ReadLineInput(keyboard));
        }

        /// 清理瞄准与绳索输入，供退出遥控时恢复唯一相机。
        internal void ResetTransientState()
        {
            equipmentHost?.SetLineInput(0f);
            equipmentHost?.ExitAimMode();
        }

        private static float ReadLineInput(Keyboard keyboard)
        {
            var value = keyboard.kKey.isPressed ? 1f : 0f;
            if (keyboard.jKey.isPressed)
            {
                value -= 1f;
            }
            return value;
        }
    }
}
