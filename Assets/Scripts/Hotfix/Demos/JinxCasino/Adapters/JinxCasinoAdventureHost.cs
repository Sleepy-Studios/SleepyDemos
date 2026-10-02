using System;
using System.Linq;
using Hotfix.JinxCasino.Adapters.UI;
using Hotfix.JinxCasino.Rules;
using UnityEngine;

namespace Hotfix.JinxCasino.Adapters
{
    public sealed partial class JinxCasinoController
    {
        [SerializeField] private JinxCasinoGameSettings gameSettings;
        [SerializeField] private JinxCasinoWorldArea[] areas;
        [SerializeField] private JinxCasinoSceneEffects sceneEffects;
        private JinxCasinoAdventurePresenter adventurePresenter;
        private int displayedArea = -1;
        private bool adventureInputBlocked;

        /// 游戏在组件构造时已存在，供较早启用的机台表现绑定；构造不读档或加载场景。
        public JinxCasinoGame Game { get; } = new JinxCasinoGame();
        /// 场景广播已标明整蛊，不修改钱包。
        public string AdventureSceneAnnouncement => sceneEffects?.Announcement ?? string.Empty;
        /// 雷达只展示本机当前区域方向和距离。
        public string AdventureRadarHint => sceneEffects?.RadarHint ?? string.Empty;
        /// 墨迹边缘强度，中心机台操作区保持可用。
        public float AdventureInkIntensity => sceneEffects?.LocalInkIntensity ?? 0;
        /// 旧房间入口尚待删除，不与当前单机流程共享状态。
        public bool IsLegacySession => network != null || coordinator != null;
        /// 模态菜单锁移动及视角；普通桌面操作继续冒险时钟。
        public bool IsAdventureInputBlocked => (adventureInputBlocked || immersionScreenOpen) && !IsLegacySession;
        /// 当前区域已经装配的安全出生位置。
        public Vector3 CurrentAdventureSafePosition => areas?.FirstOrDefault(area => area != null && area.Index == displayedArea)?.SafePosition ?? Vector3.zero;

        private void Awake()
        {
            Game.Changed += OnGameChanged;
            Game.BeforeRunReplacement += OnBeforeRunReplacement;
            Game.ValidatingRestore += ValidateImmersionRestore;
            Game.BeforeSave += OnBeforeGameSave;
        }

        private void OnBeforeGameSave() => FlushTutorialMovement(true, false);
        private void OnBeforeRunReplacement(bool restoring)
        {
            ResetImmersionTableForRestore();
            displayedArea = -1;
            if (restoring) sceneEffects?.ClearEffects();
        }

        /// <summary>验证任务碰撞属于本地玩家。</summary>
        /// <param name="actor">碰撞体所在Transform；助手和非本地角色拒绝。</param>
        public bool IsLocalAdventureActor(Transform actor) => body != null && (actor == body.transform || actor.IsChildOf(body.transform));

        /// <summary>提交已经发生的实体任务交互，先检查真实接近距离。</summary>
        /// <param name="missionId">当前任务ID。</param>
        /// <param name="action">触发目标的领域动作。</param>
        /// <param name="pointIndex">唯一拾取/检查点序号。</param>
        /// <param name="worldPosition">目标实例的真实世界位置。</param>
        public CasinoAdventureResult InteractWithMission(string missionId, CasinoTaskAction action, int pointIndex, Vector3 worldPosition)
        {
            if (body == null || (body.transform.position - worldPosition).sqrMagnitude > 4)
                return new CasinoAdventureResult { Error = "请实际接近任务目标。" };
            string requestId = missionId + ":" + (int)action + ":" + pointIndex + ":local";
            return Game.InteractWithMission(requestId, missionId, action, pointIndex, "local");
        }

        /// <summary>绑定当前场景的配置、区域和演出引用。</summary>
        /// <param name="configuration">新局使用的可编辑设置，不替换已经进行的局。</param>
        /// <param name="worldAreas">区域及安全位置。</param>
        /// <param name="effects">场景演出组件。</param>
        public void ConfigureAdventure(JinxCasinoGameSettings configuration, JinxCasinoWorldArea[] worldAreas, JinxCasinoSceneEffects effects)
        {
            gameSettings = configuration; areas = worldAreas; sceneEffects = effects;
            if (UsesImmersion) sceneEffects?.BindPresentationClock(PresentationClock);
        }

