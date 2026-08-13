using UnityEngine;

namespace Hotfix.DroneFlight
{
    /// <summary>DroneFlight 场景向正式 UI View 暴露的唯一引用上下文。</summary>
    public sealed class DroneFlightSceneContext : MonoBehaviour
    {
        [SerializeField] private DroneFlightController flightController;
        [SerializeField] private DronePlayerInput playerInput;
        [SerializeField] private DroneCameraRig cameraRig;
        [SerializeField] private DroneRemoteControllerExperience remoteExperience;
        [SerializeField] private PayloadMount payloadMount;
        [SerializeField] private DroneMechanicalHook grapple;
        [SerializeField] private DroneWinchController winch;
        [SerializeField] private DroneLandingGearController landingGear;
        [SerializeField] private DronePayload[] scenePayloads = System.Array.Empty<DronePayload>();

        /// 当前已加载 DroneFlight 场景的唯一上下文。
        internal static DroneFlightSceneContext Current { get; private set; }

        internal DroneFlightController FlightController => flightController;
        internal DronePlayerInput PlayerInput => playerInput;
        internal DroneCameraRig CameraRig => cameraRig;
        internal DroneRemoteControllerExperience RemoteExperience => remoteExperience;
        internal PayloadMount PayloadMount => payloadMount;
        internal DroneMechanicalHook Grapple => grapple;
        internal DroneWinchController Winch => winch;
        internal DroneLandingGearController LandingGear => landingGear;
        internal DronePayload[] ScenePayloads => scenePayloads;

        private void Awake()
        {
            Current = this;
        }

        private void OnDestroy()
        {
            if (Current == this)
            {
                Current = null;
            }
        }

        internal void Configure(
            DroneFlightController controller,
            DronePlayerInput input,
            DroneCameraRig rig,
            DroneRemoteControllerExperience remote,
            PayloadMount mount,
            DroneMechanicalHook hook,
            DroneWinchController winchController,
            DroneLandingGearController gear,
            DronePayload[] payloads = null)
        {
            flightController = controller;
            playerInput = input;
            cameraRig = rig;
            remoteExperience = remote;
            payloadMount = mount;
            grapple = hook;
            winch = winchController;
            landingGear = gear;
            scenePayloads = payloads ?? System.Array.Empty<DronePayload>();
        }
    }
}
