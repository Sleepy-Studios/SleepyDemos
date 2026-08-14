using System;
using System.Threading;
using Core.Runtime;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Hotfix.SceneManagement
{
    /// 运行期全局业务场景导航入口。
    public sealed class GameSceneNavigator
    {
        private readonly IGameSceneRuntime runtime;
        private readonly IGameSceneLoadingPresenter loadingPresenter;
        private bool isTransitioning;
        private bool isEditorDirect;

        internal GameSceneNavigator(
            IGameSceneRuntime runtime,
            IGameSceneLoadingPresenter loadingPresenter)
        {
            this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            this.loadingPresenter = loadingPresenter ?? throw new ArgumentNullException(nameof(loadingPresenter));
        }

        /// 已初始化的全局导航实例。
        public static GameSceneNavigator Instance { get; private set; }

        /// 当前稳定业务场景。
        public GameSceneId CurrentScene { get; private set; } = GameSceneId.Hub;

        /// 当前是否正在切换场景。
        public bool IsTransitioning => isTransitioning;

        /// 当前是否由 Editor Demo 直启宿主管理。
        public bool IsEditorDirect => isEditorDirect;

        /// <summary>
        /// 等待指定业务场景完成加载、激活与 Loading UI 收尾。
        /// </summary>
        /// <param name="target">预期稳定的业务场景。</param>
        /// <param name="cancellationToken">等待取消令牌。</param>
        public async UniTask WaitUntilStableAsync(
            GameSceneId target,
            CancellationToken cancellationToken = default)
        {
            await UniTask.WaitUntil(
                () => CurrentScene == target && !isTransitioning,
                cancellationToken: cancellationToken);
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, cancellationToken);
        }

        /// 使用当前资源服务和 UI 框架初始化全局场景导航。
        public static void Initialize()
        {
            Instance ??= new GameSceneNavigator(
                new GameSceneRuntime(ResourceServices.CreateSceneLoader()),
                new GameSceneLoadingPresenter());
        }

#if UNITY_EDITOR
        internal static void InitializeEditorDirect(IEditorDirectGameSceneRuntime runtime)
        {
            if (Instance != null)
            {
                return;
            }

            Instance = new GameSceneNavigator(runtime, new GameSceneLoadingPresenter())
            {
                CurrentScene = GameSceneId.DroneFlight,
                isEditorDirect = true
            };
        }

        internal static void ReleaseEditorDirect()
        {
            if (Instance != null && Instance.isEditorDirect)
            {
                Instance = null;
            }
        }
#endif

        /// <summary>
        /// 切换到指定业务场景。
        /// </summary>
        /// <param name="target">全局业务场景标识。</param>
        /// <returns>成功、已在目标、忙碌或失败结果。</returns>
        public async UniTask<GameSceneSwitchResult> SwitchAsync(GameSceneId target)
        {
            if (!GameSceneCatalog.TryGet(target, out var definition))
            {
                return GameSceneSwitchResult.Failed(target, $"未登记的业务场景: {target}");
            }

            if (isTransitioning)
            {
                return GameSceneSwitchResult.Busy(target);
            }

            if (CurrentScene == target)
            {
                return GameSceneSwitchResult.Ignored(target);
            }

            var source = CurrentScene;
            isTransitioning = true;
            try
            {
                var uiError = await loadingPresenter.BeginAsync(definition);
                if (!string.IsNullOrEmpty(uiError))
                {
                    return GameSceneSwitchResult.Failed(target, uiError);
                }

                loadingPresenter.SetProgress(0.1f, "加载场景", $"正在加载{definition.DisplayName}");
                var displayedProgress = 0.1f;
                void ReportRuntimeProgress(float progress, string step, string description)
                {
                    displayedProgress = Mathf.Max(
                        displayedProgress,
                        Mathf.Lerp(0.1f, 0.9f, Mathf.Clamp01(progress)));
                    loadingPresenter.SetProgress(displayedProgress, step, description);
                }

                var runtimeResult = definition.IsHub
                    ? await runtime.ReturnToHubAsync(progress =>
                        ReportRuntimeProgress(
                            progress,
                            "卸载场景",
                            "正在返回主界面"))
                    : await runtime.LoadAsync(definition.Address, progress =>
                        ReportRuntimeProgress(
                            progress,
                            "加载场景",
                            $"正在加载{definition.DisplayName}"));
                if (!runtimeResult.Succeeded)
                {
                    await loadingPresenter.RestoreAsync(source);
                    return GameSceneSwitchResult.Failed(target, runtimeResult.Error);
                }

                CurrentScene = target;
                loadingPresenter.SetProgress(0.95f, "切换场景", "正在完成场景初始化");
                uiError = await loadingPresenter.CompleteAsync(target);
                if (!string.IsNullOrEmpty(uiError))
                {
                    return GameSceneSwitchResult.Failed(target, uiError);
                }

                return GameSceneSwitchResult.Succeeded(target);
            }
            catch (Exception exception)
            {
                await loadingPresenter.RestoreAsync(source);
                Debug.LogException(exception);
                return GameSceneSwitchResult.Failed(target, exception.Message);
            }
            finally
            {
                isTransitioning = false;
            }
        }

        /// <summary>
        /// 卸载并重新加载当前 Demo 场景，Hub 启动壳和全局服务保持常驻。
        /// </summary>
        /// <returns>成功、忽略、忙碌或失败结果。</returns>
        public async UniTask<GameSceneSwitchResult> ReloadCurrentAsync()
        {
            var target = CurrentScene;
            if (target == GameSceneId.Hub)
            {
                return GameSceneSwitchResult.Ignored(target);
            }

            if (!GameSceneCatalog.TryGet(target, out var definition))
            {
                return GameSceneSwitchResult.Failed(target, $"未登记的业务场景: {target}");
            }

            if (isTransitioning)
            {
                return GameSceneSwitchResult.Busy(target);
            }

            isTransitioning = true;
            try
            {
                var uiError = await loadingPresenter.BeginAsync(definition);
                if (!string.IsNullOrEmpty(uiError))
                {
                    return GameSceneSwitchResult.Failed(target, uiError);
                }

                var displayedProgress = 0.1f;
                void ReportProgress(float progress, float from, float to, string step, string description)
                {
                    displayedProgress = Mathf.Max(
                        displayedProgress,
                        Mathf.Lerp(from, to, Mathf.Clamp01(progress)));
                    loadingPresenter.SetProgress(displayedProgress, step, description);
                }

                loadingPresenter.SetProgress(0.1f, "重新运行场景", $"正在卸载{definition.DisplayName}");
                if (runtime is IEditorDirectGameSceneRuntime editorDirectRuntime)
                {
                    var directResult = await editorDirectRuntime.ReloadAsync(definition.Address, progress =>
                        ReportProgress(progress, 0.1f, 0.9f, "重新运行场景", $"正在重新加载{definition.DisplayName}"));
                    if (!directResult.Succeeded)
                    {
                        await loadingPresenter.RestoreAsync(target);
                        return GameSceneSwitchResult.Failed(target, directResult.Error);
                    }
                }
                else
                {
                    var unloadResult = await runtime.ReturnToHubAsync(progress =>
                        ReportProgress(progress, 0.1f, 0.45f, "重新运行场景", $"正在卸载{definition.DisplayName}"));
                    if (!unloadResult.Succeeded)
                    {
                        await loadingPresenter.RestoreAsync(target);
                        return GameSceneSwitchResult.Failed(target, unloadResult.Error);
                    }

                    CurrentScene = GameSceneId.Hub;
                    var loadResult = await runtime.LoadAsync(definition.Address, progress =>
                        ReportProgress(progress, 0.45f, 0.9f, "重新运行场景", $"正在重新加载{definition.DisplayName}"));
                    if (!loadResult.Succeeded)
                    {
                        await loadingPresenter.RestoreAsync(GameSceneId.Hub);
                        return GameSceneSwitchResult.Failed(target, loadResult.Error);
                    }
                }

                CurrentScene = target;
                loadingPresenter.SetProgress(0.95f, "重新运行场景", "正在完成场景初始化");
                uiError = await loadingPresenter.CompleteAsync(target);
                return string.IsNullOrEmpty(uiError)
                    ? GameSceneSwitchResult.Succeeded(target)
                    : GameSceneSwitchResult.Failed(target, uiError);
            }
            catch (Exception exception)
            {
                await loadingPresenter.RestoreAsync(CurrentScene);
                Debug.LogException(exception);
                return GameSceneSwitchResult.Failed(target, exception.Message);
            }
            finally
            {
                isTransitioning = false;
            }
        }
    }
}
