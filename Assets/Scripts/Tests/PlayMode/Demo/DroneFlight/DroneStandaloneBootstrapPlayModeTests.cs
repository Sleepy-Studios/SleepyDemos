using System.Collections;
using Hotfix.DroneFlight;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Tests.Demo
{
    /*
     * 测试说明：验证不依赖 SleepyDemos UI、资源和导航服务的独立手动启动闭环。
     */
    public sealed class DroneStandaloneBootstrapPlayModeTests
    {
#if UNITY_EDITOR
        [UnityTest]
        public IEnumerator ManualMode_InstantiatesAndActivatesFormalDrone()
        {
            const string prefabPath =
                "Assets/LoadResources/Demos/drone_flight/Prefabs/DronePrototype.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefab, Is.Not.Null);

            var fixture = new GameObject("StandaloneBootstrapFixture");
            var spawn = new GameObject("SpawnPoint").transform;
            spawn.SetParent(fixture.transform, false);
            var cameraObject = new GameObject("WaitingCamera", typeof(Camera), typeof(AudioListener));
            cameraObject.transform.SetParent(fixture.transform, false);
            var bootstrap = fixture.AddComponent<DroneFlightStandaloneBootstrap>();
            bootstrap.Configure(prefab, spawn, cameraObject.GetComponent<Camera>());

            yield return null;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.That(bootstrap.Runtime.Root, Is.Not.Null);
            Assert.That(bootstrap.Runtime.Root.activeInHierarchy, Is.True);
            Assert.That(bootstrap.Runtime.ControlSession.IsActive, Is.True);
            Assert.That(bootstrap.Runtime.Controller.IsArmed, Is.False);

            Object.Destroy(fixture);
            yield return null;
        }
#endif
    }
}
