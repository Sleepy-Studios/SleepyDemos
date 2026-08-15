using System;
using UnityEngine;

namespace Hotfix.DroneFlight
{
    /// <summary>单根刚性吊臂四爪抓斗；上端万向节被动摆动，闭爪包围载荷后建立临时刚性连接。</summary>
    public sealed class DroneGrappleModule : MonoBehaviour, IDroneEquipmentModule
    {
        [SerializeField] private DroneGrappleConfig configSource;
        [SerializeField] private Transform bellyMount;
        [SerializeField] private Transform liftCarriage;
        [SerializeField] private Transform liftSleeveVisual;
        [SerializeField] private Rigidbody grappleBody;
        [SerializeField] private ConfigurableJoint suspensionJoint;
        [SerializeField] private BoxCollider captureVolume;
        [SerializeField] private Rigidbody[] clawBodies = Array.Empty<Rigidbody>();
        [SerializeField] private HingeJoint[] clawJoints = Array.Empty<HingeJoint>();
        [SerializeField] private Collider[] clawColliders = Array.Empty<Collider>();

        private DroneGrappleConfig runtimeConfig;
        private Rigidbody droneBody;
        private readonly Collider[] captureHits = new Collider[32];
        private FixedJoint gripJoint;
        private DronePayload attachedPayload;
        private string sourceSignature;
        private float supportedPayloadMass;
        private bool clawsClosed;
        private bool initialized;
        private float maximumPayloadKilograms = float.PositiveInfinity;
        private int captureCandidateCount;
        private float currentLiftTravel;
        private float targetLiftTravel;
        private float currentLiftSpeed;
        private float lineInput;
        private LineRenderer assistRing;
        private TextMesh assistLabel;
        private Material assistMaterial;
        private DronePayload nearestCaptureCandidate;

        private const float GrappleSolverMassKilograms = 0.05f;
        private const int AssistRingSegments = 48;

        public DroneEquipmentKind Kind => DroneEquipmentKind.Grapple;
        public DroneEquipmentState State { get; private set; } = DroneEquipmentState.Ready;
        public float IntegratedDynamicMassKilograms => CalculateIntegratedDynamicMass();
        public float SupportedIntegratedDynamicMassKilograms => IntegratedDynamicMassKilograms;
        public float HardwareMassKilograms => 0f;
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
            currentLiftTravel,
            gripJoint != null ? gripJoint.currentForce.magnitude : 0f,
            captureCandidateCount,
            State is DroneEquipmentState.Ready or DroneEquipmentState.Carrying,
            Vector3.zero,
            attachedPayload != null ? attachedPayload.Body.worldCenterOfMass : transform.position);

        private void Awake()
        {
            CreateRuntimeConfig();
            ApplyMassDistribution();
            ConfigureInternalCollisions();
            SetClaws(false);
            SetClawCollision(true);
            TryInitializeAssembly();
            EnsureAssistVisual();
        }

        private void FixedUpdate()
        {
            SynchronizeRuntimeConfig();
            if (!initialized || runtimeConfig == null)
            {
                return;
            }

            StepGrip();
            StepLift(Time.fixedDeltaTime);
            StepSupportedLoad(Time.fixedDeltaTime);
            UpdateAssistVisual();
        }

        private void OnDestroy()
        {
            ReleaseAndCleanup();
            if (assistMaterial != null)
            {
                Destroy(assistMaterial);
            }
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
            TryInitializeAssembly();
        }

