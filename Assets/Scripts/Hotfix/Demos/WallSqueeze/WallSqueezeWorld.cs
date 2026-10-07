using System;
using Core.Runtime;
using Core.Runtime.Inputs;
using Cysharp.Threading.Tasks;
using Hotfix.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Hotfix.WallSqueeze
{
    /// 场景宿主，负责规则时钟、输入与 UI 生命周期。
    public sealed class WallSqueezeWorld : MonoBehaviour
    {
        [SerializeField] private WallSqueezeSettings settings;
        [SerializeField] private WallSqueezeLevel[] levels;
        [SerializeField] private InputActionAsset inputTemplate;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private WallSqueezePresentation presentation;
        private WallSqueezeInput input;
        private MenuInputScope menu;
        private bool ready;
        private bool exiting;
        private bool modalOpen;
        private bool modalBusy;
        private bool modalDirty;
        private WallSqueezeHudView hudView;
        private WallSqueezeMenuView menuView;
        private float crushFeedbackSeconds;
        private int displayedSeconds;
        /// 最近一次群夹数量；提示结束后归零。
        public int LastCrushCount { get; private set; }
        /// 当前关卡唯一规则实例。
        public WallSqueezeSimulation Simulation { get; private set; }
        /// 当前关卡的零基索引。
        public int LevelIndex { get; private set; }
        /// 保存的关卡总数。
        public int LevelCount => levels.Length;
        /// 保存的当前关卡名称。
        public string LevelTitle => levels[LevelIndex].Title;
        /// 当前关卡是否设置了限时目标。
        public bool HasTimeLimit => levels[LevelIndex].TimeLimit > 0;
        /// 本会话最近实际操作设备。
        public InputDeviceKind Device => input?.Router.DeviceKind ?? InputDeviceKind.KeyboardMouse;
        /// 规则与演出是否冻结。
        public bool Paused => input?.Router.PauseState.IsPaused == true;
        /// 物理阻塞已解除，可明确继续。
        public bool CanContinue => input?.Router.PauseState.CanResume == true;
        /// 当前独占输入消费会话。
        public WallSqueezeInput Input => input;

        private void Start() => InitializeAsync().Forget();

        private async UniTaskVoid InitializeAsync()
        {
            try
            {
                var token = this.GetCancellationTokenOnDestroy();
                if (!await DemoIslandEditorBootstrap.EnsureReadyAsync(token))
                {
                    return;
                }
                await GameSceneNavigator.Instance.WaitUntilStableAsync(GameSceneId.WallSqueeze, token);
                input = new WallSqueezeInput(inputTemplate, worldCamera);
                menu = new MenuInputScope(EventSystem.current, input.Router);
                menu.SetContext(GameplayInputContext.Interaction);
                input.Router.PauseState.Changed += OnPauseChanged;
                input.Router.DeviceChanged += OnDeviceChanged;
                var data = GlobalData.Add<WallSqueezeData>();
                data.World = this;
                BuildLevel();
                var shown = await UIManager.Instance.ShowAsync<WallSqueezeHudView>(view => view.SetData(this), new UIShowOptions(animated: false), token);
                if (shown.Status == UIOperationStatus.Failed)
                {
                    throw shown.Exception;
                }
                ready = true;
                hudView = shown.View as WallSqueezeHudView;
                Refresh();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        private void Update()
        {
            if (!ready || exiting)
            {
                return;
            }
            FitCamera();
            bool pause = input.Read(Simulation, Time.unscaledDeltaTime, settings.WallSpeed, out int wall, out float target);
            if (pause && Simulation.Result == WallSqueezeResult.Playing)
            {
                Dispatch(Paused ? WallSqueezeCommand.Continue : WallSqueezeCommand.Pause);
            }
            menu.Update();
            if (Paused || Simulation.Result != WallSqueezeResult.Playing)
            {
                if (!modalOpen && !modalBusy)
                {
                    SynchronizeModalAsync().Forget();
                }
                return;
            }
            int before = Simulation.Remaining;
            Simulation.Advance(Time.unscaledDeltaTime, wall, target);
            presentation.Render(Simulation, input.Selected, Time.unscaledDeltaTime);
            bool feedbackEnded = LastCrushCount > 0 && crushFeedbackSeconds <= Time.unscaledDeltaTime;
            crushFeedbackSeconds = Mathf.Max(0, crushFeedbackSeconds - Time.unscaledDeltaTime);
            if (feedbackEnded)
            {
                LastCrushCount = 0;
            }
            if (Simulation.KilledThisAdvance > 0)
            {
                input.Router.PlayRumble(.25f, .55f, .1f);
                LastCrushCount = Simulation.KilledThisAdvance;
                crushFeedbackSeconds = 1.2f;
            }
            int seconds = HasTimeLimit ? Mathf.CeilToInt(Simulation.RemainingSeconds) : 0;
            if (before != Simulation.Remaining || Simulation.Result != WallSqueezeResult.Playing || feedbackEnded || seconds != displayedSeconds)
            {
                displayedSeconds = seconds;
                Refresh();
            }
            if (Simulation.Result != WallSqueezeResult.Playing)
            {
                input.CancelHold();
                SynchronizeModalAsync().Forget();
            }
        }

        private void BuildLevel()
        {
            input.ResetForLevel();
            Simulation = new WallSqueezeSimulation(settings, levels[LevelIndex]);
            LastCrushCount = 0;
            crushFeedbackSeconds = 0;
            displayedSeconds = HasTimeLimit ? Mathf.CeilToInt(Simulation.RemainingSeconds) : 0;
            presentation.Build(Simulation);
            FitCamera();
        }

        private void FitCamera()
        {
            worldCamera.clearFlags = CameraClearFlags.SolidColor;
            worldCamera.backgroundColor = new Color(244f / 255, 241f / 255, 234f / 255);
            worldCamera.orthographicSize = Mathf.Max(settings.Room.y / 2 + 2, (settings.Room.x / 2 + .6f) / Mathf.Max(.1f, worldCamera.aspect));
            worldCamera.transform.position = new Vector3(settings.Room.x / 2, settings.Room.y / 2, -10);
        }

        internal void ApplyCommand(WallSqueezeCommand command)
        {
            if (!ready || exiting)
            {
                return;
            }
            switch (command)
            {
                case WallSqueezeCommand.Pause:
                    input.Router.PauseState.RequestPause(LocalPauseReason.User);
                    break;
                case WallSqueezeCommand.Continue:
                    input.Router.PauseState.TryResume();
                    break;
                case WallSqueezeCommand.Retry:
                    BuildLevel();
                    input.Router.PauseState.TryResume();
                    break;
                case WallSqueezeCommand.Next:
                    if (Simulation.Result == WallSqueezeResult.Won && LevelIndex + 1 < levels.Length)
                    {
                        LevelIndex++;
                        BuildLevel();
                        input.Router.PauseState.TryResume();
                    }
                    break;
                case WallSqueezeCommand.Hub:
                    ReturnHubAsync().Forget();
                    return;
                case WallSqueezeCommand.Refresh:
                    return;
            }
            SynchronizeModalAsync().Forget();
        }

        private async UniTaskVoid SynchronizeModalAsync()
        {
            modalDirty = true;
            if (modalBusy || !ready || exiting)
            {
                return;
            }
            modalBusy = true;
            try
            {
                while (modalDirty && ready && !exiting)
                {
                    modalDirty = false;
                    bool desired = Paused || Simulation.Result != WallSqueezeResult.Playing;
                    if (desired && !modalOpen)
                    {
                        input.CancelHold();
                        input.Router.SetContext(GameplayInputContext.Menu);
                        var shown = await UIManager.Instance.ShowAsync<WallSqueezeMenuView>(view => view.SetData(this), new UIShowOptions(animated: false), this.GetCancellationTokenOnDestroy());
                        if (shown.Status == UIOperationStatus.Failed)
                        {
                            throw shown.Exception;
                        }
                        modalOpen = true;
                        menuView = shown.View as WallSqueezeMenuView;
                        menu.Dispose();
                        menu = new MenuInputScope(EventSystem.current, input.Router, menuView.transform);
                        menu.SetContext(GameplayInputContext.Menu, shown.View?.gameObject.GetComponent<WallSqueezeMenuPanel>()?.FirstSelection);
                    }
                    else if (!desired && modalOpen)
                    {
                        await UIManager.Instance.CloseAsync(menuView);
                        modalOpen = false;
                        menuView = null;
                        input.Router.SetContext(GameplayInputContext.Interaction);
                        menu.Dispose();
                        menu = new MenuInputScope(EventSystem.current, input.Router);
                        menu.SetContext(GameplayInputContext.Interaction);
                    }
                    Refresh();
                    modalDirty |= (Paused || Simulation.Result != WallSqueezeResult.Playing) != modalOpen;
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                modalBusy = false;
            }
        }

        private async UniTaskVoid ReturnHubAsync()
        {
            exiting = true;
            input.CancelHold();
            input.Router.SetContext(GameplayInputContext.Menu);
            while (modalBusy)
            {
                await UniTask.Yield();
            }
            if (menuView != null)
            {
                await UIManager.Instance.CloseAsync(menuView);
                menuView = null;
                modalOpen = false;
            }
            if (hudView != null)
            {
                await UIManager.Instance.CloseAsync(hudView);
                hudView = null;
            }
            var result = await GameSceneNavigator.Instance.SwitchAsync(GameSceneId.Hub);
            if (result.Status == GameSceneSwitchStatus.Failed)
            {
                exiting = false;
                Debug.LogError(result.Error, this);
                var shown = await UIManager.Instance.ShowAsync<WallSqueezeHudView>(view => view.SetData(this), new UIShowOptions(animated: false), this.GetCancellationTokenOnDestroy());
                hudView = shown.View as WallSqueezeHudView;
                if (!Paused && Simulation.Result == WallSqueezeResult.Playing)
                {
                    input.Router.SetContext(GameplayInputContext.Interaction);
                    menu.SetContext(GameplayInputContext.Interaction);
                }
                SynchronizeModalAsync().Forget();
            }
        }

        private void OnPauseChanged()
        {
            if (ready)
            {
                SynchronizeModalAsync().Forget();
                Refresh();
            }
        }

        private void OnDeviceChanged(InputDeviceKind kind) => Refresh();
        private void OnApplicationFocus(bool focused) => input?.Router.PauseState.SetApplicationFocus(focused);
        private void OnApplicationPause(bool paused) => input?.Router.PauseState.SetApplicationPaused(paused);
        private void Refresh() => GlobalData.Dispatch(new WallSqueezeAction(this, WallSqueezeCommand.Refresh));

        /// <summary>从输入和页面派发流程命令。</summary>
        /// <param name="command">当前场景的业务操作。</param>
        public void Dispatch(WallSqueezeCommand command) => GlobalData.Dispatch(new WallSqueezeAction(this, command));

        private void OnDestroy()
        {
            ready = false;
            menu?.Dispose();
            menu = null;
            if (input != null)
            {
                input.Router.PauseState.Changed -= OnPauseChanged;
                input.Router.DeviceChanged -= OnDeviceChanged;
                input.Dispose();
                input = null;
            }
            if (GlobalData.Get<WallSqueezeData>()?.World == this)
            {
                GlobalData.Remove<WallSqueezeData>();
            }
        }
    }
}
