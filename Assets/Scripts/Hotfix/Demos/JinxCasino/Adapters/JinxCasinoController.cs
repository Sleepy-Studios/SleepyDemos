using Core.Runtime.Inputs;
using System;
using System.Threading;
using Core.Runtime;
using Cysharp.Threading.Tasks;
using Hotfix.JinxCasino.Adapters.UI;
using Hotfix.SceneManagement;
using UnityEngine;

namespace Hotfix.JinxCasino.Adapters
{
    /// 单机场景入口，负责初始化、菜单和离场；冒险规则由游戏对象管理。
    public sealed partial class JinxCasinoController : MonoBehaviour
    {
        [SerializeField] private Camera worldCamera;
        private View hud;
        private CancellationTokenSource lifetime;
        private CharacterController body;
        private TouchInputPad movePad;
        private TouchInputPad lookPad;
        private float yaw;
        private float pitch;
        private bool isExiting;
        private bool hasFocus = true;
        private bool isApplicationPaused;

        /// 已提交的单机钱包。
        public long Balance => Game.State?.Coins ?? 0;
        /// 场景离开过程中停止接受命令。
        public bool IsBusy => isExiting;
        /// 当前状态改变。
        public event Action Changed;

        private void Start() => InitializeAsync().Forget();

        private async UniTaskVoid InitializeAsync()
        {
            lifetime = new CancellationTokenSource();
            try
            {
                var navigator = GameSceneNavigator.Instance;
                if (navigator == null) throw new InvalidOperationException("请从 AppEntrance → Hub 进入倒霉蛋俱乐部。");
                await navigator.WaitUntilStableAsync(GameSceneId.JinxCasino, lifetime.Token);
                if (worldCamera == null || immersionInputAsset == null)
                    throw new InvalidOperationException("单机场景缺少主相机或输入配置。");
                body = worldCamera.GetComponentInParent<CharacterController>();
                if (body == null) throw new InvalidOperationException("主相机必须挂在本地玩家移动组件下。");
                yaw = worldCamera.transform.eulerAngles.y;
                hud = await ShowLocalHudAsync();
                Changed?.Invoke();
            }
            catch (OperationCanceledException) { }
            catch (Exception exception) { Game.SetStatus(exception.Message); Debug.LogException(exception, this); Changed?.Invoke(); }
        }

        private async UniTask<View> ShowLocalHudAsync()
        {
            var result = await UIManager.Instance.ShowAsync<JinxCasinoImmersionHudView, JinxCasinoController>(this,
                new UIShowOptions(animated: false), lifetime.Token);
            if (result.Status == UIOperationStatus.Failed) throw result.Exception;
            return result.View;
        }

        /// <summary>为当前 View 注册两块独立触控区域。</summary>
        /// <param name="move">移动区域，界面释放时传 null。</param>
        /// <param name="look">视角区域，界面释放时传 null。</param>
        public void BindTouchPads(TouchInputPad move, TouchInputPad look)
        {
            movePad?.ResetInput(); lookPad?.ResetInput(); movePad = move; lookPad = look;
        }

        private void Update()
        {
            Game.CommandInputEnabled = !IsBusy;
            UpdateImmersion();
        }

        private void OnApplicationFocus(bool focused)
        {
            hasFocus = focused;
            immersionInput?.PauseState.SetApplicationFocus(focused);
            if (!focused) { movePad?.ResetInput(); lookPad?.ResetInput(); }
        }
        private void OnApplicationPause(bool paused)
        {
            isApplicationPaused = paused;
            immersionInput?.PauseState.SetApplicationPaused(paused);
            if (paused) { movePad?.ResetInput(); lookPad?.ResetInput(); }
        }

        /// 保存单机进度并关闭具体 View，再返回 Hub。
        public void RequestExit() { if (!IsBusy) ExitAsync().Forget(); }
        /// 独立包返回本游戏主菜单，Editor的Hub接入保持原行为。
        public bool IsStandalonePlayer => GameSceneNavigator.Instance?.StandaloneScene == GameSceneId.JinxCasino;
        /// 仅独立包主菜单接受退出应用，不用此方法丢弃正在进行的旅程。
        public void QuitStandaloneApplication() { if (IsStandalonePlayer && !Game.HasAdventure && !IsBusy) Application.Quit(); }
        private async UniTaskVoid ExitAsync()
        {
            isExiting = true;
            Game.CommandInputEnabled = false;
            SaveAdventureBeforeExit();
            try
            {
                if (hud != null) { await UIManager.Instance.CloseAsync(hud); hud = null; }
                var result = IsStandalonePlayer
                    ? await GameSceneNavigator.Instance.ReloadCurrentAsync()
                    : await GameSceneNavigator.Instance.SwitchAsync(GameSceneId.Hub);
                if (result.Status != GameSceneSwitchStatus.Succeeded && result.Status != GameSceneSwitchStatus.Ignored)
                {
                    if (this != null) Game.SetStatus(result.Error ?? "导航繁忙，请稍后再试。");
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception exception)
            {
                if (this != null) { Game.SetStatus(exception.Message); Debug.LogException(exception, this); }
            }
            finally
            {
                // 导航可能已经卸载 Demo，随后才因 Hub UI 失败返回；销毁后不能恢复旧 HUD。
                if (this != null && lifetime != null && !lifetime.IsCancellationRequested &&
                    GameSceneNavigator.Instance?.CurrentScene == GameSceneId.JinxCasino)
                {
                    isExiting = false;
                    if (hud == null)
                    {
                        try
                        {
                            hud = await ShowLocalHudAsync();
                        }
                        catch (OperationCanceledException) { }
                        catch (Exception exception)
                        {
                            if (this != null) { Game.SetStatus(exception.Message); Debug.LogException(exception, this); }
                        }
                    }
                    if (this != null) Changed?.Invoke();
                }
            }
        }

        private void OnDestroy()
        {
            isExiting = true;
            DisposeImmersionInput();
            SaveAdventureBeforeExit();
            ReleaseGameSubscriptions();
            Game.CommandInputEnabled = false;
            lifetime?.Cancel();
            movePad?.ResetInput(); lookPad?.ResetInput();
            Changed = null;
            lifetime?.Dispose();
            lifetime = null;
        }
    }
}
