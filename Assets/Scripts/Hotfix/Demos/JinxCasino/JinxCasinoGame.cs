using System;
using Hotfix.JinxCasino.Persistence;
using Hotfix.JinxCasino.Rules;
using UnityEngine;

namespace Hotfix.JinxCasino
{
    /// 赌场单机游戏实例；拥有旅程、命令提交和本地持久化，不持有场景对象。
    public sealed class JinxCasinoGame
    {
        private CasinoAdventureSession adventure;
        private CasinoAdventureState state;
        private CasinoLocalSaveStore localSaveStore;
        private double accumulatedMilliseconds;
        private int selectedSaveSlot;
        private int lastCommandFrame = -1;
        private bool isRestoring;
        private bool isSaving;
        private string status = "选择一场冒险，或继续已保存的旅程。";
        private CasinoProfile profile;
        private CasinoProfileStore profileStore;
        private string profileCommittedRun;
        private float profileRetryAfter;
        private float profileReadRetryAfter;
        private string profileStatus = "正式旅程结束后记录成长；练习不计。";

        /// 最近一次提交后的状态缓存；消费者只读使用，不通过此引用修改游戏。
        public CasinoAdventureState State => state;
        /// 最近操作及持久化反馈。
        public string Status => status;
        /// 是否已经安装旅程。
        public bool HasAdventure => adventure != null;
        /// 是否存在尚未结算的已投入局。
        public bool HasActiveRound => adventure?.HasActiveRound ?? false;
        /// 当前区域的实际筹码目标。
        public long Target => adventure?.CurrentTarget ?? 0;
        /// 下次投入前需展示的规则变化。
        public string BetRules => adventure?.NextBetDescription ?? string.Empty;
        /// 当前局或最近结算局的公开说明。
        public string ActiveRoundDescription => adventure?.ActiveRoundDescription ?? string.Empty;
        /// 当前旅程关联的存档槽；零表示尚未选择。
        public int SelectedSaveSlot => selectedSaveSlot;
        /// 读档安装与通知期间为真，表现层据此直接显示已有结果。
        public bool IsRestoring => isRestoring;
        /// 是否允许提交游戏命令；时钟、保存与教学观察仍可继续执行。
        public bool CommandInputEnabled { get; set; } = true;
        /// 永久档案深副本；读取失败返回空，不覆盖已有损坏文件。
        public CasinoProfileData ProfileData => EnsureProfile() ? profile.Data : null;
        /// 档案读取、记录及装备反馈。
        public string ProfileStatus => profileStatus;
        /// 永久声望等级，不影响局内收益。
        public long ProfileLevel => EnsureProfile() ? profile.Level : 1;

        /// 状态提交后的通知；效果仅供场景消费，重复回执不再次派发效果。
        public event Action<CasinoSceneEffect[]> Changed;
        /// 候选读档安装前检查场景能力；抛出异常将保留当前旅程。
        public event Action<CasinoAdventureState> ValidatingRestore;
        /// 替换旅程前清理场景交互，参数表示是否读档。
        public event Action<bool> BeforeRunReplacement;
        /// 写盘前同步尚未报告的教学事实；观察应使用不通知路径。
        public event Action BeforeSave;

        /// <summary>仅更新操作提示，并通知已有状态的消费者。</summary>
        /// <param name="message">提示文字，空值按空串处理。</param>
        public void SetStatus(string message)
        {
            status = message ?? string.Empty;
            Publish(null);
        }

        /// <summary>先创建单人旅程，再替换当前游戏；构造失败不会丢弃旧局。</summary>
        /// <param name="mode">正式、练习或无尽模式。</param>
        /// <param name="config">本局配置，规则层会复制配置。</param>
        /// <param name="seed">可选固定种子，未提供时生成新种子。</param>
        public void StartAdventure(CasinoAdventureMode mode, CasinoAdventureConfig config, uint? seed = null)
        {
            var candidate = CasinoAdventureSession.Start(seed ?? NewSeed(), mode, 1, config);
            InstallAdventure(candidate, mode == CasinoAdventureMode.Practice ? "练习已开始；练习不计正式成长。" : "旅程已开始。");
        }

