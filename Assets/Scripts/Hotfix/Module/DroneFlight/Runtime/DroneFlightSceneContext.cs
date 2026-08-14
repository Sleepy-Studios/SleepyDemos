using UnityEngine;

namespace Hotfix.DroneFlight
{
    /// <summary>单台已生成无人机向强类型 UI 数据暴露的实例引用；不持有静态 Current。</summary>
    public sealed class DroneFlightSceneContext : MonoBehaviour
    {
        [SerializeField] private DroneFlightController flightController;
        [SerializeField] private DronePlayerInput playerInput;
        [SerializeField] private DroneCameraRig cameraRig;
        [SerializeField] private DroneRemoteControllerExperience remoteExperience;
        [SerializeField] private DroneEquipmentHost equipmentHost;
        [SerializeField] private DroneLandingGearController landingGear;
        [SerializeField] private DroneFlightUiTelemetrySource telemetrySource;
        [SerializeField] private DroneFlightDebugDrawRenderer debugDrawRenderer;

        internal DroneFlightController FlightController => flightController;
        internal DronePlayerInput PlayerInput => playerInput;
        internal DroneCameraRig CameraRig => cameraRig;
        internal DroneRemoteControllerExperience RemoteExperience => remoteExperience;
        internal DroneEquipmentHost EquipmentHost => equipmentHost;
        internal DroneLandingGearController LandingGear => landingGear;
        internal DroneFlightUiTelemetrySource TelemetrySource => telemetrySource;
        internal DroneFlightDebugDrawRenderer DebugDrawRenderer => debugDrawRenderer;

        internal void Configure(
            DroneFlightController controller,
            DronePlayerInput input,
            DroneCameraRig rig,
            DroneRemoteControllerExperience remote,
            DroneEquipmentHost equipment,
            DroneLandingGearController gear,
            DroneFlightUiTelemetrySource telemetry = null,
            DroneFlightDebugDrawRenderer debugRenderer = null)
        {
            flightController = controller;
            playerInput = input;
            cameraRig = rig;
            remoteExperience = remote;
            equipmentHost = equipment;
            landingGear = gear;
            telemetrySource = telemetry;
            debugDrawRenderer = debugRenderer;
        }
    }

    /// <summary>DroneFlight HUD 与 F3 的强类型 View 数据。</summary>
    public sealed class DroneFlightViewData
    {
        public DroneFlightViewData(
            DroneFlightUiTelemetrySource telemetrySource,
            string sessionId)
        {
            TelemetrySource = telemetrySource;
            SessionId = sessionId;
        }

        public DroneFlightUiTelemetrySource TelemetrySource { get; }
        public string SessionId { get; }
    }
}
