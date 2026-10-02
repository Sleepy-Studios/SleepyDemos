using Core.Runtime.Inputs;
using System;
using System.Threading;
using Core.Runtime;
using Core.Runtime.Networking;
using Cysharp.Threading.Tasks;
using Hotfix.JinxCasino.Adapters.UI;
using Hotfix.JinxCasino.Rules;
using Hotfix.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TMPro;

namespace Hotfix.JinxCasino.Adapters
{
    /// P0 双平台接入宿主；只承担输入、导航和会话生命周期，开奖由纯规则协调器完成。
    public sealed partial class JinxCasinoController : MonoBehaviour
    {
        [SerializeField] private Camera worldCamera;
        [SerializeField] private NetworkSessionSettings settings;
        [SerializeField] private Transform[] stationAnchors;
        private INetworkSessionService network;
        private CasinoNetworkCoordinator coordinator;
        private View hud;
        private CancellationTokenSource lifetime;
        private CharacterController body;
        private TouchInputPad movePad;
        private TouchInputPad lookPad;
        private float yaw;
        private float pitch;
        private float poseClock;
        private bool isBusy;
        private bool isExiting;
        private bool hasFocus = true;
        private bool isApplicationPaused;

        /// P0 状态与错误信息。
        public string Status { get; private set; } = "P0：选择互联网房间或明确的离线规则验证。";
        /// 当前已提交的共享钱包。
        public long Balance => adventureState?.Coins ?? coordinator?.Session?.Balance ?? 0;
        /// 真正的传输性质，空闲时没有会话。
        public NetworkTransportKind? TransportKind => adventure != null ? NetworkTransportKind.Offline : network?.CurrentSession?.TransportKind;
        /// 当前房间码，离线会话不作为可分享的互联网房间。
        public string RoomCode => network?.CurrentSession?.Code.ToString() ?? string.Empty;
        /// 最近一笔本地请求的结果。
        public CasinoBetReceipt LastReceipt => coordinator?.LastReceipt;
        /// 按钮操作互斥锁。
        public bool IsBusy => isBusy || isExiting;
        /// 已拥有可以下注的已提交局状态。
        public bool HasRun => coordinator?.Session != null;
        /// 当前状态改变。
        public event Action Changed;

        /// <summary>由 Builder 装配固定场景引用。</summary>
        /// <param name="camera">场景唯一主相机，父级包含本地 CharacterController。</param>
        /// <param name="sessionSettings">PC / Android 共用的协议和 App ID 配置。</param>
        /// <param name="anchors">三个原型机台的交互锚点，正式小游戏接入继续使用。</param>
        public void Configure(Camera camera, NetworkSessionSettings sessionSettings, Transform[] anchors)
        {
            worldCamera = camera; settings = sessionSettings; stationAnchors = anchors;
        }

        private void Start() => InitializeAsync().Forget();

        private async UniTaskVoid InitializeAsync()
        {
            lifetime = new CancellationTokenSource();
            try
            {
                var navigator = GameSceneNavigator.Instance;
                if (navigator == null) throw new InvalidOperationException("请从 AppEntrance → Hub 进入倒霉蛋俱乐部。");
                await navigator.WaitUntilStableAsync(GameSceneId.JinxCasino, lifetime.Token);
                body = worldCamera.GetComponentInParent<CharacterController>();
                yaw = worldCamera.transform.eulerAngles.y;
                hud = await ShowLocalHudAsync();
                Changed?.Invoke();
            }
            catch (OperationCanceledException) { }
            catch (Exception exception) { Status = exception.Message; Debug.LogException(exception, this); Changed?.Invoke(); }
        }

        private async UniTask<View> ShowLocalHudAsync()
        {
            if (UsesImmersion)
            {
                var result = await UIManager.Instance.ShowAsync<JinxCasinoImmersionHudView, JinxCasinoController>(this,
                    new UIShowOptions(animated: false), lifetime.Token);
                if (result.Status == UIOperationStatus.Failed) throw result.Exception;
                return result.View;
            }
            var legacy = await UIManager.Instance.ShowAsync<JinxCasinoHudView, JinxCasinoController>(this,
                new UIShowOptions(animated: false), lifetime.Token);
            if (legacy.Status == UIOperationStatus.Failed) throw legacy.Exception;
            return legacy.View;
        }

        /// <summary>为当前 View 注册两块独立触控区域。</summary>
        /// <param name="move">移动区域，界面释放时传 null。</param>
        /// <param name="look">视角区域，界面释放时传 null。</param>
        public void BindTouchPads(TouchInputPad move, TouchInputPad look)
        {
            movePad?.ResetInput(); lookPad?.ResetInput(); movePad = move; lookPad = look;
        }

