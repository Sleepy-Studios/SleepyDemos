using Hotfix.JinxCasino;
using Hotfix.JinxCasino.Presentation;
using System;
using System.Collections.Generic;
using System.Linq;
using Core.Runtime.Inputs;
using Hotfix.JinxCasino.Rules;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Hotfix.JinxCasino.Interaction
{
    /// 本地玩家的移动、输入上下文及实体操作，规则直接提交Game；生命周期由场景入口驱动。
    public sealed class JinxCasinoPlayerInteraction : IDisposable
    {
        private Transform sceneRoot;
        private JinxCasinoGameSettings configuration;
        private JinxCasinoSceneEffects effects;
        private TouchInputPad movePad;
        private TouchInputPad lookPad;
        private float yaw;
        private float pitch;
        private bool active;
        private bool hasFocus = true;
        private bool isApplicationPaused;
        private GameplayInputSettings inputSettings = new GameplayInputSettings();
        private InputActionAsset immersionInputAsset;
        private GameplayInputRouter immersionInput;
        private MenuInputScope immersionMenu;
        private JinxCasinoTableFocus tableFocus;
        private JinxCasinoTableSelection tableSelection;
        private JinxCasinoTableSession tableSession;
        private JinxCasinoStation focusedStation;
        private JinxCasinoS1Presentation focusedPresentation;
        private GameplayInputContext? appliedInputContext;
        private GameplayInputContext? lastReadInputContext;
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
        private JinxCasinoShopCounter shopCounter;
        private JinxCasinoShopCounter focusedShop;
        private IReadOnlyList<JinxCasinoTableTarget> focusedShopTargets = Array.Empty<JinxCasinoTableTarget>();
        private int selectedShopProduct;
        private int lastShopActionFrame = -1;


        public JinxCasinoGame Game { get; private set; }
        public Camera Camera { get; private set; }
        public CharacterController Body { get; private set; }
        public JinxCasinoPresentationClock Clock { get; } = new JinxCasinoPresentationClock();
        public JinxCasinoTutorialGuide Tutorial { get; private set; }
        public JinxCasinoExitInteraction Exit { get; private set; }
        public bool AcceptsCommands => Game != null && Game.CommandInputEnabled;
        public bool IsMenuOpen => immersionScreenOpen;
        public bool HasFocus => tableFocus?.IsActive ?? false;
        /// 相机已归位且探索输入已接管；HUD据此开放触区，避免手指在切上下文时被清掉。
        public bool IsExplorationInputReady => !HasFocus && !IsPaused && !IsMenuOpen &&
            appliedInputContext == GameplayInputContext.Gameplay && lastReadInputContext == GameplayInputContext.Gameplay;
        internal bool IsFocusReady => tableFocus?.IsReady ?? false;
        internal JinxCasinoStation FocusedStation => focusedStation;
        internal JinxCasinoS1Presentation FocusedPresentation => focusedPresentation;
        internal JinxCasinoShopCounter ShopCounter => shopCounter;
        public GameplayInputSettings InputSettings => immersionInput?.Settings ?? inputSettings.Copy();

        // 场景只装配一次依赖；Core输入在导航稳定后激活，不占用启动菜单的EventSystem。
        internal void Bind(JinxCasinoGame game, Transform root, Camera camera, InputActionAsset actions,
            JinxCasinoGameSettings settings, JinxCasinoSceneEffects sceneEffects, JinxCasinoShopCounter counter,
            JinxCasinoExitTerminal[] terminals, Action leaveScene)
        {
            Game = game; sceneRoot = root; Camera = camera; immersionInputAsset = actions;
            configuration = settings; effects = sceneEffects; shopCounter = counter;
            Clock.BindPauseSource(() => IsPaused);
            Tutorial = new JinxCasinoTutorialGuide(game, this, settings);
            Exit = new JinxCasinoExitInteraction(game, this, terminals, leaveScene);
        }

        internal void Activate()
        {
            if (Camera == null || immersionInputAsset == null || configuration == null)
                throw new InvalidOperationException("单机场景缺少主相机、输入或玩法配置。");
            Body = Camera.GetComponentInParent<CharacterController>();
            if (Body == null) throw new InvalidOperationException("主相机必须挂在本地玩家移动组件下。");
            yaw = Camera.transform.eulerAngles.y;
            active = true;
        }

        internal void Configure(JinxCasinoGameSettings settings, JinxCasinoSceneEffects sceneEffects)
        { configuration = settings; effects = sceneEffects; effects?.BindPresentationClock(Clock); Tutorial?.SetConfiguration(settings); }

        public void BindTouchPads(TouchInputPad move, TouchInputPad look)
        { movePad?.ResetInput(); lookPad?.ResetInput(); movePad = move; lookPad = look; }

        internal void ApplyInputSettings(GameplayInputSettings settings)
        { inputSettings = settings.Copy(); immersionInput?.ApplySettings(settings); }

        internal void SetApplicationFocus(bool focused)
        { hasFocus = focused; immersionInput?.PauseState.SetApplicationFocus(focused); if (!focused) { movePad?.ResetInput(); lookPad?.ResetInput(); } }
        internal void SetApplicationPaused(bool paused)
        { isApplicationPaused = paused; immersionInput?.PauseState.SetApplicationPaused(paused); if (paused) { movePad?.ResetInput(); lookPad?.ResetInput(); } }

        internal void Teleport(Vector3 position)
        {
            if (Body == null) return;
            bool enabled = Body.enabled; Body.enabled = false;
            Body.transform.position = position; Body.enabled = enabled;
        }

        internal void NotifyChanged() => Changed?.Invoke();

        public void Interact()
        {
            if (!Game.HasAdventure || IsMenuOpen || !AcceptsCommands || Body == null) return;
            var nearest = FindNearbyStation();
            if (Exit.PreferNearby(nearest)) { Exit.Interact(); return; }
            if (PreferNearbyShop(nearest))
            { if (!TryOpenShop()) Game.SetStatus("柜台暂不可操作。"); return; }
            if (nearest != null)
            { if (!TryOpenTable(nearest)) Game.SetStatus("此机台暂不可操作。"); }
            else Game.SetStatus("靠近机台后按E或触碰交互按钮。");
        }

        private CasinoAdventureResult UseItem(string itemId, string targetId)
        {
            var item = CasinoContentCatalog.FindItem(itemId);
            if (item?.IsPrank == true && effects != null && !effects.CanApplyPrank(targetId))
            {
                Game.SetStatus("目标不存在或仍在五秒保护内，库存保留。");
                return new CasinoAdventureResult { Error = "TargetProtected", Description = Game.Status, Balance = Game.State?.Coins ?? 0 };
            }
            return Game.UseItem(itemId, targetId, Time.frameCount);
        }


        /// 暂停覆盖领域、探索和桌面；其它场景演出须读取同一状态。
        public bool IsPaused => immersionModalPaused || (immersionInput?.PauseState.IsPaused ?? false);
        /// 当前具体桌面及只读展示状态，HUD和物件表现消费此副本。
        public JinxCasinoTableView TableView { get; private set; }
        /// 最近一次桌面操作的直接反馈。
        public string TableFeedback { get; private set; }
        /// HUD仅展示操作失败原因，成功状态由专属桌面物件呈现。
        public bool HasTableFeedbackError { get; private set; }
        /// 当前物件是否仍在演出，不能提前宣称结果已经展示。
        public bool IsTableAnimating => focusedPresentation != null && focusedPresentation.IsAnimating;
        /// 当前设备用于切换按键图标，不读取全局Gamepad.current。
        public InputDeviceKind DeviceKind => immersionInput?.DeviceKind ?? InputDeviceKind.KeyboardMouse;
        /// 暂停面板及设备提示订阅，离场随宿主释放。
        public event Action Changed;

        /// <summary>返回当前聚焦桌面的最新公开视图，避免表现使用上一帧缓存。</summary>
        /// <param name="station">具体机台对象，不能只传玩法类型。</param>
        /// <returns>当前聚焦对象的视图，否则null，由场景观察者读取原局。</returns>
        public JinxCasinoTableView GetFocusedTableView(JinxCasinoStation station)
            => focusedStation == station ? tableSession?.GetView() : null;

        /// <summary>由保存的样板菜单声明屏幕导航范围与初始控件，不能代为点击。</summary>
        /// <param name="open">菜单是否打开。</param>
        /// <param name="pauseClock">是否冻结单机时钟。</param>
        /// <param name="firstSelection">菜单保存的首个可用控件。</param>
        /// <param name="cancel">由菜单统一处理取消；为空使用暂停/继续操作。</param>
        public void SetMenuState(bool open, bool pauseClock, GameObject firstSelection, Action cancel = null)
        {
            immersionMenuCancel = open ? cancel : null;
            immersionScreenOpen = open; immersionModalPaused = open && pauseClock; immersionFirstSelection = firstSelection;
            appliedInputContext = null;
            if (immersionInput != null) ApplyContext(open ? GameplayInputContext.Menu : tableFocus.IsActive ? GameplayInputContext.Interaction : GameplayInputContext.Gameplay);
        }

        /// 查找实际三米范围内的可用机台，HUD提示与交互提交使用同一目标。
        internal JinxCasinoStation FindNearbyStation()
        {
            if (Body == null) return null;
            return sceneRoot.GetComponentsInChildren<JinxCasinoStation>()
                .Where(station => station.isActiveAndEnabled && (station.InteractionPosition - Body.transform.position).sqrMagnitude <= 9)
                .OrderBy(station => (station.InteractionPosition - Body.transform.position).sqrMagnitude).FirstOrDefault();
        }

        private void EnsureInput()
        {
            if (immersionInput != null) return;
            effects?.BindPresentationClock(Clock);
            previousCursorLock = Cursor.lockState; previousCursorVisible = Cursor.visible; cursorCaptured = true;
            immersionInput = new GameplayInputRouter(immersionInputAsset, inputSettings, "Exploration", "Table");
            immersionInput.DeviceChanged += OnDeviceChanged;
            immersionInput.PauseState.Changed += OnPauseChanged;
            immersionInput.PauseState.SetApplicationFocus(hasFocus);
            immersionInput.PauseState.SetApplicationPaused(isApplicationPaused);
            tableFocus = new JinxCasinoTableFocus(Camera);
            tableSelection = new JinxCasinoTableSelection();
            if (EventSystem.current != null)
            {
                immersionMenu = new MenuInputScope(EventSystem.current, immersionInput);
                tablePointer = new PointerEventData(EventSystem.current);
            }
        }

        internal void Tick()
        {
            if (!active || !AcceptsCommands) return;
            EnsureInput();
            var context = IsPaused || IsMenuOpen ? GameplayInputContext.Menu :
                tableFocus.IsActive ? GameplayInputContext.Interaction : GameplayInputContext.Gameplay;
            ApplyContext(context);
            Vector2 movement = movePad != null ? movePad.Move : Vector2.zero;
            Vector2 look = lookPad != null ? lookPad.ConsumeLook() : Vector2.zero;
            immersionInput.SetTouchFrame(movement, look, (movement - previousTouchMove).sqrMagnitude + look.sqrMagnitude > 0.0001f);
            previousTouchMove = movement;
            var frame = immersionInput.ReadFrame(Time.unscaledDeltaTime, configuration.LookSensitivity,
                context == GameplayInputContext.Gameplay && Cursor.lockState == CursorLockMode.Locked);
            lastReadInputContext = context;
            var actions = immersionInput.ConsumeActions();
            immersionMenu?.Update();
            if ((actions & GameplayInputActions.Pause) != 0)
            {
                if (immersionMenuCancel != null) immersionMenuCancel();
                else if (IsPaused) Resume(); else Pause();
            }
            Clock.Advance(Time.unscaledDeltaTime, Time.frameCount);
            float presentationDelta = Clock.GetDeltaSeconds(Time.frameCount);
            // 每帧都Tick，以便机台销毁后也恢复借用相机，不能先按IsActive提前跳过。
            tableFocus.Tick(presentationDelta);
            if (tableSession != null && (!tableFocus.IsActive || focusedStation == null)) CloseTable();
            if (HasShopBinding && (!tableFocus.IsActive || focusedShop == null || !focusedShop.isActiveAndEnabled)) CloseTable();
            if (IsPaused) return;
            Game.Tick(presentationDelta);
            Exit.Tick();
            Tutorial.UpdateSceneFacts();
            if (IsMenuOpen) return;
            if (tableFocus.IsActive)
            {
                if ((actions & GameplayInputActions.Back) != 0) { CloseTable(true); return; }
                if (!tableFocus.IsReady || tableSession == null && !HasShopFocus) return;
                if (HasShopFocus) RefreshShop(); else RefreshTable();
                UpdateTableInput(frame, actions);
                return;
            }
            if ((actions & GameplayInputActions.Interact) != 0) { Interact(); if (tableFocus.IsActive || IsMenuOpen) return; }
            float beforeTutorialYaw = yaw, beforeTutorialPitch = pitch;
            Vector3 beforeTutorialPosition = Body.transform.position;
            if (presentationDelta > 0)
            { yaw += frame.LookDegrees.x; pitch = Mathf.Clamp(pitch - frame.LookDegrees.y, -70, 70); }
            Body.transform.rotation = Quaternion.Euler(0, yaw, 0);
            Camera.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
            float speed = configuration.MovementSpeed * (effects != null ? effects.LocalMovementMultiplier : 1);
            Body.Move((Body.transform.right * frame.Move.x + Body.transform.forward * frame.Move.y) * (speed * presentationDelta)
                + Vector3.down * (3 * presentationDelta));
            Tutorial.ObserveExploration(Mathf.DeltaAngle(beforeTutorialYaw, yaw), pitch - beforeTutorialPitch,
                Vector3.ProjectOnPlane(Body.transform.position - beforeTutorialPosition, Vector3.up).magnitude, frame.Move.sqrMagnitude > .0001f);
        }

        private void ApplyContext(GameplayInputContext context)
        {
            if (appliedInputContext == context) return;
            lastReadInputContext = null;
            movePad?.ResetInput(); lookPad?.ResetInput();
            immersionInput.SetContext(context);
            immersionMenu?.SetContext(context, immersionFirstSelection != null ? immersionFirstSelection : EventSystem.current?.currentSelectedGameObject);
            appliedInputContext = context; navigationDelay = 0; previousNavigation = 0;
            UpdateCursor();
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
                tableSelection.Point(Camera.ScreenPointToRay(frame.PointerPosition));
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

        private bool TryOpenTable(JinxCasinoStation station)
        {
            if (immersionInput == null || IsPaused || IsMenuOpen || station == null || !station.HasTableInteraction) return false;
            if (!tableFocus.TryEnter(station)) return false;
            focusedStation = station;
            focusedPresentation = station.GetComponent<JinxCasinoS1Presentation>();
            tableSession = new JinxCasinoTableSession(Game, station);
            foreach (var target in station.Targets) if (target != null)
            { target.BindPresentationClock(Clock); target.Invoked += OnTableTargetInvoked; }
            tableSelection.Bind(station); TableFeedback = null; HasTableFeedbackError = false;
            RefreshTable(); ApplyContext(GameplayInputContext.Interaction);
            return true;
        }

        private void OnTableTargetInvoked(JinxCasinoTableTarget target) => ApplyTableCommand(target.Action, target.Value);
        private void ApplyTableCommand(JinxCasinoTableAction action, int value)
        {
            if (tableSession == null || !tableFocus.IsReady || IsPaused) return;
            if (focusedPresentation != null && focusedPresentation.IsAnimating && action != JinxCasinoTableAction.Help)
            { TableFeedback = "请等机台完成当前动作。"; HasTableFeedbackError = false; return; }
            var result = tableSession.Apply(action, value, Time.frameCount);
            TableFeedback = result.Description; HasTableFeedbackError = !result.Success;
            Tutorial.ObserveTableCommand(action, result);
            RefreshTable(); Changed?.Invoke();
        }

        private void RefreshTable()
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
        public void CloseTable(bool playerInitiated = false)
        {
            if (playerInitiated) Tutorial.RegisterPlayerExit();
            CloseShop();
            if (focusedStation != null)
                foreach (var target in focusedStation.Targets) if (target != null) target.Invoked -= OnTableTargetInvoked;
            tableSession?.Close(); tableSession = null; focusedStation = null; focusedPresentation = null;
            tableSelection?.Bind(null); tableFocus?.Exit(); TableView = null; TableFeedback = null; HasTableFeedbackError = false;
        }

        internal void ResetForRunReplacement()
        {
            Tutorial.Reset();
            Exit.Reset();
            CloseTable();
            tableFocus?.RestoreImmediately();
            movePad?.ResetInput(); lookPad?.ResetInput();
        }

        /// 明确打开单机暂停；不写全局timeScale。
        public void Pause() => immersionInput?.PauseState.RequestPause(LocalPauseReason.User);
        /// 玩家明确继续，仍在后台或手柄未恢复时不解除暂停。
        public bool Resume() => immersionInput?.PauseState.TryResume() ?? false;

        private void OnPauseChanged()
        {
            Clock?.SetPaused(IsPaused);
            movePad?.ResetInput(); lookPad?.ResetInput();
            UpdateCursor(); Changed?.Invoke();
        }
        private void OnDeviceChanged(InputDeviceKind device) { UpdateCursor(); Changed?.Invoke(); }
        private void UpdateCursor()
        {
            bool locked = !IsPaused && appliedInputContext == GameplayInputContext.Gameplay && DeviceKind == InputDeviceKind.KeyboardMouse;
            if (locked && Cursor.lockState != CursorLockMode.Locked) immersionInput?.DiscardNextLookDelta();
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked && DeviceKind == InputDeviceKind.KeyboardMouse;
        }
        public void Dispose()
        {
            active = false; immersionMenuCancel = null;
            CloseTable(); tableFocus?.Dispose(); tableSelection?.Dispose(); immersionMenu?.Dispose();
            if (immersionInput != null)
            {
                immersionInput.DeviceChanged -= OnDeviceChanged;
                immersionInput.PauseState.Changed -= OnPauseChanged;
                immersionInput.Dispose(); immersionInput = null;
            }
            if (cursorCaptured) { Cursor.lockState = previousCursorLock; Cursor.visible = previousCursorVisible; cursorCaptured = false; }
            Changed = null;
        }

        public bool HasShopFocus => focusedShop != null;
        private bool HasShopBinding => !ReferenceEquals(focusedShop, null);
        public string ShopFeedback { get; private set; }

        /// 玩家与柜台接近锚点的真实距离，两端采用同一交互范围。
        public bool IsShopNearby => shopCounter != null && shopCounter.isActiveAndEnabled && Body != null &&
            (shopCounter.InteractionPosition - Body.transform.position).sqrMagnitude <= 9;

        private bool PreferNearbyShop(JinxCasinoStation station) => IsShopNearby && (station == null ||
            (shopCounter.InteractionPosition - Body.transform.position).sqrMagnitude < (station.InteractionPosition - Body.transform.position).sqrMagnitude);

        private bool TryOpenShop()
        {
            if (immersionInput == null || !IsShopNearby || IsPaused || IsMenuOpen ||
                !tableFocus.TryEnter(shopCounter, shopCounter.FocusPose, 58)) return false;
            focusedShop = shopCounter; selectedShopProduct = 0; lastShopActionFrame = -1; ShopFeedback = null;
            focusedShopTargets = focusedShop.Targets;
            foreach (var target in focusedShopTargets) { target.BindPresentationClock(Clock); target.Invoked += OnShopTargetInvoked; }
            tableSelection.Bind(focusedShop.transform, focusedShop.Targets);
            RefreshShop(); ApplyContext(GameplayInputContext.Interaction); Changed?.Invoke();
            return true;
        }

        private void OnShopTargetInvoked(JinxCasinoTableTarget target) => ApplyShopCommand(target.Action, target.Value);

        private void ApplyShopCommand(JinxCasinoTableAction action, int value)
        {
            if (focusedShop == null || !tableFocus.IsReady || IsPaused || !IsShopNearby || !Game.HasAdventure ||
                lastShopActionFrame == Time.frameCount) return;
            lastShopActionFrame = Time.frameCount;
            if (action == JinxCasinoTableAction.SelectProduct)
            {
                if (focusedShop.Product(value) == null) return;
                selectedShopProduct = value; ShopFeedback = "已选择，尚未扣款；按购买确认。";
            }
            else
            {
                var item = focusedShop.Product(selectedShopProduct);
                if (item == null) return;
                CasinoAdventureResult result = null;
                if (action == JinxCasinoTableAction.PurchaseProduct) result = Game.PurchaseItem(item.Id, Time.frameCount);
                else if (action == JinxCasinoTableAction.UseProduct || action == JinxCasinoTableAction.Secondary)
                    result = Game.State.PreparedItems.Contains(item.Id) ? Game.CancelPreparedItem(item.Id, Time.frameCount) : UseItem(item.Id, "team");
                else if (action == JinxCasinoTableAction.Help) ShopFeedback = item.Description;
                if (result != null)
                {
                    ShopFeedback = result.Description ?? result.Error;
                    Tutorial.ObserveShopCommand(action, item.Id, result);
                }
            }
            RefreshShop(); Changed?.Invoke();
        }

        private void RefreshShop() => focusedShop?.Present(Game.State, selectedShopProduct, ShopFeedback);

        private void CloseShop()
        {
            // 组件可能先于其物件销毁；用进入时的目标集合退订，不能依赖Unity fake-null组件。
            foreach (var target in focusedShopTargets) if (target != null) target.Invoked -= OnShopTargetInvoked;
            focusedShopTargets = Array.Empty<JinxCasinoTableTarget>();
            focusedShop = null; ShopFeedback = null;
        }
    }
}
