using System.Linq;
using Hotfix.DroneFlight;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.Demo
{
    /*
     * 测试说明：验证正式 FBX、URP 材质、基础 Prefab、旋翼坐标与推力轴、起落架净空及两种装备 Variant 的静态契约。
     */
    public sealed class DronePrototypeContractTests
    {
        private const string BasePath = "Assets/LoadResources/Demos/drone_flight/Prefabs/DronePrototype.prefab";
        private const string GrappleEquipmentPath =
            "Assets/LoadResources/Demos/drone_flight/Prefabs/Equipment/DroneGrappleEquipment.prefab";
        private const string HarpoonEquipmentPath =
            "Assets/LoadResources/Demos/drone_flight/Prefabs/Equipment/DroneHarpoonEquipment.prefab";
        private const string GrapplePath = "Assets/LoadResources/Demos/drone_flight/Prefabs/DroneGrappleVariant.prefab";
        private const string HarpoonPath = "Assets/LoadResources/Demos/drone_flight/Prefabs/DroneHarpoonVariant.prefab";
        private const string ModelPath = "Assets/LoadResources/Demos/drone_flight/Art/Models/DroneFlight.fbx";
        private const string MaterialRoot = "Assets/LoadResources/Demos/drone_flight/Art/Materials/";

        [Test]
        public void BasePrefab_ContainsOnlySharedFlightRuntime()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BasePath);
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponentsInChildren<DroneRotor>(true), Has.Length.EqualTo(4));
            Assert.That(prefab.GetComponent<DroneFlightController>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<DroneEquipmentHost>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<DroneEquipmentInput>(), Is.Not.Null);
            Assert.That(prefab.GetComponentsInChildren<DroneGrappleModule>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<DroneHarpoonModule>(true), Is.Empty);
            Assert.That(prefab.transform.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(prefab.transform.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(prefab.transform.localScale, Is.EqualTo(Vector3.one));
        }

        [Test]
        public void BasePrefab_UsesOfficialMeshesSharedRotorSourcesAndUrpMaterials()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BasePath);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            Assert.That(prefab, Is.Not.Null);
            Assert.That(model, Is.Not.Null);
            Assert.That(AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Mesh>().Select(mesh => mesh.name),
                Is.SupersetOf(DroneFlightModelContract.FormalObjectNames));
            Assert.That(DroneFlightModelContract.FormalObjectNames,
                Has.Count.EqualTo(DroneFlightModelContract.FormalObjectCount));

            var officialVisual = prefab.GetComponentsInChildren<MeshFilter>(true)
                .FirstOrDefault(filter => filter.sharedMesh != null && filter.sharedMesh.name == "Airframe");
            Assert.That(officialVisual, Is.Not.Null);
            Assert.That(officialVisual.sharedMesh.name, Is.EqualTo("Airframe"));
            Assert.That(prefab.GetComponentsInChildren<MeshCollider>(true), Is.Empty);

            foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true)
                         .Where(filter => IsOfficialMesh(filter.sharedMesh)))
            {
                Assert.That(filter.transform.localScale, Is.EqualTo(Vector3.one), filter.name);
                var renderer = filter.GetComponent<MeshRenderer>();
                Assert.That(renderer, Is.Not.Null, filter.name);
                foreach (var material in renderer.sharedMaterials)
                {
                    Assert.That(material, Is.Not.Null, filter.name);
                    Assert.That(material.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"), material.name);
                    Assert.That(AssetDatabase.GetAssetPath(material), Does.StartWith(MaterialRoot), material.name);
                }
            }

            var rotors = prefab.GetComponentsInChildren<DroneRotor>(true);
            var frontLeft = rotors.Single(rotor => rotor.Position == DroneRotorPosition.FrontLeft).transform;
            var frontRight = rotors.Single(rotor => rotor.Position == DroneRotorPosition.FrontRight).transform;
            var rearLeft = rotors.Single(rotor => rotor.Position == DroneRotorPosition.RearLeft).transform;
            var rearRight = rotors.Single(rotor => rotor.Position == DroneRotorPosition.RearRight).transform;
            AssertPosition(prefab.transform, frontLeft, DroneFlightModelContract.RotorPositions[0]);
            AssertPosition(prefab.transform, frontRight, DroneFlightModelContract.RotorPositions[1]);
            AssertPosition(prefab.transform, rearLeft, DroneFlightModelContract.RotorPositions[2]);
            AssertPosition(prefab.transform, rearRight, DroneFlightModelContract.RotorPositions[3]);
            foreach (var rotor in rotors)
            {
                var thrustAxis = prefab.transform.InverseTransformDirection(rotor.ForceDirection).normalized;
                Assert.That(Vector3.Dot(thrustAxis, DroneFlightModelContract.PhysicalThrustAxis),
                    Is.GreaterThan(0.9999f),
                    $"{rotor.name} 的物理推力轴必须与机体局部 +Y 一致，否则正式飞行无法建立控制分配矩阵。");
            }
            Assert.That(GetRotorBlade(frontLeft), Is.SameAs(GetRotorBlade(rearRight)));
            Assert.That(GetRotorBlade(frontRight), Is.SameAs(GetRotorBlade(rearLeft)));
            Assert.That(GetRotorBlade(frontLeft).name,
                Is.EqualTo(DroneFlightModelContract.CounterClockwiseBladeName));
            Assert.That(GetRotorBlade(frontRight).name,
                Is.EqualTo(DroneFlightModelContract.ClockwiseBladeName));

            var yaw = FindDeep(prefab.transform, DroneFlightModelContract.GimbalYawName);
            var pitch = FindDeep(prefab.transform, DroneFlightModelContract.GimbalPitchName);
            var cameraBody = FindDeep(prefab.transform, DroneFlightModelContract.CameraBodyName);
            Assert.That(yaw, Is.Not.Null);
            Assert.That(pitch?.parent, Is.EqualTo(yaw));
            Assert.That(cameraBody?.parent, Is.EqualTo(pitch));
            Assert.That(prefab.transform.Find(DroneFlightModelContract.BellyEquipmentMountName).localPosition,
                Is.EqualTo(DroneFlightModelContract.BellyEquipmentMountPosition));
        }

        [Test]
        public void BasePrefab_LandingGearUsesRealHingesAndKeepsPointTwoThreeMeterClearance()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BasePath);
            for (var index = 0; index < DroneFlightModelContract.LandingGearHingePositions.Count; index++)
            {
                var leg = FindDeep(prefab.transform, DroneFlightModelContract.LandingGearNames[index]);
                Assert.That(leg, Is.Not.Null);
                AssertPosition(prefab.transform, leg, DroneFlightModelContract.LandingGearHingePositions[index]);
                Assert.That(leg.localScale, Is.EqualTo(Vector3.one));
                Assert.That(leg.Find("Foot")?.GetComponent<BoxCollider>(), Is.Not.Null);
            }

            var footMinimum = prefab.GetComponentsInChildren<BoxCollider>(true)
                .Where(collider => collider.name == "Foot")
                .Min(GetMinimumY);
            Assert.That(footMinimum, Is.EqualTo(-0.236f).Within(0.001f));
        }

        [Test]
        public void BasePrefab_RetractedLandingGearRaisesEveryFoot()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BasePath);
            var instance = Object.Instantiate(prefab);
            try
            {
                var gear = instance.GetComponent<DroneLandingGearController>();
                var serialized = new SerializedObject(gear);
                var offsets = serialized.FindProperty("retractedEulerOffsets");
                for (var index = 0; index < DroneFlightModelContract.LandingGearNames.Count; index++)
                {
                    var leg = FindDeep(instance.transform, DroneFlightModelContract.LandingGearNames[index]);
                    var foot = leg.Find("Foot");
                    var deployedY = instance.transform.InverseTransformPoint(foot.position).y;
                    leg.localRotation *= Quaternion.Euler(offsets.GetArrayElementAtIndex(index).vector3Value);
                    var retractedY = instance.transform.InverseTransformPoint(foot.position).y;
                    Assert.That(retractedY, Is.GreaterThan(deployedY), leg.name);
                }
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void GrappleVariant_HasOneBaseFourClawsFourHingesAndOneSuspensionJoint()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GrapplePath);
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponentsInChildren<DroneGrappleModule>(true), Has.Length.EqualTo(1));
            Assert.That(prefab.GetComponentsInChildren<DroneHarpoonModule>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<HingeJoint>(true), Has.Length.EqualTo(4));
            var grappleModule = prefab.GetComponentInChildren<DroneGrappleModule>(true);
            var claws = Enumerable.Range(1, 4)
                .Select(index => grappleModule.transform.Find($"Claw_{index}"))
                .ToArray();
            Assert.That(claws.All(claw => claw != null), Is.True);
            var clawBodies = claws.Select(claw => claw.GetComponent<Rigidbody>()).ToArray();
            Assert.That(clawBodies.All(body => body != null && body.transform.localScale == Vector3.one), Is.True);
            Assert.That(prefab.GetComponentsInChildren<ConfigurableJoint>(true)
                .Count(joint => joint.gameObject.name == "GrappleBase"), Is.EqualTo(1));
            var grappleBody = grappleModule.transform.Find("GrappleBase")?.GetComponent<Rigidbody>();
            Assert.That(grappleBody, Is.Not.Null);
            Assert.That(grappleModule.transform.Find("GrappleBase/GrappleArm"), Is.Not.Null);
            Assert.That(grappleModule.transform.Find("GrappleBase/GrappleArm").GetComponent<Rigidbody>(), Is.Null);
            Assert.That(grappleModule.transform.Find("UniversalJointUpperSeat"), Is.Not.Null);
            Assert.That(grappleModule.transform.Find("LiftSleeveVisual"), Is.Not.Null);
            Assert.That(prefab.GetComponentsInChildren<Rigidbody>(true).Length, Is.EqualTo(6));
            var suspension = grappleBody.GetComponent<ConfigurableJoint>();
            Assert.That(suspension.xMotion, Is.EqualTo(ConfigurableJointMotion.Locked));
            Assert.That(suspension.yMotion, Is.EqualTo(ConfigurableJointMotion.Locked));
            Assert.That(suspension.zMotion, Is.EqualTo(ConfigurableJointMotion.Locked));
            Assert.That(suspension.angularXMotion, Is.EqualTo(ConfigurableJointMotion.Locked));
            Assert.That(suspension.angularYMotion, Is.EqualTo(ConfigurableJointMotion.Limited));
            Assert.That(suspension.angularZMotion, Is.EqualTo(ConfigurableJointMotion.Limited));
            Assert.That(suspension.anchor.y, Is.EqualTo(0.08f).Within(0.0001f));
            var captureVolume = grappleBody.transform.Find("GrappleCaptureVolume")?.GetComponent<BoxCollider>();
            Assert.That(captureVolume, Is.Not.Null);
            Assert.That(captureVolume.isTrigger, Is.True);
            Assert.That(captureVolume.GetComponent<Rigidbody>(), Is.Null);
            Assert.That(captureVolume.size.x, Is.GreaterThanOrEqualTo(0.46f));
            Assert.That(grappleBody.mass + clawBodies.Sum(body => body.mass),
                Is.EqualTo(0.05f).Within(0.0001f));
            Assert.That(prefab.transform.localScale, Is.EqualTo(Vector3.one));
            var grappleBase = grappleModule.transform.Find("GrappleBase");
            foreach (var claw in claws)
            {
                var distance = Vector2.Distance(
                    new Vector2(claw.position.x, claw.position.z),
                    new Vector2(grappleBase.position.x, grappleBase.position.z));
                Assert.That(distance, Is.EqualTo(0.065f).Within(0.002f), claw.name);
                var hinge = claw.GetComponent<HingeJoint>();
                var clawAnchor = hinge.transform.TransformPoint(hinge.anchor);
                var baseAnchor = hinge.connectedBody.transform.TransformPoint(hinge.connectedAnchor);
                Assert.That(Vector3.Distance(clawAnchor, baseAnchor), Is.LessThan(0.0001f), claw.name);
                Assert.That(claw.transform.Find("Upper/Visual"), Is.Not.Null);
                Assert.That(claw.transform.Find("Tip/Visual"), Is.Not.Null);
                var tipRadius = Vector2.Distance(
                    new Vector2(claw.transform.Find("Tip").position.x, claw.transform.Find("Tip").position.z),
                    new Vector2(grappleBase.position.x, grappleBase.position.z));
                Assert.That(tipRadius * 2f, Is.GreaterThanOrEqualTo(0.38f), claw.name);
            }
        }

        [Test]
        public void BasePrefab_IsThePureDroneSelectionWithoutAdditionalEquipment()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BasePath);
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponentsInChildren<DroneGrappleModule>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<DroneHarpoonModule>(true), Is.Empty);
            Assert.That(prefab.transform.Find("GrappleEquipment"), Is.Null);
            Assert.That(prefab.transform.Find("HarpoonEquipment"), Is.Null);
            Assert.That(prefab.GetComponent<DroneEquipmentHost>().Kind, Is.EqualTo(DroneEquipmentKind.None));
            Assert.That(prefab.transform.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(prefab.transform.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(prefab.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(
                DroneFlightSceneCoordinator.PlainDroneAddress,
                Is.EqualTo("LoadResources/Demos/drone_flight/Prefabs/DronePrototype"));
        }

        [Test]
        public void EquipmentPrefabs_AreIndependentAssetsAndVariantsKeepNestedInstances()
        {
            AssertNestedEquipment(
                GrappleEquipmentPath,
                GrapplePath,
                typeof(DroneGrappleModule));
            AssertNestedEquipment(
                HarpoonEquipmentPath,
                HarpoonPath,
                typeof(DroneHarpoonModule));
        }

        [TestCase(GrapplePath)]
        [TestCase(HarpoonPath)]
        public void EquipmentVariant_IsSavedAsBaseDronePrefabVariant(string path)
        {
            var variant = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(variant, Is.Not.Null);
            Assert.That(PrefabUtility.GetPrefabAssetType(variant), Is.EqualTo(PrefabAssetType.Variant));
            var source = PrefabUtility.GetCorrespondingObjectFromSource(variant);
            Assert.That(AssetDatabase.GetAssetPath(source), Is.EqualTo(BasePath));
        }

        [Test]
        public void HarpoonVariant_HasOnlyHarpoonAndSingleReusableProjectile()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HarpoonPath);
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponentsInChildren<DroneHarpoonModule>(true), Has.Length.EqualTo(1));
            Assert.That(prefab.GetComponentsInChildren<DroneGrappleModule>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<DroneHarpoonProjectile>(true), Has.Length.EqualTo(1));
            Assert.That(prefab.GetComponentsInChildren<DroneHarpoonRopeVisual>(true), Has.Length.EqualTo(1));
            var harpoonModule = prefab.GetComponentInChildren<DroneHarpoonModule>(true);
            var launcher = harpoonModule.transform.Find("HarpoonLauncher");
            var muzzle = harpoonModule.transform.Find("HarpoonLauncher/HarpoonGimbal/Muzzle");
            var tube = harpoonModule.transform.Find("HarpoonLauncher/HarpoonGimbal/LaunchTube");
            var projectile = prefab.GetComponentInChildren<DroneHarpoonProjectile>(true);
            var projectileBody = projectile.GetComponent<Rigidbody>();
            var capsule = projectile.GetComponent<CapsuleCollider>();
            Assert.That(muzzle, Is.Not.Null);
            Assert.That(launcher.GetComponent<Rigidbody>(), Is.Null);
            Assert.That(launcher.GetComponent<ConfigurableJoint>(), Is.Null);
            Assert.That(tube, Is.Not.Null);
            Assert.That(capsule.direction, Is.EqualTo(2));
            Assert.That(Vector3.Angle(tube.up, muzzle.forward), Is.LessThan(0.1f));
            Assert.That(Vector3.Angle(projectile.transform.forward, muzzle.forward), Is.LessThan(0.1f));
            Assert.That(Vector3.Angle(prefab.transform.InverseTransformDirection(muzzle.forward), Vector3.down),
                Is.LessThan(0.1f));
            Assert.That(projectile.transform.Find("Shaft"), Is.Not.Null);
            Assert.That(projectile.transform.Find("HarpoonTip"), Is.Not.Null);
            Assert.That(projectile.transform.Cast<Transform>().Count(child => child.name.StartsWith("TailFin_")),
                Is.EqualTo(4));
            Assert.That(harpoonModule.transform.Find("HarpoonAimReticle")?.GetComponent<LineRenderer>(), Is.Not.Null);
            Assert.That(prefab.GetComponentsInChildren<Renderer>(true)
                .All(renderer => renderer.sharedMaterials.All(material => material != null)), Is.True);
            Assert.That(harpoonModule.transform.Find("HarpoonRopeVisual").GetComponent<LineRenderer>().enabled,
                Is.False);
            Assert.That(projectileBody.useGravity, Is.False);
            Assert.That(projectileBody.isKinematic, Is.True);
            Assert.That(capsule.enabled, Is.False);
            var rope = harpoonModule.transform.Find("HarpoonRopeVisual").GetComponent<LineRenderer>();
            Assert.That(rope.widthMultiplier, Is.EqualTo(0.003f).Within(0.0001f));
            Assert.That(rope.sharedMaterial, Is.Not.Null);
        }

        [TestCase(BasePath)]
        [TestCase(GrapplePath)]
        [TestCase(HarpoonPath)]
        public void Variant_CanBePlacedFromGroundMarkerWithoutLandingGearPenetration(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var staging = new GameObject("InactiveStaging");
            var marker = new GameObject("GroundMarker").transform;
            staging.SetActive(false);
            marker.position = new Vector3(4f, 0.012f, -3f);
            marker.rotation = Quaternion.Euler(0f, 37f, 0f);
            var instance = Object.Instantiate(prefab, staging.transform);
            try
            {
                Assert.That(DroneSpawnPlacement.TryPlaceOnGround(
                    instance,
                    marker,
                    DroneSpawnPlacement.DefaultGroundClearanceMeters,
                    out var footMinimumY), Is.True);
                Assert.That(footMinimumY, Is.EqualTo(0.022f).Within(0.0001f));
                Assert.That(instance.transform.position.y, Is.EqualTo(0.258f).Within(0.002f));
                Assert.That(instance.transform.localScale, Is.EqualTo(Vector3.one));
            }
            finally
            {
                Object.DestroyImmediate(staging);
                Object.DestroyImmediate(marker.gameObject);
            }
        }

        [TestCase(GrapplePath)]
        [TestCase(HarpoonPath)]
        public void EquipmentVariant_StowedVisualAndEnabledCollidersStayAboveLandingFeet(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var module = prefab.GetComponentsInChildren<MonoBehaviour>(true)
                .FirstOrDefault(component => component is IDroneEquipmentModule);
            Assert.That(module, Is.Not.Null);
            var rendererMinimum = module.GetComponentsInChildren<Renderer>(true).Min(renderer => renderer.bounds.min.y);
            var colliderMinimum = module.GetComponentsInChildren<Collider>(true)
                .Where(collider => collider.enabled)
                .Min(collider => collider.bounds.min.y);
            Assert.That(rendererMinimum, Is.GreaterThanOrEqualTo(-0.221f), path);
            Assert.That(colliderMinimum, Is.GreaterThanOrEqualTo(-0.221f), path);
        }

        [Test]
        public void FlightConfig_HasNoEquipmentSerializedFields()
        {
            var config = ScriptableObject.CreateInstance<DroneFlightConfig>();
            try
            {
                var serialized = new SerializedObject(config);
                foreach (var field in new[] { "grappleHardwareMassKilograms", "winchStowedLengthMeters",
                             "grappleBreakForceNewtons", "harpoon", "ropeSpring" })
                {
                    Assert.That(serialized.FindProperty(field), Is.Null, field);
                }
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        private static void AssertNestedEquipment(
            string equipmentPath,
            string variantPath,
            System.Type moduleType)
        {
            var equipmentPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(equipmentPath);
            var variant = AssetDatabase.LoadAssetAtPath<GameObject>(variantPath);
            Assert.That(equipmentPrefab, Is.Not.Null, equipmentPath);
            Assert.That(variant, Is.Not.Null, variantPath);
            Assert.That(equipmentPrefab.transform.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(equipmentPrefab.transform.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(equipmentPrefab.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(equipmentPrefab.GetComponent(moduleType), Is.Not.Null);

            var nestedModule = variant.GetComponentInChildren(moduleType, true) as Component;
            Assert.That(nestedModule, Is.Not.Null, $"{variantPath} 缺少嵌套装备模块 {moduleType.Name}");
            var nestedSource = PrefabUtility.GetCorrespondingObjectFromSource(nestedModule.gameObject);
            Assert.That(AssetDatabase.GetAssetPath(nestedSource), Is.EqualTo(equipmentPath));
        }

        private static bool IsOfficialMesh(Mesh mesh)
        {
            return mesh != null && AssetDatabase.GetAssetPath(mesh) == ModelPath;
        }

        private static Mesh GetRotorBlade(Transform rotor)
        {
            return rotor.GetComponent<DroneRotor>()?.VisualPropeller
                ?.GetComponentInChildren<MeshFilter>(true)?.sharedMesh;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            return root.GetComponentsInChildren<Transform>(true).FirstOrDefault(child => child.name == name);
        }

        private static void AssertPosition(Transform root, Transform target, Vector3 expected)
        {
            Assert.That(Vector3.Distance(root.InverseTransformPoint(target.position), expected),
                Is.LessThan(0.0001f), target.name);
        }

        private static float GetMinimumY(BoxCollider collider)
        {
            var minimum = float.PositiveInfinity;
            var half = collider.size * 0.5f;
            for (var x = -1; x <= 1; x += 2)
            for (var y = -1; y <= 1; y += 2)
            for (var z = -1; z <= 1; z += 2)
            {
                var point = collider.center + Vector3.Scale(half, new Vector3(x, y, z));
                minimum = Mathf.Min(minimum, collider.transform.TransformPoint(point).y);
            }
            return minimum;
        }
    }
}
