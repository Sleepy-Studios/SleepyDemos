using System;
using UnityEngine;

namespace Hotfix.DroneFlight
{
    /// <summary>飞控只读的外部承载质量来源。</summary>
    internal interface IDroneExternalMassProvider
    {
        float SupportedMassKilograms { get; }
        float HardwareMassKilograms { get; }
        float PayloadMassKilograms { get; }
        float SupportedPayloadMassKilograms { get; }
    }

    /// <summary>管理吊链、抓斗和六爪在停靠与动态物理之间的切换。</summary>
    public sealed class DroneSuspensionRig : MonoBehaviour
    {
        [SerializeField] private Rigidbody droneBody;
        [SerializeField] private Transform parkingRoot;
        [SerializeField] private Rigidbody[] dynamicBodies = Array.Empty<Rigidbody>();
        [SerializeField] private Collider[] mechanismColliders = Array.Empty<Collider>();

        [SerializeField] private Transform[] originalParents = Array.Empty<Transform>();
        [SerializeField] private Vector3[] deployedOwnerLocalPositions = Array.Empty<Vector3>();
        [SerializeField] private Quaternion[] deployedOwnerLocalRotations = Array.Empty<Quaternion>();
        private Joint[] mechanismJoints = Array.Empty<Joint>();
        private Rigidbody[] mechanismConnectedBodies = Array.Empty<Rigidbody>();
        private float[] hardwareMassWeights = Array.Empty<float>();
        private bool stateApplied;

        /// 吊挂物理当前是否启用。
        internal bool IsPhysicsActive { get; private set; }

        /// 设备固定质量，单位 kg。
        internal float HardwareMassKilograms
        {
            get
            {
                var mass = 0f;
                foreach (var body in dynamicBodies)
                {
                    if (body != null && float.IsFinite(body.mass) && body.mass > 0f)
                    {
                        mass += body.mass;
                    }
                }

                return mass;
            }
        }

        private void Awake()
        {
            ConfigureVisualSmoothing();
            CaptureHardwareMassWeights();
            CaptureDeploymentPose(force: false);
            CollectMechanismJoints(captureConnections: true);
            IgnoreInternalCollisions();
            SetPhysicsActive(false);
        }

        /// <summary>启用或停靠整套吊挂物理。</summary>
        internal void SetPhysicsActive(bool active)
        {
            if (stateApplied && active == IsPhysicsActive && originalParents.Length == dynamicBodies.Length)
            {
                return;
            }

            stateApplied = true;
            IsPhysicsActive = active;
            CollectMechanismJoints(captureConnections: false);
            // 停靠期间若 Joint 仍启用，运动学链节会通过不可断裂约束反向掀翻无人机。
            // 部署时也先保持禁用，待所有刚体回到配对锚点后再统一接入求解器。
            SetMechanismJointConnections(false);
            if (active)
            {
                // IgnoreCollision 不会序列化，进入物理态前再次建立内部碰撞契约。
                IgnoreInternalCollisions();
            }
            for (var index = 0; index < dynamicBodies.Length; index++)
            {
                var body = dynamicBodies[index];
                if (body == null)
                {
                    continue;
                }

                if (active)
                {
                    body.transform.SetParent(null, true);
                    if (droneBody != null && index < deployedOwnerLocalPositions.Length)
                    {
                        body.transform.SetPositionAndRotation(
                            GetAlignedDeployedPosition(index),
                            droneBody.rotation * deployedOwnerLocalRotations[index]);
                    }

                    body.isKinematic = false;
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                    body.useGravity = true;
                }
                else
                {
                    if (!body.isKinematic)
                    {
                        body.linearVelocity = Vector3.zero;
                        body.angularVelocity = Vector3.zero;
                    }

                    body.isKinematic = true;
                    body.useGravity = false;
                    var parent = index < originalParents.Length && originalParents[index] != null
                        ? originalParents[index]
                        : transform;
                    body.transform.SetParent(parent, true);
                    body.transform.localScale = Vector3.one;
                    var parkedWorldPosition = parkingRoot != null
                        ? parkingRoot.TransformPoint(GetParkingPosition(index))
                        : transform.TransformPoint(GetParkingPosition(index));
                    body.transform.localPosition = parent.InverseTransformPoint(parkedWorldPosition);
                }
            }

            Physics.SyncTransforms();
            if (active)
            {
                SetMechanismJointConnections(true);
                foreach (var body in dynamicBodies)
                {
                    body?.WakeUp();
                }
            }

            foreach (var mechanismCollider in mechanismColliders)
            {
                if (mechanismCollider != null)
                {
                    mechanismCollider.enabled = active;
                }
            }

            Physics.SyncTransforms();
        }

