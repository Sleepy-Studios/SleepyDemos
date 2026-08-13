using System;
using System.Collections;
using System.Linq;
using Core.Runtime;
using Cysharp.Threading.Tasks;
using Hotfix.SceneManagement;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Hotfix.Tests
{
    public sealed class GameSceneRuntimePlayModeTests
    {
        private Scene originalScene;
        private Scene hubScene;
        private Camera hubCamera;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            originalScene = SceneManager.GetActiveScene();
            hubScene = SceneManager.CreateScene("GameSceneRuntimeHubTest");
            SceneManager.SetActiveScene(hubScene);
            hubCamera = CreatePresentationCamera(hubScene, "Hub Camera");
            yield return UIRootManager.Instance.BuildUIRoot().ToCoroutine();
            UIRootManager.Instance.BindToBaseCamera(hubCamera);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (originalScene.IsValid() && originalScene.isLoaded)
            {
                SceneManager.SetActiveScene(originalScene);
            }

            var manager = UIRootManager.Instance;
            if (manager.Root != null)
            {
                UnityEngine.Object.Destroy(manager.Root.gameObject);
            }

            if (manager.UICamera != null)
            {
                UnityEngine.Object.Destroy(manager.UICamera.gameObject);
            }

            if (hubScene.IsValid() && hubScene.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(hubScene);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator LoadAndReturnHub_SwitchesActiveSceneCameraAndAudioListener()
        {
            var loader = new FakeSceneLoader();
            var runtime = new GameSceneRuntime(loader);
            GameSceneRuntimeResult loadResult = default;

            yield return runtime.LoadAsync("TestContent", null)
                .ContinueWith(result => loadResult = result)
                .ToCoroutine();

            Assert.That(loadResult.Succeeded, Is.True, loadResult.Error);
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(loader.ContentScene));
            Assert.That(UIRootManager.Instance.BaseCamera, Is.SameAs(loader.ContentCamera));
            Assert.That(hubCamera.enabled, Is.False);
            Assert.That(loader.ContentCamera.enabled, Is.True);
            Assert.That(CountEnabledAudioListeners(), Is.EqualTo(1));

            GameSceneRuntimeResult returnResult = default;
            yield return runtime.ReturnToHubAsync(null)
                .ContinueWith(result => returnResult = result)
                .ToCoroutine();

            Assert.That(returnResult.Succeeded, Is.True, returnResult.Error);
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(hubScene));
            Assert.That(UIRootManager.Instance.BaseCamera, Is.SameAs(hubCamera));
            Assert.That(hubCamera.enabled, Is.True);
            Assert.That(loader.ContentScene.isLoaded, Is.False);
            Assert.That(CountEnabledAudioListeners(), Is.EqualTo(1));
        }

        private static Camera CreatePresentationCamera(Scene scene, string name)
        {
            var gameObject = new GameObject(name);
            gameObject.tag = "MainCamera";
            SceneManager.MoveGameObjectToScene(gameObject, scene);
            var camera = gameObject.AddComponent<Camera>();
            gameObject.AddComponent<AudioListener>();
            return camera;
        }

        private static int CountEnabledAudioListeners()
        {
            return UnityEngine.Object.FindObjectsByType<AudioListener>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .Count(listener => listener.enabled);
        }

        private sealed class FakeSceneHandle : IResourceSceneHandle
        {
            internal FakeSceneHandle(string address, Scene scene)
            {
                Address = address;
                Scene = scene;
            }

            public string Address { get; }
            public Scene Scene { get; }
        }

        private sealed class FakeSceneLoader : IResourceSceneLoader
        {
            internal Scene ContentScene { get; private set; }
            internal Camera ContentCamera { get; private set; }

            public UniTask<ResourceSceneLoadResult> LoadSceneAsync(
                string address,
                LoadSceneMode loadMode = LoadSceneMode.Additive,
                Action<float> onProgress = null)
            {
                onProgress?.Invoke(0f);
                ContentScene = SceneManager.CreateScene("GameSceneRuntimeContentTest");
                ContentCamera = CreatePresentationCamera(ContentScene, "Content Camera");
                onProgress?.Invoke(1f);
                return UniTask.FromResult(
                    ResourceSceneLoadResult.Success(new FakeSceneHandle(address, ContentScene)));
            }

            public async UniTask<ResourceSceneUnloadResult> UnloadSceneAsync(
                IResourceSceneHandle handle,
                Action<float> onProgress = null)
            {
                onProgress?.Invoke(0f);
                var operation = SceneManager.UnloadSceneAsync(handle.Scene);
                if (operation == null)
                {
                    return ResourceSceneUnloadResult.Failure("测试场景卸载操作为空。");
                }

                await operation;
                onProgress?.Invoke(1f);
                return ResourceSceneUnloadResult.Success();
            }
        }
    }
}
