using UnityEngine;
using UnityEngine.InputSystem;

namespace Hotfix.DroneFlight
{
    /// <summary>处理腹部装备和起落架的玩家输入。</summary>
    public sealed class DroneEquipmentInput : MonoBehaviour
    {
        [SerializeField] private DroneEquipmentHost equipmentHost;
        [SerializeField] private DroneLandingGearController landingGear;
        [SerializeField] private DroneRemoteControllerExperience controlSession;

        internal void Configure(
            DroneEquipmentHost host,
            DroneLandingGearController gear = null,
            DroneRemoteControllerExperience session = null)
        {
            equipmentHost = host;
            landingGear = gear;
            controlSession = session;
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null
                || (controlSession != null && controlSession.State != DroneControlSessionState.Active))
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
                if (keyboard.jKey.wasPressedThisFrame)
                {
                    equipmentHost.ToggleDeployment();
                }

                equipmentHost.SetLineInput(0f);
                return;
            }

            var lineInput = keyboard.kKey.isPressed ? 1f : 0f;
            if (keyboard.jKey.isPressed)
            {
                lineInput -= 1f;
            }

            equipmentHost.SetLineInput(lineInput);
        }
    }
}