        /// <summary>
        /// 在物理尚未启用时，将停靠结构逐帧插值到完整放出姿态。
        /// </summary>
        /// <param name="normalizedProgress">0 为腹部收纳，1 为完整放出。</param>
        internal void SetDeploymentProgress(float normalizedProgress)
        {
            if (IsPhysicsActive || droneBody == null)
            {
                return;
            }

            var progress = Mathf.Clamp01(normalizedProgress);
            for (var index = 0; index < dynamicBodies.Length; index++)
            {
                var body = dynamicBodies[index];
                if (body == null || index >= deployedOwnerLocalPositions.Length)
                {
                    continue;
                }

                var parkingAnchor = parkingRoot != null ? parkingRoot : transform;
                var parkedPosition = parkingAnchor.TransformPoint(GetParkingPosition(index));
                var deployedPosition = GetAlignedDeployedPosition(index);
                var deployedRotation = droneBody.rotation * deployedOwnerLocalRotations[index];
                body.transform.SetPositionAndRotation(
                    Vector3.Lerp(parkedPosition, deployedPosition, progress),
                    Quaternion.Slerp(parkingAnchor.rotation, deployedRotation, progress));
            }

            Physics.SyncTransforms();
        }

        internal void Configure(
            Rigidbody owner,
            Transform park,
            Rigidbody[] bodies,
            Collider[] colliders)
        {
            droneBody = owner;
            parkingRoot = park;
            dynamicBodies = bodies ?? Array.Empty<Rigidbody>();
            mechanismColliders = colliders ?? Array.Empty<Collider>();
            ConfigureVisualSmoothing();
            CaptureHardwareMassWeights();
            stateApplied = false;
            CaptureDeploymentPose(force: true);
            CollectMechanismJoints(captureConnections: true);
            IgnoreInternalCollisions();
            SetPhysicsActive(false);
            SetDeploymentProgress(0f);
        }

        internal void SetTotalHardwareMass(float totalMassKilograms)
        {
            if (!float.IsFinite(totalMassKilograms) || totalMassKilograms <= 0f
                || dynamicBodies.Length == 0)
            {
                return;
            }

            if (hardwareMassWeights.Length != dynamicBodies.Length)
            {
                CaptureHardwareMassWeights();
            }

            for (var index = 0; index < dynamicBodies.Length; index++)
            {
                if (dynamicBodies[index] != null)
                {
                    dynamicBodies[index].mass = Mathf.Max(0.0001f, totalMassKilograms * hardwareMassWeights[index]);
                }
            }
        }

        private void CaptureHardwareMassWeights()
        {
            hardwareMassWeights = new float[dynamicBodies.Length];
            var total = HardwareMassKilograms;
            if (total <= 0f)
            {
                return;
            }

            for (var index = 0; index < dynamicBodies.Length; index++)
            {
                var body = dynamicBodies[index];
                hardwareMassWeights[index] = body != null ? body.mass / total : 0f;
            }
        }

        private void ConfigureVisualSmoothing()
        {
            foreach (var body in dynamicBodies)
            {
                if (body != null)
                {
                    body.interpolation = RigidbodyInterpolation.Interpolate;
                }
            }
        }

        private void CaptureDeploymentPose(bool force)
        {
            if (!force
                && originalParents.Length == dynamicBodies.Length
                && deployedOwnerLocalPositions.Length == dynamicBodies.Length
                && deployedOwnerLocalRotations.Length == dynamicBodies.Length)
            {
                return;
            }

            originalParents = new Transform[dynamicBodies.Length];
            deployedOwnerLocalPositions = new Vector3[dynamicBodies.Length];
            deployedOwnerLocalRotations = new Quaternion[dynamicBodies.Length];
            for (var index = 0; index < dynamicBodies.Length; index++)
            {
                var body = dynamicBodies[index];
                if (body == null)
                {
                    continue;
                }

                originalParents[index] = body.transform.parent;
                if (droneBody != null)
                {
                    deployedOwnerLocalPositions[index] = droneBody.transform.InverseTransformPoint(body.position);
                    deployedOwnerLocalRotations[index] = Quaternion.Inverse(droneBody.rotation) * body.rotation;
                }
            }
        }

