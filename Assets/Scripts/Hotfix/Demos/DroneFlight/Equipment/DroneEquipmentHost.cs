using UnityEngine;

namespace Hotfix.DroneFlight
{
    /// <summary>把具体腹部装备适配到现有飞控外部质量接口，并集中转发玩家操作。</summary>
    public sealed class DroneEquipmentHost : MonoBehaviour, IDroneExternalMassProvider,
        IDroneExternalMassSynchronizer
    {
        // 装配器在实例化后统一注入，避免把运行时连接暴露为可调参数。
        private DroneFlightController flightController;
        private Rigidbody droneBody;
        private Camera aimCamera;
        private DroneCameraRig cameraRig;
        private MonoBehaviour moduleSource;

        private IDroneEquipmentModule module;

        public DroneEquipmentKind Kind => module?.Kind ?? DroneEquipmentKind.None;
        public DroneEquipmentState State => module?.State ?? DroneEquipmentState.Stowed;
        public string LastHint => module?.LastHint ?? string.Empty;
        public DroneEquipmentSnapshot Snapshot => module?.Snapshot ?? default;

        float IDroneExternalMassProvider.SupportedMassKilograms =>
            Mathf.Max(0f, SupportedIntegratedDynamicMassKilograms + SupportedPayloadMassKilograms);

        float IDroneExternalMassProvider.IntegratedDynamicMassKilograms => IntegratedDynamicMassKilograms;
        float IDroneExternalMassProvider.HardwareMassKilograms => HardwareMassKilograms;
        float IDroneExternalMassProvider.PayloadMassKilograms => PayloadMassKilograms;
        float IDroneExternalMassProvider.SupportedPayloadMassKilograms => SupportedPayloadMassKilograms;

        internal float IntegratedDynamicMassKilograms =>
            Mathf.Max(0f, module?.IntegratedDynamicMassKilograms ?? 0f);
        internal float SupportedIntegratedDynamicMassKilograms =>
            Mathf.Clamp(
                module?.SupportedIntegratedDynamicMassKilograms ?? 0f,
                0f,
                IntegratedDynamicMassKilograms);
        internal float HardwareMassKilograms => 0f;
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

        private void FixedUpdate()
        {
            flightController?.RefreshMassDistribution();
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
            flightController?.RefreshMassDistribution();
        }

        internal void ToggleAimMode()
        {
            if (module is IDroneAimingEquipment aiming)
            {
                aiming.SetAimMode(!aiming.IsAimModeActive);
            }
        }

        internal void ExitAimMode()
        {
            if (module is IDroneAimingEquipment aiming)
            {
                aiming.SetAimMode(false);
            }
        }

        internal void SetAimViewportPosition(Vector2 viewportPosition)
        {
            if (module is IDroneAimingEquipment aiming)
            {
                aiming.SetAimViewportPosition(viewportPosition);
            }
        }

        internal bool TrySetAutomatedAimTarget(Vector3 worldPoint)
        {
            return module is IDroneAutomatedAimingEquipment automated
                   && automated.TrySetAutomatedAimTarget(worldPoint);
        }

        internal void ClearAutomatedAimTarget()
        {
            if (module is IDroneAutomatedAimingEquipment automated)
            {
                automated.ClearAutomatedAimTarget();
            }
        }

        internal void ReleaseAndCleanup()
        {
            module?.ReleaseAndCleanup();
            flightController?.RefreshMassDistribution();
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
                cameraRig = GetComponentInChildren<DroneCameraRig>(true);
                aimCamera = cameraRig?.OutputCamera;
            }

            cameraRig ??= GetComponentInChildren<DroneCameraRig>(true);

            var maximumPayloadKilograms = flightController != null && flightController.Config != null
                ? flightController.Config.MaximumPayloadMassKilograms
                : float.PositiveInfinity;
            module?.ConfigureHost(droneBody, aimCamera, maximumPayloadKilograms);
            if (module is IDroneAimingEquipment aiming)
            {
                aiming.ConfigureAim(cameraRig);
            }
            flightController?.ConfigureExternalMassProvider(this);
        }
    }
}
