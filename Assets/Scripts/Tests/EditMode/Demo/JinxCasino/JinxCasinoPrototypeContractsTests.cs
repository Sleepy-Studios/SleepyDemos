using Core.Runtime;
using Core.Runtime.Networking;
using Hotfix.JinxCasino.Adapters;
using Hotfix.JinxCasino.Adapters.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Tests.Demo
{
    public sealed class JinxCasinoPrototypeContractsTests
    {
        private const string Root = "Assets/LoadResources/Demos/jinx_casino";

        [Test]
        public void SavedSceneHasOneLocalCameraAndListenerAndConfiguredSession()
        {
            var previous = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var scene = EditorSceneManager.OpenScene(Root + "/Scenes/Main.unity", OpenSceneMode.Additive);
            try
            {
                int mainCameras = 0, listeners = 0, controllers = 0;
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var camera in root.GetComponentsInChildren<Camera>(true))
                    {
                        if (!camera.CompareTag("MainCamera")) continue;
                        mainCameras++;
                        Assert.That(camera.GetComponentInParent<CharacterController>(), Is.Not.Null,
                            "本地相机必须跟随预先装配的玩家控制器。");
                    }
                    listeners += root.GetComponentsInChildren<AudioListener>(true).Length;
                    controllers += root.GetComponentsInChildren<JinxCasinoController>(true).Length;
                }
                Assert.That(mainCameras, Is.EqualTo(1));
                Assert.That(listeners, Is.EqualTo(1));
                Assert.That(controllers, Is.EqualTo(1));
                Assert.That(AssetDatabase.LoadAssetAtPath<NetworkSessionSettings>(Root + "/Data/SessionSettings.asset"), Is.Not.Null);
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void SavedHudUsesCoreCanvasAndStableComponentIndex()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/UI/JinxCasinoHudView.prefab");
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponent<Canvas>(), Is.Null);
            Assert.That(prefab.GetComponent<CanvasScaler>(), Is.Null);
            Assert.That(prefab.GetComponent<GraphicRaycaster>(), Is.Null);
            var index = prefab.GetComponent<ComponentItemIndex>();
            Assert.That(index, Is.Not.Null);
            Assert.That(index.Get<JinxCasinoHudPresenter>(0), Is.SameAs(prefab.GetComponent<JinxCasinoHudPresenter>()));
            Assert.That(prefab.GetComponentsInChildren<JinxCasinoTouchPad>(true).Length, Is.EqualTo(2),
                "移动与视角必须有两个独立触控指针区域。");
        }
    }
}
