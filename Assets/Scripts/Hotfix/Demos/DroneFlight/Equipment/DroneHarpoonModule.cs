using System;
using UnityEngine;

namespace Hotfix.DroneFlight
{
    /// <summary>单发可回收渔叉、真实后坐、刚性命中与只受拉柔性绳索。</summary>
    public sealed class DroneHarpoonModule : MonoBehaviour, IDroneEquipmentModule, IDroneAimingEquipment,
        IDroneAutomatedAimingEquipment
    {
        [SerializeField] private DroneHarpoonConfig configSource;
        [SerializeField] private Transform gimbal;
        [SerializeField] private Transform muzzle;
        [SerializeField] private Rigidbody projectileBody;
        [SerializeField] private Collider projectileCollider;
        [SerializeField] private DroneHarpoonProjectile projectile;
        [SerializeField] private DroneHarpoonRopeVisual ropeVisual;
        [SerializeField] private LineRenderer aimReticle;

        private DroneHarpoonConfig runtimeConfig;
        private Rigidbody droneBody;
        private Camera aimCamera;
        private DroneCameraRig cameraRig;
        private FixedJoint hitJoint;
        private Rigidbody hitBody;
        private Collider hitCollider;
        private string sourceSignature;
        private float targetRopeLength;
        private float ropeTension;
        private float supportedPayloadMass;
        private float lineInput;
        private bool aimValid;
        private bool automatedAimActive;
        private Vector3 aimDirection;
        private Vector3 automatedAimTarget;
        private Vector3 hitPoint;
        private Vector2 aimViewportPosition = new(0.5f, 0.5f);

        bool IDroneAimingEquipment.IsAimModeActive => cameraRig != null
                                                     && cameraRig.Mode == DroneCameraMode.HarpoonAim;

        public DroneEquipmentKind Kind => DroneEquipmentKind.Harpoon;
        public DroneEquipmentState State { get; private set; } = DroneEquipmentState.Stowed;
        public float IntegratedDynamicMassKilograms => projectileBody != null && !projectileBody.isKinematic
            ? Mathf.Max(0f, projectileBody.mass)
            : 0f;
        public float SupportedIntegratedDynamicMassKilograms => 0f;
        public float HardwareMassKilograms => 0f;
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
            ropeVisual?.ConfigureEndpoints(muzzle, projectileBody != null ? projectileBody.transform : null);
            ropeVisual?.SetTargetLength(targetRopeLength);
            ApplyMassDistribution();
            DockProjectileImmediate();
        }

        private void FixedUpdate()
        {
            SynchronizeRuntimeConfig();
            if (runtimeConfig == null || droneBody == null || projectileBody == null || muzzle == null)
            {
                return;
            }

            UpdateAim();
            if (State == DroneEquipmentState.Stowed)
            {
                MaintainDockedPose();
            }
            StepRopeLength(Time.fixedDeltaTime);
            StepRopePhysics(Time.fixedDeltaTime);
            StepRecovery();
            ropeVisual?.SetTargetLength(targetRopeLength);
        }

