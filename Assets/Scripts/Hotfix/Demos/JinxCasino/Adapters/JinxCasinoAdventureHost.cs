using System;
using System.Linq;
using Hotfix.JinxCasino.Persistence;
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
        private CasinoAdventureSession adventure;
        private CasinoAdventureState adventureState;
        private CasinoLocalSaveStore localSaveStore;
        private JinxCasinoAdventurePresenter adventurePresenter;
        private float adventureMilliseconds;
        private int lastUserCommandFrame = -1;
        private int displayedArea = -1;
        private int selectedSaveSlot;
        private bool adventureInputBlocked;
        private bool adventureRestoreInProgress;
        private string adventureStatus = "选择一场冒险，或继续已保存的旅程。";

        /// 当前离线冒险界面使用的快照；它是领域状态副本。
        public CasinoAdventureState AdventureState => adventureState;
        /// 当前冒险的反馈，不暴露网络或构建内部状态。
        public string AdventureStatus => adventureStatus;
        /// 场景广播已标明整蛊，不修改钱包。
        public string AdventureSceneAnnouncement => sceneEffects?.Announcement ?? string.Empty;
        /// 雷达只展示本机当前区域方向和距离。
        public string AdventureRadarHint => sceneEffects?.RadarHint ?? string.Empty;
        /// 墨迹边缘强度，中心机台操作区保持可用。
        public float AdventureInkIntensity => sceneEffects?.LocalInkIntensity ?? 0;
        /// 恢复过程只重建持久状态，音效和演出应建立基线而不重播历史结果。
        public bool IsAdventureRestoreInProgress => adventureRestoreInProgress;
        /// 当前已公开的机台表现副本，不包含隐藏牌堆或密码。
        public CasinoMiniGamePresentation GetAdventurePresentation() => adventure?.GetPresentation();
        /// 是否存在独立的内容冒险局。
        public bool HasAdventure => adventure != null;
        /// 旧P0规则验证使用独立入口，不与标准冒险共享钱包。
        public bool IsLegacySession => network != null || coordinator != null;
        /// 当前是否需要完成已经确认投入的机台。
        public bool HasActiveAdventureRound => adventure?.HasActiveRound ?? false;
        /// 当前区域需要达到的实际额度，包含人数和事件修正。
        public long AdventureTarget => adventure?.CurrentTarget ?? 0;
        /// 下一笔投入前的事件、最高投入和道具规则说明。
        public string AdventureBetRules => adventure?.NextBetDescription ?? string.Empty;
        /// 当前机台的操作说明与已生成结果。
        public string ActiveRoundDescription => adventure?.ActiveRoundDescription ?? adventureState?.LastRoundDescription ?? string.Empty;
        /// 正在使用的本地存档槽；0表示尚未主动保存。
        public int SelectedSaveSlot => selectedSaveSlot;
        /// 打开操作面板时停止移动/转向，冒险计时继续推进。
        public bool IsAdventureInputBlocked => (adventureInputBlocked || immersionScreenOpen) && !IsLegacySession;
        /// 当前区域已经装配的安全出生位置。
        public Vector3 CurrentAdventureSafePosition => areas?.FirstOrDefault(area => area != null && area.Index == displayedArea)?.SafePosition ?? Vector3.zero;

        /// <summary>验证场景任务碰撞属于本地玩家。</summary>
        /// <param name="actor">碰撞体所在Transform；助手和非本地角色拒绝。</param>
        public bool IsLocalAdventureActor(Transform actor) => body != null && (actor == body.transform || actor.IsChildOf(body.transform));

        /// <summary>提交已经发生的场景任务交互，先验证实际接近距离。</summary>
        /// <param name="missionId">当前任务ID。</param>
        /// <param name="action">触发目标的领域动作。</param>
        /// <param name="pointIndex">唯一拾取/检查点序号。</param>
        /// <param name="worldPosition">保存目标实例的真实世界位置。</param>
        public CasinoAdventureResult InteractWithMission(string missionId, CasinoTaskAction action, int pointIndex, Vector3 worldPosition)
        {
            if (body == null || (body.transform.position - worldPosition).sqrMagnitude > 4)
                return new CasinoAdventureResult { Error = "请实际接近任务目标。" };
            string requestId = missionId + ":" + (int)action + ":" + pointIndex + ":local";
            return ApplyAdventureCommand(() => adventure.AdvanceTask(requestId, missionId, action, pointIndex, "local"), true);
        }

        /// 当前随机事件可用的团队选择。
        public CasinoEventActionDescriptor[] GetAdventureEventActions() => adventure?.GetEventActions() ?? Array.Empty<CasinoEventActionDescriptor>();

        /// <summary>明确选择当前事件选项，不代替下注确认。</summary>
        /// <param name="choice">事件展示的选项。</param>
        /// <param name="itemId">道具交换时选择的库存ID，其余事件为空。</param>
        public CasinoAdventureResult ResolveAdventureEvent(int choice, string itemId = null)
            => ApplyAdventureCommand(() => adventure.ResolveEvent(CommandId(), choice, itemId));

        /// <summary>装配内容宿主，复用原场景主相机及生命周期。</summary>
        /// <param name="configuration">可编辑的默认冒险设置。</param>
        /// <param name="worldAreas">四区内容和安全位置。</param>
        /// <param name="effects">预制资源的场景演出接收器。</param>
        public void ConfigureAdventure(JinxCasinoGameSettings configuration, JinxCasinoWorldArea[] worldAreas, JinxCasinoSceneEffects effects)
        {
            gameSettings = configuration; areas = worldAreas; sceneEffects = effects;
            if (UsesImmersion) sceneEffects?.BindPresentationClock(PresentationClock);
        }

        /// <summary>为本机用户或测试宿主注入独立存档存储。</summary>
        /// <param name="store">当前用户的三个存档槽，不改变正在进行的规则局。</param>
        public void SetLocalSaveStore(CasinoLocalSaveStore store) => localSaveStore = store ?? throw new ArgumentNullException(nameof(store));

        /// <summary>界面拥有模态操作状态，解绑时必须释放。</summary>
        /// <param name="presenter">具体界面实例；解绑传null。</param>
        /// <param name="blocked">是否正在选择/操作机台或菜单。</param>
        /// <param name="pausesClock">设置等菜单要求暂停本机领域时钟，旧原型默认不变。</param>
        public void BindAdventurePresenter(JinxCasinoAdventurePresenter presenter, bool blocked, bool pausesClock = false)
        {
            adventurePresenter = presenter; adventureInputBlocked = blocked;
            immersionModalPaused = blocked && pausesClock;
            PresentationClock?.SetPaused(IsImmersionPaused);
            if (blocked) { movePad?.ResetInput(); lookPad?.ResetInput(); }
        }

        /// <summary>创建离线内容局；单人助手属于规则，不伪装互联网成员。</summary>
        /// <param name="mode">标准、练习或无尽。</param>
        /// <param name="seed">可选固定种子用于分享相同内容，默认生成新的种子。</param>
        public void StartAdventure(CasinoAdventureMode mode, uint? seed = null)
        {
            if (IsBusy || IsLegacySession) { adventureStatus = "请先结束当前操作。"; Changed?.Invoke(); return; }
            var config = gameSettings != null ? gameSettings.CreateConfig() : new CasinoAdventureConfig();
            var started = CasinoAdventureSession.Start(seed ?? unchecked((uint)Guid.NewGuid().GetHashCode()), mode, 1, config);
            AdoptAdventureSession(started, mode == CasinoAdventureMode.Practice ? "练习局已开始：无倒计时，不计入正式成长。" : "第一站已开放，机台结果与事件由本局种子决定。");
        }

        // 普通模式和教学共用新局安装，避免镜头、选定槽及区域清理出现两套生命周期。
        private void AdoptAdventureSession(CasinoAdventureSession started, string message)
        {
            ResetImmersionTableForRestore();
            adventure = started;
            adventureMilliseconds = 0; selectedSaveSlot = 0; displayedArea = -1;
            adventureStatus = message;
            RefreshAdventureState();
        }

        /// <summary>确认投入后开始机台；同编号重试由聚合拒绝重复付款。</summary>
        /// <param name="requestId">本次确认操作的稳定编号。</param>
        /// <param name="game">机台类型。</param>
        /// <param name="stake">最高投入，实际成交或亏损按规则结算。</param>
        /// <param name="choice">投入前展示并确认的选择。</param>
        /// <param name="stationId">新桌面保存的机台实例ID，null仅兼容旧原型。</param>
        public CasinoAdventureResult BeginAdventureGame(string requestId, CasinoGameKind game, long stake, int choice, string stationId = null)
            => ApplyAdventureCommand(() => adventure.BeginGame(requestId, game, stake, choice, stationId));

        /// <summary>提交当前机台操作，UI不能直接修改结果或钱包。</summary>
        /// <param name="requestId">一次操作的稳定编号。</param>
        /// <param name="action">机台允许的操作。</param>
        /// <param name="value">密码、号码、竞价或选择参数。</param>
        /// <param name="stationId">必须与已投入的机台相同。</param>
        public CasinoAdventureResult ActInAdventure(string requestId, CasinoMiniGameAction action, int value = 0, string stationId = null)
            => ApplyAdventureCommand(() => adventure.Act(requestId, action, value, stationId));

        /// <summary>购买当前商店可售道具。</summary>
        /// <param name="itemId">目录稳定ID。</param>
        public CasinoAdventureResult PurchaseItem(string itemId)
            => ApplyAdventureCommand(() => adventure.Purchase(CommandId(), itemId));

        /// <summary>使用库存中的道具，失败不消耗，场景演出来自领域效果记录。</summary>
        /// <param name="itemId">目录稳定ID。</param>
        /// <param name="targetId">local、buddy或team；实际网络玩家映射后续由权威适配器绑定。</param>
        public CasinoAdventureResult UseAdventureItem(string itemId, string targetId = "buddy")
        {
            var item = CasinoContentCatalog.FindItem(itemId);
            if (item?.IsPrank == true && sceneEffects != null && !sceneEffects.CanApplyPrank(targetId))
            {
                // team/local等别名可能指向同一实体；在提交消费前检查实际目标，库存不能因演出拒绝而白扣。
                adventureStatus = "目标不存在或仍在五秒保护内，库存保留。";
                Changed?.Invoke();
                return new CasinoAdventureResult { Error = "TargetProtected", Description = adventureStatus, Balance = adventureState?.Coins ?? 0 };
            }
            return ApplyAdventureCommand(() => adventure.UseItem(CommandId(), itemId, targetId));
        }

        /// 达到额度后主动结束本区。
        public CasinoAdventureResult CompleteAdventureStage() => ApplyAdventureCommand(() => adventure.CompleteStage(CommandId()));
        /// 明确结束购物准备并推进下一站。
        public CasinoAdventureResult BeginNextAdventureStage() => ApplyAdventureCommand(() => adventure.BeginNextStage(CommandId()));

        /// <summary>按展示的条件作出最终选择。</summary>
        /// <param name="ending">离场、接管或撤离。</param>
        public CasinoAdventureResult SelectAdventureEnding(CasinoAdventureEnding ending)
            => ApplyAdventureCommand(() => adventure.ChooseEnding(CommandId(), ending));

        /// 当前机台允许的操作；空闲返回空数组。
        public CasinoMiniGameActionDescriptor[] GetAdventureActions() => adventure?.GetAvailableActions() ?? Array.Empty<CasinoMiniGameActionDescriptor>();
        /// 当前冒险开放的机台，供界面导航和探索提示使用。
        public CasinoGameDefinition[] GetAvailableAdventureGames() => adventure?.GetAvailableGames() ?? Array.Empty<CasinoGameDefinition>();

        /// <summary>取消尚未随下注使用的预备道具，不改变库存。</summary>
        /// <param name="itemId">已预备规则道具的稳定ID。</param>
        public CasinoAdventureResult CancelPreparedItem(string itemId)
            => ApplyAdventureCommand(() => adventure.CancelPreparedItem(CommandId(), itemId));
        /// 练习时明确补充筹码；已有机台须先完成。
        public CasinoAdventureResult RefillPractice() => ApplyAdventureCommand(() => adventure.ResetPractice(CommandId()));

        /// <summary>取得指定本地槽的展示信息。</summary>
        /// <param name="slot">1到3。</param>
        public CasinoSaveSlotInfo GetSaveSlotInfo(int slot) => SaveStore.GetInfo(slot);

        /// <summary>主动保存到选定槽；之后阶段边界和返回时继续保存此槽。</summary>
        /// <param name="slot">1到3；界面先展示并确认覆盖已有槽。</param>
        public bool SaveAdventure(int slot)
        {
            if (adventure == null) return false;
            try { FlushTutorialMovement(true, false); SaveStore.Save(slot, adventure); selectedSaveSlot = slot; adventureStatus = "已保存到存档" + slot + "。"; Changed?.Invoke(); return true; }
            catch (Exception exception) { adventureStatus = "保存失败：" + exception.Message; Changed?.Invoke(); return false; }
        }

        /// <summary>恢复完整局，不重新开奖或支付已提交结果。</summary>
        /// <param name="slot">1到3。</param>
        public bool LoadAdventure(int slot)
        {
            if (IsBusy || IsLegacySession) return false;
            try
            {
                var restored = SaveStore.Load(slot);
                ValidateImmersionRestore(restored.CaptureState());
                ResetImmersionTableForRestore();
                adventureRestoreInProgress = true;
                adventure = restored; selectedSaveSlot = slot; adventureMilliseconds = 0; displayedArea = -1;
                adventureStatus = "已继续存档" + slot + "，未完成的机台保持原状态。";
                sceneEffects?.ClearEffects(); RefreshAdventureState(null, true); return true;
            }
            catch (Exception exception) { adventureStatus = "读取失败：" + exception.Message; Changed?.Invoke(); return false; }
            finally { adventureRestoreInProgress = false; }
        }

        // 领域校验只保证存档自洽；场景必须能继续它，才允许替换当前局与借用相机。
        private void ValidateImmersionRestore(CasinoAdventureState candidate)
        {
            if (!UsesImmersion) return;
            const string unavailable = "此存档需要当前场景尚未开放的内容，无法在此继续；原存档和当前旅程均已保留。";
            if (candidate.PlayerCount != 1 || areas == null || gameSettings == null)
                throw new InvalidOperationException(unavailable);
            int configuredStages = gameSettings.CreateConfig().StageCount;
            if (candidate.Mode == CasinoAdventureMode.Endless && configuredStages == 1 ||
                candidate.Mode == CasinoAdventureMode.Standard && (candidate.Config.StageCount > configuredStages ||
                    Enumerable.Range(0, candidate.Config.StageCount).Any(index => !areas.Any(area => area != null && area.Index == index % 4))))
                throw new InvalidOperationException(unavailable);
            int areaIndex = candidate.StageIndex % 4;
            if (!areas.Any(area => area != null && area.Index == areaIndex)) throw new InvalidOperationException(unavailable);
            if (string.IsNullOrEmpty(candidate.ActiveRoundJson)) return;
            // 已投入的局必须回到具体原机台，不允许按玩法类型补绑定或接管未定位局。
            bool playable = !string.IsNullOrEmpty(candidate.ActiveStationId) && GetComponentsInChildren<JinxCasinoStation>(true).Any(station => station.HasTableInteraction &&
                station.AreaIndex == areaIndex && station.Game == candidate.ActiveGame &&
                station.StationId == candidate.ActiveStationId);
            if (!playable) throw new InvalidOperationException(unavailable);
        }

        /// 在可交互范围内开启机台，不允许键鼠提供超出触控的远距离交互。
        public void InteractWithNearbyStation()
        {
            if (adventure == null || IsAdventureInputBlocked || IsBusy || body == null) return;
            var nearest = FindNearbyStation();
            if (UsesImmersion && PreferNearbyExit(nearest))
            {
                InteractWithExitTerminal(); return;
            }
            if (UsesImmersion && PreferNearbyShop(nearest))
            {
                if (!TryOpenImmersionShop()) { adventureStatus = "柜台暂不可操作。"; Changed?.Invoke(); }
                return;
            }
            if (nearest != null && UsesImmersion)
            {
                if (!TryOpenImmersionTable(nearest)) { adventureStatus = "此机台暂不可操作。"; Changed?.Invoke(); }
            }
            else if (nearest != null) adventurePresenter?.ShowStation(nearest.Game);
            else { adventureStatus = "靠近机台后按E或触碰交互按钮。"; Changed?.Invoke(); }
        }

        private CasinoLocalSaveStore SaveStore => localSaveStore ?? (localSaveStore = new CasinoLocalSaveStore());
        private static string CommandId() => Guid.NewGuid().ToString("N");

        private CasinoAdventureResult ApplyAdventureCommand(Func<CasinoAdventureResult> operation, bool sceneInteraction = false)
        {
            if (adventure == null || IsBusy) return new CasinoAdventureResult { Error = "当前没有可操作的冒险。" };
            // Unity同一帧的重复按钮事件不能被包装为两次新的确认操作。
            if (!sceneInteraction)
            {
                if (lastUserCommandFrame == Time.frameCount) return new CasinoAdventureResult { Error = "请等待当前操作完成。" };
                lastUserCommandFrame = Time.frameCount;
            }
            try
            {
                var result = operation();
                if (!string.IsNullOrEmpty(result.Description)) adventureStatus = result.Description;
                else if (!result.Success) adventureStatus = result.Error ?? "当前不能执行此操作。";
                RefreshAdventureState(result.Effects); return result;
            }
            catch (Exception exception)
            {
                adventureStatus = exception.Message; Changed?.Invoke();
                return new CasinoAdventureResult { Error = exception.Message };
            }
        }

        private void UpdateAdventure(float deltaSeconds)
        {
            if (adventure == null || IsLegacySession) return;
            adventureMilliseconds += deltaSeconds * 1000;
            if (adventureMilliseconds < 100) return;
            int elapsed = (int)adventureMilliseconds;
            adventureMilliseconds -= elapsed;
            var result = adventure.Advance(elapsed);
            if (!string.IsNullOrEmpty(result.Description)) adventureStatus = result.Description;
            RefreshAdventureState(result.Effects);
        }

        private void RefreshAdventureState(CasinoSceneEffect[] effects = null, bool restore = false)
        {
            var previousPhase = adventureState?.Phase;
            adventureState = adventure?.CaptureState();
            RecordFinishedProfile();
            sceneEffects?.SynchronizeState(adventureState, restore);
            sceneEffects?.ApplyEffects(effects);
            if (adventureState != null && areas != null)
            {
                int currentArea = Mathf.Clamp(adventureState.StageIndex % 4, 0, 3);
                bool practice = adventureState.Mode == CasinoAdventureMode.Practice;
                foreach (var area in areas) if (area != null) area.SetUnlocked(practice || area.Index <= currentArea);
                if (displayedArea != currentArea)
                {
                    ResetImmersionTableForRestore();
                    displayedArea = currentArea;
                    var area = areas.FirstOrDefault(candidate => candidate != null && candidate.Index == currentArea);
                    if (area != null && body != null)
                    {
                        body.enabled = false; body.transform.position = area.SafePosition; body.enabled = true;
                    }
                }
                if (selectedSaveSlot > 0 && previousPhase.HasValue && previousPhase.Value != adventureState.Phase)
                    SaveAdventure(selectedSaveSlot);
            }
            Changed?.Invoke();
        }

        private void SaveAdventureBeforeExit()
        {
            if (adventure != null && selectedSaveSlot > 0)
            {
                try { FlushTutorialMovement(true, false); SaveStore.Save(selectedSaveSlot, adventure); }
                catch (Exception exception) { Debug.LogWarning("赌场离场存档失败：" + exception.Message, this); }
            }
            sceneEffects?.ClearEffects();
        }

        private void ClearAdventureForLegacy()
        {
            ResetImmersionTableForRestore();
            SaveAdventureBeforeExit(); adventure = null; adventureState = null;
            adventureMilliseconds = 0; displayedArea = -1; selectedSaveSlot = 0;
        }
    }
}
