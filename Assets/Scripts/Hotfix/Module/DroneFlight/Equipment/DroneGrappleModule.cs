using System;
using UnityEngine;

namespace Hotfix.DroneFlight
{
    /// <summary>紧凑短行程四爪抓斗；四爪由 HingeJoint 驱动并以真实接触建立软保险约束。</summary>
    public sealed class DroneGrappleModule : MonoBehaviour, IDroneEquipmentModule
    {
        [SerializeField] private DroneGrappleConfig configSource;
        [SerializeField] private Transform bellyMount;
        [SerializeField] private Rigidbody grappleBody;
        [SerializeField] private ConfigurableJoint suspensionJoint;
        [SerializeField] private DroneGrappleContactCollector contactCollector;
        [SerializeField] private Rigidbody[] clawBodies = Array.Empty<Rigidbody>();
        [SerializeField] private HingeJoint[] clawJoints = Array.Empty<HingeJoint>();
        [SerializeField] private Collider[] clawColliders = Array.Empty<Collider>();

        private DroneGrappleConfig runtimeConfig;
        private Rigidbody droneBody;
        private ConfigurableJoint gripJoint;
        private DronePayload attachedPayload;
        private string sourceSignature;
        private float currentDistance;
        private float targetDistance;
        private float supportedPayloadMass;
        private bool clawsClosed;
        private bool initialized;
        private float maximumPayloadKilograms = float.PositiveInfinity;

        public DroneEquipmentKind Kind => DroneEquipmentKind.Grapple;
        public DroneEquipmentState State { get; private set; } = DroneEquipmentState.Stowed;
        public float HardwareMassKilograms => runtimeConfig != null ? runtimeConfig.HardwareMassKilograms : 0f;
        public float PayloadMassKilograms => attachedPayload != null ? attachedPayload.Body.mass : 0f;
        public float SupportedPayloadMassKilograms => supportedPayloadMass;
        public string LastHint { get; private set; } = string.Empty;
        public DroneEquipmentSnapshot Snapshot => new(
            Kind,
            State,
            LastHint,
            HardwareMassKilograms,
            PayloadMassKilograms,
            SupportedPayloadMassKilograms,
            currentDistance,
            gripJoint != null ? gripJoint.currentForce.magnitude : 0f,
            contactCollector != null ? contactCollector.ActiveContactCount : 0,
            State == DroneEquipmentState.Ready,
            Vector3.zero,
            attachedPayload != null ? attachedPayload.Body.worldCenterOfMass : transform.position);

        private void Awake()
        {
            CreateRuntimeConfig();
            currentDistance = runtimeConfig != null ? runtimeConfig.StowedDistanceMeters : 0.08f;
            targetDistance = currentDistance;
            ApplyMassDistribution();
            ConfigureInternalCollisions();
            SetClaws(false);
            SetClawCollision(false);
        }

        private void FixedUpdate()
        {
            SynchronizeRuntimeConfig();
            if (!initialized || runtimeConfig == null)
            {
                return;
            }

            StepTravel(Time.fixedDeltaTime);
            StepGrip();
            StepSupportedLoad(Time.fixedDeltaTime);
        }

        private void OnDestroy()
        {
            ReleaseAndCleanup();
            if (runtimeConfig != null)
            {
                Destroy(runtimeConfig);
            }
        }

        public void ConfigureHost(Rigidbody body, Camera aimCamera, float maximumPayloadMass)
        {
            droneBody = body;
            maximumPayloadKilograms = float.IsFinite(maximumPayloadMass)
                ? Mathf.Max(0f, maximumPayloadMass)
                : float.PositiveInfinity;
            initialized = droneBody != null && grappleBody != null && suspensionJoint != null;
            ConfigureSuspensionJoint();
            ConfigureInternalCollisions();
        }

        public void PrimaryAction()
        {
            if (State is not DroneEquipmentState.Ready and not DroneEquipmentState.Carrying)
            {
                SetHint("请先按 J 放下抓斗");
                return;
            }

            if (clawsClosed)
            {
                ReleaseGrip();
                SetClaws(false);
                SetHint("四爪已张开");
            }
            else
            {
                SetClaws(true);
                SetHint("四爪闭合中");
            }
        }