        private void LateUpdate()
        {
            if (State == DroneEquipmentState.Stowed)
            {
                MaintainDockedRenderPose();
            }
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

        void IDroneAimingEquipment.ConfigureAim(DroneCameraRig rig)
        {
            cameraRig = rig;
        }

        void IDroneAimingEquipment.SetAimMode(bool active)
        {
            if (cameraRig == null || runtimeConfig == null)
            {
                return;
            }

            if (active)
            {
                cameraRig.EnterHarpoonAim();
                SetHint("机腹瞄准已开启，移动鼠标后按 H 发射");
            }
            else
            {
                cameraRig.ExitHarpoonAim();
                aimValid = false;
                if (aimReticle != null)
                {
                    aimReticle.enabled = false;
                }
            }
        }

        bool IDroneAutomatedAimingEquipment.TrySetAutomatedAimTarget(Vector3 worldPoint)
        {
            if (!IsFinite(worldPoint) || runtimeConfig == null || State != DroneEquipmentState.Stowed)
            {
                return false;
            }

            automatedAimTarget = worldPoint;
            automatedAimActive = true;
            return true;
        }

        void IDroneAutomatedAimingEquipment.ClearAutomatedAimTarget()
        {
            automatedAimActive = false;
            aimValid = false;
            if (aimReticle != null)
            {
                aimReticle.enabled = false;
            }
        }

        void IDroneAimingEquipment.SetAimViewportPosition(Vector2 viewportPosition)
        {
            aimViewportPosition = new Vector2(
                Mathf.Clamp01(viewportPosition.x),
                Mathf.Clamp01(viewportPosition.y));
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
            hitBody = null;
            hitCollider = null;
            ropeTension = 0f;
            supportedPayloadMass = 0f;
            ropeVisual?.SetVisible(false);
            if (aimReticle != null)
            {
                aimReticle.enabled = false;
            }
            cameraRig?.ExitHarpoonAim();
            automatedAimActive = false;
        }

        internal void Configure(
            DroneHarpoonConfig config,
            Transform aimingGimbal,
            Transform firePoint,
            Rigidbody projectileRigidBody,
            Collider projectileCollision,
            DroneHarpoonProjectile projectileRelay,
            DroneHarpoonRopeVisual rope,
            LineRenderer reticle)
        {
            configSource = config;
            gimbal = aimingGimbal;
            muzzle = firePoint;
            projectileBody = projectileRigidBody;
            projectileCollider = projectileCollision;
            projectile = projectileRelay;
            ropeVisual = rope;
            aimReticle = reticle;
            ropeVisual?.ConfigureEndpoints(muzzle, projectileBody != null ? projectileBody.transform : null);
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
            if (runtimeConfig == null || projectileBody == null)
            {
                return;
            }

            projectileBody.mass = runtimeConfig.ProjectileMassKilograms;
            projectileBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            projectileBody.interpolation = State == DroneEquipmentState.Stowed
                ? RigidbodyInterpolation.None
                : RigidbodyInterpolation.Interpolate;
        }

        private void UpdateAim()
        {
            if (gimbal == null || muzzle == null || droneBody == null || runtimeConfig == null)
            {
                InvalidateAim();
                return;
            }

            if (automatedAimActive)
            {
                UpdateAutomatedAim();
                return;
            }

            if (aimCamera == null || cameraRig == null || cameraRig.Mode != DroneCameraMode.HarpoonAim)
            {
                InvalidateAim();
                return;
            }

            var ray = aimCamera.ViewportPointToRay(new Vector3(aimViewportPosition.x, aimViewportPosition.y, 0f));
            var maximumDistance = runtimeConfig.MaximumFlightDistanceMeters;
            var hits = Physics.RaycastAll(ray, maximumDistance, runtimeConfig.HittableLayers,
                QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            var hasHit = false;
            var hit = default(RaycastHit);
            foreach (var candidate in hits)
            {
                if (candidate.collider == null || candidate.transform.IsChildOf(transform)
                    || candidate.transform.IsChildOf(droneBody.transform))
                {
                    continue;
                }

                hit = candidate;
                hasHit = true;
                break;
            }
            if (hasHit)
            {
                hitPoint = hit.point;
            }
            else
            {
                var fallbackPlane = new Plane(Vector3.up, droneBody.position - Vector3.up * 3f);
                hitPoint = fallbackPlane.Raycast(ray, out var enter)
                    ? ray.GetPoint(Mathf.Min(enter, maximumDistance))
                    : ray.GetPoint(maximumDistance);
            }

            var verticalAxis = -droneBody.transform.up;
            var worldDirection = (hitPoint - muzzle.position).normalized;
            var withinEnvelope = DroneEquipmentPhysicsMath.IsWithinHarpoonAimEnvelope(
                droneBody.worldCenterOfMass,
                muzzle.position,
                verticalAxis,
                hitPoint,
                runtimeConfig.MaximumAimRadiusMeters,
                runtimeConfig.MaximumAimConeDegrees);
            if (worldDirection.sqrMagnitude > 0.0001f)
            {
                gimbal.rotation = Quaternion.LookRotation(worldDirection, droneBody.transform.forward);
            }
            aimDirection = muzzle.forward;
            aimValid = withinEnvelope
                       && Vector3.Angle(aimDirection, worldDirection) <= runtimeConfig.AllowedAimErrorDegrees;
            UpdateAimReticle();
            if (!aimValid && State == DroneEquipmentState.Stowed)
            {
                SetHint("目标超出 3 m 水平范围或向下射界");
            }
        }

        private void UpdateAutomatedAim()
        {
            var delta = automatedAimTarget - muzzle.position;
            var maximumDistance = runtimeConfig.MaximumFlightDistanceMeters;
            if (delta.sqrMagnitude <= 0.0001f || delta.magnitude > maximumDistance)
            {
                InvalidateAim();
                return;
            }

            var direction = delta.normalized;
            var hits = Physics.RaycastAll(
                muzzle.position,
                direction,
                maximumDistance,
                runtimeConfig.HittableLayers,
                QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            var foundTarget = false;
            foreach (var candidate in hits)
            {
                if (candidate.collider == null || candidate.transform.IsChildOf(transform)
                    || candidate.transform.IsChildOf(droneBody.transform))
                {
                    continue;
                }

                hitPoint = candidate.point;
                foundTarget = true;
                break;
            }

            if (!foundTarget)
            {
                InvalidateAim();
                SetHint("自动瞄准射线上没有有效目标");
                return;
            }

            var verticalAxis = -droneBody.transform.up;
            var worldDirection = (hitPoint - muzzle.position).normalized;
            var withinEnvelope = DroneEquipmentPhysicsMath.IsWithinHarpoonAimEnvelope(
                droneBody.worldCenterOfMass,
                muzzle.position,
                verticalAxis,
                hitPoint,
                runtimeConfig.MaximumAimRadiusMeters,
                runtimeConfig.MaximumAimConeDegrees);
            var launchSpeed = runtimeConfig.LaunchImpulseNewtonSeconds
                              / Mathf.Max(0.0001f, runtimeConfig.ProjectileMassKilograms);
            if (!DroneEquipmentPhysicsMath.TryCalculateBallisticDirection(
                    muzzle.position,
                    hitPoint,
                    launchSpeed,
                    Physics.gravity,
                    out var ballisticDirection))
            {
                InvalidateAim();
                SetHint("自动目标超出渔叉弹道范围");
                return;
            }

            gimbal.rotation = Quaternion.LookRotation(ballisticDirection, droneBody.transform.forward);
            aimDirection = muzzle.forward;
            aimValid = withinEnvelope
                       && Vector3.Angle(aimDirection, ballisticDirection) <= runtimeConfig.AllowedAimErrorDegrees;
            UpdateAimReticle();
            if (!aimValid)
            {
                SetHint("自动目标超出渔叉射界");
            }
        }

        private void InvalidateAim()
        {
            aimValid = false;
            if (aimReticle != null)
            {
                aimReticle.enabled = false;
            }
        }

        private void Fire()
        {
            var manualAimActive = cameraRig != null && cameraRig.Mode == DroneCameraMode.HarpoonAim;
            if ((!manualAimActive && !automatedAimActive) || !aimValid)
            {
                SetHint("请先按 V 进入机腹瞄准，并将准星移入有效范围");
                return;
            }

            MaintainDockedRenderPose();
            projectileBody.position = muzzle.position;
            projectileBody.rotation = muzzle.rotation;
            projectileBody.interpolation = RigidbodyInterpolation.Interpolate;
            projectileBody.isKinematic = false;
            projectileBody.useGravity = true;
            projectileCollider.enabled = true;
            projectileBody.linearVelocity = droneBody.GetPointVelocity(muzzle.position);
            projectileBody.angularVelocity = Vector3.zero;
            var impulse = DroneEquipmentPhysicsMath.CalculateHarpoonImpulse(
                aimDirection,
                runtimeConfig.LaunchImpulseNewtonSeconds);
            projectileBody.AddForce(impulse, ForceMode.Impulse);
            droneBody.AddForceAtPosition(-impulse, muzzle.position, ForceMode.Impulse);
            targetRopeLength = runtimeConfig.MinimumRopeLengthMeters;
            ropeTension = 0f;
            ropeVisual?.ResetSimulation(muzzle.position, projectileBody.position);
            ropeVisual?.SetVisible(true);
            State = DroneEquipmentState.Fired;
            automatedAimActive = false;
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

            if (State == DroneEquipmentState.Recovering)
            {
                targetRopeLength = Mathf.MoveTowards(
                    targetRopeLength,
                    0f,
                    runtimeConfig.AutomaticRecoverySpeedMetersPerSecond * deltaTime);
                return;
            }

            targetRopeLength = Mathf.Clamp(
                targetRopeLength + lineInput * runtimeConfig.ReelSpeedMetersPerSecond * deltaTime,
                runtimeConfig.MinimumRopeLengthMeters,
                runtimeConfig.MaximumRopeLengthMeters);
        }

        private void StepRopePhysics(float deltaTime)
        {
            if (State is DroneEquipmentState.Stowed or DroneEquipmentState.Recovering)
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
            projectileBody.useGravity = true;
            SetHint(hint);
        }

        private void StepRecovery()
        {
            if (State != DroneEquipmentState.Recovering)
            {
                return;
            }

            var toMuzzle = muzzle.position - projectileBody.worldCenterOfMass;
            var positionError = toMuzzle.magnitude;
            var muzzleVelocity = droneBody.GetPointVelocity(muzzle.position);
            var relativeVelocity = projectileBody.linearVelocity - muzzleVelocity;
            var responseSeconds = Mathf.Max(0.01f, runtimeConfig.RecoveryResponseSeconds);
            var desiredRelativeVelocity = positionError > 0.0001f
                ? toMuzzle / positionError
                  * Mathf.Min(runtimeConfig.AutomaticRecoverySpeedMetersPerSecond, positionError / responseSeconds)
                : Vector3.zero;
            var acceleration = Vector3.ClampMagnitude(
                (desiredRelativeVelocity - relativeVelocity) / responseSeconds - Physics.gravity,
                runtimeConfig.MaximumRecoveryAccelerationMetersPerSecondSquared);
            var recoveryForce = acceleration * projectileBody.mass;
            projectileBody.AddForce(recoveryForce, ForceMode.Force);
            droneBody.AddForceAtPosition(-recoveryForce, muzzle.position, ForceMode.Force);
            ropeTension = recoveryForce.magnitude;

            if (projectileCollider != null && positionError <= 0.3f)
            {
                projectileCollider.enabled = false;
            }

            var relativeSpeed = relativeVelocity.magnitude;
            if (positionError <= runtimeConfig.DockPositionToleranceMeters
                && relativeSpeed <= runtimeConfig.DockSpeedToleranceMetersPerSecond)
            {
                DockProjectileImmediate();
            }
        }

        private void DockProjectileImmediate()
        {
            if (projectileBody == null || muzzle == null)
            {
                return;
            }

            DestroyJoint(ref hitJoint);
            if (!projectileBody.isKinematic)
            {
                projectileBody.linearVelocity = Vector3.zero;
                projectileBody.angularVelocity = Vector3.zero;
            }
            projectileBody.useGravity = false;
            projectileBody.isKinematic = true;
            projectileBody.interpolation = RigidbodyInterpolation.None;
            projectileBody.position = muzzle.position;
            projectileBody.rotation = muzzle.rotation;
            MaintainDockedRenderPose();
            projectileCollider.enabled = false;
            targetRopeLength = runtimeConfig != null ? runtimeConfig.MinimumRopeLengthMeters : 0.25f;
            ropeTension = 0f;
            supportedPayloadMass = 0f;
            ropeVisual?.ResetSimulation(muzzle.position, muzzle.position);
            ropeVisual?.SetVisible(false);
            State = DroneEquipmentState.Stowed;
            SetHint("渔叉已回到发射器");
        }

        // 停靠弹体是发射器的一部分；瞄准云台转动时必须持续跟随 Muzzle，且绝不能参与物理。
        private void MaintainDockedPose()
        {
            if (projectileBody == null || muzzle == null)
            {
                return;
            }

            projectileBody.position = muzzle.position;
            projectileBody.rotation = muzzle.rotation;
            projectileBody.useGravity = false;
            projectileBody.isKinematic = true;
            projectileBody.interpolation = RigidbodyInterpolation.None;
            if (projectileCollider != null)
            {
                projectileCollider.enabled = false;
            }

            MaintainDockedRenderPose();
            ropeVisual?.SetVisible(false);
        }

        // 停靠态禁用 Rigidbody 插值后，在渲染帧末贴合当前枪口，避免弹体落后于机体插值姿态。
        private void MaintainDockedRenderPose()
        {
            if (projectileBody == null || muzzle == null)
            {
                return;
            }

            projectileBody.transform.SetPositionAndRotation(muzzle.position, muzzle.rotation);
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

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        }

        private void UpdateAimReticle()
        {
            if (aimReticle == null)
            {
                return;
            }

            const int segmentCount = 24;
            const float radius = 0.12f;
            aimReticle.enabled = true;
            aimReticle.positionCount = segmentCount;
            aimReticle.loop = true;
            aimReticle.startColor = aimReticle.endColor = aimValid ? Color.green : Color.red;
            for (var index = 0; index < segmentCount; index++)
            {
                var angle = index * Mathf.PI * 2f / segmentCount;
                aimReticle.SetPosition(index,
                    hitPoint + Vector3.up * 0.01f
                    + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius);
            }
        }
    }
}