        /// <summary>原子创建新的教学练习，只有教学启动成功才替换当前旅程。</summary>
        /// <param name="config">教学三台与柜台配置，最大投入在候选副本内限制为10。</param>
        /// <param name="replaceCurrentRun">是否已明确允许放弃当前旅程。</param>
        /// <param name="seed">可选固定种子，不改变随机规则。</param>
        /// <returns>教学启动的实际领域回执。</returns>
        public CasinoAdventureResult StartTutorialAdventure(CasinoAdventureConfig config, bool replaceCurrentRun = false, uint? seed = null)
        {
            if (!CommandInputEnabled) return Reject("Busy", "请先结束当前操作。");
            if (HasAdventure && !replaceCurrentRun)
                return Reject("TutorialReplacementRequired", "先选择保存或放弃当前局，再明确重玩教学。");
            try
            {
                var tutorialConfig = JsonUtility.FromJson<CasinoAdventureConfig>(JsonUtility.ToJson(config ?? new CasinoAdventureConfig()));
                tutorialConfig.MaximumStake = 10;
                tutorialConfig.EventIntervalMilliseconds = 0;
                var candidate = CasinoAdventureSession.Start(seed ?? NewSeed(), CasinoAdventureMode.Practice, 1, tutorialConfig);
                var result = candidate.StartTutorial(NewRequestId());
                if (!result.Success) { SetStatus(result.Description ?? result.Error); return result; }
                InstallAdventure(candidate, result.Description);
                return result;
            }
            catch (Exception exception)
            {
                return Reject("TutorialStartFailed", exception.Message);
            }
        }

        /// <summary>安装已创建并验证的候选；恢复标志覆盖替换前回调和状态通知。</summary>
        /// <param name="candidate">由调用方移交的独立候选，安装后不应继续直接操作。</param>
        /// <param name="message">安装后的反馈。</param>
        /// <param name="slot">关联存档槽，零表示新旅程。</param>
        /// <param name="restoring">是否按读档静态恢复表现。</param>
        public void InstallAdventure(CasinoAdventureSession candidate, string message, int slot = 0, bool restoring = false)
        {
            if (candidate == null) throw new ArgumentNullException(nameof(candidate));
            ValidateInstallationSlot(slot);
            // 候选快照先完成，任何验证失败都发生在旧局和场景清理之前。
            Install(candidate, candidate.CaptureState(), message, slot, restoring);
        }

        /// 清除旅程、计时、存档槽与提交帧，永久档案仍保留。
        public void ClearAdventure()
        {
            BeforeRunReplacement?.Invoke(false);
            adventure = null;
            state = null;
            accumulatedMilliseconds = 0;
            selectedSaveSlot = 0;
            lastCommandFrame = -1;
            isRestoring = false;
            status = "选择一场冒险，或继续已保存的旅程。";
            Publish(null);
        }

        /// <summary>累积实际经过时间，以至少100毫秒的批次推进领域时钟。</summary>
        /// <param name="deltaSeconds">宿主排除暂停后的非负秒增量。</param>
        public void Tick(float deltaSeconds)
        {
            if (!HasAdventure || float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds) || deltaSeconds < 0) return;
            accumulatedMilliseconds += deltaSeconds * 1000d;
            if (accumulatedMilliseconds < 100) return;
            int elapsed = (int)Math.Min(Math.Floor(accumulatedMilliseconds), 3600000d);
            accumulatedMilliseconds -= elapsed;
            Advance(elapsed);
        }

        /// <summary>直接推进规则时间，不占玩家提交帧或依赖设备输入。</summary>
        /// <param name="milliseconds">规则接受的0至3600000毫秒增量。</param>
        /// <returns>时间推进回执，活动机台按原规则处理。</returns>
        public CasinoAdventureResult Advance(int milliseconds)
        {
            if (!HasAdventure) return Reject("NoAdventure", "请先开始一场旅程。");
            var result = adventure.Advance(milliseconds);
            UpdateStatus(result);
            if (result.Changed) RefreshState(result.Effects);
            else
            {
                // 已结束旅程仍可重试失败的档案写盘，不重算奖励或重开机台。
                RecordFinishedProfile();
                Publish(null);
            }
            return result;
        }