        public void ToggleDeployment()
        {
            if (runtimeConfig == null)
            {
                return;
            }

            if (State is DroneEquipmentState.Stowed or DroneEquipmentState.Retracting)
            {
                targetDistance = runtimeConfig.DeployedDistanceMeters;
                State = DroneEquipmentState.Deploying;
                SetClawCollision(true);
                SetHint("抓斗放下中");
                return;
            }

            if (attachedPayload != null)
            {
                SetHint("请先释放载荷再收纳抓斗");
                return;
            }

            if (clawsClosed)
            {
                SetHint("请先按 H 张开四爪");
                return;
            }

            targetDistance = runtimeConfig.StowedDistanceMeters;
            State = DroneEquipmentState.Retracting;
            SetHint("抓斗收纳中");
        }

        public void SetLineInput(float input)
        {
        }

        public void SynchronizeRuntimeConfig()
        {
            if (configSource == null)
            {
                return;
            }

            var json = JsonUtility.ToJson(configSource);
            if (runtimeConfig != null && json == sourceSignature)
            {
                return;
            }

            if (runtimeConfig == null)
            {
                runtimeConfig = Instantiate(configSource);
                runtimeConfig.name = $"{configSource.name} (Runtime)";
            }
            else
            {
                JsonUtility.FromJsonOverwrite(json, runtimeConfig);
            }

            sourceSignature = json;
            ApplyMassDistribution();
            ConfigureSuspensionJoint();
            ApplyClawDrive();
        }

        public void ReleaseAndCleanup()
        {
            ReleaseGrip();
            contactCollector?.Clear();
        }

        internal void Configure(
            DroneGrappleConfig config,
            Transform mount,
            Rigidbody body,
            ConfigurableJoint suspension,
            DroneGrappleContactCollector contacts,
            Rigidbody[] bodies,
            HingeJoint[] joints,
            Collider[] colliders)
        {
            configSource = config;
            bellyMount = mount;
            grappleBody = body;
            suspensionJoint = suspension;
            contactCollector = contacts;
            clawBodies = bodies ?? Array.Empty<Rigidbody>();
            clawJoints = joints ?? Array.Empty<HingeJoint>();
            clawColliders = colliders ?? Array.Empty<Collider>();
            CreateRuntimeConfig();
            ApplyMassDistribution();
            ConfigureInternalCollisions();
            ConfigureSuspensionJoint();
        }

        /// <summary>由已保存的组合机体在编辑期绑定基础无人机的腹部挂点。</summary>
        internal void BindBellyMount(Transform mount)
        {
            bellyMount = mount;
        }

        private void CreateRuntimeConfig()
        {
            if (configSource != null && runtimeConfig == null)
            {
                runtimeConfig = Instantiate(configSource);
                runtimeConfig.name = $"{configSource.name} (Runtime)";
                sourceSignature = JsonUtility.ToJson(configSource);
            }
        }

        private void ApplyMassDistribution()
        {
            if (runtimeConfig == null || grappleBody == null)
            {
                return;
            }

            var clawCount = Mathf.Max(1, clawBodies?.Length ?? 0);
            var clawMass = runtimeConfig.HardwareMassKilograms * 0.6f / clawCount;
            grappleBody.mass = runtimeConfig.HardwareMassKilograms * 0.4f;
            foreach (var body in clawBodies)
            {
                if (body != null)
                {
                    body.mass = clawMass;
                    body.interpolation = RigidbodyInterpolation.Interpolate;
                }
            }

            grappleBody.interpolation = RigidbodyInterpolation.Interpolate;
        }

        private void ConfigureSuspensionJoint()
        {
            if (suspensionJoint == null || runtimeConfig == null)
            {
                return;
            }

            suspensionJoint.connectedBody = droneBody;
            suspensionJoint.autoConfigureConnectedAnchor = false;
            suspensionJoint.xMotion = ConfigurableJointMotion.Locked;
            suspensionJoint.yMotion = ConfigurableJointMotion.Locked;
            suspensionJoint.zMotion = ConfigurableJointMotion.Locked;
            suspensionJoint.projectionMode = JointProjectionMode.None;
            suspensionJoint.lowAngularXLimit = new SoftJointLimit { limit = -runtimeConfig.TwistLimitDegrees };
            suspensionJoint.highAngularXLimit = new SoftJointLimit { limit = runtimeConfig.TwistLimitDegrees };
            suspensionJoint.angularYLimit = new SoftJointLimit { limit = runtimeConfig.SwingLimitDegrees };
            suspensionJoint.angularZLimit = new SoftJointLimit { limit = runtimeConfig.SwingLimitDegrees };
            var damping = Mathf.Min(
                runtimeConfig.MaximumDampingTorqueNewtonMeters,
                2f * runtimeConfig.DampingRatio * Mathf.Sqrt(Mathf.Max(0.001f, HardwareMassKilograms * 9.81f)));
            suspensionJoint.rotationDriveMode = RotationDriveMode.Slerp;
            suspensionJoint.slerpDrive = new JointDrive
            {
                positionSpring = 0f,
                positionDamper = damping,
                maximumForce = runtimeConfig.MaximumDampingTorqueNewtonMeters
            };
            ApplyAngularMode(State == DroneEquipmentState.Ready || State == DroneEquipmentState.Carrying);
            UpdateConnectedAnchor();
        }

