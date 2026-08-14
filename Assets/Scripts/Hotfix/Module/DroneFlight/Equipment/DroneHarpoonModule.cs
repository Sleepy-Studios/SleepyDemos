using UnityEngine;

namespace Hotfix.DroneFlight
{
    /// <summary>单发可回收渔叉、真实后坐、刚性命中与只受拉柔性绳索。</summary>
    public sealed class DroneHarpoonModule : MonoBehaviour, IDroneEquipmentModule
    {
        [SerializeField] private DroneHarpoonConfig configSource;
        [SerializeField] private Rigidbody launcherBody;
        [SerializeField] private ConfigurableJoint launcherJoint;
        [SerializeField] private Transform gimbal;
        [SerializeField] private Transform muzzle;
        [SerializeField] private Rigidbody projectileBody;
        [SerializeField] private Collider projectileCollider;
        [SerializeField] private DroneHarpoonProjectile projectile;
        [SerializeField] private DroneHarpoonRopeVisual ropeVisual;

        private DroneHarpoonConfig runtimeConfig;
        private Rigidbody droneBody;
        private Camera aimCamera;
        private FixedJoint dockJoint;
        private FixedJoint hitJoint;
        private Rigidbody hitBody;
        private Collider hitCollider;
        private string sourceSignature;
        private float targetRopeLength;
        private float ropeTension;
        private float supportedPayloadMass;
        private float lineInput;
        private bool aimValid;
        private Vector3 aimDirection;
        private Vector3 hitPoint;

        public DroneEquipmentKind Kind => DroneEquipmentKind.Harpoon;
        public DroneEquipmentState State { get; private set; } = DroneEquipmentState.Stowed;
        public float HardwareMassKilograms => runtimeConfig != null ? runtimeConfig.HardwareMassKilograms : 0f;
        public float PayloadMassKilograms => hitBody != null ? hitBody.mass : 0f;
        public float SupportedPayloadMassKilograms => supportedPayloadMass;
        public string LastHint { get; private set; } = string.Empty;
        public DroneEquipmentSnapshot Snapshot => new(
            Kind,
            State,
            LastHint,
            HardwareMassKilograms,
            PayloadMassKilograms,
            SupportedPayloadMassKilograms,
            targetRopeLength,
            ropeTension,
            hitCollider != null ? 1 : 0,
            aimValid && State == DroneEquipmentState.Stowed,
            aimDirection,
            hitPoint);

        private void Awake()
        {
            CreateRuntimeConfig();
            projectile?.Configure(this);
            targetRopeLength = runtimeConfig != null ? runtimeConfig.MinimumRopeLengthMeters : 0.25f;
            ApplyMassDistribution();
        }

        private void FixedUpdate()
        {
            SynchronizeRuntimeConfig();
            if (runtimeConfig == null || droneBody == null || projectileBody == null || muzzle == null)
            {
                return;
            }

            UpdateAim();
            StepRopeLength(Time.fixedDeltaTime);
            StepRopePhysics(Time.fixedDeltaTime);
            StepRecovery();
            ropeVisual?.Step(muzzle.position, projectileBody.position, targetRopeLength, Time.fixedDeltaTime);
        }

        private void OnDestroy()
        {
            ReleaseAndCleanup();
            if (runtimeConfig != null)
            {
                Destroy(runtimeConfig);
            }
        }

        public void ConfigureHost(Rigidbody body, Camera camera, float maximumPayloadKilograms)
        {
            droneBody = body;
            aimCamera = camera;
            if (launcherJoint != null)
            {
                launcherJoint.connectedBody = droneBody;
                launcherJoint.projectionMode = JointProjectionMode.None;
            }

            ConfigureInternalCollisions();
            DockProjectileImmediate();
        }

        public void PrimaryAction()
        {
            if (State == DroneEquipmentState.Stowed)
            {
                Fire();
            }
            else
            {
                BeginRecovery("渔叉已解除，自动回收中");
            }
        }

        public void ToggleDeployment()
        {
        }

        public void SetLineInput(float input)
        {
            lineInput = Mathf.Clamp(input, -1f, 1f);
        }