        /// <summary>显式创建离线单人规则验证，不连接 Photon。</summary>
        /// <param name="displayName">本地显示名。</param>
        public void StartOffline(string displayName) => ConnectAsync(true, null, displayName).Forget();
        /// <summary>创建真实互联网私人房间。</summary>
        /// <param name="displayName">本地显示名。</param>
        public void CreateRoom(string displayName) => ConnectAsync(false, null, displayName).Forget();
        /// <summary>根据含区域的房间码加入互联网会话。</summary>
        /// <param name="code">用户复制的房间码。</param>
        /// <param name="displayName">本地显示名。</param>
        public void JoinRoom(string code, string displayName) => ConnectAsync(false, code, displayName).Forget();

        private async UniTaskVoid ConnectAsync(bool offline, string code, string displayName)
        {
            if (IsBusy || lifetime == null) return;
            isBusy = true;
            CancellationToken token = lifetime.Token;
            try
            {
                await DisconnectAsync();
                ClearAdventureForLegacy();
                network = offline ? new OfflineLocalNetworkSessionService() : NetworkSessionServices.CreateInternet(settings);
                network.StateChanged += OnNetworkStateChanged;
                coordinator = new CasinoNetworkCoordinator(network);
                coordinator.Changed += OnCoordinatorChanged;
                Status = offline ? "正在创建离线规则验证…" : "正在连接 Photon…";
                Changed?.Invoke();
                if (code == null)
                    await network.CreateAsync(new NetworkCreateOptions
                    {
                        Region = settings.DefaultRegion, ProtocolVersion = settings.ProtocolVersion,
                        ContentVersion = settings.ContentVersion, MaxPlayers = offline ? 1 : settings.MaxPlayers,
                        LocalDisplayName = string.IsNullOrWhiteSpace(displayName) ? "倒霉蛋" : displayName
                    }, token);
                else
                    await network.JoinAsync(new NetworkJoinOptions
                    {
                        Code = code, ContentVersion = settings.ContentVersion,
                        DisplayName = string.IsNullOrWhiteSpace(displayName) ? "倒霉蛋" : displayName
                    }, token);
                token.ThrowIfCancellationRequested();
                if (network.LocalMemberId == network.AuthorityMemberId)
                    await coordinator.InitializeRunAsync(20261001, token);
                Status = offline ? "离线规则验证 · 仅本机1人 · 不代表 Photon 联机通过" : "互联网房间已连接 · " + network.Members.Count + " 人";
            }
            catch (OperationCanceledException) { await DisconnectAsync(); }
            catch (Exception exception) { await DisconnectAsync(); Status = exception.Message; }
            finally { isBusy = false; if (!isExiting) Changed?.Invoke(); }
        }

        /// <summary>提交一次 P0 机台请求；UI 不提前扣除团队筹码。</summary>
        /// <param name="game">当前三种规则之一。</param>
        /// <param name="stake">正整数投入。</param>
        /// <param name="choice">轮盘 0..36、硬币 0/1、水果机 0。</param>
        public void Bet(CasinoGameKind game, long stake, int choice) => BetAsync(game, stake, choice).Forget();

        private async UniTaskVoid BetAsync(CasinoGameKind game, long stake, int choice)
        {
            if (IsBusy || !HasRun) return;
            isBusy = true; Changed?.Invoke();
            try { await coordinator.BetAsync(game, stake, choice, lifetime.Token); }
            catch (OperationCanceledException) { }
            catch (Exception exception) { Status = exception.Message; }
            finally { isBusy = false; if (!isExiting) Changed?.Invoke(); }
        }

        private void OnCoordinatorChanged()
        {
            if (!string.IsNullOrEmpty(coordinator.LastError)) Status = coordinator.LastError;
            Changed?.Invoke();
        }
        private void OnNetworkStateChanged(NetworkSessionState state) => Changed?.Invoke();

        /// 离开当前房间，保留场景与输入。
        public void LeaveRoom() => LeaveRoomAsync().Forget();
        private async UniTaskVoid LeaveRoomAsync()
        {
            if (IsBusy) return;
            isBusy = true;
            try { await DisconnectAsync(); Status = "已离开房间。"; }
            finally { isBusy = false; Changed?.Invoke(); }
        }

        private async UniTask DisconnectAsync()
        {
            if (coordinator != null) { coordinator.Changed -= OnCoordinatorChanged; coordinator.Dispose(); coordinator = null; }
            var previous = network; network = null;
            if (previous == null) return;
            previous.StateChanged -= OnNetworkStateChanged;
            try { await previous.LeaveAsync(); }
            finally { previous.Dispose(); }
        }

