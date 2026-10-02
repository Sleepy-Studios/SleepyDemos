using Hotfix.JinxCasino.Presentation;
using Hotfix.JinxCasino.Interaction;
using Hotfix.JinxCasino.UI;
using Hotfix.JinxCasino.Rules;
using System.Linq;
using UnityEngine.InputSystem;
using System;
using System.Threading;
using Core.Runtime;
using Cysharp.Threading.Tasks;
using Hotfix.SceneManagement;
using UnityEngine;

namespace Hotfix.JinxCasino
{
    /// 单机场景入口，负责初始化、菜单和离场；冒险规则由游戏对象管理。
    [DefaultExecutionOrder(-500)]
    public sealed class JinxCasinoController : MonoBehaviour
    {
        [SerializeField] private Camera worldCamera;
        [SerializeField] private JinxCasinoGameSettings gameSettings;
        [SerializeField] private JinxCasinoWorldArea[] areas;
        [SerializeField] private JinxCasinoSceneEffects sceneEffects;
        [SerializeField] private InputActionAsset immersionInputAsset;
        [SerializeField] private JinxCasinoShopCounter shopCounter;
        [SerializeField] private JinxCasinoExitTerminal[] exitTerminals;
        private int displayedArea = -1;

        /// 三个具体对象构造不读档；场景Awake接线，导航稳定后才激活玩家输入。
        public JinxCasinoGame Game { get; } = new JinxCasinoGame();
        public JinxCasinoPlayerInteraction Player { get; } = new JinxCasinoPlayerInteraction();
        public JinxCasinoLocalSettings Settings { get; } = new JinxCasinoLocalSettings();
        public bool HasInputConfiguration => immersionInputAsset != null;
        private View hud;
        private CancellationTokenSource lifetime;
        private bool isExiting;

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
                Player.Activate();
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

        private void Update()
        {
            Game.CommandInputEnabled = !IsBusy;
            Player.Tick();
        }

        private void OnApplicationFocus(bool focused) => Player.SetApplicationFocus(focused);
        private void OnApplicationPause(bool paused) => Player.SetApplicationPaused(paused);

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
            SaveAdventureBeforeExit();
            Player.Dispose();
            ReleaseGameSubscriptions();
            Game.CommandInputEnabled = false;
            lifetime?.Cancel();
            Settings.Changed -= ApplyLocalSettings;
            Changed = null;
            lifetime?.Dispose();
            lifetime = null;
        }
        private void Awake()
        {
            Player.Bind(Game, transform, worldCamera, immersionInputAsset, gameSettings, sceneEffects, shopCounter, exitTerminals, RequestExit);
            Settings.Changed += ApplyLocalSettings;
            ApplyLocalSettings();
            Game.Changed += OnGameChanged;
            Game.BeforeRunReplacement += OnBeforeRunReplacement;
            Game.ValidatingRestore += ValidateImmersionRestore;
            Game.BeforeSave += OnBeforeGameSave;
        }

        private void OnBeforeGameSave() => Player.Tutorial.FlushMovement(true, false);
        private void OnBeforeRunReplacement(bool restoring)
        {
            Player.ResetForRunReplacement();
            displayedArea = -1;
            if (restoring) sceneEffects?.ClearEffects();
        }

        private void ApplyLocalSettings()
        {
            var preferences = Settings.Value;
            Player.ApplyInputSettings(preferences.ToInputSettings());
            GetComponent<JinxCasinoAudioDirector>()?.SetVolume(preferences.Volume, preferences.Muted);
        }

        /// 当前已装配区域的安全位置，供环境演出查询。
        public Vector3 CurrentAdventureSafePosition => areas?.FirstOrDefault(area => area != null && area.Index == displayedArea)?.SafePosition ?? Vector3.zero;

        /// <summary>绑定当前场景的配置、区域和演出引用。</summary>
        /// <param name="configuration">新局使用的可编辑设置，不替换已经进行的局。</param>
        /// <param name="worldAreas">区域及安全位置。</param>
        /// <param name="effects">场景演出组件。</param>
        public void ConfigureAdventure(JinxCasinoGameSettings configuration, JinxCasinoWorldArea[] worldAreas, JinxCasinoSceneEffects effects)
        {
            gameSettings = configuration; areas = worldAreas; sceneEffects = effects;
            Player.Configure(configuration, effects);
        }

