using System.IO;
using System.Linq;
using Hotfix.DroneFlight;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Tests.Demo
{
    /*
     * 测试说明：验证捕鱼 MVP 的贝塞尔路径、自动驾驶输入和独立场景装配契约。
     */
    public sealed class DroneFishingMissionTests
    {
        private const string ScenePath =
            "Assets/LoadResources/Demos/drone_flight/Scenes/FishingBurstMvp.unity";
        private const string PathPrefabPath =
            "Assets/LoadResources/Demos/drone_flight/Prefabs/Mission/DroneFishingBezierRoute.prefab";

        [Test]
        public void BezierPath_EvaluatesExactEndpointsAndMovesWithTargetRoot()
        {
            var root = new GameObject("PathFixture");
            try
            {
                var path = root.AddComponent<DroneBezierMissionPath>();
                var segment = CreateSegment(root.transform,
                    Vector3.zero,
                    new Vector3(1f, 0f, 0f),
                    new Vector3(2f, 0f, 0f),
                    new Vector3(3f, 0f, 0f));
                path.Configure(new[] { segment }, new[] { segment }, new[] { segment });

                Assert.That(path.Evaluate(DroneMissionPathSection.Entry, 0f), Is.EqualTo(Vector3.zero));
                Assert.That(path.Evaluate(DroneMissionPathSection.Entry, 1f), Is.EqualTo(new Vector3(3f, 0f, 0f)));
                Assert.That(path.GetApproximateLength(DroneMissionPathSection.Entry), Is.EqualTo(3f).Within(0.01f));

                root.transform.position = new Vector3(5f, 2f, -4f);
                Assert.That(path.Evaluate(DroneMissionPathSection.Entry, 0f),
                    Is.EqualTo(new Vector3(5f, 2f, -4f)));
                Assert.That(path.Evaluate(DroneMissionPathSection.Entry, 1f),
                    Is.EqualTo(new Vector3(8f, 2f, -4f)));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void AutopilotInput_IsFiniteClampedAndHeadingRelative()
        {
            var input = DroneMissionAutopilot.CalculateInput(
                Vector3.zero,
                90f,
                new Vector3(10f, 0f, 0f),
                Vector3.forward,
                1f,
                4f,
                45f);

            Assert.That(input.HadInvalidValue, Is.False);
            Assert.That(input.Forward, Is.EqualTo(1f).Within(0.001f));
            Assert.That(input.Right, Is.EqualTo(0f).Within(0.001f));
            Assert.That(input.Yaw, Is.InRange(-1f, 1f));
        }

        [Test]
        public void FishingMvpScene_HasSingleMissionPathAndNoPreplacedDrone()
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(PathPrefabPath), Is.Not.Null);
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                Assert.That(roots.SelectMany(root =>
                        root.GetComponentsInChildren<DroneFishingMissionCoordinator>(true)).ToArray(),
                    Has.Length.EqualTo(1));
                Assert.That(roots.SelectMany(root =>
                        root.GetComponentsInChildren<DroneBezierMissionPath>(true)).ToArray(),
                    Has.Length.EqualTo(1));
                Assert.That(roots.SelectMany(root =>
                        root.GetComponentsInChildren<DroneFlightController>(true)).ToArray(),
                    Is.Empty,
                    "捕鱼 MVP 应在按钮点击后实例化无人机，不得在场景中预放活动机体。");
                var fish = roots.SelectMany(root => root.GetComponentsInChildren<Rigidbody>(true))
                    .Single(body => body.name == "FishPayloadCube_YMinus5");
                Assert.That(fish.position.y, Is.EqualTo(-5f).Within(0.001f));
                Assert.That(fish.isKinematic, Is.True);
                Assert.That(fish.mass, Is.EqualTo(0.35f).Within(0.001f));
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void PortableBoundary_IncludesMissionRuntimeWithoutHostServices()
        {
            var missionRoot = Path.GetFullPath("Assets/Scripts/Hotfix/Module/DroneFlight/Mission");
            var forbidden = new[]
            {
                "using Core.Runtime", "using Hotfix.SceneManagement", "UIManager.",
                "ResourceServices.", "GameSceneNavigator."
            };
            foreach (var file in Directory.GetFiles(missionRoot, "*.cs", SearchOption.AllDirectories))
            {
                var source = File.ReadAllText(file);
                foreach (var dependency in forbidden)
                {
                    StringAssert.DoesNotContain(dependency, source, file);
                }
            }
        }

        private static DroneBezierSegment CreateSegment(
            Transform parent,
            Vector3 point0,
            Vector3 point1,
            Vector3 point2,
            Vector3 point3)
        {
            var segment = new DroneBezierSegment();
            segment.Configure(
                CreatePoint(parent, "P0", point0),
                CreatePoint(parent, "P1", point1),
                CreatePoint(parent, "P2", point2),
                CreatePoint(parent, "P3", point3));
            return segment;
        }

        private static Transform CreatePoint(Transform parent, string name, Vector3 position)
        {
            var point = new GameObject(name).transform;
            point.SetParent(parent, false);
            point.localPosition = position;
            return point;
        }
    }
}