        public void SynchronizeRuntimeConfig()
        {
            if (configSource == null)
            {
                return;
            }

            var json = JsonUtility.ToJson(configSource);
            if (runtimeConfig != null && sourceSignature == json)
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
            targetRopeLength = Mathf.Clamp(
                targetRopeLength,
                runtimeConfig.MinimumRopeLengthMeters,
                runtimeConfig.MaximumRopeLengthMeters);
        }

        public void ReleaseAndCleanup()
        {
            if (hitCollider != null && projectileCollider != null)
            {
                Physics.IgnoreCollision(projectileCollider, hitCollider, false);
            }

            DestroyJoint(ref hitJoint);
            DestroyJoint(ref dockJoint);
            hitBody = null;
            hitCollider = null;
            ropeTension = 0f;
            supportedPayloadMass = 0f;
            ropeVisual?.SetVisible(false);
        }

        internal void Configure(
            DroneHarpoonConfig config,
            Rigidbody launcher,
            ConfigurableJoint launcherConnection,
            Transform aimingGimbal,
            Transform firePoint,
            Rigidbody projectileRigidBody,
            Collider projectileCollision,
            DroneHarpoonProjectile projectileRelay,
            DroneHarpoonRopeVisual rope)
        {
            configSource = config;
            launcherBody = launcher;
            launcherJoint = launcherConnection;
            gimbal = aimingGimbal;
            muzzle = firePoint;
            projectileBody = projectileRigidBody;
            projectileCollider = projectileCollision;
            projectile = projectileRelay;
            ropeVisual = rope;
            CreateRuntimeConfig();
            ApplyMassDistribution();
            projectile?.Configure(this);
        }

        internal void NotifyProjectileHit(Collider other, Vector3 worldPoint)
        {
            if (State != DroneEquipmentState.Fired || other == null || IsIgnoredLayer(other.gameObject.layer)
                || !IsHittableLayer(other.gameObject.layer) || other.transform.IsChildOf(transform))
            {
                return;
            }

            hitPoint = worldPoint;
            hitCollider = other;
            hitBody = other.attachedRigidbody;
            hitJoint = projectileBody.gameObject.AddComponent<FixedJoint>();
            hitJoint.connectedBody = hitBody;
            hitJoint.breakForce = Mathf.Infinity;
            hitJoint.breakTorque = Mathf.Infinity;
            hitJoint.enablePreprocessing = true;
            Physics.IgnoreCollision(projectileCollider, other, true);
            State = DroneEquipmentState.Attached;
            SetHint(hitBody != null ? "渔叉已刚性命中动态目标" : "渔叉已固定到静态目标");
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
            if (runtimeConfig == null || projectileBody == null || launcherBody == null)
            {
                return;
            }

            projectileBody.mass = runtimeConfig.ProjectileMassKilograms;
            launcherBody.mass = Mathf.Max(
                0.001f,
                runtimeConfig.HardwareMassKilograms - runtimeConfig.ProjectileMassKilograms);
            projectileBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            projectileBody.interpolation = RigidbodyInterpolation.Interpolate;
            launcherBody.interpolation = RigidbodyInterpolation.Interpolate;
        }

        private void UpdateAim()
        {
            if (aimCamera == null || gimbal == null)
            {
                aimValid = false;
                return;
            }

            var ray = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            var maximumDistance = runtimeConfig.MaximumFlightDistanceMeters;
            hitPoint = Physics.Raycast(ray, out var hit, maximumDistance, runtimeConfig.HittableLayers,
                QueryTriggerInteraction.Ignore)
                ? hit.point
                : ray.GetPoint(maximumDistance);
            var worldDirection = (hitPoint - muzzle.position).normalized;
            var local = droneBody.transform.InverseTransformDirection(worldDirection);
            var yaw = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            var pitch = -Mathf.Asin(Mathf.Clamp(local.y, -1f, 1f)) * Mathf.Rad2Deg;
            var clampedYaw = Mathf.Clamp(yaw, -runtimeConfig.GimbalYawLimitDegrees, runtimeConfig.GimbalYawLimitDegrees);
            var clampedPitch = Mathf.Clamp(
                pitch,
                -runtimeConfig.GimbalPitchUpLimitDegrees,
                runtimeConfig.GimbalPitchDownLimitDegrees);
            gimbal.localRotation = Quaternion.Euler(clampedPitch, clampedYaw, 0f);
            aimDirection = muzzle.forward;
            aimValid = Mathf.Abs(yaw - clampedYaw) <= runtimeConfig.AllowedAimErrorDegrees
                       && Mathf.Abs(pitch - clampedPitch) <= runtimeConfig.AllowedAimErrorDegrees
                       && Vector3.Angle(aimDirection, worldDirection) <= runtimeConfig.AllowedAimErrorDegrees;
            if (!aimValid && State == DroneEquipmentState.Stowed)
            {
                SetHint("目标超出渔叉云台限位");
            }
        }