        public void PrimaryAction()
        {
            if (State is not DroneEquipmentState.Ready and not DroneEquipmentState.Carrying)
            {
                SetHint("抓斗当前不可操作");
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
            currentLiftTravel = Mathf.Clamp(
                currentLiftTravel,
                0f,
                runtimeConfig.MaximumLiftTravelMeters);
            targetLiftTravel = Mathf.Clamp(
                targetLiftTravel,
                0f,
                runtimeConfig.MaximumLiftTravelMeters);
            ApplyMassDistribution();
            ConfigureSuspensionJoint();
            ApplyClawDrive();
            TryInitializeAssembly();
        }

        public void ReleaseAndCleanup()
        {
            ReleaseGrip();
            captureCandidateCount = 0;
            lineInput = 0f;
            currentLiftSpeed = 0f;
            nearestCaptureCandidate = null;
            SetAssistVisible(false);
        }

        internal void Configure(
            DroneGrappleConfig config,
            Transform mount,
            Transform carriage,
            Transform sleeveVisual,
            Rigidbody body,
            ConfigurableJoint suspension,
            BoxCollider capture,
            Rigidbody[] bodies,
            HingeJoint[] joints,
            Collider[] colliders)
        {
            configSource = config;
            bellyMount = mount;
            liftCarriage = carriage;
            liftSleeveVisual = sleeveVisual;
            grappleBody = body;
            suspensionJoint = suspension;
            captureVolume = capture;
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
            var clawMass = GrappleSolverMassKilograms * 0.6f / clawCount;
            grappleBody.mass = GrappleSolverMassKilograms * 0.4f;
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
            suspensionJoint.angularYLimit = new SoftJointLimit { limit = runtimeConfig.SwingLimitDegrees };
            suspensionJoint.angularZLimit = new SoftJointLimit { limit = runtimeConfig.SwingLimitDegrees };
            var damping = Mathf.Min(
                runtimeConfig.MaximumDampingTorqueNewtonMeters,
                2f * runtimeConfig.DampingRatio
                * Mathf.Sqrt(Mathf.Max(0.001f, IntegratedDynamicMassKilograms * 9.81f)));
            suspensionJoint.rotationDriveMode = RotationDriveMode.Slerp;
            suspensionJoint.slerpDrive = new JointDrive
            {
                positionSpring = 0f,
                positionDamper = damping,
                maximumForce = runtimeConfig.MaximumDampingTorqueNewtonMeters
            };
            suspensionJoint.angularXMotion = ConfigurableJointMotion.Locked;
            suspensionJoint.angularYMotion = ConfigurableJointMotion.Limited;
            suspensionJoint.angularZMotion = ConfigurableJointMotion.Limited;
            UpdateJointAnchors();
        }

        private void PrepareInitialAssembly()
        {
            if (runtimeConfig == null || bellyMount == null || grappleBody == null)
            {
                return;
            }

            SetAssemblyKinematic(true);
            var totalArmLength = runtimeConfig.ArmLengthMeters + currentLiftTravel;
            var desiredBasePosition = bellyMount.position - bellyMount.up * totalArmLength;
            var delta = desiredBasePosition - grappleBody.position;
            grappleBody.position += delta;
            foreach (var body in clawBodies)
            {
                if (body != null)
                {
                    body.position += delta;
                }
            }
        }

        private void TryInitializeAssembly()
        {
            if (initialized || runtimeConfig == null || droneBody == null || bellyMount == null
                || grappleBody == null || suspensionJoint == null)
            {
                return;
            }

            PrepareInitialAssembly();
            ConfigureSuspensionJoint();
            ConfigureInternalCollisions();
            EnableAssemblyPhysics();
            initialized = true;
            State = DroneEquipmentState.Ready;
            SetHint("抓斗已就绪，可按 H 开合四爪");
        }

        private void UpdateJointAnchors()
        {
            if (suspensionJoint == null || droneBody == null || bellyMount == null)
            {
                return;
            }

            var fixedBellyAnchor = bellyMount.position;
            suspensionJoint.anchor = new Vector3(
                0f,
                runtimeConfig.ArmLengthMeters + currentLiftTravel,
                0f);
            suspensionJoint.connectedAnchor = droneBody.transform.InverseTransformPoint(fixedBellyAnchor);
            UpdateLiftVisual();
        }

        private void EnableAssemblyPhysics()
        {
            SetAssemblyKinematic(false);
        }

        private void SetAssemblyKinematic(bool value)
        {
            SetBodyKinematic(grappleBody, value);
            foreach (var body in clawBodies)
            {
                SetBodyKinematic(body, value);
            }
        }

        private static void SetBodyKinematic(Rigidbody body, bool value)
        {
            if (body == null)
            {
                return;
            }

            body.useGravity = !value;
            body.isKinematic = value;
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
                || captureVolume == null)
            {
                return;
            }

            var payload = nearestCaptureCandidate != null
                ? nearestCaptureCandidate
                : FindNearestEnclosedPayload();
            if (payload != null)
            {
                AttachPayload(payload);
            }
        }

