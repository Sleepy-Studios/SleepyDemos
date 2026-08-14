using UnityEngine;

namespace Hotfix.DroneFlight
{
    /// 无人机腹部装备类型。
    public enum DroneEquipmentKind
    {
        None,
        Grapple,
        Harpoon
    }

    /// 装备公共状态。
    public enum DroneEquipmentState
    {
        Stowed,
        Deploying,
        Ready,
        Retracting,
        Carrying,
        Fired,
        Attached,
        Recovering,
        Broken
    }

    /// <summary>HUD 与 F3 使用的只读装备快照。</summary>
    public readonly struct DroneEquipmentSnapshot
    {
        public DroneEquipmentSnapshot(
            DroneEquipmentKind kind,
            DroneEquipmentState state,
            string statusText,
            float hardwareMassKilograms,
            float payloadMassKilograms,
            float supportedPayloadMassKilograms,
            float travelMeters,
            float tensionNewtons,
            int contactCount,
            bool canUsePrimary,
            Vector3 aimDirection,
            Vector3 attachmentPoint)
        {
            Kind = kind;
            State = state;
            StatusText = statusText;
            HardwareMassKilograms = hardwareMassKilograms;
            PayloadMassKilograms = payloadMassKilograms;
            SupportedPayloadMassKilograms = supportedPayloadMassKilograms;
            TravelMeters = travelMeters;
            TensionNewtons = tensionNewtons;
            ContactCount = contactCount;
            CanUsePrimary = canUsePrimary;
            AimDirection = aimDirection;
            AttachmentPoint = attachmentPoint;
        }

        public DroneEquipmentKind Kind { get; }
        public DroneEquipmentState State { get; }
        public string StatusText { get; }
        public float HardwareMassKilograms { get; }
        public float PayloadMassKilograms { get; }
        public float SupportedPayloadMassKilograms { get; }
        public float TravelMeters { get; }
        public float TensionNewtons { get; }
        public int ContactCount { get; }
        public bool CanUsePrimary { get; }
        public Vector3 AimDirection { get; }
        public Vector3 AttachmentPoint { get; }
    }

    /// <summary>飞控只读的外部承载质量来源。</summary>
    internal interface IDroneExternalMassProvider
    {
        float SupportedMassKilograms { get; }
        float InstalledHardwareMassKilograms { get; }
        float HardwareMassKilograms { get; }
        float PayloadMassKilograms { get; }
        float SupportedPayloadMassKilograms { get; }
    }

    /// <summary>飞控读取的吊挂方向和相对运动来源。</summary>
    internal interface IDroneSuspensionStateProvider
    {
        DroneSuspensionState SuspensionState { get; }
    }

    /// <summary>在飞控读取质量前原子同步可热调的外部刚体质量。</summary>
    internal interface IDroneExternalMassSynchronizer
    {
        void SynchronizeExternalMass();
    }

    internal interface IDroneEquipmentModule
    {
        DroneEquipmentKind Kind { get; }
        DroneEquipmentState State { get; }
        float HardwareMassKilograms { get; }
        float PayloadMassKilograms { get; }
        float SupportedPayloadMassKilograms { get; }
        DroneEquipmentSnapshot Snapshot { get; }
        string LastHint { get; }

        void ConfigureHost(Rigidbody droneBody, Camera aimCamera, float maximumPayloadKilograms);
        void PrimaryAction();
        void SetLineInput(float input);
        void ToggleDeployment();
        void SynchronizeRuntimeConfig();
        void ReleaseAndCleanup();
    }
}
