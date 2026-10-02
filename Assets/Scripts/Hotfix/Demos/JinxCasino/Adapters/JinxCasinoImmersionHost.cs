using Hotfix.JinxCasino.Interaction;
using System;
using System.Collections.Generic;
using Core.Runtime.Inputs;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Hotfix.JinxCasino.Adapters
{
    public sealed partial class JinxCasinoController
    {
        [SerializeField] private InputActionAsset immersionInputAsset;
        private GameplayInputRouter immersionInput;
        private MenuInputScope immersionMenu;
        private JinxCasinoTableFocus tableFocus;
        private JinxCasinoTableSelection tableSelection;
        private JinxCasinoTableSession tableSession;
        private JinxCasinoStation focusedStation;
        private JinxCasinoS1Presentation focusedPresentation;
        private GameplayInputContext? appliedInputContext;
        private CursorLockMode previousCursorLock;
        private bool previousCursorVisible;
        private bool cursorCaptured;
        private bool immersionModalPaused;
        private bool immersionScreenOpen;
        private GameObject immersionFirstSelection;
        private Action immersionMenuCancel;
        private float navigationDelay;
        private int previousNavigation;
        private Vector2 previousTouchMove;
        private readonly List<RaycastResult> tableUiHits = new List<RaycastResult>();
        private PointerEventData tablePointer;

        /// 保存输入配置的样板场景启用沉浸路径，旧原型保持可恢复。
        public bool UsesImmersion => immersionInputAsset != null;
        /// 暂停覆盖领域、探索和桌面；其它场景演出须读取同一状态。
        public bool IsImmersionPaused => immersionModalPaused || (immersionInput?.PauseState.IsPaused ?? false);
        /// 当前具体桌面及只读展示状态，HUD和物件表现消费此副本。
        public JinxCasinoTableView TableView { get; private set; }
        /// 最近一次桌面操作的直接反馈。
        public string TableFeedback { get; private set; }
        /// HUD仅展示操作失败原因，成功状态由专属桌面物件呈现。
        public bool HasTableFeedbackError { get; private set; }
        /// 当前物件是否仍在演出，不能提前宣称结果已经展示。
        public bool IsTableAnimating => focusedPresentation != null && focusedPresentation.IsAnimating;
        /// 当前设备用于切换按键图标，不读取全局Gamepad.current。
        public InputDeviceKind InputDeviceKind => immersionInput?.DeviceKind ?? InputDeviceKind.KeyboardMouse;
        /// 暂停面板及设备提示订阅，离场随宿主释放。
        public event Action ImmersionInputChanged;

        /// <summary>为保存的样板场景指定独立输入资产，不覆盖全局输入配置。</summary>
        /// <param name="asset">已经过Editor装配校验的三上下文动作资产。</param>
        public void ConfigureImmersion(InputActionAsset asset) => immersionInputAsset = asset;

        /// <summary>返回当前聚焦桌面的最新公开视图，避免表现使用上一帧缓存。</summary>
        /// <param name="station">具体机台对象，不能只传玩法类型。</param>
        /// <returns>当前聚焦对象的视图，否则null，由场景观察者读取原局。</returns>
        public JinxCasinoTableView GetFocusedTableView(JinxCasinoStation station)
            => focusedStation == station ? tableSession?.GetView() : null;

        /// <summary>由保存的样板菜单声明屏幕导航范围与初始控件，不能代为点击。</summary>
        /// <param name="open">菜单是否打开。</param>
        /// <param name="pauseClock">是否冻结单机时钟。</param>
        /// <param name="firstSelection">菜单保存的首个可用控件。</param>
        /// <param name="cancel">由菜单统一处理取消；null保留旧暂停切换。</param>
        public void SetImmersionMenuState(bool open, bool pauseClock, GameObject firstSelection, Action cancel = null)
        {
            immersionMenuCancel = open ? cancel : null;
            immersionScreenOpen = open; immersionModalPaused = open && pauseClock; immersionFirstSelection = firstSelection;
            appliedInputContext = null;
            if (immersionInput != null) ApplyImmersionContext(open ? GameplayInputContext.Menu : tableFocus.IsActive ? GameplayInputContext.Interaction : GameplayInputContext.Gameplay);
        }

        private void EnsureImmersionInput()
        {
            if (immersionInput != null) return;
            sceneEffects?.BindPresentationClock(PresentationClock);
            previousCursorLock = Cursor.lockState; previousCursorVisible = Cursor.visible; cursorCaptured = true;
            immersionInput = new GameplayInputRouter(immersionInputAsset, localPreferences.ToInputSettings(), "Exploration", "Table");
            immersionInput.DeviceChanged += OnImmersionDeviceChanged;
            immersionInput.PauseState.Changed += OnImmersionPauseChanged;
            immersionInput.PauseState.SetApplicationFocus(hasFocus);
            immersionInput.PauseState.SetApplicationPaused(isApplicationPaused);
            tableFocus = new JinxCasinoTableFocus(worldCamera);
            tableSelection = new JinxCasinoTableSelection();
            if (EventSystem.current != null)
            {
                immersionMenu = new MenuInputScope(EventSystem.current, immersionInput);
                tablePointer = new PointerEventData(EventSystem.current);
            }
        }

        private void UpdateImmersion()
        {
            if (isExiting || worldCamera == null || body == null) return;
            EnsureImmersionInput();
            var context = IsImmersionPaused || IsAdventureInputBlocked ? GameplayInputContext.Menu :
                tableFocus.IsActive ? GameplayInputContext.Interaction : GameplayInputContext.Gameplay;
            ApplyImmersionContext(context);
            Vector2 movement = movePad != null ? movePad.Move : Vector2.zero;
            Vector2 look = lookPad != null ? lookPad.ConsumeLook() : Vector2.zero;
            immersionInput.SetTouchFrame(movement, look, (movement - previousTouchMove).sqrMagnitude + look.sqrMagnitude > 0.0001f);
            previousTouchMove = movement;
            var frame = immersionInput.ReadFrame(Time.unscaledDeltaTime, gameSettings != null ? gameSettings.LookSensitivity : 0.12f,
                context == GameplayInputContext.Gameplay && Cursor.lockState == CursorLockMode.Locked);
            var actions = immersionInput.ConsumeActions();
            immersionMenu?.Update();
            if ((actions & GameplayInputActions.Pause) != 0)
            {
                if (immersionMenuCancel != null) immersionMenuCancel();
                else if (IsImmersionPaused) ResumeImmersion(); else PauseImmersion();
            }
            AdvanceImmersionPresentation();
            float presentationDelta = PresentationClock.GetDeltaSeconds(Time.frameCount);
            // 每帧都Tick，以便机台销毁后也恢复借用相机，不能先按IsActive提前跳过。
            tableFocus.Tick(presentationDelta);
            if (tableSession != null && (!tableFocus.IsActive || focusedStation == null)) CloseImmersionTable();
            if (HasShopBinding && (!tableFocus.IsActive || focusedShop == null || !focusedShop.isActiveAndEnabled)) CloseImmersionTable();
            if (IsImmersionPaused) return;
            UpdateAdventure(presentationDelta);
            UpdateExitIntent();
            UpdateTutorialSceneFacts();
            if (IsAdventureInputBlocked) return;
            if (tableFocus.IsActive)
            {
                if ((actions & GameplayInputActions.Back) != 0) { CloseImmersionTable(true); return; }
                if (!tableFocus.IsReady || tableSession == null && !HasShopFocus) return;
                if (HasShopFocus) RefreshImmersionShop(); else RefreshImmersionTable();
                UpdateTableInput(frame, actions);
                return;
            }
            if ((actions & GameplayInputActions.Interact) != 0) { InteractWithNearbyStation(); if (tableFocus.IsActive || IsAdventureInputBlocked) return; }
            float beforeTutorialYaw = yaw, beforeTutorialPitch = pitch;
            Vector3 beforeTutorialPosition = body.transform.position;
            if (presentationDelta > 0)
            { yaw += frame.LookDegrees.x; pitch = Mathf.Clamp(pitch - frame.LookDegrees.y, -70, 70); }
            body.transform.rotation = Quaternion.Euler(0, yaw, 0);
            worldCamera.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
            float speed = (gameSettings != null ? gameSettings.MovementSpeed : 3.5f) * (sceneEffects != null ? sceneEffects.LocalMovementMultiplier : 1);
            body.Move((body.transform.right * frame.Move.x + body.transform.forward * frame.Move.y) * (speed * presentationDelta)
                + Vector3.down * (3 * presentationDelta));
            ObserveTutorialAppliedExploration(Mathf.DeltaAngle(beforeTutorialYaw, yaw), pitch - beforeTutorialPitch,
                Vector3.ProjectOnPlane(body.transform.position - beforeTutorialPosition, Vector3.up).magnitude, frame.Move.sqrMagnitude > .0001f);
        }

        private void ApplyImmersionContext(GameplayInputContext context)
        {
            if (appliedInputContext == context) return;
            movePad?.ResetInput(); lookPad?.ResetInput();
            immersionInput.SetContext(context);
            immersionMenu?.SetContext(context, immersionFirstSelection != null ? immersionFirstSelection : EventSystem.current?.currentSelectedGameObject);
            appliedInputContext = context; navigationDelay = 0; previousNavigation = 0;
            UpdateImmersionCursor();
        }

        private void UpdateTableInput(GameplayInputFrame frame, GameplayInputActions actions)
        {
            int direction = Mathf.Abs(frame.InteractionNavigation.x) > Mathf.Abs(frame.InteractionNavigation.y)
                ? Math.Sign(frame.InteractionNavigation.x) : -Math.Sign(frame.InteractionNavigation.y);
            navigationDelay -= Time.unscaledDeltaTime;
            if (direction != 0 && (direction != previousNavigation || navigationDelay <= 0))
            {
                tableSelection.Navigate(direction); navigationDelay = direction != previousNavigation ? 0.35f : 0.16f;
            }
            previousNavigation = direction;
            if (frame.PointerMoved || frame.PointerPressed)
                tableSelection.Point(worldCamera.ScreenPointToRay(frame.PointerPosition));
            else if (frame.DeviceKind == InputDeviceKind.Gamepad && tableSelection.Selected == null) tableSelection.Navigate(1);
            if (frame.PointerPressed && !PointerHitsMenu(frame.PointerPosition) || (actions & GameplayInputActions.Confirm) != 0)
                tableSelection.TryInvoke();
            else if ((actions & GameplayInputActions.Secondary) != 0) ApplyFocusedCommand(JinxCasinoTableAction.Secondary);
            else if ((actions & GameplayInputActions.Help) != 0) ApplyFocusedCommand(JinxCasinoTableAction.Help);
            else if ((actions & (GameplayInputActions.PreviousGroup | GameplayInputActions.NextGroup)) != 0)
            {
                int chipDirection = (actions & GameplayInputActions.NextGroup) != 0 ? 1 : -1;
                var targets = HasShopFocus ? focusedShop.Targets : focusedStation?.Targets;
                for (int i = 0; targets != null && i < targets.Count; i++)
                {
                    var target = tableSelection.Navigate(chipDirection);
                    if (target == null || target.Action == (HasShopFocus ? JinxCasinoTableAction.SelectProduct : JinxCasinoTableAction.ChipAdd)) break;
                }
            }
        }

        private void ApplyFocusedCommand(JinxCasinoTableAction action)
        { if (HasShopFocus) ApplyShopCommand(action, 0); else ApplyTableCommand(action, 0); }

        private bool PointerHitsMenu(Vector2 position)
        {
            if (tablePointer == null || EventSystem.current == null) return false;
            tablePointer.position = position; tableUiHits.Clear();
            EventSystem.current.RaycastAll(tablePointer, tableUiHits);
            return tableUiHits.Count > 0;
        }

        private bool TryOpenImmersionTable(JinxCasinoStation station)
        {
            if (immersionInput == null || IsImmersionPaused || IsAdventureInputBlocked || station == null || !station.HasTableInteraction) return false;
            if (!tableFocus.TryEnter(station)) return false;
            focusedStation = station;
            focusedPresentation = station.GetComponent<JinxCasinoS1Presentation>();
            tableSession = new JinxCasinoTableSession(Game, station);
            foreach (var target in station.Targets) if (target != null)
            { target.BindPresentationClock(PresentationClock); target.Invoked += OnTableTargetInvoked; }
            tableSelection.Bind(station); TableFeedback = null; HasTableFeedbackError = false;
            RefreshImmersionTable(); ApplyImmersionContext(GameplayInputContext.Interaction);
            return true;
        }

        private void OnTableTargetInvoked(JinxCasinoTableTarget target) => ApplyTableCommand(target.Action, target.Value);
        private void ApplyTableCommand(JinxCasinoTableAction action, int value)
        {
            if (tableSession == null || !tableFocus.IsReady || IsImmersionPaused) return;
            if (focusedPresentation != null && focusedPresentation.IsAnimating && action != JinxCasinoTableAction.Help)
            { TableFeedback = "请等机台完成当前动作。"; HasTableFeedbackError = false; return; }
            var result = tableSession.Apply(action, value, Time.frameCount);
            TableFeedback = result.Description; HasTableFeedbackError = !result.Success;
            ObserveTutorialTableCommand(action, result);
            RefreshImmersionTable(); ImmersionInputChanged?.Invoke();
        }

        private void RefreshImmersionTable()
        {
            TableView = tableSession?.GetView();
            if (TableView == null || focusedStation == null) return;
            foreach (var target in focusedStation.Targets)
            {
                if (target == null) continue;
                var available = TableView.GetAvailability(target.Action, target.Value);
                bool busy = focusedPresentation != null && focusedPresentation.IsAnimating && target.Action != JinxCasinoTableAction.Help;
                target.SetAvailable(available.IsAvailable && !busy, busy ? "请等机台完成当前动作。" : available.Reason);
            }
        }

        /// <summary>离开桌面仅清草稿；已投入局保持原定位，真实玩家离桌要等返回过渡结束再报告教学。</summary>
        /// <param name="playerInitiated">玩家Back/离桌按钮传true；加载、传送、机台失效或销毁清理保持false。</param>
        public void CloseImmersionTable(bool playerInitiated = false)
        {
            if (playerInitiated) RegisterTutorialPlayerExit();
            CloseImmersionShop();
            if (focusedStation != null)
                foreach (var target in focusedStation.Targets) if (target != null) target.Invoked -= OnTableTargetInvoked;
            tableSession?.Close(); tableSession = null; focusedStation = null; focusedPresentation = null;
            tableSelection?.Bind(null); tableFocus?.Exit(); TableView = null; TableFeedback = null; HasTableFeedbackError = false;
        }

        private void ResetImmersionTableForRestore()
        {
            ResetTutorialObservations();
            ResetExitInteraction();
            CloseImmersionTable();
            tableFocus?.RestoreImmediately();
            movePad?.ResetInput(); lookPad?.ResetInput();
        }

        /// 明确打开单机暂停；不写全局timeScale。
        public void PauseImmersion() => immersionInput?.PauseState.RequestPause(LocalPauseReason.User);
        /// 玩家明确继续，仍在后台或手柄未恢复时不解除暂停。
        public bool ResumeImmersion() => immersionInput?.PauseState.TryResume() ?? false;

        private void OnImmersionPauseChanged()
        {
            PresentationClock?.SetPaused(IsImmersionPaused);
            movePad?.ResetInput(); lookPad?.ResetInput();
            UpdateImmersionCursor(); ImmersionInputChanged?.Invoke();
        }
        private void OnImmersionDeviceChanged(InputDeviceKind device) { UpdateImmersionCursor(); ImmersionInputChanged?.Invoke(); }
        private void UpdateImmersionCursor()
        {
            bool locked = !IsImmersionPaused && appliedInputContext == GameplayInputContext.Gameplay && InputDeviceKind == InputDeviceKind.KeyboardMouse;
            if (locked && Cursor.lockState != CursorLockMode.Locked) immersionInput?.DiscardNextLookDelta();
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked && InputDeviceKind == InputDeviceKind.KeyboardMouse;
        }
        private void DisposeImmersionInput()
        {
            immersionMenuCancel = null;
            CloseImmersionTable(); tableFocus?.Dispose(); tableSelection?.Dispose(); immersionMenu?.Dispose();
            if (immersionInput != null)
            {
                immersionInput.DeviceChanged -= OnImmersionDeviceChanged;
                immersionInput.PauseState.Changed -= OnImmersionPauseChanged;
                immersionInput.Dispose(); immersionInput = null;
            }
            if (cursorCaptured) { Cursor.lockState = previousCursorLock; Cursor.visible = previousCursorVisible; cursorCaptured = false; }
            ImmersionInputChanged = null;
        }
    }
}
