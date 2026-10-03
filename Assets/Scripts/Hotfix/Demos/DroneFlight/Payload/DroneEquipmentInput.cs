using UnityEngine;

namespace Hotfix.DroneFlight
{
    /// <summary>处理腹部装备和起落架的玩家输入。</summary>
    public sealed class DroneEquipmentInput : MonoBehaviour
    {
        private DronePlayerInput input;
        private void Awake() => input = GetComponent<DronePlayerInput>();
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
            if (input == null || !input.AcceptsFlightInput || controlSession != null && !controlSession.IsActive)
            { equipmentHost?.SetLineInput(0); return; }
            if (input.Pressed("Equipment")) equipmentHost?.PrimaryAction();
            if (equipmentHost == null) return;
            if (input.Pressed("Aim")) equipmentHost.ToggleAimMode();
            equipmentHost.SetAimViewportPosition(input.AimPosition);
            equipmentHost.SetLineInput(input.LineInput);
        }

        /// 清理瞄准与绳索输入，供退出遥控时恢复唯一相机。
        internal void ResetTransientState()
        {
            equipmentHost?.SetLineInput(0f);
            equipmentHost?.ExitAimMode();
        }

    }
}