        private DronePayload FindNearestEnclosedPayload()
        {
            var center = captureVolume.transform.TransformPoint(captureVolume.center);
            var halfExtents = Vector3.Scale(captureVolume.size * 0.5f, captureVolume.transform.lossyScale);
            var hitCount = Physics.OverlapBoxNonAlloc(
                center,
                halfExtents,
                captureHits,
                captureVolume.transform.rotation,
                ~0,
                QueryTriggerInteraction.Collide);
            DronePayload nearest = null;
            var nearestDistanceSquared = float.PositiveInfinity;
            var candidates = 0;
            for (var index = 0; index < hitCount; index++)
            {
                var collider = captureHits[index];
                captureHits[index] = null;
                var payload = collider != null ? collider.GetComponentInParent<DronePayload>() : null;
                if (payload == null || payload.Body == null || payload.Body == grappleBody)
                {
                    continue;
                }

                var localCenter = captureVolume.transform.InverseTransformPoint(payload.Body.worldCenterOfMass)
                                  - captureVolume.center;
                var horizontalDistance = new Vector2(localCenter.x, localCenter.z).magnitude;
                if (horizontalDistance > runtimeConfig.EnclosureRadiusMeters
                    || Mathf.Abs(localCenter.y) > runtimeConfig.EnclosureHalfHeightMeters)
                {
                    continue;
                }

                candidates++;
                var distanceSquared = localCenter.sqrMagnitude;
                if (distanceSquared < nearestDistanceSquared)
                {
                    nearest = payload;
                    nearestDistanceSquared = distanceSquared;
                }
            }

            captureCandidateCount = candidates;
            return nearest;
        }

        private void StepLift(float deltaTime)
        {
            if (runtimeConfig == null || suspensionJoint == null || bellyMount == null || droneBody == null)
            {
                return;
            }

            targetLiftTravel = Mathf.Clamp(
                targetLiftTravel + lineInput * runtimeConfig.LiftSpeedMetersPerSecond * deltaTime,
                0f,
                runtimeConfig.MaximumLiftTravelMeters);
            var remaining = targetLiftTravel - currentLiftTravel;
            if (Mathf.Abs(remaining) <= 0.00001f)
            {
                currentLiftTravel = targetLiftTravel;
                currentLiftSpeed = 0f;
                return;
            }

            var acceleration = runtimeConfig.LiftAccelerationMetersPerSecondSquared;
            var brakingSpeed = Mathf.Sqrt(2f * acceleration * Mathf.Abs(remaining));
            var desiredSpeed = Mathf.Sign(remaining)
                               * Mathf.Min(runtimeConfig.LiftSpeedMetersPerSecond, brakingSpeed);
            currentLiftSpeed = Mathf.MoveTowards(
                currentLiftSpeed,
                desiredSpeed,
                acceleration * deltaTime);
            var step = currentLiftSpeed * deltaTime;
            if (Mathf.Sign(step) != Mathf.Sign(remaining) || Mathf.Abs(step) >= Mathf.Abs(remaining))
            {
                currentLiftTravel = targetLiftTravel;
                currentLiftSpeed = 0f;
            }
            else
            {
                currentLiftTravel += step;
            }
            UpdateJointAnchors();
        }

        private void UpdateLiftVisual()
        {
            if (liftCarriage != null)
            {
                liftCarriage.SetPositionAndRotation(bellyMount.position, bellyMount.rotation);
            }

            if (liftSleeveVisual == null || suspensionJoint == null || runtimeConfig == null)
            {
                return;
            }

            var armLength = runtimeConfig.ArmLengthMeters;
            var extensionLength = Mathf.Max(0f, suspensionJoint.anchor.y - armLength);
            var visible = extensionLength > 0.001f;
            liftSleeveVisual.gameObject.SetActive(visible);
            if (!visible)
            {
                return;
            }

            liftSleeveVisual.localPosition = new Vector3(
                0f,
                armLength + extensionLength * 0.5f,
                0f);
            liftSleeveVisual.localRotation = Quaternion.identity;
            var scale = liftSleeveVisual.localScale;
            scale.y = extensionLength * 0.5f;
            liftSleeveVisual.localScale = scale;
        }

        private float CalculateIntegratedDynamicMass()
        {
            var result = grappleBody != null ? grappleBody.mass : 0f;
            if (clawBodies == null)
            {
                return Mathf.Max(0f, result);
            }

            foreach (var body in clawBodies)
            {
                if (body != null)
                {
                    result += body.mass;
                }
            }

            return Mathf.Max(0f, result);
        }