        /// <summary>绑定模态界面的输入状态。</summary>
        /// <param name="presenter">尚待清理的原型页面实例，解绑传null。</param>
        /// <param name="blocked">是否锁住探索操作。</param>
        /// <param name="pausesClock">设置等菜单是否暂停本机逻辑时钟。</param>
        public void BindAdventurePresenter(JinxCasinoAdventurePresenter presenter, bool blocked, bool pausesClock = false)
        {
            adventurePresenter = presenter; adventureInputBlocked = blocked;
            immersionModalPaused = blocked && pausesClock;
            PresentationClock?.SetPaused(IsImmersionPaused);
            if (blocked) { movePad?.ResetInput(); lookPad?.ResetInput(); }
        }

        /// <summary>从当前场景配置创建单机冒险，物理清理由Game替换事件统一处理。</summary>
        /// <param name="mode">标准、练习或无尽。</param>
        /// <param name="seed">可选固定种子，不设置必中结果。</param>
        public void StartAdventure(CasinoAdventureMode mode, uint? seed = null)
        {
            if (IsBusy || IsLegacySession) { Game.SetStatus("请先结束当前操作。"); return; }
            Game.StartAdventure(mode, gameSettings != null ? gameSettings.CreateConfig() : new CasinoAdventureConfig(), seed);
        }

        /// <summary>先检查场景是否可继续候选旅程，再安装；失败不清焦点或替换当前局。</summary>
        /// <param name="slot">玩家明确选择的1到3槽。</param>
        public bool LoadAdventure(int slot) => !IsBusy && !IsLegacySession && Game.LoadAdventure(slot);

        /// <summary>使用道具前核对真实场景目标，资金和库存仍由Game规则提交。</summary>
        /// <param name="itemId">目录稳定ID。</param>
        /// <param name="targetId">本机保存的目标ID。</param>
        public CasinoAdventureResult UseAdventureItem(string itemId, string targetId = "buddy")
        {
            var item = CasinoContentCatalog.FindItem(itemId);
            if (item?.IsPrank == true && sceneEffects != null && !sceneEffects.CanApplyPrank(targetId))
            {
                Game.SetStatus("目标不存在或仍在五秒保护内，库存保留。");
                return new CasinoAdventureResult { Error = "TargetProtected", Description = Game.Status, Balance = Game.State?.Coins ?? 0 };
            }
            return Game.UseItem(itemId, targetId, Time.frameCount);
        }

        private void ValidateImmersionRestore(CasinoAdventureState candidate)
        {
            if (!UsesImmersion) return;
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

        /// 在真实交互范围开启机台或柜台。
        public void InteractWithNearbyStation()
        {
            if (!Game.HasAdventure || IsAdventureInputBlocked || IsBusy || body == null) return;
            var nearest = FindNearbyStation();
            if (UsesImmersion && PreferNearbyExit(nearest)) { InteractWithExitTerminal(); return; }
            if (UsesImmersion && PreferNearbyShop(nearest))
            {
                if (!TryOpenImmersionShop()) Game.SetStatus("柜台暂不可操作。");
                return;
            }
            if (nearest != null && UsesImmersion)
            {
                if (!TryOpenImmersionTable(nearest)) Game.SetStatus("此机台暂不可操作。");
            }
            else if (nearest != null) adventurePresenter?.ShowStation(nearest.Game);
            else Game.SetStatus("靠近机台后按E或触碰交互按钮。");
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
                    ResetImmersionTableForRestore(); displayedArea = currentArea;
                    var area = areas.FirstOrDefault(candidate => candidate != null && candidate.Index == currentArea);
                    if (area != null && body != null)
                    {
                        body.enabled = false; body.transform.position = area.SafePosition; body.enabled = true;
                    }
                }
            }
            Changed?.Invoke();
        }

        private void UpdateAdventure(float deltaSeconds)
        {
            if (!IsLegacySession) Game.Tick(deltaSeconds);
        }

        private void SaveAdventureBeforeExit()
        {
            if (Game.HasAdventure && Game.SelectedSaveSlot > 0) Game.SaveAdventure(Game.SelectedSaveSlot);
            sceneEffects?.ClearEffects();
        }

        private void ClearAdventureForLegacy()
        {
            ResetImmersionTableForRestore(); SaveAdventureBeforeExit();
            displayedArea = -1; Game.ClearAdventure();
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