        private void StepTravel(float deltaTime)
        {
            currentDistance = Mathf.MoveTowards(
                currentDistance,
                targetDistance,
                runtimeConfig.TravelSpeedMetersPerSecond * deltaTime);
            UpdateConnectedAnchor();
            var reached = Mathf.Abs(currentDistance - targetDistance) <= runtimeConfig.DockPositionToleranceMeters;
            if (!reached)
            {
                return;
            }

            if (State == DroneEquipmentState.Deploying)
            {
                State = attachedPayload != null ? DroneEquipmentState.Carrying : DroneEquipmentState.Ready;
                ApplyAngularMode(true);
                SetHint("抓斗已放下，可按 H 开合四爪");
            }
            else if (State == DroneEquipmentState.Retracting)
            {
                var relativeSpeed = droneBody != null
                    ? (grappleBody.linearVelocity - droneBody.GetPointVelocity(grappleBody.worldCenterOfMass)).magnitude
                    : grappleBody.linearVelocity.magnitude;
                if (relativeSpeed > runtimeConfig.DockSpeedToleranceMetersPerSecond)
                {
                    return;
                }

                State = DroneEquipmentState.Stowed;
                ApplyAngularMode(false);
                SetClawCollision(false);
                SetHint("抓斗已收纳");
            }
        }

        private void UpdateConnectedAnchor()
        {
            if (suspensionJoint == null || droneBody == null || bellyMount == null)
            {
                return;
            }

            var worldPoint = bellyMount.position - bellyMount.up * currentDistance;
            suspensionJoint.connectedAnchor = droneBody.transform.InverseTransformPoint(worldPoint);
        }

        private void ApplyAngularMode(bool allowSwing)
        {
            if (suspensionJoint == null)
            {
                return;
            }

            suspensionJoint.angularXMotion = allowSwing
                ? ConfigurableJointMotion.Limited
                : ConfigurableJointMotion.Locked;
            suspensionJoint.angularYMotion = allowSwing
                ? ConfigurableJointMotion.Limited
                : ConfigurableJointMotion.Locked;
            suspensionJoint.angularZMotion = allowSwing
                ? ConfigurableJointMotion.Limited
                : ConfigurableJointMotion.Locked;
        }

        private void SetClaws(bool closed)
        {
            clawsClosed = closed;
            ApplyClawDrive();
        }

        private void ApplyClawDrive()
        {
            if (runtimeConfig == null)
            {
                return;
            }

            foreach (var joint in clawJoints)
            {
                if (joint == null)
                {
                    continue;
                }

                joint.useLimits = true;
                joint.useSpring = true;
                joint.spring = new JointSpring
                {
                    spring = runtimeConfig.ClawSpring,
                    damper = runtimeConfig.ClawDamper,
                    targetPosition = clawsClosed
                        ? runtimeConfig.ClosedAngleDegrees
                        : runtimeConfig.OpenAngleDegrees
                };
                joint.breakForce = Mathf.Infinity;
                joint.breakTorque = Mathf.Infinity;
            }
        }

        private void StepGrip()
        {
            if (!clawsClosed || attachedPayload != null || State != DroneEquipmentState.Ready
                || contactCollector == null)
            {
                return;
            }

            if (contactCollector.TryGetOpposingCandidate(
                    grappleBody.transform,
                    runtimeConfig.EnclosureRadiusMeters,
                    runtimeConfig.EnclosureHalfHeightMeters,
                    runtimeConfig.StableContactSteps,
                    out var payload,
                    out var centroid,
                    out _))
            {
                AttachPayload(payload, centroid);
            }
        }

