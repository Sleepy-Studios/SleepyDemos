using UnityEngine;

namespace Hotfix.DroneFlight
{
    /// <summary>集中恢复无人机与显式场景载荷的进入场景初始状态。</summary>
    public sealed class DroneResetCoordinator : MonoBehaviour
    {
        [SerializeField] private DroneFlightController flightController;
        [SerializeField] private DronePlayerInput flightInput;
        [SerializeField] private DroneCameraRig cameraRig;
        [SerializeField] private DroneLandingGearController landingGear;
        [SerializeField] private DroneWinchController winch;
        [SerializeField] private DroneMechanicalHook grapple;
        [SerializeField] private PayloadMount payloadMount;
        [SerializeField] private DroneRemoteControllerExperience controlSession;
        [SerializeField] private DronePayload[] scenePayloads = System.Array.Empty<DronePayload>();

        private Rigidbody droneBody;
        private Vector3 spawnPosition;
        private Quaternion spawnRotation;
        private DronePayloadSnapshot[] payloadSnapshots = System.Array.Empty<DronePayloadSnapshot>();

        private void Awake()
        {
            droneBody = flightController != null && flightController.Body != null
                ? flightController.Body
                : GetComponent<Rigidbody>();
            if (droneBody != null)
            {
                spawnPosition = droneBody.position;
                spawnRotation = droneBody.rotation;
            }

            CapturePayloadSnapshots();
        }

        /// 将无人机和场景载荷恢复到进入场景时的完整状态。
        internal void ResetDrone()
        {
            if (droneBody == null || flightController == null)
            {
                return;
            }

            flightController.SetArmed(false);
            payloadMount?.Release(PayloadReleaseReason.Manual);
            grapple?.ResetOpen();
            winch?.ResetStowed();
            landingGear?.ResetToDeployed();
            flightInput?.ResetBufferedInput();

            foreach (var snapshot in payloadSnapshots)
            {
                snapshot.Restore();
            }

            droneBody.position = spawnPosition;
            droneBody.rotation = spawnRotation;
            droneBody.linearVelocity = Vector3.zero;
            droneBody.angularVelocity = Vector3.zero;
            droneBody.Sleep();
            Physics.SyncTransforms();

            flightController.ResetFlightState();
            cameraRig?.SetMode(DroneCameraMode.ThirdPerson);
            controlSession?.ReturnToWaiting();
        }

        /// <summary>
        /// 由 Prefab 装配或测试夹具绑定需要复位的无人机子系统。
        /// </summary>
        internal void Configure(
            DroneFlightController controller,
            DronePlayerInput input,
            DroneCameraRig rig,
            DroneLandingGearController gear,
            DroneWinchController winchController,
            DroneMechanicalHook hook,
            PayloadMount mount,
            DroneRemoteControllerExperience session = null,
            DronePayload[] payloads = null)
        {
            flightController = controller;
            flightInput = input;
            cameraRig = rig;
            landingGear = gear;
            winch = winchController;
            grapple = hook;
            payloadMount = mount;
            controlSession = session;
            scenePayloads = payloads ?? System.Array.Empty<DronePayload>();
            droneBody = controller != null && controller.Body != null
                ? controller.Body
                : GetComponent<Rigidbody>();
            if (droneBody != null)
            {
                spawnPosition = droneBody.position;
                spawnRotation = droneBody.rotation;
            }

            CapturePayloadSnapshots();
        }

        private void CapturePayloadSnapshots()
        {
            payloadSnapshots = new DronePayloadSnapshot[scenePayloads.Length];
            for (var index = 0; index < scenePayloads.Length; index++)
            {
                payloadSnapshots[index] = scenePayloads[index] != null
                    ? scenePayloads[index].CaptureSnapshot()
                    : default;
            }
        }
    }
}
