using UnityEngine;

namespace Hotfix.DroneFlight
{
    /// <summary>把具体腹部装备适配到现有飞控外部质量接口，并集中转发玩家操作。</summary>
    public sealed class DroneEquipmentHost : MonoBehaviour, IDroneExternalMassProvider,
        IDroneExternalMassSynchronizer
    {
        [SerializeField] private DroneFlightController flightController;
        [SerializeField] private Rigidbody droneBody;
        [SerializeField] private Camera aimCamera;
        [SerializeField] private MonoBehaviour moduleSource;

        private IDroneEquipmentModule module;

        public DroneEquipmentKind Kind => module?.Kind ?? DroneEquipmentKind.None;
        public DroneEquipmentState State => module?.State ?? DroneEquipmentState.Stowed;
        public string LastHint => module?.LastHint ?? string.Empty;
        public DroneEquipmentSnapshot Snapshot => module?.Snapshot ?? default;

        float IDroneExternalMassProvider.SupportedMassKilograms =>
            Mathf.Max(0f, HardwareMassKilograms + SupportedPayloadMassKilograms);

        float IDroneExternalMassProvider.InstalledHardwareMassKilograms => HardwareMassKilograms;
        float IDroneExternalMassProvider.HardwareMassKilograms => HardwareMassKilograms;
        float IDroneExternalMassProvider.PayloadMassKilograms => PayloadMassKilograms;
        float IDroneExternalMassProvider.SupportedPayloadMassKilograms => SupportedPayloadMassKilograms;

        internal float HardwareMassKilograms => Mathf.Max(0f, module?.HardwareMassKilograms ?? 0f);
        internal float PayloadMassKilograms => Mathf.Max(0f, module?.PayloadMassKilograms ?? 0f);
        internal float SupportedPayloadMassKilograms => Mathf.Clamp(
            module?.SupportedPayloadMassKilograms ?? 0f,
            0f,
            PayloadMassKilograms);

        private void Awake()
        {
            ResolveModule();
            ConfigureRuntimeReferences();
        }

        private void OnDestroy()
        {
            module?.ReleaseAndCleanup();
        }

        internal void Configure(
            DroneFlightController controller,
            Rigidbody body,
            Camera camera,
            MonoBehaviour equipmentModule)
        {
            flightController = controller;
            droneBody = body;
            aimCamera = camera;
            moduleSource = equipmentModule;
            ResolveModule();
            ConfigureRuntimeReferences();
        }

        internal void PrimaryAction()
        {
            module?.PrimaryAction();
        }

        internal void ToggleDeployment()
        {
            module?.ToggleDeployment();
        }

        internal void SetLineInput(float input)
        {
            module?.SetLineInput(Mathf.Clamp(input, -1f, 1f));
        }

        void IDroneExternalMassSynchronizer.SynchronizeExternalMass()
        {
            module?.SynchronizeRuntimeConfig();
        }

        private void ResolveModule()
        {
            module = moduleSource as IDroneEquipmentModule;
            if (module == null)
            {
                foreach (var candidate in GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (candidate is IDroneEquipmentModule resolved)
                    {
                        moduleSource = candidate;
                        module = resolved;
                        break;
                    }
                }
            }
        }

        private void ConfigureRuntimeReferences()
        {
            flightController ??= GetComponent<DroneFlightController>();
            droneBody ??= GetComponent<Rigidbody>();
            if (aimCamera == null)
            {
                aimCamera = GetComponentInChildren<DroneCameraRig>(true)?.OutputCamera;
            }

            var maximumPayloadKilograms = flightController != null && flightController.Config != null
                ? flightController.Config.MaximumPayloadMassKilograms
                : float.PositiveInfinity;
            module?.ConfigureHost(droneBody, aimCamera, maximumPayloadKilograms);
            flightController?.ConfigureExternalMassProvider(this);
        }
    }
}