        private void AttachPayload(DronePayload payload, Vector3 worldAnchor)
        {
            if (payload == null || payload.Body == null || payload.Body == grappleBody)
            {
                return;
            }

            if (payload.Body.mass > maximumPayloadKilograms)
            {
                SetHint($"超载：{payload.Body.mass:0.##} kg，最大允许 {maximumPayloadKilograms:0.##} kg");
                return;
            }

            gripJoint = grappleBody.gameObject.AddComponent<ConfigurableJoint>();
            gripJoint.connectedBody = payload.Body;
            gripJoint.autoConfigureConnectedAnchor = false;
            gripJoint.anchor = grappleBody.transform.InverseTransformPoint(worldAnchor);
            gripJoint.connectedAnchor = payload.Body.transform.InverseTransformPoint(worldAnchor);
            gripJoint.xMotion = ConfigurableJointMotion.Limited;
            gripJoint.yMotion = ConfigurableJointMotion.Limited;
            gripJoint.zMotion = ConfigurableJointMotion.Limited;
            gripJoint.angularXMotion = ConfigurableJointMotion.Free;
            gripJoint.angularYMotion = ConfigurableJointMotion.Free;
            gripJoint.angularZMotion = ConfigurableJointMotion.Free;
            gripJoint.linearLimit = new SoftJointLimit { limit = runtimeConfig.LinearFreedomMeters };
            var drive = new JointDrive
            {
                positionSpring = runtimeConfig.ConstraintSpring,
                positionDamper = runtimeConfig.ConstraintDamper,
                maximumForce = runtimeConfig.BreakForceNewtons
            };
            gripJoint.xDrive = drive;
            gripJoint.yDrive = drive;
            gripJoint.zDrive = drive;
            gripJoint.breakForce = runtimeConfig.BreakForceNewtons;
            gripJoint.breakTorque = runtimeConfig.BreakTorqueNewtonMeters;
            gripJoint.projectionMode = JointProjectionMode.None;
            attachedPayload = payload;
            supportedPayloadMass = 0f;
            State = DroneEquipmentState.Carrying;
            SetHint($"已抓住 {payload.Body.mass:0.##} kg 载荷");
        }

        private void StepSupportedLoad(float deltaTime)
        {
            if (attachedPayload == null || gripJoint == null)
            {
                supportedPayloadMass = Mathf.MoveTowards(supportedPayloadMass, 0f, deltaTime * 10f);
                return;
            }

            var gravity = Mathf.Max(0.01f, Physics.gravity.magnitude);
            var verticalTension = Mathf.Max(0f, Vector3.Dot(gripJoint.currentForce, Vector3.up));
            var targetMass = Mathf.Clamp(verticalTension / gravity, 0f, attachedPayload.Body.mass);
            var speed = attachedPayload.Body.mass / Mathf.Max(0.01f, runtimeConfig.SupportedLoadSmoothingSeconds);
            supportedPayloadMass = Mathf.MoveTowards(supportedPayloadMass, targetMass, speed * deltaTime);
        }

        private void ReleaseGrip()
        {
            if (gripJoint != null)
            {
                gripJoint.connectedBody = null;
                Destroy(gripJoint);
            }

            gripJoint = null;
            attachedPayload = null;
            supportedPayloadMass = 0f;
            if (State == DroneEquipmentState.Carrying)
            {
                State = DroneEquipmentState.Ready;
            }
        }

        private void SetClawCollision(bool enabled)
        {
            foreach (var collider in clawColliders)
            {
                if (collider != null)
                {
                    collider.enabled = enabled;
                }
            }
        }

        private void ConfigureInternalCollisions()
        {
            var ownColliders = GetComponentsInChildren<Collider>(true);
            foreach (var collider in ownColliders)
            {
                foreach (var other in ownColliders)
                {
                    if (collider != null && other != null && collider != other)
                    {
                        Physics.IgnoreCollision(collider, other, true);
                    }
                }
            }

            if (droneBody == null)
            {
                return;
            }

            foreach (var collider in ownColliders)
            {
                foreach (var bodyCollider in droneBody.GetComponentsInChildren<Collider>(true))
                {
                    if (collider != null && bodyCollider != null)
                    {
                        Physics.IgnoreCollision(collider, bodyCollider, true);
                    }
                }
            }
        }

        private void SetHint(string value)
        {
            LastHint = value;
        }
    }
}
