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
}