        private void Update()
        {
            if (UsesImmersion) { UpdateImmersion(); return; }
            if (isExiting || !hasFocus || isApplicationPaused || worldCamera == null || body == null) return;
            UpdateAdventure(Time.deltaTime);
            if (IsAdventureInputBlocked) { movePad?.ResetInput(); lookPad?.ResetInput(); return; }
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            var input = selected != null ? selected.GetComponent<TMP_InputField>() : null;
            bool isTyping = input != null && input.isFocused;
            if (isTyping) { movePad?.ResetInput(); lookPad?.ResetInput(); return; }
            Vector2 movement = movePad != null ? movePad.Move : Vector2.zero;
            Vector2 touchRotation = lookPad != null ? lookPad.ConsumeLook() : Vector2.zero;
            Vector2 mouseRotation = Vector2.zero;
            var keyboard = Keyboard.current;
            if (!isTyping && keyboard != null)
            {
                if (keyboard.eKey.wasPressedThisFrame)
                {
                    InteractWithNearbyStation();
                    if (IsAdventureInputBlocked) return;
                }
                movement += new Vector2((keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0),
                    (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0));
                if (keyboard.escapeKey.wasPressedThisFrame) { movePad?.ResetInput(); lookPad?.ResetInput(); }
            }
            if (!isTyping && Mouse.current != null && Mouse.current.rightButton.isPressed)
                mouseRotation = Mouse.current.delta.ReadValue();
            Vector2 rotation = ScaleLocalLookInput(touchRotation, mouseRotation);
            float sensitivity = gameSettings != null ? gameSettings.LookSensitivity : 0.12f;
            yaw += rotation.x * sensitivity;
            pitch = Mathf.Clamp(pitch - rotation.y * sensitivity, -70, 70);
            body.transform.rotation = Quaternion.Euler(0, yaw, 0);
            worldCamera.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
            movement = Vector2.ClampMagnitude(movement, 1);
            float speed = (gameSettings != null ? gameSettings.MovementSpeed : 3.5f) * (sceneEffects != null ? sceneEffects.LocalMovementMultiplier : 1f);
            body.Move((body.transform.right * movement.x + body.transform.forward * movement.y) * (speed * Time.deltaTime)
                + Vector3.down * (3f * Time.deltaTime));
            poseClock += Time.deltaTime;
            if (network?.CurrentSession != null && poseClock >= 0.1f)
            {
                poseClock = 0;
                network.PublishAvatarPose(new NetworkAvatarPose(body.transform.position, body.transform.rotation, pitch));
            }
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

        /// 先收口会话和具体 View，再返回 Hub。
        public void RequestExit() { if (!IsBusy) ExitAsync().Forget(); }
        /// 独立包返回本游戏主菜单，Editor的Hub接入保持原行为。
        public bool IsStandalonePlayer => GameSceneNavigator.Instance?.StandaloneScene == GameSceneId.JinxCasino;
        /// 仅独立包主菜单接受退出应用，不用此方法丢弃正在进行的旅程。
        public void QuitStandaloneApplication() { if (IsStandalonePlayer && !HasAdventure && !IsBusy) Application.Quit(); }
        private async UniTaskVoid ExitAsync()
        {
            isExiting = true;
            SaveAdventureBeforeExit();
            try
            {
                await DisconnectAsync();
                if (hud != null) { await UIManager.Instance.CloseAsync(hud); hud = null; }
                var result = IsStandalonePlayer
                    ? await GameSceneNavigator.Instance.ReloadCurrentAsync()
                    : await GameSceneNavigator.Instance.SwitchAsync(GameSceneId.Hub);
                if (result.Status != GameSceneSwitchStatus.Succeeded && result.Status != GameSceneSwitchStatus.Ignored)
                {
                    if (this != null) Status = result.Error ?? "导航繁忙，请稍后再试。";
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception exception)
            {
                if (this != null) { Status = exception.Message; Debug.LogException(exception, this); }
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
                            if (this != null) { Status = exception.Message; Debug.LogException(exception, this); }
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
            lifetime?.Cancel();
            var previousCoordinator = coordinator; coordinator = null;
            previousCoordinator?.Dispose();
            var previousNetwork = network; network = null;
            if (previousNetwork != null) { previousNetwork.StateChanged -= OnNetworkStateChanged; previousNetwork.Dispose(); }
            movePad?.ResetInput(); lookPad?.ResetInput();
            Changed = null;
            lifetime?.Dispose();
            lifetime = null;
        }
    }
}
