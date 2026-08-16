using UnityEngine;

namespace Hotfix.DroneFlight
{
    /// <summary>单台已生成无人机向强类型 UI 数据暴露的实例引用；不持有静态 Current。</summary>
    public sealed class DroneFlightSceneContext : MonoBehaviour
    {
        private DroneFlightController flightController;
        private DronePlayerInput playerInput;
        private DroneCameraRig cameraRig;
        private IDroneControlSession controlSession;
        private DroneEquipmentHost equipmentHost;
        private DroneLandingGearController landingGear;
        private DroneFlightUiTelemetrySource telemetrySource;

        internal DroneFlightController FlightController => flightController;
        internal DronePlayerInput PlayerInput => playerInput;
        internal DroneCameraRig CameraRig => cameraRig;
        internal IDroneControlSession ControlSession => controlSession;
        internal DroneEquipmentHost EquipmentHost => equipmentHost;
        internal DroneLandingGearController LandingGear => landingGear;
        internal DroneFlightUiTelemetrySource TelemetrySource => telemetrySource;

        internal void Configure(
            DroneFlightController controller,
            DronePlayerInput input,
            DroneCameraRig rig,
            IDroneControlSession session,
            DroneEquipmentHost equipment,
            DroneLandingGearController gear,
            DroneFlightUiTelemetrySource telemetry = null)
        {
            flightController = controller;
            playerInput = input;
            cameraRig = rig;
            controlSession = session;
            equipmentHost = equipment;
            landingGear = gear;
            telemetrySource = telemetry;
        }
    }
}