        private void Fire()
        {
            if (!aimValid)
            {
                SetHint("当前瞄准方向不可发射");
                return;
            }

            DestroyJoint(ref dockJoint);
            projectileBody.isKinematic = false;
            projectileBody.useGravity = true;
            projectileCollider.enabled = true;
            projectileBody.position = muzzle.position;
            projectileBody.rotation = muzzle.rotation;
            projectileBody.linearVelocity = droneBody.GetPointVelocity(muzzle.position);
            projectileBody.angularVelocity = Vector3.zero;
            var impulse = DroneEquipmentPhysicsMath.CalculateHarpoonImpulse(
                aimDirection,
                runtimeConfig.ProjectileMassKilograms,
                runtimeConfig.MuzzleSpeedMetersPerSecond);
            projectileBody.AddForce(impulse, ForceMode.Impulse);
            droneBody.AddForceAtPosition(-impulse, muzzle.position, ForceMode.Impulse);
            targetRopeLength = runtimeConfig.MinimumRopeLengthMeters;
            ropeTension = 0f;
            ropeVisual?.ResetSimulation(muzzle.position, projectileBody.position);
            ropeVisual?.SetVisible(true);
            State = DroneEquipmentState.Fired;
            SetHint("渔叉已发射");
        }

        private void StepRopeLength(float deltaTime)
        {
            if (State == DroneEquipmentState.Stowed)
            {
                return;
            }

            if (State == DroneEquipmentState.Fired && lineInput >= 0f)
            {
                targetRopeLength = Mathf.Max(
                    targetRopeLength,
                    Mathf.Min(runtimeConfig.MaximumRopeLengthMeters,
                        Vector3.Distance(muzzle.position, projectileBody.position)));
            }

            var speed = State == DroneEquipmentState.Recovering
                ? runtimeConfig.AutomaticRecoverySpeedMetersPerSecond
                : runtimeConfig.ReelSpeedMetersPerSecond;
            var direction = State == DroneEquipmentState.Recovering ? -1f : lineInput;
            targetRopeLength = Mathf.Clamp(
                targetRopeLength + direction * speed * deltaTime,
                runtimeConfig.MinimumRopeLengthMeters,
                runtimeConfig.MaximumRopeLengthMeters);
        }

        private void StepRopePhysics(float deltaTime)
        {
            if (State == DroneEquipmentState.Stowed)
            {
                ropeTension = 0f;
                supportedPayloadMass = 0f;
                return;
            }

            var delta = projectileBody.worldCenterOfMass - muzzle.position;
            var distance = delta.magnitude;
            if (distance <= targetRopeLength || distance <= 0.0001f)
            {
                ropeTension = 0f;
                supportedPayloadMass = Mathf.MoveTowards(supportedPayloadMass, 0f, deltaTime * 10f);
                return;
            }

            var direction = delta / distance;
            var droneVelocity = droneBody.GetPointVelocity(muzzle.position);
            var relativeVelocity = Vector3.Dot(projectileBody.GetPointVelocity(projectileBody.worldCenterOfMass)
                                               - droneVelocity, direction);
            var rawTension = DroneEquipmentPhysicsMath.CalculateRawTension(
                distance,
                targetRopeLength,
                relativeVelocity,
                runtimeConfig.RopeSpringNewtonsPerMeter,
                runtimeConfig.RopeDamperNewtonSecondsPerMeter);
            ropeTension = Mathf.Min(
                rawTension,
                runtimeConfig.MaximumTensionNewtons);
            var force = direction * ropeTension;
            droneBody.AddForceAtPosition(force, muzzle.position, ForceMode.Force);
            projectileBody.AddForceAtPosition(-force, projectileBody.worldCenterOfMass, ForceMode.Force);
            var supported = DroneEquipmentPhysicsMath.CalculateSupportedMass(
                Mathf.Max(0f, -Vector3.Dot(force, Vector3.up)),
                Physics.gravity.magnitude,
                PayloadMassKilograms);
            supportedPayloadMass = Mathf.MoveTowards(
                supportedPayloadMass,
                Mathf.Min(PayloadMassKilograms, supported),
                deltaTime * Mathf.Max(1f, PayloadMassKilograms * 6f));

            if (rawTension >= runtimeConfig.RopeBreakForceNewtons)
            {
                State = DroneEquipmentState.Broken;
                BeginRecovery("绳索超过断裂力，正在回收渔叉");
            }
        }

