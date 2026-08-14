using System;
using System.Collections.Generic;
using Core.Runtime;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hotfix.SceneManagement
{
    internal readonly struct GameSceneRuntimeResult
    {
        private GameSceneRuntimeResult(bool succeeded, string error)
        {
            Succeeded = succeeded;
            Error = error;
        }

        internal bool Succeeded { get; }
        internal string Error { get; }

        internal static GameSceneRuntimeResult Success() => new GameSceneRuntimeResult(true, null);
        internal static GameSceneRuntimeResult Failure(string error) => new GameSceneRuntimeResult(false, error);
    }

    internal interface IGameSceneRuntime
    {
        UniTask<GameSceneRuntimeResult> LoadAsync(string address, Action<float> onProgress);
        UniTask<GameSceneRuntimeResult> ReturnToHubAsync(Action<float> onProgress);
    }

    internal interface IEditorDirectGameSceneRuntime : IGameSceneRuntime
    {
        UniTask<GameSceneRuntimeResult> ReloadAsync(string address, Action<float> onProgress);
    }

    internal sealed class GameSceneRuntime : IGameSceneRuntime
    {
        private readonly IResourceSceneLoader sceneLoader;
        private readonly Scene hubScene;
        private readonly Camera hubCamera;
        private readonly AudioListener hubAudioListener;
        private IResourceSceneHandle currentHandle;
        private Camera currentCamera;
        private AudioListener currentAudioListener;

        internal GameSceneRuntime(IResourceSceneLoader sceneLoader)
        {
            this.sceneLoader = sceneLoader ?? throw new ArgumentNullException(nameof(sceneLoader));
            hubScene = SceneManager.GetActiveScene();
            hubCamera = UIRootManager.Instance.BaseCamera;
            if (!hubScene.IsValid() || !hubScene.isLoaded)
            {
                throw new InvalidOperationException("初始化场景导航时找不到有效的 Hub 启动场景。");
            }

            if (hubCamera == null || hubCamera.gameObject.scene != hubScene)
            {
                throw new InvalidOperationException("初始化场景导航时找不到 Hub 基础相机。");
            }

            hubAudioListener = hubCamera.GetComponent<AudioListener>();
            if (hubAudioListener == null)
            {
                throw new InvalidOperationException("Hub 基础相机缺少 AudioListener。");
            }
        }

        public async UniTask<GameSceneRuntimeResult> LoadAsync(string address, Action<float> onProgress)
        {
            var sourceScene = currentHandle?.Scene ?? hubScene;
            var sourceCamera = currentCamera != null ? currentCamera : hubCamera;
            var sourceAudioListener = currentAudioListener != null ? currentAudioListener : hubAudioListener;
            sourceAudioListener.enabled = false;

            var loadResult = await sceneLoader.LoadSceneAsync(
                address,
                LoadSceneMode.Additive,
                onProgress);
            if (!loadResult.Succeeded)
            {
                sourceAudioListener.enabled = true;
                return GameSceneRuntimeResult.Failure(loadResult.Error);
            }

            var targetHandle = loadResult.Handle;
            if (!TryFindScenePresentation(
                    targetHandle.Scene,
                    out var targetCamera,
                    out var targetAudioListener,
                    out var presentationError))
            {
                await sceneLoader.UnloadSceneAsync(targetHandle);
                sourceAudioListener.enabled = true;
                return GameSceneRuntimeResult.Failure(presentationError);
            }

            var previousHandle = currentHandle;
            try
            {
                UIRootManager.Instance.BindToBaseCamera(targetCamera);
                if (!SceneManager.SetActiveScene(targetHandle.Scene))
                {
                    throw new InvalidOperationException($"无法激活场景: {targetHandle.Scene.path}");
                }

                targetCamera.enabled = true;
                targetAudioListener.enabled = true;
                sourceCamera.enabled = false;

                if (previousHandle != null)
                {
                    var unloadResult = await sceneLoader.UnloadSceneAsync(previousHandle);
                    if (!unloadResult.Succeeded)
                    {
                        throw new InvalidOperationException(unloadResult.Error);
                    }
                }

                currentHandle = targetHandle;
                currentCamera = targetCamera;
                currentAudioListener = targetAudioListener;
                return GameSceneRuntimeResult.Success();
            }
            catch (Exception exception)
            {
                targetAudioListener.enabled = false;
                targetCamera.enabled = false;
                UIRootManager.Instance.BindToBaseCamera(sourceCamera);
                SceneManager.SetActiveScene(sourceScene);
                sourceCamera.enabled = true;
                sourceAudioListener.enabled = true;
                await sceneLoader.UnloadSceneAsync(targetHandle);
                return GameSceneRuntimeResult.Failure($"场景表现切换失败: {exception.Message}");
            }
        }

        public async UniTask<GameSceneRuntimeResult> ReturnToHubAsync(Action<float> onProgress)
        {
            if (currentHandle == null)
            {
                onProgress?.Invoke(1f);
                return GameSceneRuntimeResult.Success();
            }

            var contentHandle = currentHandle;
            var contentCamera = currentCamera;
            var contentAudioListener = currentAudioListener;
            try
            {
                contentAudioListener.enabled = false;
                UIRootManager.Instance.BindToBaseCamera(hubCamera);
                if (!SceneManager.SetActiveScene(hubScene))
                {
                    throw new InvalidOperationException("无法重新激活 Hub 启动场景。");
                }

                hubCamera.enabled = true;
                hubAudioListener.enabled = true;
                contentCamera.enabled = false;

                var unloadResult = await sceneLoader.UnloadSceneAsync(contentHandle, onProgress);
                if (!unloadResult.Succeeded)
                {
                    throw new InvalidOperationException(unloadResult.Error);
                }

                currentHandle = null;
                currentCamera = null;
                currentAudioListener = null;
                return GameSceneRuntimeResult.Success();
            }
            catch (Exception exception)
            {
                hubAudioListener.enabled = false;
                hubCamera.enabled = false;
                UIRootManager.Instance.BindToBaseCamera(contentCamera);
                SceneManager.SetActiveScene(contentHandle.Scene);
                contentCamera.enabled = true;
                contentAudioListener.enabled = true;
                return GameSceneRuntimeResult.Failure($"返回 Hub 失败: {exception.Message}");
            }
        }

        private static bool TryFindScenePresentation(
            Scene scene,
            out Camera camera,
            out AudioListener audioListener,
            out string error)
        {
            camera = null;
            audioListener = null;
            error = null;
            if (!scene.IsValid() || !scene.isLoaded)
            {
                error = "目标场景无效或尚未加载完成。";
                return false;
            }

            var cameras = new List<Camera>();
            var listeners = new List<AudioListener>();
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var candidate in root.GetComponentsInChildren<Camera>(true))
                {
                    if (candidate.CompareTag("MainCamera"))
                    {
                        cameras.Add(candidate);
                    }
                }

                listeners.AddRange(root.GetComponentsInChildren<AudioListener>(true));
            }

            if (cameras.Count != 1)
            {
                error = $"目标场景必须且只能包含一个 MainCamera，当前数量: {cameras.Count}。";
                return false;
            }

            if (listeners.Count != 1)
            {
                error = $"目标场景必须且只能包含一个 AudioListener，当前数量: {listeners.Count}。";
                return false;
            }

            camera = cameras[0];
            audioListener = listeners[0];
            return true;
        }
    }