        private void EnsureAssistVisual()
        {
            if (assistRing != null)
            {
                return;
            }

            var root = new GameObject("GrappleAssistVisual");
            root.transform.SetParent(transform, false);
            assistRing = root.AddComponent<LineRenderer>();
            assistMaterial = new Material(Shader.Find("Sprites/Default"))
            {
                name = "Grapple Assist Material"
            };
            assistRing.sharedMaterial = assistMaterial;
            assistRing.useWorldSpace = true;
            assistRing.loop = true;
            assistRing.positionCount = AssistRingSegments;
            assistRing.widthMultiplier = 0.008f;
            assistRing.numCapVertices = 2;

            var labelObject = new GameObject("HeightLabel", typeof(TextMesh));
            labelObject.transform.SetParent(root.transform, false);
            assistLabel = labelObject.GetComponent<TextMesh>();
            assistLabel.anchor = TextAnchor.MiddleCenter;
            assistLabel.alignment = TextAlignment.Center;
            assistLabel.characterSize = 0.035f;
            assistLabel.fontSize = 32;
        }

        private void UpdateAssistVisual()
        {
            EnsureAssistVisual();
            if (assistRing == null || assistLabel == null || captureVolume == null
                || State == DroneEquipmentState.Carrying)
            {
                SetAssistVisible(false);
                return;
            }

            nearestCaptureCandidate = FindNearestEnclosedPayload();
            var center = captureVolume.transform.TransformPoint(captureVolume.center);
            var hits = Physics.RaycastAll(center, Vector3.down, 5f, ~0, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            var hasSurface = false;
            var point = center - Vector3.up;
            var distance = 0f;
            foreach (var hit in hits)
            {
                if (hit.collider == null || hit.transform.IsChildOf(transform)
                    || (droneBody != null && hit.transform.IsChildOf(droneBody.transform)))
                {
                    continue;
                }

                hasSurface = true;
                point = hit.point + Vector3.up * 0.01f;
                distance = hit.distance;
                break;
            }

            var color = !hasSurface
                ? new Color(1f, 0.2f, 0.15f)
                : nearestCaptureCandidate != null ? Color.green : new Color(1f, 0.55f, 0.05f);
            assistRing.startColor = assistRing.endColor = color;
            assistLabel.color = color;
            SetAssistVisible(true);
            var radius = runtimeConfig != null ? runtimeConfig.EnclosureRadiusMeters : 0.23f;
            for (var index = 0; index < AssistRingSegments; index++)
            {
                var radians = index * Mathf.PI * 2f / AssistRingSegments;
                assistRing.SetPosition(index, point + new Vector3(Mathf.Cos(radians), 0f, Mathf.Sin(radians)) * radius);
            }

            assistLabel.text = hasSurface ? $"抓斗 {distance:0.00} m" : "抓斗 无投射面";
            assistLabel.transform.position = point + Vector3.forward * (radius + 0.06f);
            var camera = Camera.main;
            if (camera != null)
            {
                assistLabel.transform.rotation = Quaternion.LookRotation(
                    assistLabel.transform.position - camera.transform.position,
                    camera.transform.up);
            }
        }

        private void SetAssistVisible(bool value)
        {
            if (assistRing != null)
            {
                assistRing.enabled = value;
            }

            if (assistLabel != null)
            {
                assistLabel.gameObject.SetActive(value);
            }
        }

        private void AttachPayload(DronePayload payload)
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

            var worldAnchor = payload.Body.worldCenterOfMass;
            gripJoint = grappleBody.gameObject.AddComponent<FixedJoint>();
            gripJoint.connectedBody = payload.Body;
            gripJoint.autoConfigureConnectedAnchor = false;
            gripJoint.anchor = grappleBody.transform.InverseTransformPoint(worldAnchor);
            gripJoint.connectedAnchor = payload.Body.transform.InverseTransformPoint(worldAnchor);
            gripJoint.breakForce = runtimeConfig.BreakForceNewtons;
            gripJoint.breakTorque = runtimeConfig.BreakTorqueNewtonMeters;
            gripJoint.enableCollision = false;
            attachedPayload = payload;
            supportedPayloadMass = 0f;
            State = DroneEquipmentState.Carrying;
            SetHint($"已抓住 {payload.Body.mass:0.##} kg 载荷");
        }

        private void StepSupportedLoad(float deltaTime)
        {
            if (attachedPayload == null || gripJoint == null)
            {
                if (attachedPayload != null)
                {
                    attachedPayload = null;
                    State = DroneEquipmentState.Ready;
                    SetHint("抓取连接已断开");
                }
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
                Destroy(gripJoint);
            }

            gripJoint = null;
            attachedPayload = null;
            supportedPayloadMass = 0f;
            captureCandidateCount = 0;
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
                    if (collider != null && bodyCollider != null && collider != bodyCollider)
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
