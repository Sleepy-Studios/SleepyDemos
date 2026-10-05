using System;
using Core.Runtime;
using Cysharp.Threading.Tasks;
using Hotfix.SceneManagement;
using UnityEngine;

namespace Hotfix
{
    /// 大厅入口选择与场景导航命令，页面只渲染状态。
    public sealed class MainMenuHandler : HandlerBase<MainMenuAction, MainMenuData>
    {
        /// <summary>
        /// 处理本模块业务命令，按实际服务与规则结果发布状态。
        /// </summary>
        /// <param name="action">当前模块的业务请求。</param>
        protected override void Reduce(MainMenuAction action)
        {
            switch (action)
            {
                case MainMenuSelectAction selected:
                    if ((uint)selected.Index >= State.Entries.Count || State.SelectedIndex == selected.Index)
                        return;
                    foreach (var entry in State.Entries)
                        entry.IsSelected = false;
                    State.SelectedIndex = selected.Index;
                    State.Entries[selected.Index].IsSelected = true;
                    break;
                case MainMenuEnterAction enter:
                    EnterAsync(enter.Target).Forget();
                    return;
                case MainMenuAvailabilityAction available:
                    RefreshAvailability(available.Transitioning);
                    break;
                case MainMenuFeedbackAction feedback:
                    State.Feedback = feedback.Message;
                    RefreshAvailability(GameSceneNavigator.Instance?.IsTransitioning == true);
                    break;
            }

            ApplyState();
        }

        private void RefreshAvailability(bool transitioning)
        {
            State.CanEnter = !State.IsEntering && !transitioning;
            foreach (var entry in State.Entries)
            {
                entry.CanEnter = State.CanEnter && entry.SceneId.HasValue;
                entry.CanBrowse = State.CanEnter;
            }
        }

        private async UniTask EnterAsync(GameSceneId target)
        {
            var navigator = GameSceneNavigator.Instance;
            if (State.IsEntering || navigator?.IsTransitioning == true)
                return;
            if (navigator == null)
            {
                State.Feedback = "入口未就绪，请稍后重试";
                ApplyState();
                return;
            }

            int version = ++State.Version;
            State.IsEntering = true;
            State.Feedback = null;
            RefreshAvailability(true);
            ApplyState();
            try
            {
                var result = await navigator.SwitchAsync(target);
                // 清理或重新注册后，旧导航结果不得写入当前大厅状态。
                if (!ReferenceEquals(GlobalData.Get<MainMenuData>(), State) || State.Version != version)
                    return;
                if (result.Status == GameSceneSwitchStatus.Failed)
                {
                    State.Feedback = "进入失败，请重试";
                    Debug.LogError($"[MainMenu] 无法进入 {target}：{result.Error}");
                }
                else if (result.Status == GameSceneSwitchStatus.Succeeded)
                {
                    foreach (var entry in State.Entries)
                        entry.IsSelected = false;
                    State.SelectedIndex = 0;
                    State.Entries[0].IsSelected = true;
                }
            }
            catch (Exception error)
            {
                if (!ReferenceEquals(GlobalData.Get<MainMenuData>(), State) || State.Version != version)
                    return;
                State.Feedback = "进入失败，请重试";
                Debug.LogError($"[MainMenu] 无法进入 {target}：{error}");
            }
            finally
            {
                if (ReferenceEquals(GlobalData.Get<MainMenuData>(), State) && State.Version == version)
                {
                    State.IsEntering = false;
                    RefreshAvailability(navigator.IsTransitioning);
                    ApplyState();
                }
            }
        }
    }
}