        /// <summary>在指定机台提交投入，同一请求重试由领域校验并返回原回执。</summary>
        /// <param name="requestId">稳定请求编号。</param>
        /// <param name="kind">玩法类型。</param>
        /// <param name="stake">最大投入金额。</param>
        /// <param name="choice">玩法起始参数。</param>
        /// <param name="stationId">具体机台编号。</param>
        /// <param name="frameId">此次设备无关输入的提交帧。</param>
        /// <returns>投入回执。</returns>
        public CasinoAdventureResult BeginGame(string requestId, CasinoGameKind kind, long stake, int choice, string stationId, int frameId)
            => Execute(requestId, frameId, () => adventure.BeginGame(requestId, kind, stake, choice, stationId));

        /// <summary>向已投入的具体机台提交操作。</summary>
        /// <param name="requestId">稳定请求编号。</param>
        /// <param name="action">公开合法动作。</param>
        /// <param name="value">动作参数。</param>
        /// <param name="stationId">具体机台编号。</param>
        /// <param name="frameId">输入提交帧。</param>
        /// <returns>实际机台操作回执。</returns>
        public CasinoAdventureResult Act(string requestId, CasinoMiniGameAction action, int value, string stationId, int frameId)
            => Execute(requestId, frameId, () => adventure.Act(requestId, action, value, stationId));

        /// <summary>购买当前配置开放的商品。</summary>
        /// <param name="itemId">稳定商品编号。</param>
        /// <param name="frameId">输入提交帧。</param>
        /// <returns>购买回执。</returns>
        public CasinoAdventureResult PurchaseItem(string itemId, int frameId)
        {
            string requestId = NewRequestId();
            return Execute(requestId, frameId, () => adventure.Purchase(requestId, itemId));
        }

        /// <summary>使用库存道具；场景目标的可达性由宿主在调用前检查。</summary>
        /// <param name="itemId">道具编号。</param>
        /// <param name="targetId">实际目标编号。</param>
        /// <param name="frameId">输入提交帧。</param>
        /// <returns>使用回执，拒绝时规则不消耗库存。</returns>
        public CasinoAdventureResult UseItem(string itemId, string targetId, int frameId)
        {
            string requestId = NewRequestId();
            return Execute(requestId, frameId, () => adventure.UseItem(requestId, itemId, targetId));
        }

        /// <summary>撤回尚未投入的预备道具。</summary>
        /// <param name="itemId">预备道具编号。</param>
        /// <param name="frameId">输入提交帧。</param>
        /// <returns>撤回回执。</returns>
        public CasinoAdventureResult CancelPreparedItem(string itemId, int frameId)
        {
            string requestId = NewRequestId();
            return Execute(requestId, frameId, () => adventure.CancelPreparedItem(requestId, itemId));
        }

        /// <summary>确认当前区域达标，不扣除区域目标额度。</summary>
        /// <param name="frameId">输入提交帧。</param>
        /// <returns>阶段确认回执。</returns>
        public CasinoAdventureResult CompleteStage(int frameId)
        {
            string requestId = NewRequestId();
            return Execute(requestId, frameId, () => adventure.CompleteStage(requestId));
        }

        /// <summary>明确进入下一阶段。</summary>
        /// <param name="frameId">输入提交帧。</param>
        /// <returns>阶段进入回执。</returns>
        public CasinoAdventureResult BeginNextStage(int frameId)
        {
            string requestId = NewRequestId();
            return Execute(requestId, frameId, () => adventure.BeginNextStage(requestId));
        }

        /// <summary>通过领域选择实际可用的旅程结局。</summary>
        /// <param name="ending">明确选择的结局。</param>
        /// <param name="frameId">输入提交帧。</param>
        /// <returns>结局回执。</returns>
        public CasinoAdventureResult ChooseEnding(CasinoAdventureEnding ending, int frameId)
        {
            string requestId = NewRequestId();
            return Execute(requestId, frameId, () => adventure.ChooseEnding(requestId, ending));
        }

        /// <summary>按练习规则补充筹码，不影响正式成长。</summary>
        /// <param name="frameId">输入提交帧。</param>
        /// <returns>练习补充回执。</returns>
        public CasinoAdventureResult RefillPractice(int frameId)
        {
            string requestId = NewRequestId();
            return Execute(requestId, frameId, () => adventure.ResetPractice(requestId));
        }

