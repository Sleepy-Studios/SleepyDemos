using System;
using System.Collections.Generic;
using Hotfix.DroneFlight;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Hotfix.Editor.DroneFlight
{
    /// <summary>只在编辑期重建基础无人机、独立装备和已保存的组合机体，不参与运行时装配。</summary>
    public static class DroneFlightMechanismBuilder
    {
        private const string Root = "Assets/LoadResources/Demos/drone_flight";
        private const string BasePrefabPath = Root + "/Prefabs/DronePrototype.prefab";
        private const string ObsoletePlainPrefabPath = Root + "/Prefabs/DronePlainVariant.prefab";
        private const string GrappleEquipmentPrefabPath =
            Root + "/Prefabs/Equipment/DroneGrappleEquipment.prefab";
        private const string HarpoonEquipmentPrefabPath =
            Root + "/Prefabs/Equipment/DroneHarpoonEquipment.prefab";
        private const string GrapplePrefabPath = Root + "/Prefabs/DroneGrappleVariant.prefab";
        private const string HarpoonPrefabPath = Root + "/Prefabs/DroneHarpoonVariant.prefab";
        private const string GrappleConfigPath = Root + "/Data/Equipment/DroneGrappleConfig.asset";
        private const string HarpoonConfigPath = Root + "/Data/Equipment/DroneHarpoonConfig.asset";
        private const string ModelPath = Root + "/Art/Models/DroneFlight.fbx";
        private const string MaterialRoot = Root + "/Art/Materials";

        private static readonly string[] LandingGearMeshNames =
        {
            "LandingGear_FL", "LandingGear_FR", "LandingGear_RL", "LandingGear_RR"
        };

        private static readonly Vector3[] RotorPositions =
        {
            new(-0.255f, 0.04f, 0.255f), new(0.255f, 0.04f, 0.255f),
            new(-0.255f, 0.04f, -0.255f), new(0.255f, 0.04f, -0.255f)
        };

        private static readonly Vector3[] LandingGearHinges =
        {
            new(-0.112f, -0.035f, 0.118f), new(0.112f, -0.035f, 0.118f),
            new(-0.112f, -0.035f, -0.118f), new(0.112f, -0.035f, -0.118f)
        };

        private static readonly Vector3[] LandingGearFeet =
        {
            new(-0.205f, -0.23f, 0.18f), new(0.205f, -0.23f, 0.18f),
            new(-0.205f, -0.23f, -0.18f), new(0.205f, -0.23f, -0.18f)
        };

        [MenuItem("Tools/SleepyDemos/DroneFlight/重建基础、装备与组合机体")]
        public static void RebuildAll()
        {
            EnsureFolder(Root + "/Data/Equipment");
            EnsureFolder(Root + "/Prefabs/Equipment");
            EnsureFolder(MaterialRoot);
            EnsureDroneMaterials();
            ConfigureModelImporter();
            var grappleConfig = GetOrCreateAsset<DroneGrappleConfig>(GrappleConfigPath);
            var harpoonConfig = GetOrCreateAsset<DroneHarpoonConfig>(HarpoonConfigPath);
            SetFloat(grappleConfig, "enclosureRadiusMeters", 0.12f);
            SetFloat(grappleConfig, "enclosureHalfHeightMeters", 0.12f);
            RebuildBasePrefab();
            BuildGrappleEquipmentPrefab(grappleConfig);
            BuildHarpoonEquipmentPrefab(harpoonConfig);
            BuildVariant(
                DroneEquipmentKind.Grapple,
                GrappleEquipmentPrefabPath,
                GrapplePrefabPath);
            BuildVariant(
                DroneEquipmentKind.Harpoon,
                HarpoonEquipmentPrefabPath,
                HarpoonPrefabPath);
            AssetDatabase.DeleteAsset(ObsoletePlainPrefabPath);
            AssetDatabase.ForceReserializeAssets(new[]
            {
                Root + "/Data/DroneFlightConfig.asset", GrappleConfigPath, HarpoonConfigPath,
                BasePrefabPath, GrappleEquipmentPrefabPath, HarpoonEquipmentPrefabPath,
                GrapplePrefabPath, HarpoonPrefabPath
            });
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                "[DroneFlight] 基础无人机、两个独立装备 Prefab 和两个已保存组合机体已重建；运行时不会动态拼装。");
        }

        private static void RebuildBasePrefab()
        {
            var root = PrefabUtility.LoadPrefabContents(BasePrefabPath);
            try
            {
                root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                root.transform.localScale = Vector3.one;
                RemoveAll<DroneResetCoordinator>(root);
                RemoveAll<PayloadMount>(root);
                RemoveAll<DroneMechanicalHook>(root);
                RemoveAll<DroneSuspensionRig>(root);
                RemoveAll<DroneWinchController>(root);
                RemoveAll<DroneEquipmentHost>(root);
                RemoveAll<DroneHookInput>(root);
                RemoveAll<DroneRemoteControllerExperience>(root);
                RemoveAll<DroneFlightSceneContext>(root);
                DestroyChild(root.transform, "SuspensionRig");
                DestroyChild(root.transform, "GrappleEquipment");
                DestroyChild(root.transform, "HarpoonEquipment");
                DestroyChild(root.transform, "DroneCameraOutput");
                DestroyChild(root.transform, "GimbalCameraMount");

                EnsureChild(root.transform, "BellyEquipmentMount", new Vector3(0f, -0.12f, 0f));
                BuildOfficialModel(root);
                BuildCommonCameraAndRuntime(root);
                PrefabUtility.SaveAsPrefabAsset(root, BasePrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ConfigureModelImporter()
        {
            if (AssetImporter.GetAtPath(ModelPath) is not ModelImporter importer)
            {
                throw new InvalidOperationException($"无法加载正式无人机模型：{ModelPath}");
            }

            var changed = !Mathf.Approximately(importer.globalScale, 1f)
                          || !importer.useFileScale
                          || importer.bakeAxisConversion
                          || importer.importAnimation
                          || importer.importCameras
                          || importer.importLights
                          || importer.importBlendShapes
                          || importer.isReadable;
            if (!changed)
            {
                return;
            }

            importer.globalScale = 1f;
            importer.useFileScale = true;
            // 保留 FBX 标准轴转换；物理推力轴由机体根的局部 +Y 显式提供。
            importer.bakeAxisConversion = false;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importBlendShapes = false;
            importer.isReadable = false;
            importer.SaveAndReimport();
        }

        private static void EnsureDroneMaterials()
        {
            CreateOrUpdateMaterial("DroneGraphite", new Color(0.105f, 0.12f, 0.135f), 0.38f, 0.58f);
            CreateOrUpdateMaterial("DroneShellTop", new Color(0.30f, 0.32f, 0.34f), 0.22f, 0.62f);
            CreateOrUpdateMaterial("DroneMechanicalBlack", new Color(0.018f, 0.022f, 0.026f), 0.50f, 0.48f);
            CreateOrUpdateMaterial("DroneSafetyOrange", new Color(1f, 0.19f, 0.025f), 0.08f, 0.52f);
            CreateOrUpdateMaterial("DroneFrontLED", new Color(0.63f, 0.86f, 1f), 0f, 0.72f,
                new Color(2.5f, 5.5f, 8f));
            CreateOrUpdateMaterial("DroneCameraLens", new Color(0.008f, 0.016f, 0.022f), 0.72f, 0.88f);
        }

        private static void CreateOrUpdateMaterial(
            string assetName,
            Color baseColor,
            float metallic,
            float smoothness,
            Color? emission = null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                throw new InvalidOperationException("项目中找不到 Universal Render Pipeline/Lit Shader。");
            }

            var path = $"{MaterialRoot}/{assetName}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.name = assetName;
            material.shader = shader;
            material.SetColor("_BaseColor", baseColor);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            if (emission.HasValue)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission.Value);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                material.DisableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", Color.black);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }
            EditorUtility.SetDirty(material);
        }

        private static void BuildOfficialModel(GameObject root)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (source == null)
            {
                throw new InvalidOperationException($"正式无人机模型尚未导入：{ModelPath}");
            }

            DestroyChild(root.transform, "Body");
            DestroyChild(root.transform, "Arm_FL_RR");
            DestroyChild(root.transform, "Arm_FR_RL");
            DestroyChild(root.transform, "NoseForward");
            DestroyChild(root.transform, "OfficialVisual");
            DestroyChild(root.transform, "DroneModel");
            DestroyChild(root.transform, "Rotor_FL_CCW");
            DestroyChild(root.transform, "Rotor_FR_CW");
            DestroyChild(root.transform, "Rotor_RL_CW");
            DestroyChild(root.transform, "Rotor_RR_CCW");
            DestroyChild(root.transform, "LandingGear");
            DestroyChild(root.transform, "BodyCollider");
            DestroyChild(root.transform, "ArmCollider_FL_RR");
            DestroyChild(root.transform, "ArmCollider_FR_RL");
            DestroyChild(root.transform, "CollisionProxies");

            var model = (GameObject)PrefabUtility.InstantiatePrefab(source, root.transform);
            if (model == null)
            {
                throw new InvalidOperationException($"无法实例化正式无人机 FBX：{ModelPath}");
            }
            model.name = "DroneModel";
            model.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            model.transform.localScale = Vector3.one;
            ApplyMappedMaterials(model);
            BuildAirframeColliders(root.transform);
            BuildRotors(root, model);
            BuildLandingGear(root, model);
            ValidateGimbal(model);
        }

        private static void BuildAirframeColliders(Transform root)
        {
            var proxies = EnsureChild(root, "CollisionProxies", Vector3.zero);
            var body = EnsureChild(proxies, "BodyCollider", Vector3.zero);
            var bodyCollider = EnsureComponent<BoxCollider>(body.gameObject);
            bodyCollider.center = Vector3.zero;
            bodyCollider.size = new Vector3(0.26f, 0.10f, 0.37f);

            CreateBoxCollider(proxies, "ArmCollider_FL_RR", new Vector3(0f, 0.025f, 0f),
                Quaternion.Euler(0f, 45f, 0f), new Vector3(0.035f, 0.038f, 0.66f));
            CreateBoxCollider(proxies, "ArmCollider_FR_RL", new Vector3(0f, 0.025f, 0f),
                Quaternion.Euler(0f, -45f, 0f), new Vector3(0.035f, 0.038f, 0.66f));
        }

        private static void BuildRotors(GameObject root, GameObject model)
        {
            for (var index = 0; index < 4; index++)
            {
                var hubName = RotorHubName(index);
                var hub = FindDeepChild(model.transform, hubName);
                if (hub == null)
                {
                    throw new InvalidOperationException($"正式 FBX 缺少旋翼轮毂：{hubName}");
                }

                var bladeName = index is 0 or 3 ? "RotorBlade_CCW" : "RotorBlade_CW";
                var blade = CloneRotorBlade(model, bladeName, hub, bladeName + "_Visual");
                var rotorVisual = EnsureComponent<DroneRotorVisual>(blade.gameObject);
                var rotor = EnsureComponent<DroneRotor>(hub.gameObject);
                var position = (DroneRotorPosition)index;
                var direction = index is 0 or 3
                    ? DroneRotorDirection.CounterClockwise
                    : DroneRotorDirection.Clockwise;
                rotor.Configure(position, direction, blade, rotorVisual, root.transform);

                var actualPosition = root.transform.InverseTransformPoint(hub.position);
                if (Vector3.Distance(actualPosition, RotorPositions[index]) > 0.0001f)
                {
                    throw new InvalidOperationException(
                        $"{hub.name} 施力坐标错误，期望 {RotorPositions[index]}，实际 {actualPosition}。");
                }

                var thrustAxis = root.transform.InverseTransformDirection(rotor.ForceDirection).normalized;
                if (Vector3.Dot(thrustAxis, Vector3.up) < 0.9999f)
                {
                    throw new InvalidOperationException(
                        $"{hub.name} 物理推力轴未朝向机体局部 +Y，实际推力轴 {thrustAxis}。");
                }
            }

            SetModelNodeRenderersEnabled(model, "RotorBlade_CCW", false);
            SetModelNodeRenderersEnabled(model, "RotorBlade_CW", false);
        }

        private static string RotorHubName(int index)
        {
            return index switch
            {
                0 => "RotorHub_FL",
                1 => "RotorHub_FR",
                2 => "RotorHub_RL",
                3 => "RotorHub_RR",
                _ => throw new ArgumentOutOfRangeException(nameof(index), index, null)
            };
        }

        private static void BuildLandingGear(GameObject root, GameObject model)
        {
            var legs = new Transform[4];
            var retractedOffsets = new Vector3[4];
            for (var index = 0; index < legs.Length; index++)
            {
                var leg = FindDeepChild(model.transform, LandingGearMeshNames[index]);
                if (leg == null)
                {
                    throw new InvalidOperationException($"正式 FBX 缺少起落架节点：{LandingGearMeshNames[index]}");
                }
                legs[index] = leg;
                var hingePosition = root.transform.InverseTransformPoint(leg.position);
                if (Vector3.Distance(hingePosition, LandingGearHinges[index]) > 0.0001f)
                {
                    throw new InvalidOperationException(
                        $"{leg.name} 铰链坐标错误，期望 {LandingGearHinges[index]}，实际 {hingePosition}。");
                }

                DestroyChild(leg, "Foot");
                DestroyChild(leg, "StrutCollider");
                var footWorld = root.transform.TransformPoint(LandingGearFeet[index]);
                var footLocal = leg.InverseTransformPoint(footWorld);
                var rootAlignedRotation = Quaternion.Inverse(leg.rotation) * root.transform.rotation;
                CreateBoxCollider(leg, "Foot", footLocal, rootAlignedRotation,
                    new Vector3(0.05f, 0.012f, 0.024f));
                var strutVector = footLocal * 0.88f;
                CreateBoxCollider(leg, "StrutCollider", strutVector * 0.5f,
                    Quaternion.FromToRotation(Vector3.down, strutVector.normalized),
                    new Vector3(0.026f, strutVector.magnitude, 0.026f));

                var radialRoot = new Vector3(
                    LandingGearFeet[index].x - LandingGearHinges[index].x,
                    0f,
                    LandingGearFeet[index].z - LandingGearHinges[index].z).normalized;
                var tangentRoot = Vector3.Cross(Vector3.up, radialRoot).normalized;
                var tangentLocal = leg.InverseTransformDirection(root.transform.TransformDirection(tangentRoot));
                retractedOffsets[index] = Quaternion.AngleAxis(67f, tangentLocal).eulerAngles;
            }

            var controller = EnsureComponent<DroneLandingGearController>(root);
            controller.Configure(root.GetComponent<DroneFlightController>(), root.GetComponent<Rigidbody>(), legs,
                retractedOffsets);
        }

        private static void ValidateGimbal(GameObject model)
        {
            var yaw = FindDeepChild(model.transform, "GimbalYaw");
            var pitch = FindDeepChild(model.transform, "GimbalPitch");
            var camera = FindDeepChild(model.transform, "CameraBody");
            if (yaw == null || pitch == null || camera == null || pitch.parent != yaw || camera.parent != pitch)
            {
                throw new InvalidOperationException("正式 FBX 云台层级必须为 GimbalYaw/GimbalPitch/CameraBody。");
            }
        }

        private static Transform CloneRotorBlade(GameObject model, string sourceName, Transform parent, string name)
        {
            var source = FindDeepChild(model.transform, sourceName);
            if (source == null)
            {
                throw new InvalidOperationException($"正式无人机 FBX 缺少节点：{sourceName}");
            }

            var clone = Object.Instantiate(source.gameObject, parent, false);
            clone.name = name;
            clone.transform.localPosition = Vector3.zero;
            clone.transform.rotation = source.rotation;
            clone.transform.localScale = Vector3.one;
            return clone.transform;
        }

        private static void ApplyMappedMaterials(GameObject model)
        {
            foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>(true))
            {
                var sourceMaterials = renderer.sharedMaterials;
                var mappedMaterials = new Material[sourceMaterials.Length];
                for (var index = 0; index < sourceMaterials.Length; index++)
                {
                    mappedMaterials[index] = LoadMappedMaterial(sourceMaterials[index]?.name);
                }
                renderer.sharedMaterials = mappedMaterials;
            }
        }

        private static void SetModelNodeRenderersEnabled(GameObject model, string nodeName, bool enabled)
        {
            var node = FindDeepChild(model.transform, nodeName);
            if (node == null)
            {
                throw new InvalidOperationException($"正式无人机 FBX 缺少节点：{nodeName}");
            }
            foreach (var renderer in node.GetComponentsInChildren<Renderer>(true))
            {
                renderer.enabled = enabled;
            }
        }

        private static Transform FindDeepChild(Transform root, string name)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name)
                {
                    return child;
                }
            }
            return null;
        }

        private static Material LoadMappedMaterial(string sourceName)
        {
            var assetName = sourceName switch
            {
                "MAT_Graphite" => "DroneGraphite",
                "MAT_ShellTop" => "DroneShellTop",
                "MAT_MechanicalBlack" => "DroneMechanicalBlack",
                "MAT_SafetyOrange" => "DroneSafetyOrange",
                "MAT_FrontLED" => "DroneFrontLED",
                "MAT_CameraLens" => "DroneCameraLens",
                _ => throw new InvalidOperationException($"正式模型包含未映射材质槽：{sourceName}")
            };
            return AssetDatabase.LoadAssetAtPath<Material>($"{MaterialRoot}/{assetName}.mat");
        }

        private static void CreateBoxCollider(
            Transform parent,
            string name,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 size)
        {
            var target = EnsureChild(parent, name, localPosition);
            target.localRotation = localRotation;
            var collider = EnsureComponent<BoxCollider>(target.gameObject);
            collider.center = Vector3.zero;
            collider.size = size;
        }

        private static void BuildCommonCameraAndRuntime(GameObject root)
        {
            var cameraObject = new GameObject("DroneCameraOutput", typeof(Camera), typeof(AudioListener),
                typeof(DroneCameraRig), typeof(DroneCameraInput));
            cameraObject.transform.SetParent(root.transform, false);
            var camera = cameraObject.GetComponent<Camera>();
            camera.enabled = false;
            cameraObject.GetComponent<AudioListener>().enabled = false;

            var cameraRig = cameraObject.GetComponent<DroneCameraRig>();
            var model = root.transform.Find("DroneModel");
            cameraRig.Configure(
                camera,
                root.transform,
                model != null ? FindDeepChild(model, "GimbalYaw") : null,
                model != null ? FindDeepChild(model, "GimbalPitch") : null,
                root.transform.Find("FixedForwardMount"),
                root.transform.Find("BellyCameraMount"));

            root.AddComponent<DroneEquipmentHost>();
            root.AddComponent<DroneHookInput>();
            root.AddComponent<DroneRemoteControllerExperience>();
            root.AddComponent<DroneFlightSceneContext>();
        }

        private static void BuildGrappleEquipmentPrefab(DroneGrappleConfig config)
        {
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var equipment = BuildGrapple(config);
                SceneManager.MoveGameObjectToScene(equipment, preview);
                PrefabUtility.SaveAsPrefabAsset(equipment, GrappleEquipmentPrefabPath);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        private static void BuildHarpoonEquipmentPrefab(DroneHarpoonConfig config)
        {
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var equipment = BuildHarpoon(config);
                SceneManager.MoveGameObjectToScene(equipment, preview);
                PrefabUtility.SaveAsPrefabAsset(equipment, HarpoonEquipmentPrefabPath);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        private static void BuildVariant(DroneEquipmentKind kind, string equipmentPrefabPath, string path)
        {
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BasePrefabPath);
                var root = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab, preview);
                root.name = kind switch
                {
                    DroneEquipmentKind.Grapple => "DroneGrappleVariant",
                    DroneEquipmentKind.Harpoon => "DroneHarpoonVariant",
                    _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "组合机体必须包含一种装备。")
                };
                root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                root.transform.localScale = Vector3.one;
                var equipmentPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(equipmentPrefabPath);
                if (equipmentPrefab == null)
                {
                    throw new InvalidOperationException($"无法加载装备 Prefab：{equipmentPrefabPath}");
                }

                var equipment = (GameObject)PrefabUtility.InstantiatePrefab(equipmentPrefab, root.transform);
                equipment.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                equipment.transform.localScale = Vector3.one;
                MonoBehaviour module = kind switch
                {
                    DroneEquipmentKind.Grapple => equipment.GetComponent<DroneGrappleModule>(),
                    DroneEquipmentKind.Harpoon => equipment.GetComponent<DroneHarpoonModule>(),
                    _ => null
                };
                if (module == null)
                {
                    throw new InvalidOperationException($"装备 Prefab 缺少 {kind} 模块：{equipmentPrefabPath}");
                }
                if (module is DroneGrappleModule grappleModule)
                {
                    grappleModule.BindBellyMount(root.transform.Find("BellyEquipmentMount"));
                }
                WireVariant(root, module);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        private static GameObject BuildGrapple(DroneGrappleConfig config)
        {
            var equipment = new GameObject("GrappleEquipment", typeof(DroneGrappleModule));
            equipment.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            equipment.transform.localScale = Vector3.one;
            var orange = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Art/Generated/GrappleOrange.mat");

            var baseObject = new GameObject(
                "GrappleBase",
                typeof(Rigidbody),
                typeof(CapsuleCollider),
                typeof(ConfigurableJoint),
                typeof(DroneGrappleContactCollector));
            baseObject.transform.SetParent(equipment.transform, false);
            baseObject.transform.localPosition = new Vector3(0f, -0.20f, 0f);
            baseObject.transform.localScale = Vector3.one;
            var baseCollider = baseObject.GetComponent<CapsuleCollider>();
            baseCollider.direction = 1;
            baseCollider.radius = 0.075f;
            baseCollider.height = 0.04f;
            CreateVisualPrimitive(baseObject.transform, "BaseRing", PrimitiveType.Cylinder,
                Vector3.zero, Quaternion.identity, new Vector3(0.075f, 0.009f, 0.075f), null);
            CreateVisualPrimitive(baseObject.transform, "CentralMechanism", PrimitiveType.Cylinder,
                new Vector3(0f, 0.012f, 0f), Quaternion.identity, new Vector3(0.038f, 0.01f, 0.038f), null);

            var baseBody = baseObject.GetComponent<Rigidbody>();
            baseBody.useGravity = true;
            var suspension = baseObject.GetComponent<ConfigurableJoint>();
            suspension.connectedBody = null;
            suspension.autoConfigureConnectedAnchor = false;
            suspension.anchor = Vector3.zero;
            suspension.projectionMode = JointProjectionMode.None;
            var collector = baseObject.GetComponent<DroneGrappleContactCollector>();

            var clawBodies = new Rigidbody[4];
            var clawJoints = new HingeJoint[4];
            var clawColliders = new List<Collider>();
            const float pivotRadius = 0.065f;
            for (var index = 0; index < 4; index++)
            {
                var yaw = index * 90f;
                var radial = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                var claw = new GameObject(
                    "Claw_" + (index + 1),
                    typeof(Rigidbody),
                    typeof(HingeJoint),
                    typeof(DroneGrappleContactSensor));
                claw.transform.SetParent(equipment.transform, false);
                claw.transform.localPosition = baseObject.transform.localPosition + radial * pivotRadius;
                claw.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                claw.transform.localScale = Vector3.one;

                var body = claw.GetComponent<Rigidbody>();
                body.useGravity = true;
                var hinge = claw.GetComponent<HingeJoint>();
                hinge.connectedBody = baseBody;
                hinge.autoConfigureConnectedAnchor = false;
                hinge.axis = Vector3.right;
                hinge.anchor = Vector3.zero;
                hinge.connectedAnchor = baseObject.transform.InverseTransformPoint(claw.transform.position);
                hinge.limits = new JointLimits { min = -25f, max = 48f };
                hinge.useLimits = true;

                CreateVisualPrimitive(claw.transform, "PivotPin", PrimitiveType.Cylinder,
                    Vector3.zero, Quaternion.Euler(0f, 0f, 90f), new Vector3(0.018f, 0.025f, 0.018f), null);
                var upper = CreateClawSegment(
                    claw.transform,
                    "Upper",
                    new Vector3(0f, 0f, 0.035f),
                    new Vector3(0.025f, 0.02f, 0.07f),
                    Vector3.zero,
                    orange);
                var tip = CreateClawSegment(
                    claw.transform,
                    "Tip",
                    new Vector3(0f, 0f, 0.095f),
                    new Vector3(0.022f, 0.018f, 0.05f),
                    Vector3.zero,
                    orange);
                clawBodies[index] = body;
                clawJoints[index] = hinge;
                clawColliders.Add(upper);
                clawColliders.Add(tip);
                claw.GetComponent<DroneGrappleContactSensor>().Configure(collector, index);
            }

            var module = equipment.GetComponent<DroneGrappleModule>();
            module.Configure(config, null, baseBody, suspension, collector, clawBodies, clawJoints,
                clawColliders.ToArray());
            return equipment;
        }

        private static Collider CreateClawSegment(
            Transform parent,
            string name,
            Vector3 localPosition,
            Vector3 size,
            Vector3 localEuler,
            Material material)
        {
            var segment = new GameObject(name, typeof(BoxCollider));
            segment.transform.SetParent(parent, false);
            segment.transform.localPosition = localPosition;
            segment.transform.localEulerAngles = localEuler;
            segment.transform.localScale = Vector3.one;
            segment.GetComponent<BoxCollider>().size = size;
            CreateVisualPrimitive(segment.transform, "Visual", PrimitiveType.Cube,
                Vector3.zero, Quaternion.identity, size, material);
            return segment.GetComponent<Collider>();
        }

        private static GameObject BuildHarpoon(DroneHarpoonConfig config)
        {
            var equipment = new GameObject("HarpoonEquipment", typeof(DroneHarpoonModule));
            equipment.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            equipment.transform.localScale = Vector3.one;

            var launcher = new GameObject(
                "HarpoonLauncher",
                typeof(Rigidbody),
                typeof(BoxCollider),
                typeof(ConfigurableJoint));
            launcher.transform.SetParent(equipment.transform, false);
            launcher.transform.localPosition = new Vector3(0f, -0.12f, 0f);
            launcher.transform.localScale = Vector3.one;
            var launcherCollider = launcher.GetComponent<BoxCollider>();
            launcherCollider.center = new Vector3(0f, 0f, 0.07f);
            launcherCollider.size = new Vector3(0.20f, 0.08f, 0.30f);

            CreateVisualPrimitive(launcher.transform, "GimbalYawRing", PrimitiveType.Cylinder,
                new Vector3(0f, 0.028f, 0f), Quaternion.identity, new Vector3(0.08f, 0.012f, 0.08f), null);
            CreateVisualPrimitive(launcher.transform, "YokeLeft", PrimitiveType.Cube,
                new Vector3(-0.078f, -0.018f, 0.04f), Quaternion.identity, new Vector3(0.025f, 0.065f, 0.055f), null);
            CreateVisualPrimitive(launcher.transform, "YokeRight", PrimitiveType.Cube,
                new Vector3(0.078f, -0.018f, 0.04f), Quaternion.identity, new Vector3(0.025f, 0.065f, 0.055f), null);

            var launcherBody = launcher.GetComponent<Rigidbody>();
            var launcherJoint = launcher.GetComponent<ConfigurableJoint>();
            launcherJoint.connectedBody = null;
            launcherJoint.xMotion = ConfigurableJointMotion.Locked;
            launcherJoint.yMotion = ConfigurableJointMotion.Locked;
            launcherJoint.zMotion = ConfigurableJointMotion.Locked;
            launcherJoint.angularXMotion = ConfigurableJointMotion.Locked;
            launcherJoint.angularYMotion = ConfigurableJointMotion.Locked;
            launcherJoint.angularZMotion = ConfigurableJointMotion.Locked;
            launcherJoint.projectionMode = JointProjectionMode.None;

            var gimbal = new GameObject("HarpoonGimbal").transform;
            gimbal.SetParent(launcher.transform, false);
            gimbal.localPosition = new Vector3(0f, -0.01f, 0.015f);
            gimbal.localScale = Vector3.one;
            CreateVisualPrimitive(gimbal, "PitchTrunnion", PrimitiveType.Cylinder,
                Vector3.zero, Quaternion.Euler(0f, 0f, 90f), new Vector3(0.028f, 0.09f, 0.028f), null);
            CreateVisualPrimitive(gimbal, "LaunchTube", PrimitiveType.Cylinder,
                new Vector3(0f, 0f, 0.13f), Quaternion.Euler(90f, 0f, 0f),
                new Vector3(0.035f, 0.14f, 0.035f), null);
            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(gimbal, false);
            muzzle.localPosition = new Vector3(0f, 0f, 0.28f);
            muzzle.localRotation = Quaternion.identity;
            muzzle.localScale = Vector3.one;

            var projectileObject = new GameObject(
                "HarpoonProjectile",
                typeof(Rigidbody),
                typeof(CapsuleCollider),
                typeof(DroneHarpoonProjectile));
            projectileObject.transform.SetParent(equipment.transform, false);
            projectileObject.transform.SetPositionAndRotation(muzzle.position, muzzle.rotation);
            projectileObject.transform.localScale = Vector3.one;
            var projectileColliderComponent = projectileObject.GetComponent<CapsuleCollider>();
            projectileColliderComponent.radius = 0.016f;
            projectileColliderComponent.height = 0.26f;
            projectileColliderComponent.direction = 2;
            CreateVisualPrimitive(projectileObject.transform, "Shaft", PrimitiveType.Cylinder,
                new Vector3(0f, 0f, -0.005f), Quaternion.Euler(90f, 0f, 0f),
                new Vector3(0.012f, 0.11f, 0.012f), null);
            CreateConeVisual(projectileObject.transform, new Vector3(0f, 0f, 0.14f));
            for (var index = 0; index < 4; index++)
            {
                var rotation = Quaternion.Euler(0f, 0f, index * 90f);
                var offset = rotation * new Vector3(0.025f, 0f, -0.085f);
                CreateVisualPrimitive(projectileObject.transform, "TailFin_" + (index + 1), PrimitiveType.Cube,
                    offset, rotation, new Vector3(0.04f, 0.008f, 0.06f), null);
            }

            var projectileBody = projectileObject.GetComponent<Rigidbody>();
            var projectileCollider = projectileObject.GetComponent<Collider>();
            var relay = projectileObject.GetComponent<DroneHarpoonProjectile>();

            var ropeObject = new GameObject("HarpoonRopeVisual", typeof(LineRenderer), typeof(DroneHarpoonRopeVisual));
            ropeObject.transform.SetParent(equipment.transform, false);
            var line = ropeObject.GetComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.widthMultiplier = 0.012f;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.startColor = line.endColor = new Color(0.95f, 0.8f, 0.25f);

            var module = equipment.GetComponent<DroneHarpoonModule>();
            module.Configure(config, launcherBody, launcherJoint, gimbal, muzzle, projectileBody,
                projectileCollider, relay, ropeObject.GetComponent<DroneHarpoonRopeVisual>());
            return equipment;
        }

        private static void CreateVisualPrimitive(
            Transform parent,
            string name,
            PrimitiveType primitiveType,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 localScale,
            Material material)
        {
            var visual = GameObject.CreatePrimitive(primitiveType);
            visual.name = name;
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = localPosition;
            visual.transform.localRotation = localRotation;
            visual.transform.localScale = localScale;
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            if (material != null)
            {
                visual.GetComponent<Renderer>().sharedMaterial = material;
            }
        }

        private static void CreateConeVisual(Transform parent, Vector3 localPosition)
        {
            var visual = new GameObject("HarpoonTip", typeof(MeshFilter), typeof(MeshRenderer));
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = localPosition;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;
            visual.GetComponent<MeshFilter>().sharedMesh = GetOrCreateHarpoonCone();
        }

        private static Mesh GetOrCreateHarpoonCone()
        {
            const string path = Root + "/Art/Generated/HarpoonCone.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null)
            {
                return mesh;
            }

            mesh = new Mesh { name = "HarpoonCone" };
            const int sides = 8;
            var vertices = new Vector3[sides + 2];
            vertices[0] = new Vector3(0f, 0f, 0.14f);
            vertices[1] = Vector3.zero;
            for (var index = 0; index < sides; index++)
            {
                var angle = index * Mathf.PI * 2f / sides;
                vertices[index + 2] = new Vector3(Mathf.Cos(angle) * 0.045f, Mathf.Sin(angle) * 0.045f, 0f);
            }

            var triangles = new int[sides * 6];
            for (var index = 0; index < sides; index++)
            {
                var next = (index + 1) % sides;
                var offset = index * 6;
                triangles[offset] = 0;
                triangles[offset + 1] = index + 2;
                triangles[offset + 2] = next + 2;
                triangles[offset + 3] = 1;
                triangles[offset + 4] = next + 2;
                triangles[offset + 5] = index + 2;
            }
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        private static void WireVariant(GameObject root, MonoBehaviour module)
        {
            var controller = root.GetComponent<DroneFlightController>();
            var body = root.GetComponent<Rigidbody>();
            var cameraRig = root.GetComponentInChildren<DroneCameraRig>(true);
            var camera = cameraRig.OutputCamera;
            var host = root.GetComponent<DroneEquipmentHost>();
            SetReference(host, "flightController", controller);
            SetReference(host, "droneBody", body);
            SetReference(host, "aimCamera", camera);
            SetReference(host, "moduleSource", module);

            var remote = root.GetComponent<DroneRemoteControllerExperience>();
            SetReference(remote, "droneCameraRig", cameraRig);
            SetReference(remote, "flightInput", root.GetComponent<DronePlayerInput>());
            SetReference(remote, "flightController", controller);
            SetReference(remote, "mechanismInput", root.GetComponent<DroneHookInput>());

            var equipmentInput = root.GetComponent<DroneHookInput>();
            SetReference(equipmentInput, "equipmentHost", host);
            SetReference(equipmentInput, "landingGear", root.GetComponent<DroneLandingGearController>());
            SetReference(equipmentInput, "controlSession", remote);

            root.GetComponent<DroneFlightSceneContext>().Configure(
                controller,
                root.GetComponent<DronePlayerInput>(),
                cameraRig,
                remote,
                host,
                root.GetComponent<DroneLandingGearController>());
        }

        private static T GetOrCreateAsset<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static Transform EnsureChild(Transform parent, string name, Vector3 localPosition)
        {
            var child = parent.Find(name);
            if (child == null)
            {
                child = new GameObject(name).transform;
                child.SetParent(parent, false);
            }
            child.localPosition = localPosition;
            child.localRotation = Quaternion.identity;
            child.localScale = Vector3.one;
            return child;
        }

        private static void DestroyChild(Transform root, string path)
        {
            var child = root.Find(path);
            if (child != null)
            {
                Object.DestroyImmediate(child.gameObject);
            }
        }

        private static void RemoveAll<T>(GameObject root) where T : Component
        {
            foreach (var component in root.GetComponentsInChildren<T>(true))
            {
                Object.DestroyImmediate(component);
            }
        }

        private static T EnsureComponent<T>(GameObject gameObject) where T : Component
        {
            var component = gameObject.GetComponent<T>();
            return component != null ? component : gameObject.AddComponent<T>();
        }

        private static void SetReference(Object target, string propertyName, Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException($"{target.GetType().Name} 缺少序列化字段 {propertyName}。");
            }
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloat(Object target, string propertyName, float value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException($"{target.GetType().Name} 缺少序列化字段 {propertyName}。");
            }
            property.floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder(string path)
        {
            var parts = path.Split('/');
            var current = parts[0];
            for (var index = 1; index < parts.Length; index++)
            {
                var next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[index]);
                }
                current = next;
            }
        }
    }
}
