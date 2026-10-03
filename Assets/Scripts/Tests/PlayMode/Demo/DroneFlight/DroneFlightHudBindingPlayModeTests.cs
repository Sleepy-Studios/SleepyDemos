using System.Collections;
using Core.Runtime;
using Cysharp.Threading.Tasks;
using Hotfix;
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
     * 测试说明：验证 DroneFlight HUD 的 MvcBind 生成字段能够在真实 Prefab 生命周期中完成初始化和刷新。
     */
    public sealed class DroneFlightHudBindingPlayModeTests
    {
#if UNITY_EDITOR
        private const string HudPrefabPath =
            "Assets/LoadResources/Demos/drone_flight/Prefabs/UI/DroneFlightHudView.prefab";

        [UnityTest]
        public IEnumerator HudView_WhenInitializedFromPrefab_UsesGeneratedControlTextBindings()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
            Assert.That(prefab, Is.Not.Null);
            var instance = Object.Instantiate(prefab);
            var index = instance.GetComponent<ComponentItemIndex>();
            var height = FindBoundText(index, "HeightText");
            var camera = FindBoundText(index, "CameraText");
            var view = new DroneFlightHudView { Loader = new OwnedObjectLoader() };
            view.InitWithGameObject(instance);
            yield return view.ShowAsync(false).ToCoroutine();
            var hud = new Hotfix.DroneFlight.DroneHudSnapshot(
                Hotfix.DroneFlight.DroneFlightOperationState.Flying, Hotfix.DroneFlight.DroneResponseProfile.Normal,
                true, 0, 12.5f, 3, -1, 7, false, Hotfix.DroneFlight.DroneCameraMode.ThirdPerson, 0, 0, 60);
            var snapshot = new Hotfix.DroneFlight.DroneFlightUiSnapshot(hud, default, "", "", default, 0, 5, true);
            var oldSource = new GameObject("OldHudTelemetry").AddComponent<DroneFlightUiTelemetrySource>();
            var newSource = new GameObject("NewHudTelemetry").AddComponent<DroneFlightUiTelemetrySource>();
            typeof(DroneFlightUiTelemetrySource).GetProperty("Current").SetValue(oldSource, snapshot);
            view.SetData(new DroneFlightViewData(oldSource, "old-session"));
            StringAssert.Contains("12.5", ReadText(height));
            StringAssert.Contains("第三人称", ReadText(camera));
            var newHud = new DroneHudSnapshot(DroneFlightOperationState.Flying, DroneResponseProfile.Normal,
                true, 0, 23.4f, 3, -1, 7, false, DroneCameraMode.Orbit, 0, 0, 60);
            var newSnapshot = new DroneFlightUiSnapshot(newHud, default, "", "", default, 0, 5, true);
            typeof(DroneFlightUiTelemetrySource).GetProperty("Current").SetValue(newSource, newSnapshot);
            view.SetData(new DroneFlightViewData(newSource, "new-session"));
            var oldListeners = typeof(DroneFlightUiTelemetrySource).GetField("SnapshotChanged",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(oldSource) as System.Action<DroneFlightUiSnapshot>;
            oldListeners?.Invoke(snapshot);
            StringAssert.Contains("23.4", ReadText(height), "显示中的 HUD 应切换数据源，旧机体不能继续覆盖读数。");
            StringAssert.Contains("环绕", ReadText(camera));
            var fault = new DroneHudSnapshot(DroneFlightOperationState.Fault, DroneResponseProfile.Normal,
                false, 0, 23.4f, 0, 0, 7, false, DroneCameraMode.Orbit, 0, 0, 60);
            typeof(DroneFlightUiTelemetrySource).GetProperty("Current").SetValue(newSource,
                new DroneFlightUiSnapshot(fault, default, "", "飞控故障", default, 0, 5, true));
            view.SetData(new DroneFlightViewData(newSource, "fault-session"));
            var warning = (TMPro.TextMeshProUGUI)FindBoundText(index, "WarningText");
            Assert.That(warning.transform.parent.gameObject.activeInHierarchy, Is.True);
            Assert.That(warning.color.r, Is.GreaterThan(warning.color.g * 2f), "飞控故障使用红色提示。");
            Assert.That(instance.GetComponent<CanvasGroup>().blocksRaycasts, Is.True);
            foreach (var component in index.Components)
                if (component is CanvasGroup group && group.name == "TelemetryRoot")
                    Assert.That(group.blocksRaycasts, Is.False);
            yield return view.DestroyAsync().ToCoroutine();
            Object.Destroy(oldSource.gameObject);
            Object.Destroy(newSource.gameObject);
            yield return null;

            Assert.That(view.State, Is.EqualTo(ViewState.Destroyed));
            Assert.That(instance == null, Is.True);
            // Test Runner 自动拒绝未预期 Error/Exception；插件普通自检日志不属于 HUD 契约。
        }

        private static Component FindBoundText(ComponentItemIndex index, string nodeName)
        {
            Assert.That(index, Is.Not.Null);
            foreach (var component in index.Components)
            {
                if (component != null &&
                    component.GetType().Name == "TextMeshProUGUI" &&
                    component.gameObject.name == nodeName)
                {
                    return component;
                }
            }

            Assert.Fail($"ComponentItemIndex 缺少 {nodeName} 的 TextMeshProUGUI 绑定。");
            return null;
        }

        private static string ReadText(Component component)
        {
            return (string)component.GetType().GetProperty("text")?.GetValue(component);
        }

        private sealed class OwnedObjectLoader : IResourceLoader
        {
            public GameObject Instantiate(string address, Transform parent)
            {
                return null;
            }

            public GameObject Instantiate(string address, Transform parent, bool worldPositionStays)
            {
                return null;
            }

            public UniTask<GameObject> InstantiateAsync(string address, Transform parent)
            {
                return UniTask.FromResult<GameObject>(null);
            }

            public UniTask<GameObject> InstantiateAsync(
                string address,
                Transform parent,
                bool worldPositionStays)
            {
                return UniTask.FromResult<GameObject>(null);
            }

            public T LoadAsset<T>(string address) where T : Object
            {
                return null;
            }

            public UniTask<T> LoadAssetAsync<T>(string address) where T : Object
            {
                return UniTask.FromResult<T>(null);
            }

            public void ReleaseAsset(Object asset)
            {
            }

            public void ReleaseInstance(GameObject instance)
            {
                if (instance != null)
                {
                    Object.Destroy(instance);
                }
            }

            public void Dispose()
            {
            }
        }
#endif
    }
}