        private Vector3 GetParkingPosition(int index)
        {
            if (index < 3)
            {
                return Vector3.down * (0.015f * index);
            }

            var deployed = index < deployedOwnerLocalPositions.Length
                ? deployedOwnerLocalPositions[index]
                : Vector3.zero;
            var radial = new Vector2(deployed.x, deployed.z).normalized * 0.045f;
            return new Vector3(radial.x, -0.045f, radial.y);
        }

        private Vector3 GetAlignedDeployedPosition(int index)
        {
            return droneBody.transform.TransformPoint(deployedOwnerLocalPositions[index])
                   + CalculateTopJointAlignmentOffset();
        }

        private Vector3 CalculateTopJointAlignmentOffset()
        {
            if (droneBody == null)
            {
                return Vector3.zero;
            }

            for (var jointIndex = 0; jointIndex < mechanismJoints.Length; jointIndex++)
            {
                if (jointIndex >= mechanismConnectedBodies.Length
                    || mechanismConnectedBodies[jointIndex] != droneBody
                    || mechanismJoints[jointIndex] == null)
                {
                    continue;
                }

                var joint = mechanismJoints[jointIndex];
                var body = joint.GetComponent<Rigidbody>();
                var bodyIndex = Array.IndexOf(dynamicBodies, body);
                if (bodyIndex < 0 || bodyIndex >= deployedOwnerLocalPositions.Length)
                {
                    continue;
                }

                // 卷扬长度直接改变顶端 Joint 的 connectedAnchor。动态机构启用前必须让
                // 第一节连接杆的本地锚点与该目标重合，否则投影会在一帧内强拉整套抓斗，
                // 表现为抓斗跳动、载荷难以离地以及错误的大幅受力。
                var bodyPosition = droneBody.transform.TransformPoint(deployedOwnerLocalPositions[bodyIndex]);
                var bodyRotation = droneBody.rotation * deployedOwnerLocalRotations[bodyIndex];
                var bodyAnchor = bodyPosition + bodyRotation * joint.anchor;
                var ownerAnchor = droneBody.transform.TransformPoint(joint.connectedAnchor);
                return ownerAnchor - bodyAnchor;
            }

            return Vector3.zero;
        }

        private void CollectMechanismJoints(bool captureConnections)
        {
            var joints = new System.Collections.Generic.List<Joint>();
            foreach (var body in dynamicBodies)
            {
                if (body == null)
                {
                    continue;
                }

                joints.AddRange(body.GetComponents<Joint>());
            }

            mechanismJoints = joints.ToArray();
            if (!captureConnections && mechanismConnectedBodies.Length == mechanismJoints.Length)
            {
                return;
            }

            mechanismConnectedBodies = new Rigidbody[mechanismJoints.Length];
            for (var index = 0; index < mechanismJoints.Length; index++)
            {
                mechanismConnectedBodies[index] = mechanismJoints[index] != null
                    ? mechanismJoints[index].connectedBody
                    : null;
            }
        }

        private void SetMechanismJointConnections(bool connected)
        {
            for (var index = 0; index < mechanismJoints.Length; index++)
            {
                var joint = mechanismJoints[index];
                if (joint != null)
                {
                    joint.connectedBody = connected && index < mechanismConnectedBodies.Length
                        ? mechanismConnectedBodies[index]
                        : null;
                }
            }
        }

        private void IgnoreInternalCollisions()
        {
            var ownerColliders = droneBody != null
                ? droneBody.GetComponentsInChildren<Collider>(true)
                : Array.Empty<Collider>();
            foreach (var mechanismCollider in mechanismColliders)
            {
                if (mechanismCollider == null)
                {
                    continue;
                }

                foreach (var ownerCollider in ownerColliders)
                {
                    if (ownerCollider != null && ownerCollider != mechanismCollider)
                    {
                        Physics.IgnoreCollision(mechanismCollider, ownerCollider, true);
                    }
                }
            }

            for (var first = 0; first < mechanismColliders.Length; first++)
            {
                for (var second = first + 1; second < mechanismColliders.Length; second++)
                {
                    if (mechanismColliders[first] != null && mechanismColliders[second] != null)
                    {
                        Physics.IgnoreCollision(mechanismColliders[first], mechanismColliders[second], true);
                    }
                }
            }
        }
    }
}
