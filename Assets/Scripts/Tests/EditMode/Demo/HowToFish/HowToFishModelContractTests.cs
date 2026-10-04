using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.Demo
{
    public sealed class HowToFishModelContractTests
    {
        [Test]
        public void AxisProbe_ImportsAtMeterScaleWithUnityForwardRightAndUp()
        {
            const string path = "Assets/LoadResources/Demos/how_to_fish/Art/Models/AxisProbe.fbx";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, "需要先导出并导入候选 FBX。");
            var instance = Object.Instantiate(prefab);
            try
            {
                var nodes = instance.GetComponentsInChildren<Transform>(true);
                foreach (var node in nodes)
                    TestContext.WriteLine($"{node.name}: world={node.position}, localRotation={node.localEulerAngles}, scale={node.localScale}");
                AssertMarker(nodes, "Forward", Vector3.forward);
                AssertMarker(nodes, "Right", Vector3.right);
                AssertMarker(nodes, "Up", Vector3.up);
                var hinge = nodes.Single(node => node.name == "Hinge");
                var tip = nodes.Single(node => node.name == "HingeForward");
                Assert.That(Quaternion.Angle(instance.transform.rotation, Quaternion.identity), Is.LessThan(0.1f), "模型根不应残留轴向包装旋转。");
                Assert.That(Quaternion.Angle(hinge.rotation, Quaternion.identity), Is.LessThan(0.1f), "活动挂点必须沿 Unity 局部坐标旋转。");
                var direction = (tip.position - hinge.position).normalized;
                Assert.That(Vector3.Dot(direction, Vector3.forward), Is.GreaterThan(0.999f));
                hinge.Rotate(Vector3.up, 90, Space.Self);
                direction = (tip.position - hinge.position).normalized;
                Assert.That(Vector3.Dot(direction, Vector3.right), Is.GreaterThan(0.999f));
                foreach (var node in nodes)
                {
                    Assert.That(node.lossyScale.x, Is.GreaterThan(0));
                    Assert.That(node.lossyScale.y, Is.GreaterThan(0));
                    Assert.That(node.lossyScale.z, Is.GreaterThan(0));
                }
            }
            finally { Object.DestroyImmediate(instance); }
        }

        private static void AssertMarker(Transform[] nodes, string name, Vector3 expected)
        {
            var marker = nodes.Single(node => node.name == name);
            Assert.That(Vector3.Distance(marker.position, expected), Is.LessThan(0.002f), name + " 轴或米制不正确。");
        }

        [TestCase("BrownCrab", "Hook")]
        [TestCase("RockCrab", "Hook")]
        [TestCase("SpiderCrab", "Hook")]
        [TestCase("Shrimp", "Hook")]
        [TestCase("Lobster", "Hook")]
        [TestCase("Clam", "Hook")]
        [TestCase("CrabRod", "RodTip")]
        [TestCase("Knife", "Strike")]
        [TestCase("BrassKnuckles", "Strike")]
        [TestCase("UpgradeAnvil", "Forward")]
        [TestCase("AmmoUpgrade", "Forward")]
        [TestCase("RedDotSight", "Forward")]
        [TestCase("SniperScope", "Forward")]
        [TestCase("Compensator", "Forward")]
        [TestCase("Suppressor", "Forward")]
        [TestCase("LaserSight", "Forward")]
        [TestCase("ExtendedMag", "Forward")]
        [TestCase("AttachmentCrate", "Forward")]
        [TestCase("Backpack", "Forward")]
        [TestCase("SmallMotor", "Forward")]
        [TestCase("MediumMotor", "Forward")]
        [TestCase("BigMotor", "Forward")]
        [TestCase("BoatRadar", "Forward")]
        [TestCase("SlotMachine", "Intake")]
        [TestCase("RouletteTable", "RedBet")]
        [TestCase("CrabMeat", "Hook")]
        [TestCase("FishingBoat", "Bow")]
        [TestCase("Lighthouse", "DoorFront")]
        [TestCase("Keeper", "Face")]
        [TestCase("RightHand", "Forward")]
        [TestCase("Beer", "Forward")]
        [TestCase("HotDog", "Forward")]
        [TestCase("Radar", "Forward")]
        [TestCase("Pine", "Forward")]
        [TestCase("Leech", "Hook")]
        [TestCase("ForestLady", "Face")]
        [TestCase("Piranha", "Hook")]
        [TestCase("GiantPiranha", "Hook")]
        [TestCase("PiranhaSkeleton", "Hook")]
        [TestCase("Pistol", "Muzzle")]
        [TestCase("Shotgun", "Muzzle")]
        [TestCase("ForestShop", "CounterFront")]
        [TestCase("FishingRod", "RodTip")]
        [TestCase("BeginnerLure", "Forward")]
        [TestCase("Mackerel", "Hook")]
        [TestCase("Gar", "Hook")]
        [TestCase("Pike", "Hook")]
        [TestCase("Cod", "Hook")]
        [TestCase("Goldfish", "Hook")]
        [TestCase("Perch", "Hook")]
        [TestCase("Triggerfish", "Hook")]
        [TestCase("Goby", "Hook")]
        [TestCase("Salmon", "Hook")]
        [TestCase("Angelfish", "Hook")]
        [TestCase("Catfish", "Hook")]
        [TestCase("SeaUrchin", "Hook")]
        [TestCase("Clownfish", "Hook")]
        [TestCase("Bluegill", "Hook")]
        [TestCase("Needlefish", "Hook")]
        [TestCase("Seahorse", "Hook")]
        [TestCase("Bowlfish", "Hook")]
        [TestCase("YellowBoxfish", "Hook")]
        [TestCase("BlueShark", "Hook")]
        [TestCase("Pufferfish", "Hook")]
        [TestCase("Palm", "Forward")]
        [TestCase("Tourist", "Face")]
        [TestCase("GrillMaster", "Face")]
        [TestCase("Grill", "Forward")]
        [TestCase("Carrot", "Hook")]
        [TestCase("FishMeat", "Hook")]
        [TestCase("PufferfishFin", "Hook")]
        [TestCase("Lighter", "Forward")]
        [TestCase("PoisonPool", "Forward")]
        [TestCase("SMG", "Muzzle")]
        [TestCase("StandardLure", "Forward")]
        [TestCase("Tuna", "Hook")]
        [TestCase("Albatross", "Hook")]
        [TestCase("AlbatrossHead", "Hook")]
        [TestCase("Islander", "Face")]
        [TestCase("SniperRifle", "Muzzle")]
        [TestCase("RocksShop", "DoorFront")]
        [TestCase("ProfessionalLure", "Forward")]
        [TestCase("BirdDropping", "Forward")]
        [TestCase("Bass", "Hook")]
        [TestCase("RedSnapper", "Hook")]
        [TestCase("Parrotfish", "Hook")]
        [TestCase("Tigerfish", "Hook")]
        [TestCase("FlyingFish", "Hook")]
        [TestCase("Sengarat", "Hook")]
        [TestCase("Eel", "Hook")]
        [TestCase("Halibut", "Hook")]
        [TestCase("Voxelfish", "Hook")]
        [TestCase("Dripper", "Hook")]
        [TestCase("BowheadWhale", "Hook")]
        [TestCase("MutatedBowheadWhale", "Hook")]
        [TestCase("WhaleFin", "Hook")]
        [TestCase("Scientist", "FaceFront")]
        [TestCase("FishBucket", "Forward")]
        [TestCase("MilitaryBoatKey", "Forward")]
        [TestCase("MilitaryTent", "DoorFront")]
        [TestCase("SupplyCrate", "Forward")]
        [TestCase("VolcanoPlank", "Forward")]
        [TestCase("AssaultRifle", "Muzzle")]
        [TestCase("ScientificLure", "Forward")]
        [TestCase("LavaGlob", "Forward")]
        [TestCase("LavaPool", "Forward")]
        [TestCase("MilitaryBoat", "Bow")]
        [TestCase("Blobfish", "Hook")]
        [TestCase("Anglerfish", "Hook")]
        [TestCase("Oarfish", "Hook")]
        [TestCase("Stonefish", "Hook")]
        [TestCase("SuperdwarfFish", "Hook")]
        [TestCase("FootSnail", "Hook")]
        [TestCase("VolcanoTerrain", "Forward")]
        [TestCase("Sunfish", "Hook")]
        [TestCase("OldPike", "Hook")]
        [TestCase("GoblinShark", "Hook")]
        [TestCase("BeginnerBossLure", "Forward")]
        [TestCase("ScientificBossLure", "Forward")]
        [TestCase("BingBong", "Hook")]
        [TestCase("Seagull", "Hook")]
        [TestCase("Coconut", "Forward")]
        [TestCase("PlayerRemains", "Forward")]
        [TestCase("Dynamite", "Fuse")]
        public void FirstIslandModel_PreservesForwardMountAndAppliedTransforms(string name, string mount)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/LoadResources/Demos/how_to_fish/Art/Models/" + name + ".fbx");
            Assert.That(prefab, Is.Not.Null);
            var instance = Object.Instantiate(prefab);
            try
            {
                var transforms = instance.GetComponentsInChildren<Transform>(true);
                var front = transforms.Single(node => node.name == mount);
                var grip = transforms.Single(node => node.name == "Grip");
                Assert.That(front.position.z, Is.GreaterThan(grip.position.z), "前方挂点不能反向。");
                foreach (var node in transforms)
                {
                    Assert.That(Quaternion.Angle(node.localRotation, Quaternion.identity), Is.LessThan(0.1f), node.name);
                    Assert.That(Vector3.Distance(node.localScale, Vector3.one), Is.LessThan(0.001f), node.name);
                }
                Assert.That(instance.GetComponentsInChildren<MeshFilter>().Sum(filter => filter.sharedMesh.vertexCount), Is.GreaterThan(30));
                if (name == "CrabRod") Assert.That(front.position.z, Is.InRange(1.7f, 1.9f));
                if (name == "RouletteTable")
                {
                    var wheel = transforms.Single(node => node.name == "Wheel");
                    var ball = transforms.Single(node => node.name == "Ball");
                    Assert.That(Vector3.Distance(wheel.position, new Vector3(0, 1.05f, -.35f)), Is.LessThan(.002f));
                    Assert.That(ball.parent, Is.SameAs(instance.transform), "白球需要独立于旋转轮。");
                    var pockets = instance.GetComponentsInChildren<MeshFilter>().Where(node => node.name.StartsWith("Pocket")).ToArray();
                    Assert.That(pockets.Length, Is.EqualTo(37));
                    foreach (var pocket in pockets)
                        foreach (var normal in pocket.sharedMesh.normals)
                            Assert.That(Vector3.Dot(pocket.transform.TransformDirection(normal), Vector3.up), Is.GreaterThan(.99f));
                }
                if (name == "SlotMachine")
                    foreach (var symbol in instance.GetComponentsInChildren<MeshFilter>().Where(value =>
                        value.name.StartsWith("RewardJewel") || value.name.StartsWith("ReelDiamond")))
                        foreach (var normal in symbol.sharedMesh.normals)
                            Assert.That(Vector3.Dot(symbol.transform.TransformDirection(normal), Vector3.forward), Is.GreaterThan(.99f),
                                symbol.name + " 正面符号法线必须朝外，避免被背面剔除。");
                if (name == "RightHand") Assert.That(transforms.Single(node => node.name == "Sleeve").GetComponent<Renderer>().bounds.center.x,
                    Is.GreaterThan(.03f), "右手袖口应位于握持轴右侧。");
            }
            finally { Object.DestroyImmediate(instance); }
        }
    }
}