        private void BeginRecovery(string hint)
        {
            if (hitCollider != null && projectileCollider != null)
            {
                Physics.IgnoreCollision(projectileCollider, hitCollider, false);
            }

            DestroyJoint(ref hitJoint);
            hitBody = null;
            hitCollider = null;
            supportedPayloadMass = 0f;
            lineInput = 0f;
            ropeTension = 0f;
            targetRopeLength = Mathf.Clamp(
                Vector3.Distance(muzzle.position, projectileBody.position),
                runtimeConfig.MinimumRopeLengthMeters,
                runtimeConfig.MaximumRopeLengthMeters);
            ropeVisual?.ResetSimulation(muzzle.position, projectileBody.position);
            State = DroneEquipmentState.Recovering;
            SetHint(hint);
        }

        private void StepRecovery()
        {
            if (State != DroneEquipmentState.Recovering)
            {
                return;
            }

            var positionError = Vector3.Distance(projectileBody.position, muzzle.position);
            var relativeSpeed = (projectileBody.linearVelocity - droneBody.GetPointVelocity(muzzle.position)).magnitude;
            if (positionError <= runtimeConfig.DockPositionToleranceMeters
                && relativeSpeed <= runtimeConfig.DockSpeedToleranceMetersPerSecond)
            {
                DockProjectileImmediate();
            }
        }

        private void DockProjectileImmediate()
        {
            if (droneBody == null || projectileBody == null || muzzle == null)
            {
                return;
            }

            DestroyJoint(ref hitJoint);
            DestroyJoint(ref dockJoint);
            projectileBody.position = muzzle.position;
            projectileBody.rotation = muzzle.rotation;
            if (!projectileBody.isKinematic)
            {
                projectileBody.linearVelocity = Vector3.zero;
                projectileBody.angularVelocity = Vector3.zero;
            }
            projectileBody.useGravity = false;
            projectileBody.isKinematic = true;
            projectileCollider.enabled = false;
            targetRopeLength = runtimeConfig != null ? runtimeConfig.MinimumRopeLengthMeters : 0.25f;
            ropeTension = 0f;
            supportedPayloadMass = 0f;
            ropeVisual?.ResetSimulation(muzzle.position, muzzle.position);
            ropeVisual?.SetVisible(false);
            State = DroneEquipmentState.Stowed;
            SetHint("渔叉已回到发射器");
        }

        private bool IsHittableLayer(int layer) => (runtimeConfig.HittableLayers.value & (1 << layer)) != 0;
        private bool IsIgnoredLayer(int layer) => (runtimeConfig.IgnoredLayers.value & (1 << layer)) != 0;

        private void ConfigureInternalCollisions()
        {
            if (droneBody == null || projectileCollider == null)
            {
                return;
            }

            foreach (var bodyCollider in droneBody.GetComponentsInChildren<Collider>(true))
            {
                if (bodyCollider != null && bodyCollider != projectileCollider)
                {
                    Physics.IgnoreCollision(projectileCollider, bodyCollider, true);
                }
            }

            if (launcherBody == null)
            {
                return;
            }

            foreach (var launcherCollider in launcherBody.GetComponentsInChildren<Collider>(true))
            {
                if (launcherCollider != null && launcherCollider != projectileCollider)
                {
                    Physics.IgnoreCollision(projectileCollider, launcherCollider, true);
                }
            }
        }

        private static void DestroyJoint<T>(ref T joint) where T : Joint
        {
            if (joint != null)
            {
                joint.connectedBody = null;
                Destroy(joint);
            }

            joint = null;
        }

        private void SetHint(string value)
        {
            LastHint = value;
        }
    }
}
