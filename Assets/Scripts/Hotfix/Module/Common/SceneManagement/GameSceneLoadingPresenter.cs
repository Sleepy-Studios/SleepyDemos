using System;
using System.Collections.Generic;
using Core.Runtime;
using Core.Runtime.Localization;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Hotfix.SceneManagement
{
    internal interface IGameSceneLoadingPresenter
    {
        UniTask<string> BeginAsync(GameSceneDefinition target);
        void SetProgress(float progress, string step, string description, string stepKey, string descriptionKey, params object[] args);
        UniTask<string> CompleteAsync(GameSceneId target);
        UniTask RestoreAsync(GameSceneId source);
    }

    internal sealed class GameSceneLoadingPresenter : IGameSceneLoadingPresenter
    {
        private CommonLoadingView loadingView;
        private readonly List<(Canvas Canvas, bool Enabled, GraphicRaycaster Raycaster, bool RaycasterEnabled)> upperLayers = new();
        private readonly List<(CanvasGroup Group, bool Interactable, bool BlocksRaycasts)> upperGroups = new();

        public async UniTask<string> BeginAsync(GameSceneDefinition target)
        {
            bool preserveSource = GameSceneNavigator.Instance != null && GameSceneNavigator.Instance.CurrentScene != GameSceneId.Hub;
            // Demo Page 的 OnHide 可能释放业务绑定，失败前不能退出或销毁来源实例。
            var options = new UIShowOptions(animated: false, hidePrevious: !preserveSource);
            var prepared = await UIManager.Instance.PreloadAsync<CommonLoadingView>();
            if (prepared.Status != UIOperationStatus.Succeeded && prepared.Status != UIOperationStatus.Ignored)
            {
                return prepared.Exception?.Message ?? $"无法准备通用 Loading: {prepared.Status}";
            }
            loadingView = prepared.View as CommonLoadingView;
            if (loadingView == null)
            {
                return "通用 Loading 预加载未返回 View 实例。";
            }
            loadingView.ResetProgress();
            loadingView.SetTitle(target.DisplayName, target.DisplayNameKey);
            await loadingView.SetBackgroundAsync(BackgroundAddress(target.Id));
            var result = target.IsHub || preserveSource
                ? await UIManager.Instance.ShowAsync<CommonLoadingView>(options)
                : await UIManager.Instance.ReplaceAsync<CommonLoadingView>(options);
            if (result.Status != UIOperationStatus.Succeeded &&
                result.Status != UIOperationStatus.Ignored)
            {
                return result.Exception?.Message ?? $"无法打开通用 Loading: {result.Status}";
            }

            loadingView = result.View as CommonLoadingView;
            if (loadingView == null)
            {
                return "通用 Loading 已打开，但无法取得 CommonLoadingView 实例。";
            }

            // Loading 保持 Base Page；只遮蔽现有高层绘制，不退出来源页面或目标控件生命周期。
            foreach (var view in UIManager.Instance.cacheStack.GetAllViews())
            {
                if (!view.IsLoaded || view.Level <= UILayer.Base) continue;
                var group = view.gameObject.GetComponent<CanvasGroup>();
                if (group == null) continue;
                upperGroups.Add((group, group.interactable, group.blocksRaycasts));
                group.interactable = false;
                group.blocksRaycasts = false;
            }
            foreach (var layer in new[] { UILayer.Foreground, UILayer.Pop, UILayer.Decorate, UILayer.Tip })
            {
                var root = UIRootManager.Instance.GetRoot(layer);
                var canvas = root.GetComponent<Canvas>();
                var raycaster = root.GetComponent<GraphicRaycaster>();
                upperLayers.Add((canvas, canvas.enabled, raycaster, raycaster != null && raycaster.enabled));
                canvas.enabled = false;
                if (raycaster != null) raycaster.enabled = false;
            }

            string name = LanguageService.IsInitialized ? LanguageService.Get(target.DisplayNameKey) : target.DisplayName;
            SetProgress(0.05f, "准备切换", $"即将进入{target.DisplayName}", "Loading.Prepare", "Loading.Enter", name);
            // 无动画导航可以同步完成，先跨过完整下一帧，让 Loading 获得呈现机会。
            await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate);
            return null;
        }

        private static string BackgroundAddress(GameSceneId scene) => scene switch
        {
            GameSceneId.DroneFlight => "LoadResources/UI/Common/Loading/Art/SceneBackdropDrone",
            GameSceneId.Dlss => "LoadResources/UI/Hall/Art/Gallery/Dlss",
            GameSceneId.WallSqueeze => "LoadResources/UI/Common/Loading/Art/StartupBackdrop",
            GameSceneId.KagitKart => "LoadResources/Demos/kagit_kart/Art/UI/HubPreview",
            _ => "LoadResources/UI/Common/Loading/Art/StartupBackdrop"
        };

        /// <summary>交付源步骤的明确文案键及原文，语言未就绪时保留原加载表现。</summary>
        /// <param name="progress">单调进度。</param>
        /// <param name="step">步骤原文。</param>
        /// <param name="description">描述原文。</param>
        /// <param name="stepKey">稳定步骤键。</param>
        /// <param name="descriptionKey">完整描述句式键；空表示清空描述。</param>
        /// <param name="args">描述句式参数。</param>
        public void SetProgress(float progress, string step, string description, string stepKey, string descriptionKey, params object[] args)
        {
            loadingView?.SetProgress(progress, step, description, stepKey: stepKey, descriptionKey: descriptionKey, args: args);
        }

        public async UniTask<string> CompleteAsync(GameSceneId target)
        {
            SetProgress(1f, "加载完成", "", "Loading.Complete", null);
            return await FinishUiAsync(target);
        }

        public async UniTask RestoreAsync(GameSceneId source)
        {
            var error = await FinishUiAsync(source);
            if (!string.IsNullOrEmpty(error))
            {
                throw new InvalidOperationException(error);
            }
        }

        private async UniTask<string> FinishUiAsync(GameSceneId scene)
        {
            try
            {
                if (scene == GameSceneId.Hub)
                {
                    var result = await UIManager.Instance.ReplaceAsync<MainMenuView>(new UIShowOptions(false));
                    if (result.Status != UIOperationStatus.Succeeded && result.Status != UIOperationStatus.Ignored)
                    {
                        return result.Exception?.Message ?? $"场景 UI 收口失败: {result.Status}";
                    }
                }

                // Editor 正式启动可能已显示 Hub，仍须清理本事务留下的 Loading 实例。
                if (loadingView != null && loadingView.State != ViewState.Destroyed)
                {
                    var result = await UIManager.Instance.CloseAsync(loadingView, false);
                    if (result.Status != UIOperationStatus.Succeeded && result.Status != UIOperationStatus.Ignored)
                    {
                        return result.Exception?.Message ?? $"Loading 收口失败: {result.Status}";
                    }
                }

                loadingView = null;
                return null;
            }
            finally
            {
                foreach (var group in upperGroups)
                {
                    if (group.Group == null) continue;
                    group.Group.interactable = group.Interactable;
                    group.Group.blocksRaycasts = group.BlocksRaycasts;
                }
                upperGroups.Clear();
                foreach (var layer in upperLayers)
                {
                    if (layer.Canvas != null) layer.Canvas.enabled = layer.Enabled;
                    if (layer.Raycaster != null) layer.Raycaster.enabled = layer.RaycasterEnabled;
                }
                upperLayers.Clear();
            }
        }
    }
}