        /// <summary>从当前场景配置创建单机冒险，物理清理由Game替换事件统一处理。</summary>
        /// <param name="mode">标准、练习或无尽。</param>
        /// <param name="seed">可选固定种子，不设置必中结果。</param>
        public void StartAdventure(CasinoAdventureMode mode, uint? seed = null)
        {
            if (IsBusy) { Game.SetStatus("请先结束当前操作。"); return; }
            if (gameSettings == null) { Game.SetStatus("场景缺少玩法配置。"); return; }
            Game.StartAdventure(mode, gameSettings.CreateConfig(), seed);
        }

        /// <summary>先检查场景是否可继续候选旅程，再安装；失败不清焦点或替换当前局。</summary>
        /// <param name="slot">玩家明确选择的1到3槽。</param>
        public bool LoadAdventure(int slot) => !IsBusy && Game.LoadAdventure(slot);

        private void ValidateImmersionRestore(CasinoAdventureState candidate)
        {
            const string unavailable = "此存档需要当前场景尚未开放的内容，无法在此继续；原存档和当前旅程均已保留。";
            if (candidate.PlayerCount != 1 || areas == null || gameSettings == null) throw new InvalidOperationException(unavailable);
            int configuredStages = gameSettings.CreateConfig().StageCount;
            if (candidate.Mode == CasinoAdventureMode.Endless && configuredStages == 1 ||
                candidate.Mode == CasinoAdventureMode.Standard && (candidate.Config.StageCount > configuredStages ||
                    Enumerable.Range(0, candidate.Config.StageCount).Any(index => !areas.Any(area => area != null && area.Index == index % 4))))
                throw new InvalidOperationException(unavailable);
            int areaIndex = candidate.StageIndex % 4;
            if (!areas.Any(area => area != null && area.Index == areaIndex)) throw new InvalidOperationException(unavailable);
            if (string.IsNullOrEmpty(candidate.ActiveRoundJson)) return;
            bool playable = !string.IsNullOrEmpty(candidate.ActiveStationId) && GetComponentsInChildren<JinxCasinoStation>(true).Any(station =>
                station.HasTableInteraction && station.AreaIndex == areaIndex && station.Game == candidate.ActiveGame && station.StationId == candidate.ActiveStationId);
            if (!playable) throw new InvalidOperationException(unavailable);
        }

        // 只同步场景表现；规则状态缓存、成长和自动保存已经由Game完成。
        private void OnGameChanged(CasinoSceneEffect[] effects)
        {
            var state = Game.State;
            sceneEffects?.SynchronizeState(state, Game.IsRestoring);
            sceneEffects?.ApplyEffects(effects);
            if (state != null && areas != null)
            {
                int currentArea = Mathf.Clamp(state.StageIndex % 4, 0, 3);
                bool practice = state.Mode == CasinoAdventureMode.Practice;
                foreach (var area in areas) if (area != null) area.SetUnlocked(practice || area.Index <= currentArea);
                if (displayedArea != currentArea)
                {
                    Player.ResetForRunReplacement(); displayedArea = currentArea;
                    var area = areas.FirstOrDefault(candidate => candidate != null && candidate.Index == currentArea);
                    if (area != null) Player.Teleport(area.SafePosition);
                }
            }
            Changed?.Invoke();
        }

        private void SaveAdventureBeforeExit()
        {
            if (Game.HasAdventure && Game.SelectedSaveSlot > 0) Game.SaveAdventure(Game.SelectedSaveSlot);
            sceneEffects?.ClearEffects();
        }

        private void ReleaseGameSubscriptions()
        {
            Game.Changed -= OnGameChanged;
            Game.BeforeRunReplacement -= OnBeforeRunReplacement;
            Game.ValidatingRestore -= ValidateImmersionRestore;
            Game.BeforeSave -= OnBeforeGameSave;
        }
    }
}