        /// <summary>提交当前事件的公开选项。</summary>
        /// <param name="choice">事件选项编号。</param>
        /// <param name="itemId">选项使用的道具编号。</param>
        /// <param name="frameId">输入提交帧。</param>
        /// <returns>事件选择回执。</returns>
        public CasinoAdventureResult ResolveEvent(int choice, string itemId, int frameId)
        {
            string requestId = NewRequestId();
            return Execute(requestId, frameId, () => adventure.ResolveEvent(requestId, choice, itemId));
        }

        /// <summary>提交已发生的任务触发事实，不占用玩家经济操作帧。</summary>
        /// <param name="requestId">场景触发事实的稳定请求编号。</param>
        /// <param name="missionId">实际任务编号。</param>
        /// <param name="action">实际触发动作。</param>
        /// <param name="pointIndex">任务点编号。</param>
        /// <param name="actorId">实际执行者编号。</param>
        /// <returns>任务事实回执。</returns>
        public CasinoAdventureResult InteractWithMission(string requestId, string missionId, CasinoTaskAction action, int pointIndex, string actorId)
            => Execute(requestId, 0, () => adventure.AdvanceTask(requestId, missionId, action, pointIndex, actorId), true);

        /// 当前活动局或最近结算局的安全表现投影，不包含隐藏结果。
        public CasinoMiniGamePresentation GetPresentation() => adventure?.GetPresentation();
        /// 当前活动局的公开合法动作。
        public CasinoMiniGameActionDescriptor[] GetActions() => adventure?.GetAvailableActions() ?? Array.Empty<CasinoMiniGameActionDescriptor>();
        /// 当前配置及区域实际开放的玩法。
        public CasinoGameDefinition[] GetAvailableGames() => adventure?.GetAvailableGames() ?? Array.Empty<CasinoGameDefinition>();
        /// 当前事件的公开选择。
        public CasinoEventActionDescriptor[] GetEventActions() => adventure?.GetEventActions() ?? Array.Empty<CasinoEventActionDescriptor>();

        /// <summary>读取存档槽摘要，不安装或修改当前旅程。</summary>
        /// <param name="slot">1至3的存档槽。</param>
        /// <returns>保存摘要或存储层提供的损坏反馈。</returns>
        public CasinoSaveSlotInfo GetSaveSlotInfo(int slot) => SaveStore.GetInfo(slot);

        /// <summary>注入本地三槽存储，允许测试与真实用户文件隔离。</summary>
        /// <param name="store">独立存储实例。</param>
        public void SetLocalSaveStore(CasinoLocalSaveStore store)
        {
            localSaveStore = store ?? throw new ArgumentNullException(nameof(store));
        }

        /// <summary>观察保存前教学事实，再原子保存当前旅程；失败保留原文件。</summary>
        /// <param name="slot">1至3的目标槽。</param>
        /// <returns>保存是否成功，成功后关联该槽。</returns>
        public bool SaveAdventure(int slot) => Save(slot, true);

        /// <summary>读取并校验候选后安装，任何安装前失败都保留当前旅程和原存档。</summary>
        /// <param name="slot">1至3的来源槽。</param>
        /// <returns>是否安装成功。</returns>
        public bool LoadAdventure(int slot)
        {
            try
            {
                var candidate = SaveStore.Load(slot);
                var candidateState = candidate.CaptureState();
                ValidatingRestore?.Invoke(candidateState);
                Install(candidate, candidateState, "已继续存档" + slot + "，未完成的机台保持原状态。", slot, true);
                return true;
            }
            catch (Exception exception)
            {
                SetStatus("读取失败：" + exception.Message);
                return false;
            }
        }

        /// <summary>提交实际教学观察，不占经济输入帧，也不覆盖交易反馈。</summary>
        /// <param name="fact">真实发生的教学事实。</param>
        /// <param name="value">观察累计量或结算序号。</param>
        /// <param name="stationId">事实所属的实际机台。</param>
        /// <param name="notify">是否通知场景；保存前观察用false避免递归刷新。</param>
        /// <returns>教学事实的领域回执。</returns>
        public CasinoAdventureResult ObserveTutorial(CasinoTutorialFact fact, int value, string stationId = null, bool notify = true)
        {
            if (!HasAdventure) return new CasinoAdventureResult { Error = "NoAdventure", Description = "当前没有教学旅程。" };
            var result = adventure.ObserveTutorial(fact, value, stationId);
            if (result.Changed)
            {
                if (notify) RefreshState(result.Effects);
                else state = adventure.CaptureState();
            }
            return result;
        }

