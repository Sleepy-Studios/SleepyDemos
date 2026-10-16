using System;
using System.Threading;
using Core.Runtime;
using Core.Runtime.Localization;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hotfix.SceneManagement
{
    /// 运行期全局业务场景导航入口。
    public sealed class GameSceneNavigator
    {
        private readonly IGameSceneRuntime runtime;
        private readonly IGameSceneLoadingPresenter loadingPresenter;
        private readonly Func<UniTask> prepareScene;
        private readonly Func<GameSceneDefinition, UniTask<GameSceneLanguageScope>> loadContext;
        private GameSceneLanguageScope currentContext;
        private bool isTransitioning;
        private bool isEditorDirect;

        internal GameSceneNavigator(
            IGameSceneRuntime runtime,
            IGameSceneLoadingPresenter loadingPresenter,
            Func<UniTask> prepareScene = null,
            GameSceneId? standaloneScene = null,
            Func<GameSceneDefinition, UniTask<GameSceneLanguageScope>> loadContext = null)
        {
            this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            this.loadingPresenter = loadingPresenter ?? throw new ArgumentNullException(nameof(loadingPresenter));
            this.prepareScene = prepareScene ?? PrepareActiveSceneAsync;
            this.loadContext = loadContext ?? (definition => GameSceneLanguageScope.LoadAsync(definition));
            StandaloneScene = standaloneScene;
        }

        /// 已初始化的全局导航实例。
        public static GameSceneNavigator Instance { get; private set; }

        /// 当前稳定业务场景。
        public GameSceneId CurrentScene { get; private set; } = GameSceneId.Hub;

        /// 当前是否正在切换场景。
        public bool IsTransitioning => isTransitioning;

        /// 独立包的唯一业务入口；返回 Hub 请求重载本游戏，null 保持正常 Hub 导航。
        public GameSceneId? StandaloneScene { get; private set; }

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

        /// <summary>使用当前资源服务和UI框架初始化导航；已存在实例保持其启动方式。</summary>
        /// <param name="standaloneScene">独立包的已登记Demo入口；null默认进入Hub，不修改Editor配置。</param>
        public static void Initialize(GameSceneId? standaloneScene = null)
        {
            if (standaloneScene.HasValue && (standaloneScene == GameSceneId.Hub || !GameSceneCatalog.TryGet(standaloneScene.Value, out _)))
                throw new System.ArgumentException("独立入口必须是已登记的Demo。", nameof(standaloneScene));
            Instance ??= new GameSceneNavigator(
                new GameSceneRuntime(ResourceServices.CreateSceneLoader()),
                new GameSceneLoadingPresenter(), standaloneScene: standaloneScene);
        }

#if UNITY_EDITOR
        internal static void InitializeEditorDirect(IEditorDirectGameSceneRuntime runtime, GameSceneId sceneId, GameSceneLanguageScope context = null)
        {
            if (Instance != null)
            {
                context?.Dispose();
                return;
            }

            Instance = new GameSceneNavigator(runtime, new GameSceneLoadingPresenter())
            {
                CurrentScene = sceneId,
                currentContext = context,
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

            if (StandaloneScene.HasValue)
            {
                if (target == GameSceneId.Hub)
                {
                    return await ReloadCurrentAsync();
                }
                if (target != StandaloneScene.Value)
                {
                    return GameSceneSwitchResult.Failed(target, "独立游戏包未包含该场景：" + target);
                }
            }

            if (CurrentScene == target)
            {
                return GameSceneSwitchResult.Ignored(target);
            }

            isTransitioning = true;
            bool preparingCommittedScene = false;
            GameSceneLanguageScope targetContext = null;
            try
            {
                var uiError = await loadingPresenter.BeginAsync(definition);
                if (!string.IsNullOrEmpty(uiError))
                {
                    return await RestoreFailedAsync(target, uiError);
                }

                if (!definition.IsHub)
                {
                    // 必须在资源场景加载前注册，覆盖目标 Awake/OnEnable 的读取。
                    targetContext = await loadContext(definition);
                }

                string name = LanguageService.IsInitialized ? LanguageService.Get(definition.DisplayNameKey) : definition.DisplayName;
                loadingPresenter.SetProgress(0.1f, "加载场景", $"正在加载{definition.DisplayName}", "Loading.LoadScene", "Loading.LoadingScene", name);
                var displayedProgress = 0.1f;
                void ReportRuntimeProgress(float progress, string step, string description, string stepKey, string descriptionKey, params object[] args)
                {
                    displayedProgress = Mathf.Max(
                        displayedProgress,
                        Mathf.Lerp(0.1f, 0.9f, Mathf.Clamp01(progress)));
                    loadingPresenter.SetProgress(displayedProgress, step, description, stepKey, descriptionKey, args);
                }

                var runtimeResult = definition.IsHub
                    ? await runtime.ReturnToHubAsync(progress =>
                        ReportRuntimeProgress(
                            progress,
                            "卸载场景",
                            "正在返回主界面", "Loading.UnloadScene", "Loading.ReturnHub"))
                    : await runtime.LoadAsync(definition.Address, progress =>
                        ReportRuntimeProgress(
                            progress,
                            "加载场景",
                            $"正在加载{definition.DisplayName}", "Loading.LoadScene", "Loading.LoadingScene", name));
                if (!runtimeResult.Succeeded)
                {
                    return await RestoreFailedAsync(target, runtimeResult.Error);
                }

                var previousContext = currentContext;
                currentContext = targetContext;
                targetContext = null;
                currentContext?.BindScene(SceneManager.GetActiveScene());
                previousContext?.Dispose();
                CurrentScene = target;
                if (!definition.IsHub)
                {
                    preparingCommittedScene = true;
                    await prepareScene();
                    preparingCommittedScene = false;
                }
                loadingPresenter.SetProgress(0.99f, "切换场景", "正在完成场景初始化", "Loading.SwitchScene", "Loading.Initialize");
                uiError = await loadingPresenter.CompleteAsync(target);
                if (!string.IsNullOrEmpty(uiError))
                {
                    return await RestoreFailedAsync(target, uiError);
                }

                return GameSceneSwitchResult.Succeeded(target);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return preparingCommittedScene
                    ? await RecoverPreparationFailedAsync(target, exception.Message)
                    : await RestoreFailedAsync(target, exception.Message);
            }
            finally
            {
                try
                {
                    targetContext?.Dispose();
                }
                finally
                {
                    isTransitioning = false;
                }
            }
        }

        /// <summary>
        /// 卸载并重新加载当前 Demo 场景，Hub 启动壳和全局服务保持常驻。
        /// </summary>
        /// <returns>成功、忽略、忙碌或失败结果。</returns>
        public async UniTask<GameSceneSwitchResult> ReloadCurrentAsync()
        {
            var target = CurrentScene;
            if (isTransitioning)
            {
                return GameSceneSwitchResult.Busy(target);
            }

            if (target == GameSceneId.Hub)
            {
                return GameSceneSwitchResult.Ignored(target);
            }

            if (!GameSceneCatalog.TryGet(target, out var definition))
            {
                return GameSceneSwitchResult.Failed(target, $"未登记的业务场景: {target}");
            }

            isTransitioning = true;
            bool preparingCommittedScene = false;
            GameSceneLanguageScope targetContext = null;
            try
            {
                var uiError = await loadingPresenter.BeginAsync(definition);
                if (!string.IsNullOrEmpty(uiError))
                {
                    return await RestoreFailedAsync(target, uiError);
                }

                var displayedProgress = 0.1f;
                string name = LanguageService.IsInitialized ? LanguageService.Get(definition.DisplayNameKey) : definition.DisplayName;
                void ReportProgress(float progress, float from, float to, string step, string description, string stepKey, string descriptionKey, params object[] args)
                {
                    displayedProgress = Mathf.Max(
                        displayedProgress,
                        Mathf.Lerp(from, to, Mathf.Clamp01(progress)));
                    loadingPresenter.SetProgress(displayedProgress, step, description, stepKey, descriptionKey, args);
                }

                targetContext = await loadContext(definition);
                loadingPresenter.SetProgress(0.1f, "重新运行场景", $"正在卸载{definition.DisplayName}", "Loading.ReloadScene", "Loading.UnloadingScene", name);
                if (runtime is IEditorDirectGameSceneRuntime editorDirectRuntime)
                {
                    ReportProgress(0, 0.1f, 0.9f, "重新运行场景", $"正在重新加载{definition.DisplayName}", "Loading.ReloadScene", "Loading.ReloadingScene", name);
                    var directResult = await editorDirectRuntime.ReloadAsync(definition.Address, progress =>
                        ReportProgress(progress, 0.1f, 0.9f, "重新运行场景", $"正在重新加载{definition.DisplayName}", "Loading.ReloadScene", "Loading.ReloadingScene", name));
                    if (!directResult.Succeeded)
                    {
                        return await RestoreFailedAsync(target, directResult.Error);
                    }
                }
                else
                {
                    var unloadResult = await runtime.ReturnToHubAsync(progress =>
                        ReportProgress(progress, 0.1f, 0.45f, "重新运行场景", $"正在卸载{definition.DisplayName}", "Loading.ReloadScene", "Loading.UnloadingScene", name));
                    if (!unloadResult.Succeeded)
                    {
                        return await RestoreFailedAsync(target, unloadResult.Error);
                    }

                    currentContext?.Dispose();
                    currentContext = null;
                    CurrentScene = GameSceneId.Hub;
                    ReportProgress(0, 0.45f, 0.9f, "重新运行场景", $"正在重新加载{definition.DisplayName}", "Loading.ReloadScene", "Loading.ReloadingScene", name);
                    var loadResult = await runtime.LoadAsync(definition.Address, progress =>
                        ReportProgress(progress, 0.45f, 0.9f, "重新运行场景", $"正在重新加载{definition.DisplayName}", "Loading.ReloadScene", "Loading.ReloadingScene", name));
                    if (!loadResult.Succeeded)
                    {
                        return await RestoreFailedAsync(target, loadResult.Error);
                    }
                }

                var previousContext = currentContext;
                currentContext = targetContext;
                targetContext = null;
                currentContext?.BindScene(SceneManager.GetActiveScene());
                previousContext?.Dispose();
                CurrentScene = target;
                preparingCommittedScene = true;
                await prepareScene();
                preparingCommittedScene = false;
                loadingPresenter.SetProgress(0.99f, "重新运行场景", "正在完成场景初始化", "Loading.ReloadScene", "Loading.Initialize");
                uiError = await loadingPresenter.CompleteAsync(target);
                return string.IsNullOrEmpty(uiError)
                    ? GameSceneSwitchResult.Succeeded(target)
                    : await RestoreFailedAsync(target, uiError);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return preparingCommittedScene
                    ? await RecoverPreparationFailedAsync(target, exception.Message)
                    : await RestoreFailedAsync(target, exception.Message);
            }
            finally
            {
                try
                {
                    targetContext?.Dispose();
                }
                finally
                {
                    isTransitioning = false;
                }
            }
        }

        private async UniTask PrepareActiveSceneAsync()
        {
            if (CurrentScene == GameSceneId.Hub)
            {
                return;
            }
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (behaviour is IGameScenePreparation preparation)
                    {
                        await preparation.PrepareSceneAsync((progress, step) =>
                            loadingPresenter.SetProgress(Mathf.Lerp(0.9f, 0.99f, Mathf.Clamp01(progress)), step, string.Empty, null, null),
                            behaviour.GetCancellationTokenOnDestroy());
                    }
                }
            }
            await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate);
        }

        /// <summary>以现有场景 Loading 遮挡场内准备，结束或失败均恢复当前页面。</summary>
        /// <param name="title">本次准备标题。</param>
        /// <param name="prepare">在主线程报告实际进度的准备操作。</param>
        /// <param name="cancellationToken">取消准备；UI 清理不随取消中断。</param>
        public async UniTask RunPreparationAsync(string title,
            Func<Action<float, string>, CancellationToken, UniTask> prepare,
            CancellationToken cancellationToken = default)
        {
            if (prepare == null)
            {
                throw new ArgumentNullException(nameof(prepare));
            }
            if (isTransitioning)
            {
                throw new InvalidOperationException("场景切换尚未结束。");
            }
            if (!GameSceneCatalog.TryGet(CurrentScene, out var current))
            {
                throw new InvalidOperationException("当前场景未登记。");
            }
            isTransitioning = true;
            var presenter = new GameSceneLoadingPresenter();
            try
            {
                string error = await presenter.BeginAsync(new GameSceneDefinition(CurrentScene, title, null, current.Address));
                if (!string.IsNullOrEmpty(error))
                {
                    throw new InvalidOperationException(error);
                }
                await prepare((progress, step) => presenter.SetProgress(progress, step, string.Empty, null, null), cancellationToken);
                await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate, cancellationToken);
            }
            finally
            {
                try
                {
                    await presenter.RestoreAsync(CurrentScene);
                }
                finally
                {
                    isTransitioning = false;
                }
            }
        }

        private async UniTask<GameSceneSwitchResult> RecoverPreparationFailedAsync(GameSceneId target, string error)
        {
            try
            {
                // 来源已经卸载；准备失败必须先卸载目标，不能仅关闭 Loading 留下不可用的 Demo。
                var result = await runtime.ReturnToHubAsync(null);
                if (result.Succeeded)
                {
                    currentContext?.Dispose();
                    currentContext = null;
                    CurrentScene = GameSceneId.Hub;
                    if (isEditorDirect && !ReferenceEquals(Instance, this))
                    {
                        // Hub 页面由正式启动链创建；旧 Presenter 仅清理自己的 Loading 和持久化 UI 遮挡。
                        return await RestoreFailedAsync(target, error, target);
                    }
                }
                else
                {
                    error = $"{error}; 准备失败后返回 Hub 失败: {result.Error}";
                }
            }
            catch (Exception exception)
            {
                error = $"{error}; 准备失败后返回 Hub 异常: {exception.Message}";
            }

            return await RestoreFailedAsync(target, error);
        }

        private async UniTask<GameSceneSwitchResult> RestoreFailedAsync(GameSceneId target, string error, GameSceneId? restoreScene = null)
        {
            try
            {
                // 场景提交后只恢复当前 UI，不能把已卸载的来源当作回滚目标。
                await loadingPresenter.RestoreAsync(restoreScene ?? CurrentScene);
            }
            catch (Exception exception)
            {
                error = $"{error}; Loading 恢复失败: {exception.Message}";
            }

            return GameSceneSwitchResult.Failed(target, error);
        }
    }
}
