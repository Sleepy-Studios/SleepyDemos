using System.Collections;
using Core.Runtime;
using Cysharp.Threading.Tasks;
using Hotfix;
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
            var header = FindBoundText(index, "ControlsHeaderText");
            var flight = FindBoundText(index, "FlightControlsText");
            var camera = FindBoundText(index, "CameraControlsText");
            var system = FindBoundText(index, "SystemControlsText");
            var view = new DroneFlightHudView { Loader = new OwnedObjectLoader() };

            view.InitWithGameObject(instance);
            yield return view.Show(false).ToCoroutine();

            Assert.That(ReadText(header), Is.EqualTo("操作提示  ·  F1 收起"));
            StringAssert.StartsWith("<b>飞行与档位</b>", ReadText(flight));
            StringAssert.StartsWith("<b>视角与机构</b>", ReadText(camera));
            StringAssert.StartsWith("<b>系统</b>", ReadText(system));

            view.ToggleControls();
            Assert.That(ReadText(header), Is.EqualTo("操作提示  ·  F1 展开"));
            Assert.That(flight.transform.parent.gameObject.activeSelf, Is.False);

            yield return view.DestroyAsync().ToCoroutine();
            yield return null;

            Assert.That(view.State, Is.EqualTo(ViewState.Destroyed));
            Assert.That(instance == null, Is.True);
            LogAssert.NoUnexpectedReceived();
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