        /// 明确跳过教学，保留练习钱包、库存与已投入局。
        public CasinoAdventureResult SkipTutorial()
        {
            string requestId = NewRequestId();
            return Execute(requestId, 0, () => adventure.SkipTutorial(requestId), true);
        }

        /// 玩家在Ready检查点明确完成教学，不自动开始正式旅程。
        public CasinoAdventureResult CompleteTutorial()
        {
            if (!HasAdventure || !CommandInputEnabled) return Reject("TutorialInactive", "当前没有可操作教学。");
            if (state.Teaching?.Status != CasinoTutorialStatus.Active || state.Teaching.Step != CasinoTutorialStep.Ready)
                return Reject("TutorialNotReady", "请先完成实际教学步骤。");
            var result = adventure.ObserveTutorial(CasinoTutorialFact.Continue, 1);
            UpdateStatus(result);
            if (result.Changed) RefreshState(result.Effects);
            else Publish(null);
            return result;
        }

        /// <summary>注入独立永久档案存储，不改变局内钱包。</summary>
        /// <param name="store">独立档案存储实例。</param>
        public void SetLocalProfileStore(CasinoProfileStore store)
        {
            profileStore = store ?? throw new ArgumentNullException(nameof(store));
            profile = null;
            profileCommittedRun = null;
            profileRetryAfter = profileReadRetryAfter = 0;
        }

        /// <summary>仅在完整档案候选保存成功后更新装备缓存。</summary>
        /// <param name="colorId">解锁配色，空值保持当前。</param>
        /// <param name="hatId">解锁帽子，空值保持当前。</param>
        /// <param name="emoteId">解锁表情，空值保持当前。</param>
        /// <param name="titleId">解锁称号，空值保持当前。</param>
        /// <returns>装备是否成功保存。</returns>
        public bool EquipProfile(string colorId = null, string hatId = null, string emoteId = null, string titleId = null)
        {
            if (!EnsureProfile()) { Publish(null); return false; }
            try
            {
                var candidate = CasinoProfile.Restore(profile.ToJson());
                if (!candidate.Equip(colorId, hatId, emoteId, titleId))
                {
                    profileStatus = "该外观尚未解锁，原装备保留。";
                    Publish(null);
                    return false;
                }
                ProfileStore.Save(candidate);
                profile = candidate;
                profileStatus = "装扮已保存。";
                Publish(null);
                return true;
            }
            catch (Exception exception)
            {
                profileStatus = "装扮保存失败：" + exception.Message;
                Publish(null);
                return false;
            }
        }

        private CasinoLocalSaveStore SaveStore => localSaveStore ?? (localSaveStore = new CasinoLocalSaveStore());
        private CasinoProfileStore ProfileStore => profileStore ?? (profileStore = new CasinoProfileStore());
        private static string NewRequestId() => Guid.NewGuid().ToString("N");
        private static uint NewSeed() => unchecked((uint)Guid.NewGuid().GetHashCode());

        private void Install(CasinoAdventureSession candidate, CasinoAdventureState snapshot, string message, int slot, bool restoring)
        {
            ValidateInstallationSlot(slot);
            bool previousRestoring = isRestoring;
            isRestoring = restoring;
            try
            {
                BeforeRunReplacement?.Invoke(restoring);
                adventure = candidate;
                state = snapshot;
                selectedSaveSlot = slot;
                accumulatedMilliseconds = 0;
                lastCommandFrame = -1;
                status = message ?? string.Empty;
                RecordFinishedProfile();
                Publish(null);
            }
            finally { isRestoring = previousRestoring; }
        }

        private static void ValidateInstallationSlot(int slot)
        {
            if (slot < 0 || slot > 3) throw new ArgumentOutOfRangeException(nameof(slot), "存档槽须为0至3。");
        }

