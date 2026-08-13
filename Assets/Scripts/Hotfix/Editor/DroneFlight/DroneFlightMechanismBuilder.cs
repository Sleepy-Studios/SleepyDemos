using System;
using System.Collections.Generic;
using System.Linq;
using Hotfix.DroneFlight;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hotfix.Editor.DroneFlight
{
    /// <summary>可重复生成 DroneFlight 工业六爪、物理装配和场景显式引用。</summary>
    public static class DroneFlightMechanismBuilder
    {
        private const string PrefabPath = "Assets/LoadResources/Demos/drone_flight/Prefabs/DronePrototype.prefab";
        private const string ScenePath = "Assets/LoadResources/Demos/drone_flight/Scenes/Main.unity";
        private const string ArtPath = "Assets/LoadResources/Demos/drone_flight/Art/Generated";
        private const float GrappleVisualScale = 0.7f;

        [MenuItem("Tools/SleepyDemos/DroneFlight/重建工业六爪与场景绑定")]
        public static void Rebuild()
        {
            EnsureFolder(ArtPath);
            var upperMesh = CreateOrReplaceMesh($"{ArtPath}/GrappleClawUpper.asset", CreateTaperedSegment(0.22f, 0.055f, 0.035f));
            var tipMesh = CreateOrReplaceMesh($"{ArtPath}/GrappleClawTip.asset", CreateTaperedSegment(0.16f, 0.045f, 0.018f));
            var ringMesh = CreateOrReplaceMesh($"{ArtPath}/GrappleRing.asset", CreateRingMesh(0.14f, 0.025f, 16));
            var darkMaterial = CreateOrReplaceMaterial($"{ArtPath}/GrappleDark.mat", new Color(0.08f, 0.09f, 0.1f));
            var orangeMaterial = CreateOrReplaceMaterial($"{ArtPath}/GrappleOrange.mat", new Color(1f, 0.42f, 0.03f));

            RebuildPrefab(upperMesh, tipMesh, ringMesh, darkMaterial, orangeMaterial);
            RewireScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[DroneFlight] 已重建工业六爪、稳定吊链和场景显式引用。");
        }

        private static void RebuildPrefab(
            Mesh upperMesh,
            Mesh tipMesh,
            Mesh ringMesh,
            Material darkMaterial,
            Material orangeMaterial)
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                foreach (var component in root.GetComponentsInChildren<DroneSuspensionRig>(true))
                {
                    UnityEngine.Object.DestroyImmediate(component);
                }
                foreach (var component in root.GetComponentsInChildren<DroneWinchController>(true))
                {
                    UnityEngine.Object.DestroyImmediate(component);
                }
                foreach (var component in root.GetComponentsInChildren<DroneMechanicalHook>(true))
                {
                    UnityEngine.Object.DestroyImmediate(component);
                }
                foreach (var component in root.GetComponentsInChildren<DroneHookInput>(true))
                {
                    UnityEngine.Object.DestroyImmediate(component);
                }
                foreach (var component in root.GetComponentsInChildren<PayloadMount>(true))
                {
                    UnityEngine.Object.DestroyImmediate(component);
                }

                var oldRig = root.transform.Find("SuspensionRig");
                if (oldRig != null)
                {
                    UnityEngine.Object.DestroyImmediate(oldRig.gameObject);
                }

                var droneBody = root.GetComponent<Rigidbody>();
                var controller = root.GetComponent<DroneFlightController>();
                var hardwareMass = controller != null && controller.Config != null
                    ? controller.Config.GrappleHardwareMassKilograms
                    : DronePayloadTuningCalculator.DefaultDeployedHardwareMassKilograms;
                var linkMass = hardwareMass * 0.2f;
                var grappleBodyMass = hardwareMass * 0.4f;
                var clawMass = hardwareMass * 0.4f / 6f;
                var gear = root.GetComponent<DroneLandingGearController>();
                var rigRoot = NewChild(root.transform, "SuspensionRig", Vector3.zero);
                var parkingRoot = NewChild(rigRoot, "ParkingRoot", new Vector3(0f, -0.16f, 0f));
                var dynamicsRoot = NewChild(rigRoot, "DynamicBodies", Vector3.zero);

                var chainOne = CreateChainBody(dynamicsRoot, "ChainLink_1", new Vector3(0f, -0.66f, 0f), linkMass, darkMaterial);
                var grappleBody = CreateGrappleBody(
                    dynamicsRoot,
                    new Vector3(0f, -0.83f, 0f),
                    grappleBodyMass,
                    ringMesh,
                    darkMaterial,
                    orangeMaterial);

                var topJoint = AddLimitedJoint(chainOne, droneBody, new Vector3(0f, 0.09f, 0f), new Vector3(0f, -0.12f, 0f));
                AddLimitedJoint(grappleBody, chainOne, new Vector3(0f, 0.056f, 0f), new Vector3(0f, -0.09f, 0f));

                var clawBodies = new List<Rigidbody>();
                var clawJoints = new List<HingeJoint>();
                var sensors = new List<DroneGrappleContactSensor>();
                for (var index = 0; index < 6; index++)
                {
                    CreateClaw(
                        dynamicsRoot,
                        grappleBody,
                        index,
                        upperMesh,
                        tipMesh,
                        orangeMaterial,
                        darkMaterial,
                        clawMass,
                        clawBodies,
                        clawJoints,
                        sensors);
                }

                var mountPoint = NewChild(grappleBody.transform, "GripCenter", new Vector3(0f, -0.084f, 0f));
                var payloadMount = root.AddComponent<PayloadMount>();
                payloadMount.Configure(mountPoint, controller.Config, grappleBody);
                var hook = root.AddComponent<DroneMechanicalHook>();
                hook.Configure(payloadMount, clawJoints.ToArray(), sensors.ToArray(), mountPoint, 0.38f * GrappleVisualScale);

                var bodies = new[] { chainOne, grappleBody }.Concat(clawBodies).ToArray();
                var mechanismColliders = bodies
                    .SelectMany(body => body.GetComponentsInChildren<Collider>(true))
                    .ToArray();
                var mechanismJointConnections = bodies
                    .SelectMany(body => body.GetComponents<Joint>())
                    .ToDictionary(joint => joint, joint => joint.connectedBody);
                var suspensionRig = root.AddComponent<DroneSuspensionRig>();
                suspensionRig.Configure(droneBody, parkingRoot, bodies, mechanismColliders);
                // Configure 会模拟运行时停靠并暂时断开求解连接；Prefab 必须保存原始拓扑，
                // 让 Awake 能先缓存连接，再安全进入停靠态。
                foreach (var pair in mechanismJointConnections)
                {
                    pair.Key.connectedBody = pair.Value;
                }
                var winch = root.AddComponent<DroneWinchController>();
                winch.Configure(controller, topJoint, payloadMount, suspensionRig);
                controller.ConfigureExternalMassProvider(winch);
                var input = root.AddComponent<DroneHookInput>();
                input.Configure(hook, winch, gear);

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void RewireScene()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var context = UnityEngine.Object.FindFirstObjectByType<DroneFlightSceneContext>();
            var controller = UnityEngine.Object.FindFirstObjectByType<DroneFlightController>();
            var input = UnityEngine.Object.FindFirstObjectByType<DronePlayerInput>();
            var rig = UnityEngine.Object.FindFirstObjectByType<DroneCameraRig>();
            var session = UnityEngine.Object.FindFirstObjectByType<DroneRemoteControllerExperience>();
            var mount = UnityEngine.Object.FindFirstObjectByType<PayloadMount>();
            var hook = UnityEngine.Object.FindFirstObjectByType<DroneMechanicalHook>();
            var winch = UnityEngine.Object.FindFirstObjectByType<DroneWinchController>();
            var gear = UnityEngine.Object.FindFirstObjectByType<DroneLandingGearController>();
            var mechanismInput = UnityEngine.Object.FindFirstObjectByType<DroneHookInput>();
            var reset = UnityEngine.Object.FindFirstObjectByType<DroneResetCoordinator>();
            var payloads = UnityEngine.Object.FindObjectsByType<DronePayload>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var playerCamera = scene.GetRootGameObjects()
                .SelectMany(item => item.GetComponentsInChildren<Camera>(true))
                .FirstOrDefault(camera => camera.name == "Main Camera");

            if (context == null || controller == null || rig == null || session == null)
            {
                throw new InvalidOperationException("DroneFlight 场景缺少 Context、飞控、相机或控制会话。" );
            }

            session.Configure(playerCamera, rig, input, controller, mechanismInput);
            mechanismInput?.Configure(hook, winch, gear, session);
            context.Configure(controller, input, rig, session, mount, hook, winch, gear, payloads);
            reset?.Configure(controller, input, rig, gear, winch, hook, mount, session, payloads);

            var remoteRoot = scene.GetRootGameObjects()
                .SelectMany(item => item.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(item => item.name == "RemoteControllerRoot");
            if (remoteRoot != null)
            {
                UnityEngine.Object.DestroyImmediate(remoteRoot.gameObject);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static Rigidbody CreateChainBody(Transform parent, string name, Vector3 position, float mass, Material material)
        {
            var root = NewChild(parent, name, position);
            var body = root.gameObject.AddComponent<Rigidbody>();
            body.mass = mass;
            body.linearDamping = 0.04f;
            body.angularDamping = 0.35f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.solverIterations = 12;
            body.solverVelocityIterations = 8;
            var collider = root.gameObject.AddComponent<CapsuleCollider>();
            collider.radius = 0.018f;
            collider.height = 0.18f;
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            visual.name = "Visual";
            visual.transform.SetParent(root, false);
            visual.transform.localScale = new Vector3(0.025f, 0.09f, 0.025f);
            visual.GetComponent<MeshRenderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(visual.GetComponent<Collider>());
            return body;
        }

        private static Rigidbody CreateGrappleBody(
            Transform parent,
            Vector3 position,
            float mass,
            Mesh ringMesh,
            Material darkMaterial,
            Material orangeMaterial)
        {
            var root = NewChild(parent, "GrappleBody", position);
            var body = root.gameObject.AddComponent<Rigidbody>();
            body.mass = mass;
            body.linearDamping = 0.04f;
            body.angularDamping = 0.4f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.solverIterations = 12;
            body.solverVelocityIterations = 8;
            var collider = root.gameObject.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.24f, 0.09f, 0.24f) * GrappleVisualScale;

            var spool = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            spool.name = "CentralSpool";
            spool.transform.SetParent(root, false);
            spool.transform.localScale = new Vector3(0.09f, 0.06f, 0.09f) * GrappleVisualScale;
            spool.GetComponent<MeshRenderer>().sharedMaterial = darkMaterial;
            UnityEngine.Object.DestroyImmediate(spool.GetComponent<Collider>());

            var ring = NewChild(root, "IndustrialRing", Vector3.down * (0.04f * GrappleVisualScale));
            ring.localScale = Vector3.one * GrappleVisualScale;
            ring.gameObject.AddComponent<MeshFilter>().sharedMesh = ringMesh;
            ring.gameObject.AddComponent<MeshRenderer>().sharedMaterial = orangeMaterial;
            return body;
        }

        private static void CreateClaw(
            Transform parent,
            Rigidbody grappleBody,
            int index,
            Mesh upperMesh,
            Mesh tipMesh,
            Material orangeMaterial,
            Material darkMaterial,
            float mass,
            ICollection<Rigidbody> bodies,
            ICollection<HingeJoint> joints,
            ICollection<DroneGrappleContactSensor> sensors)
        {
            var angle = index * 60f;
            var radial = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            var root = NewChild(parent, $"Claw_{index + 1}", grappleBody.transform.localPosition + radial * (0.14f * GrappleVisualScale));
            root.rotation = Quaternion.Euler(0f, angle, 0f);
            root.localScale = Vector3.one;
            var body = root.gameObject.AddComponent<Rigidbody>();
            body.mass = mass;
            body.linearDamping = 0.04f;
            body.angularDamping = 0.45f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.solverIterations = 12;
            body.solverVelocityIterations = 8;
            var sensor = root.gameObject.AddComponent<DroneGrappleContactSensor>();

            CreateSegment(root, "UpperSegment", upperMesh, orangeMaterial,
                new Vector3(0f, -0.08f, 0.09f) * GrappleVisualScale, Quaternion.Euler(28f, 0f, 0f), new Vector3(0.055f, 0.045f, 0.22f));
            CreateSegment(root, "TipSegment", tipMesh, orangeMaterial,
                new Vector3(0f, -0.19f, 0.17f) * GrappleVisualScale, Quaternion.Euler(118f, 0f, 0f), new Vector3(0.045f, 0.04f, 0.16f));

            var pivot = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pivot.name = "Pivot";
            pivot.transform.SetParent(root, false);
            pivot.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            pivot.transform.localScale = new Vector3(0.035f, 0.045f, 0.035f) * GrappleVisualScale;
            pivot.GetComponent<MeshRenderer>().sharedMaterial = darkMaterial;
            UnityEngine.Object.DestroyImmediate(pivot.GetComponent<Collider>());

            var joint = root.gameObject.AddComponent<HingeJoint>();
            joint.connectedBody = grappleBody;
            joint.autoConfigureConnectedAnchor = false;
            joint.anchor = Vector3.zero;
            joint.connectedAnchor = grappleBody.transform.InverseTransformPoint(root.position);
            joint.axis = Vector3.right;
            joint.useLimits = true;
            joint.limits = new JointLimits { min = -45f, max = 32f, bounciness = 0f };
            joint.useSpring = true;
            joint.spring = new JointSpring { targetPosition = -42f, spring = 4f, damper = 0.6f };
            joint.enableCollision = false;
            joint.breakForce = float.PositiveInfinity;
            joint.breakTorque = float.PositiveInfinity;
            bodies.Add(body);
            joints.Add(joint);
            sensors.Add(sensor);
        }

        private static void CreateSegment(
            Transform parent,
            string name,
            Mesh mesh,
            Material material,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 colliderSize)
        {
            var segment = NewChild(parent, name, localPosition);
            segment.localRotation = localRotation;
            segment.localScale = Vector3.one * GrappleVisualScale;
            segment.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            segment.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
            var collider = segment.gameObject.AddComponent<BoxCollider>();
            collider.center = Vector3.forward * (colliderSize.z * 0.5f);
            collider.size = colliderSize;
        }

        private static ConfigurableJoint AddLimitedJoint(
            Rigidbody body,
            Rigidbody connectedBody,
            Vector3 anchor,
            Vector3 connectedAnchor)
        {
            var joint = body.gameObject.AddComponent<ConfigurableJoint>();
            joint.connectedBody = connectedBody;
            joint.autoConfigureConnectedAnchor = false;
            joint.anchor = anchor;
            joint.connectedAnchor = connectedAnchor;
            joint.xMotion = ConfigurableJointMotion.Locked;
            joint.yMotion = ConfigurableJointMotion.Locked;
            joint.zMotion = ConfigurableJointMotion.Locked;
            joint.angularXMotion = ConfigurableJointMotion.Limited;
            joint.angularYMotion = ConfigurableJointMotion.Limited;
            joint.angularZMotion = ConfigurableJointMotion.Limited;
            joint.lowAngularXLimit = new SoftJointLimit { limit = -35f };
            joint.highAngularXLimit = new SoftJointLimit { limit = 35f };
            joint.angularYLimit = new SoftJointLimit { limit = 35f };
            joint.angularZLimit = new SoftJointLimit { limit = 35f };
            joint.angularXLimitSpring = new SoftJointLimitSpring { spring = 25f, damper = 5f };
            joint.angularYZLimitSpring = new SoftJointLimitSpring { spring = 25f, damper = 5f };
            // 机构内部连接不得断裂；游戏化脱落只由载荷弱约束负责。
            joint.breakForce = float.PositiveInfinity;
            joint.breakTorque = float.PositiveInfinity;
            joint.projectionMode = JointProjectionMode.PositionAndRotation;
            joint.projectionDistance = 0.025f;
            joint.projectionAngle = 6f;
            joint.enablePreprocessing = false;
            joint.massScale = Mathf.Clamp(body.mass / connectedBody.mass, 0.05f, 1f);
            joint.connectedMassScale = Mathf.Clamp(connectedBody.mass / body.mass, 0.05f, 1f);
            joint.enableCollision = false;
            return joint;
        }

        private static Transform NewChild(Transform parent, string name, Vector3 localPosition)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            child.localPosition = localPosition;
            child.localRotation = Quaternion.identity;
            child.localScale = Vector3.one;
            return child;
        }

        private static Mesh CreateTaperedSegment(float length, float startWidth, float endWidth)
        {
            var height = 0.04f;
            var vertices = new[]
            {
                new Vector3(-startWidth * 0.5f, -height * 0.5f, 0f), new Vector3(startWidth * 0.5f, -height * 0.5f, 0f),
                new Vector3(-startWidth * 0.5f, height * 0.5f, 0f), new Vector3(startWidth * 0.5f, height * 0.5f, 0f),
                new Vector3(-endWidth * 0.5f, -height * 0.5f, length), new Vector3(endWidth * 0.5f, -height * 0.5f, length),
                new Vector3(-endWidth * 0.5f, height * 0.5f, length), new Vector3(endWidth * 0.5f, height * 0.5f, length)
            };
            var triangles = new[]
            {
                0,2,1, 1,2,3, 4,5,6, 5,7,6,
                0,1,4, 1,5,4, 2,6,3, 3,6,7,
                0,4,2, 2,4,6, 1,3,5, 3,7,5
            };
            var mesh = new Mesh { name = "GrappleTaperedSegment" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh CreateRingMesh(float radius, float thickness, int segments)
        {
            const int sides = 4;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (var segment = 0; segment < segments; segment++)
            {
                var angle = segment * Mathf.PI * 2f / segments;
                var radial = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                for (var side = 0; side < sides; side++)
                {
                    var sideAngle = side * Mathf.PI * 2f / sides;
                    vertices.Add(radial * (radius + Mathf.Cos(sideAngle) * thickness) + Vector3.up * (Mathf.Sin(sideAngle) * thickness));
                }
            }
            for (var segment = 0; segment < segments; segment++)
            {
                var next = (segment + 1) % segments;
                for (var side = 0; side < sides; side++)
                {
                    var nextSide = (side + 1) % sides;
                    var a = segment * sides + side;
                    var b = next * sides + side;
                    var c = segment * sides + nextSide;
                    var d = next * sides + nextSide;
                    triangles.AddRange(new[] { a, c, b, b, c, d });
                }
            }
            var mesh = new Mesh { name = "GrappleIndustrialRing" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh CreateOrReplaceMesh(string path, Mesh mesh)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
            EditorUtility.CopySerialized(mesh, existing);
            UnityEngine.Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        private static Material CreateOrReplaceMaterial(string path, Color color)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = color;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void EnsureFolder(string path)
        {
            var current = "Assets";
            foreach (var part in path.Split('/').Skip(1))
            {
                var next = $"{current}/{part}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, part);
                }
                current = next;
            }
        }
    }
}