#if UNITY_EDITOR
    /// <summary>仅供 Editor 直接运行 Demo 场景的宿主；所有原生 SceneManager 操作仍封装在场景运行时内。</summary>
    internal sealed class EditorDirectGameSceneRuntime : IEditorDirectGameSceneRuntime
    {
        private readonly IResourceSceneLoader sceneLoader;
        private Scene currentScene;
        private Camera currentCamera;
        private AudioListener currentAudioListener;
        private IResourceSceneHandle currentHandle;

        internal EditorDirectGameSceneRuntime(IResourceSceneLoader loader, Scene scene)
        {
            sceneLoader = loader ?? throw new ArgumentNullException(nameof(loader));
            currentScene = scene;
            if (!TryFindPresentation(scene, out currentCamera, out currentAudioListener, out var error))
            {
                throw new InvalidOperationException(error);
            }

            UIRootManager.Instance.BindToBaseCamera(currentCamera);
        }

        public UniTask<GameSceneRuntimeResult> LoadAsync(string address, Action<float> onProgress)
        {
            return ReloadAsync(address, onProgress);
        }

        public async UniTask<GameSceneRuntimeResult> ReloadAsync(string address, Action<float> onProgress)
        {
            var sourceScene = currentScene;
            var sourceCamera = currentCamera;
            var sourceListener = currentAudioListener;
            var sourceHandle = currentHandle;
            sourceListener.enabled = false;
            var result = await sceneLoader.LoadSceneAsync(address, LoadSceneMode.Additive, onProgress);
            if (!result.Succeeded)
            {
                sourceListener.enabled = true;
                return GameSceneRuntimeResult.Failure(result.Error);
            }

            if (!TryFindPresentation(result.Handle.Scene, out var targetCamera, out var targetListener, out var error))
            {
                await sceneLoader.UnloadSceneAsync(result.Handle);
                sourceListener.enabled = true;
                return GameSceneRuntimeResult.Failure(error);
            }

            try
            {
                UIRootManager.Instance.BindToBaseCamera(targetCamera);
                SceneManager.SetActiveScene(result.Handle.Scene);
                targetCamera.enabled = true;
                targetListener.enabled = true;
                sourceCamera.enabled = false;
                if (sourceHandle != null)
                {
                    var unload = await sceneLoader.UnloadSceneAsync(sourceHandle);
                    if (!unload.Succeeded)
                    {
                        throw new InvalidOperationException(unload.Error);
                    }
                }
                else
                {
                    await SceneManager.UnloadSceneAsync(sourceScene);
                }

                currentScene = result.Handle.Scene;
                currentHandle = result.Handle;
                currentCamera = targetCamera;
                currentAudioListener = targetListener;
                return GameSceneRuntimeResult.Success();
            }
            catch (Exception exception)
            {
                await sceneLoader.UnloadSceneAsync(result.Handle);
                sourceCamera.enabled = true;
                sourceListener.enabled = true;
                return GameSceneRuntimeResult.Failure($"Editor 直启场景重载失败: {exception.Message}");
            }
        }

        public async UniTask<GameSceneRuntimeResult> ReturnToHubAsync(Action<float> onProgress)
        {
            try
            {
                GameSceneNavigator.ReleaseEditorDirect();
                DemoIslandEditorBootstrap.ReleaseForOfficialStartup();
                var operation = SceneManager.LoadSceneAsync("Assets/Scenes/AppEntrance.unity", LoadSceneMode.Single);
                while (operation is { isDone: false })
                {
                    onProgress?.Invoke(operation.progress);
                    await UniTask.Yield();
                }

                onProgress?.Invoke(1f);
                return GameSceneRuntimeResult.Success();
            }
            catch (Exception exception)
            {
                return GameSceneRuntimeResult.Failure($"Editor 直启返回 AppEntrance 失败: {exception.Message}");
            }
        }

        private static bool TryFindPresentation(
            Scene scene,
            out Camera camera,
            out AudioListener listener,
            out string error)
        {
            camera = null;
            listener = null;
            error = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var candidate in root.GetComponentsInChildren<Camera>(true))
                {
                    if (candidate.CompareTag("MainCamera"))
                    {
                        if (camera != null)
                        {
                            error = "Editor 直启场景必须且只能包含一个 MainCamera。";
                            return false;
                        }

                        camera = candidate;
                    }
                }

                foreach (var candidate in root.GetComponentsInChildren<AudioListener>(true))
                {
                    if (listener != null)
                    {
                        error = "Editor 直启场景必须且只能包含一个 AudioListener。";
                        return false;
                    }

                    listener = candidate;
                }
            }

            if (camera == null || listener == null)
            {
                error = "Editor 直启场景缺少 MainCamera 或 AudioListener。";
                return false;
            }

            return true;
        }
    }
#endif
}