        private CasinoAdventureResult Execute(string requestId, int frameId, Func<CasinoAdventureResult> operation, bool sceneFact = false)
        {
            if (!HasAdventure) return Reject("NoAdventure", "请先开始一场旅程。");
            if (!CommandInputEnabled) return Reject("Busy", "请先结束当前操作。");
            bool retry = state.ProcessedRequests.Exists(record => record.RequestId == requestId);
            if (!sceneFact && !retry)
            {
                if (frameId < 0) return Reject("InvalidFrame", "输入提交帧不能为负数。");
                if (lastCommandFrame == frameId) return Reject("Busy", "请等待当前操作完成。");
                lastCommandFrame = frameId;
            }
            try
            {
                var result = operation();
                UpdateStatus(result);
                // 失败的新请求也进入领域回执表，必须刷新一次缓存才能正确识别后续重试。
                RefreshState(retry ? null : result.Effects);
                return result;
            }
            catch (Exception exception)
            {
                return Reject("CommandFailed", exception.Message);
            }
        }

        private CasinoAdventureResult Reject(string error, string message)
        {
            SetStatus(message);
            return new CasinoAdventureResult { Error = error, Description = message, Balance = state?.Coins ?? 0 };
        }

        private void UpdateStatus(CasinoAdventureResult result)
        {
            if (!string.IsNullOrEmpty(result.Description)) status = result.Description;
            else if (!result.Success) status = result.Error ?? "操作未完成。";
        }

        private void RefreshState(CasinoSceneEffect[] effects)
        {
            var previousPhase = state?.Phase;
            state = adventure.CaptureState();
            RecordFinishedProfile();
            // 自动保存不另发Changed；保存前观察只能更新缓存，最终统一发布此次提交。
            if (selectedSaveSlot > 0 && previousPhase.HasValue && previousPhase.Value != state.Phase)
                Save(selectedSaveSlot, false);
            Publish(effects);
        }

        private bool Save(int slot, bool notify)
        {
            if (!HasAdventure || isSaving) return false;
            isSaving = true;
            try
            {
                if (slot < 1 || slot > 3) throw new ArgumentOutOfRangeException(nameof(slot), "存档槽须为1至3。");
                BeforeSave?.Invoke();
                SaveStore.Save(slot, adventure);
                selectedSaveSlot = slot;
                status = "已保存到存档" + slot + "。";
                return true;
            }
            catch (Exception exception)
            {
                status = "保存失败：" + exception.Message;
                return false;
            }
            finally
            {
                isSaving = false;
                if (notify) Publish(null);
            }
        }

        private bool EnsureProfile()
        {
            if (profile != null) return true;
            if (Time.unscaledTime < profileReadRetryAfter) return false;
            try { profile = ProfileStore.LoadOrCreate(); return true; }
            catch (Exception exception)
            {
                profileStatus = "档案读取失败，已有文件保留：" + exception.Message;
                profileReadRetryAfter = Time.unscaledTime + 5;
                return false;
            }
        }

        private void RecordFinishedProfile()
        {
            if (state == null || state.Phase != CasinoAdventurePhase.Ended || state.Mode == CasinoAdventureMode.Practice ||
                profileCommittedRun == state.RunId || Time.unscaledTime < profileRetryAfter) return;
            if (!EnsureProfile()) { profileRetryAfter = Time.unscaledTime + 5; return; }
            try
            {
                var candidate = CasinoProfile.Restore(profile.ToJson());
                if (candidate.RecordFinishedRun(state))
                {
                    // 写盘失败时不提前提交RunId，否则会吞掉五秒后的重试。
                    ProfileStore.Save(candidate);
                    profile = candidate;
                    profileStatus = "本次正式旅程已记录成长与图鉴。";
                }
                profileCommittedRun = state.RunId;
            }
            catch (Exception exception)
            {
                profileStatus = "成长保存失败，稍后重试：" + exception.Message;
                profileRetryAfter = Time.unscaledTime + 5;
            }
        }

        private void Publish(CasinoSceneEffect[] effects)
        {
            var listeners = Changed;
            if (listeners == null) return;
            // 游戏与文件已提交后，单个表现订阅者异常不能把成功操作报告为失败。
            foreach (Action<CasinoSceneEffect[]> listener in listeners.GetInvocationList())
            {
                try { listener(effects); }
                catch (Exception exception) { Debug.LogException(exception); }
            }
        }
    }
}
