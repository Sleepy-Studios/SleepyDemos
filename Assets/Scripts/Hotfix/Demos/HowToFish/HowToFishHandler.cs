using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Core.Runtime;
using UnityEngine;

namespace Hotfix.HowToFish
{
    /// 本场业务命令、持久状态修改及完整操作后的发布入口。
    internal sealed class HowToFishHandler : HandlerBase<HowToFishAction, HowToFishData>, IDisposable
    {
        private readonly HowToFishWorld scene;

        private int reducing;

        private float sampleAt;

        private HowToFishSaveStore summaryStore;

        internal HowToFishHandler(HowToFishWorld scene)
        {
            this.scene = scene;
        }

        private HowToFishWorldItem Spawn(string id, Vector3 position, bool drip) => scene.Spawn(id, position, drip);

        internal void Initialize()
        {
            RefreshSlots();
            Publish();
        }

        private void OnRulesChanged()
        {
            scene.ApplyRulePresentation();
            Publish();
        }

        internal void Publish()
        {
            if (reducing != 0 || !ReferenceEquals(GlobalData.Get<HowToFishData>(), State))
                return;
            if (State.Session == null && !ReferenceEquals(summaryStore, scene.SaveStore))
                RefreshSlots();
            if (State.MainPage == HowToFishPage.None)
                State.RequestedOverlay = HowToFishPage.None;
            ApplyState();
        }

        /// <summary>
        /// 同步处理本场命令，嵌套规则事件在完整操作结束后统一发布。
        /// </summary>
        /// <param name="action">本场业务请求；旧场景或旧规则来源被忽略。</param>
        protected override void Reduce(HowToFishAction action)
        {
            if (!ReferenceEquals(GlobalData.Get<HowToFishData>(), State))
                return;
            // 嵌套规则事件不能让页面读到尚未完成的交易或场景装配。
            reducing++;
            try
            {
                switch (action)
                {
                    case HowToFishExitingAction closing when ReferenceEquals(closing.Source, scene):
                        State.IsExiting = closing.Exiting;
                        break;
                    case HowToFishTryConsumeAction request when State.Session != null && ReferenceEquals(request.Source, State.Session):
                        request.Result = State.Session.TryConsume(request.Id);
                        break;
                    case HowToFishGrantItemAction request when State.Session != null && ReferenceEquals(request.Source, State.Session):
                        State.Session.GrantItem(request.Id, request.Count);
                        break;
                    case HowToFishGrantEquipmentAction request when State.Session != null && ReferenceEquals(request.Source, State.Session):
                        State.Session.GrantEquipment(request.Equipment);
                        break;
                    case HowToFishTryStoreEquipmentAction request when State.Session != null && ReferenceEquals(request.Source, State.Session):
                        request.Result = State.Session.TryStoreEquipment(request.Id, request.Slot);
                        break;
                    case HowToFishSellCatchAction request when State.Session != null && ReferenceEquals(request.Source, State.Session):
                        request.Result = State.Session.SellCatch(request.CreatureId, request.Cooking, request.Drip, request.StyleMultiplier, request.BettingMultiplier, request.WeightMultiplier);
                        break;
                    case HowToFishRegisterCreatureAction request when State.Session != null && ReferenceEquals(request.Source, State.Session):
                        State.Session.RegisterCreature(request.CreatureId, request.Defeated, request.Drip);
                        break;
                    case HowToFishStartSlotAction request when ReferenceEquals(request.Source, scene):
                        StartSlot(request.Index, request.NewGame, request.RecoverBackup);
                        break;
                    case HowToFishSaveAction request when ReferenceEquals(request.Source, scene):
                        TrySave();
                        break;
                    case HowToFishApplyPreferencesAction request when ReferenceEquals(request.Source, scene):
                        ApplyPreferences(request.Value);
                        break;
                    case HowToFishSavePreferencesAction request when ReferenceEquals(request.Source, scene):
                        request.Result = SavePreferences();
                        break;
                    case HowToFishSetEditingSettingsAction request when ReferenceEquals(request.Source, scene):
                        SetEditingSettings(request.Editing);
                        break;
                    case HowToFishTrySelectOutfitAction request when ReferenceEquals(request.Source, scene):
                        request.Result = TrySelectOutfit(request.Id);
                        break;
                    case HowToFishTryPlaySlotMachineAction request when ReferenceEquals(request.Source, scene):
                        request.Result = TryPlaySlotMachine(request.Island, request.Item, out var slotMessage);
                        request.Message = slotMessage;
                        break;
                    case HowToFishChangeSkinAction request when ReferenceEquals(request.Source, scene):
                        ChangeSkin();
                        break;
                    case HowToFishTrySettleRouletteAction request when ReferenceEquals(request.Source, scene):
                        request.Result = TrySettleRoulette(request.Roulette, request.Bets, request.WinningColor, out var announcement);
                        request.Announcement = announcement;
                        break;
                    case HowToFishSetPausedAction request when ReferenceEquals(request.Source, scene):
                        SetPaused(request.Paused);
                        break;
                    case HowToFishNotifyAction request when ReferenceEquals(request.Source, scene):
                        Notify(request.Message);
                        break;
                    case HowToFishInteractAction request when ReferenceEquals(request.Source, scene):
                        Interact(request.Collider);
                        break;
                    case HowToFishDeliverToStationAction request when ReferenceEquals(request.Source, scene):
                        DeliverToStation(request.Station, request.Item);
                        break;
                    case HowToFishContinueAfterEndingAction request when ReferenceEquals(request.Source, scene):
                        ContinueAfterEnding();
                        break;
                    case HowToFishRespawnAction request when ReferenceEquals(request.Source, scene):
                        Respawn();
                        break;
                    case HowToFishOnCreatureEatenAction request when ReferenceEquals(request.Source, scene):
                        OnCreatureEaten(request.Item);
                        break;
                    case HowToFishSaveOutfitProgressAction request when ReferenceEquals(request.Source, scene):
                        SaveOutfitProgress();
                        break;
                    case HowToFishDamageAction request when State.Session != null && ReferenceEquals(request.Source, State.Session):
                        request.Died = Damage(request.Amount);
                        return;
                    case HowToFishDamageStatusAction request when State.Session != null && ReferenceEquals(request.Source, State.Session):
                        ApplyDamageStatus(request);
                        return;
                    case HowToFishDamageStatusTickAction request when State.Session != null && ReferenceEquals(request.Source, State.Session):
                        TickDamageStatus(request);
                        return;
                    case HowToFishRestoreFoodAction request when State.Session != null && ReferenceEquals(request.Source, State.Session):
                        RestoreFood(request);
                        return;
                    case HowToFishAmmoAction request when State.Session != null && ReferenceEquals(request.Source, State.Session):
                        Ammo(request);
                        return;
                    case HowToFishCookingAction request when State.Session != null && ReferenceEquals(request.Source, State.Session):
                        Cooking(request);
                        return;
                    case HowToFishEquipAction request when State.Session != null && ReferenceEquals(request.Source, State.Session):
                        Equip(request.ItemId);
                        return;
                    case HowToFishSelectSlotAction request when State.Session != null && ReferenceEquals(request.Source, State.Session):
                        State.Session.State.selectedEquipmentSlot = request.Slot;
                        return;
                    case HowToFishTickAction request when ReferenceEquals(request.Source, scene):
                        Tick(request);
                        if (Time.unscaledTime < sampleAt)
                            return;
                        sampleAt = Time.unscaledTime + .05f;
                        break;
                    case HowToFishUiAction request when ReferenceEquals(request.Source, scene):
                        Ui(request);
                        break;
                    default:
                        return;
                }
            }
            finally
            {
                reducing--;
            }

            Publish();
        }

        private void ApplyPreferences(HowToFishLocalPreferences value)
        {
            if (value == null || !value.IsValid)
                throw new ArgumentException("输入设置无效。", nameof(value));
            scene.Input.LoadBindings(value.Bindings);
            scene.Player.MouseSensitivity = value.MouseSensitivity;
            scene.Player.GamepadSensitivity = value.GamepadSensitivity;
            scene.Player.DeadZone = value.DeadZone;
            scene.Player.InvertY = value.InvertY;
        }

        private bool SavePreferences()
        {
            try
            {
                scene.PreferencesStore.Save(scene.GetPreferences());
                Notify("输入设置已保存。");
                return true;
            }
            catch (Exception exception)
            {
                Notify("输入设置未保存：" + exception.Message);
                return false;
            }
        }

        private void SetEditingSettings(bool editing)
        {
            State.IsEditingSettings = editing;
            if (!editing)
                scene.SettingsClosedGate();
        }

        private bool TrySelectOutfit(string id)
        {
            if (State.Session == null || State.IsExiting || !HowToFishOutfitCatalog.IsUnlocked(id, State.Session.State.unlockedOutfits) || scene.Catalog.FindOutfit(id)?.Prefab == null)
                return false;
            try
            {
                var profile = scene.SaveStore.LoadSkinProfile(out bool recovered);
                foreach (string unlocked in State.Session.State.unlockedOutfits)
                    if (!profile.unlockedOutfits.Contains(unlocked))
                        profile.unlockedOutfits.Add(unlocked);
                profile.selectedOutfitId = id;
                scene.SaveStore.SaveSkinProfile(profile);
                State.SelectedOutfitId = id;
                scene.Player.SetOutfit(scene.Catalog.FindOutfit(id));
                Notify("当前服装：" + HowToFishOutfitCatalog.Find(id).Name + (recovered ? "；共享档案已从备份恢复，损坏原件已保留。" : ""));
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is NotSupportedException)
            {
                Notify("服装选择未保存，原外观已保留：" + exception.Message);
                return false;
            }
        }

        private void SaveOutfitProgress()
        {
            // 独立成就立即进入共享档案，不在死亡事件中抢拍仍等待帧末清理的世界实体。
            try
            {
                scene.SaveStore.LoadSharedSkins(State.Session.State);
                if (!string.IsNullOrEmpty(scene.SaveStore.SkinProfileNotice))
                    Notify(scene.SaveStore.SkinProfileNotice);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is NotSupportedException)
            {
                Notify("服装奖励仍在本次航程中，共享保存失败，请再次保存进度：" + exception.Message);
            }
        }

        private void OnCreatureEaten(HowToFishWorldItem item)
        {
            if (item.IsBurnt && State.Session.UnlockOutfit("KioskLady"))
                SaveOutfitProgress();
        }

        private bool TryPlaySlotMachine(int island, HowToFishWorldItem item, out string result)
        {
            result = null;
            if (State.Session == null || State.IsPaused || State.IsExiting || island < 0 || island > 4 || island > State.Session.State.unlockedIsland || item == null || item.IsConsumed || item.IsHeld || item.IsAlive || item.Creature == null || !item.IsDrip || !item.HasBeenHeld || !scene.Items.Contains(item))
                return false;
            if (scene.Items.Any(value => value != null && !value.IsConsumed && value.Creature?.IsBoss == true && value.IsAlive))
            {
                Notify("首领战斗结束后才能投入老虎机。");
                return false;
            }

            HowToFishSkinDefinition reward = null;
            bool isNew = false;
            bool newOutfit = false;
            try
            {
                if (!item.TryConsume(() =>
                {
                    int roll = UnityEngine.Random.Range(0, 100);
                    var rarity = roll < 70 ? HowToFishSkinRarity.Common : roll < 90 ? HowToFishSkinRarity.Rare : HowToFishSkinRarity.Legendary;
                    var pool = HowToFishSkinCatalog.Rewards(island, rarity);
                    reward = pool[UnityEngine.Random.Range(0, pool.Length)];
                    isNew = State.Session.UnlockSkin(reward.Id);
                    if (rarity == HowToFishSkinRarity.Legendary)
                        newOutfit = State.Session.UnlockOutfit("Jacob");
                    if (TrySave())
                        return;
                    if (isNew)
                        State.Session.State.unlockedSkins.Remove(reward.Id);
                    if (newOutfit)
                        State.Session.State.unlockedOutfits.Remove("Jacob");
                    throw new IOException("老虎机进度未能保存，鱼获已保留。");
                }))
                    return false;
            }
            catch (IOException exception)
            {
                Notify(exception.Message);
                return false;
            }

            result = $"老虎机：{reward.ItemId} · {reward.Name}（{reward.Rarity}）" + (isNew ? "，已解锁。" : "，重复中奖，鱼获已消耗。") + (string.IsNullOrEmpty(scene.SaveStore.SkinProfileNotice) ? "" : "\n" + scene.SaveStore.SkinProfileNotice);
            Notify("老虎机转动中……");
            return true;
        }

        private void ChangeSkin()
        {
            if (State.Session == null || State.IsPaused || State.IsExiting)
                return;
            string skinId;
            if (scene.Player.IsDriving)
            {
                State.Session.ChangeBoatSkin();
                skinId = State.Session.State.boatSkinId;
            }
            else if (scene.Player.HeldItem != null)
            {
                if (!scene.Player.HeldItem.ChangeSkin())
                {
                    Notify("手持物品没有可切换的皮肤。");
                    return;
                }

                skinId = scene.Player.HeldItem.SkinId;
            }
            else
            {
                if (scene.Player.Equipment == null || !State.Session.ChangeEquipmentSkin(scene.Player.Equipment.Id))
                {
                    Notify("请先手持有皮肤的装备或驾驶船只。");
                    return;
                }

                skinId = State.Session.State.inventory.Find(item => item.id == scene.Player.Equipment.Id).skinId;
            }

            Notify("当前外观：" + (HowToFishSkinCatalog.Find(skinId)?.Name ?? "Default"));
        }

        private bool TrySettleRoulette(HowToFishRoulette roulette, IReadOnlyDictionary<HowToFishWorldItem, HowToFishRouletteColor> bets, HowToFishRouletteColor result, out string announcement)
        {
            announcement = null;
            if (State.Session == null || State.IsPaused || State.IsExiting || roulette == null || roulette.gameObject.scene != scene.gameObject.scene || roulette.Island < 0 || roulette.Island > State.Session.State.unlockedIsland || bets == null || bets.Count == 0 || (uint)result > 2)
                return false;
            var settled = new Dictionary<HowToFishWorldItem, float>();
            int winners = 0;
            try
            {
                foreach (var bet in bets)
                {
                    if (bet.Key == null || !bet.Key.CanBet || !scene.Items.Contains(bet.Key) || (uint)bet.Value > 2)
                    {
                        Notify("押注物品状态已变化，请重新放置。");
                        return false;
                    }

                    float multiplier = bet.Key.BettingMultiplier * (bet.Value == HowToFishRouletteColor.Green ? 35 : 2);
                    // 每件物品按它所押颜色的最高可得值检查，不能靠抽到输局避开溢出检查。
                    bet.Key.ValueAfterRoulette(multiplier);
                    bool won = bet.Value == result;
                    settled.Add(bet.Key, won ? multiplier : 0);
                    if (won)
                        winners++;
                }
            }
            catch (Exception exception) when (exception is ArgumentOutOfRangeException || exception is InvalidOperationException)
            {
                Notify("本轮潜在中奖价值超出当前金额数值范围，未扣物品；请取回高倍率鱼获。");
                return false;
            }

            bool newOutfit = result == HowToFishRouletteColor.Green && winners > 0 && State.Session.UnlockOutfit("Andrei");
            if (!TrySave(settled))
            {
                if (newOutfit)
                    State.Session.State.unlockedOutfits.Remove("Andrei");
                return false;
            }

            foreach (var entry in settled)
                entry.Key.ApplyRouletteResult(entry.Value);
            string color = result == HowToFishRouletteColor.Green ? "绿" : result == HowToFishRouletteColor.Red ? "红" : "黑";
            announcement = $"轮盘落在{color}色：{winners}件获胜，{settled.Count - winners}件失去。取回获胜鱼获后出售兑现。";
            return true;
        }

        private bool TrySave(IReadOnlyDictionary<HowToFishWorldItem, float> rouletteResults = null)
        {
            if (State.Session == null)
                return false;
            if (scene.Items.Any(item => item != null && !item.IsConsumed && item.Creature?.IsBoss == true && item.IsAlive))
            {
                Notify("首领战斗结束后才能保存。");
                return false;
            }

            try
            {
                var state = State.Session.State;
                State.Session.ReconcileOutfits();
                state.boatPosition = scene.Boat.transform.position;
                state.boatYaw = scene.Boat.transform.eulerAngles.y;
                state.isDriving = scene.Player.IsDriving;
                state.isOnBoat = scene.Player.IsDriving || (Physics.Raycast(scene.Player.transform.position + Vector3.up * .1f, Vector3.down, out var floor, 2, ~0, QueryTriggerInteraction.Ignore) && floor.rigidbody == scene.Boat.GetComponent<Rigidbody>());
                state.boatLocalPosition = state.isOnBoat ? scene.Boat.transform.InverseTransformPoint(scene.Player.transform.position) : Vector3.zero;
                state.boatLocalYaw = state.isOnBoat ? Mathf.DeltaAngle(scene.Boat.transform.eulerAngles.y, scene.Player.transform.eulerAngles.y) : 0;
                if (!state.isOnBoat && scene.Player.GetComponent<CharacterController>().isGrounded && scene.Player.transform.position.y > .1f)
                {
                    state.safePosition = scene.Player.transform.position;
                    state.safeYaw = scene.Player.transform.eulerAngles.y;
                    state.safeIsland = scene.Player.Island;
                }

                state.worldItems.Clear();
                foreach (var item in scene.Items)
                    if (item != null && !item.IsConsumed)
                    {
                        var snapshot = item.Snapshot();
                        if (rouletteResults != null && rouletteResults.TryGetValue(item, out float multiplier))
                        {
                            if (multiplier == 0)
                                continue;
                            snapshot.bettingMultiplier = multiplier;
                        }

                        state.worldItems.Add(snapshot);
                    }

                scene.SaveStore.Save(State.Slot, state);
                Notify("已保存到存档 " + (State.Slot + 1) + (string.IsNullOrEmpty(scene.SaveStore.SkinProfileNotice) ? "" : "\n" + scene.SaveStore.SkinProfileNotice));
                return true;
            }
            catch (Exception exception)
            {
                Notify("保存失败，原存档已保留：" + exception.Message);
                Debug.LogException(exception, scene);
                return false;
            }
        }

        private void SetPaused(bool paused)
        {
            State.IsPaused = paused || State.Session == null || State.ShowEnding;
            if (!State.IsPaused)
                State.ShowJournal = false;
            scene.ApplyPauseState();
        }

        private void Notify(string message)
        {
            State.Notice = message;
            State.NoticeUntil = Time.unscaledTime + 6;
        }

        private void Interact(Collider collider)
        {
            if (scene.Player.IsDriving)
            {
                scene.Boat.SetDriver(null);
                scene.Player.Teleport(scene.Boat.ExitPosition, scene.Boat.transform.eulerAngles.y);
                return;
            }

            if (collider == null)
                return;
            var roulette = collider.GetComponentInParent<HowToFishRoulette>();
            if (roulette != null && collider.GetComponentInParent<HowToFishWorldItem>() == null)
            {
                roulette.TrySpin();
                return;
            }

            var station = collider.GetComponentInParent<HowToFishStation>();
            if (station == null)
            {
                scene.Player.PickUp(collider.GetComponentInParent<HowToFishWorldItem>());
                return;
            }

            int outfitsBefore = State.Session.State.unlockedOutfits.Count;
            int moneyBefore = State.Session.State.money;
            switch (station.Kind)
            {
                case HowToFishStationKind.Product:
                    Notify(State.Session.TryBuy(station.ItemId, station.Island, out var reason) ? "已购买 " + scene.Catalog.FindItem(station.ItemId).DisplayName : reason);
                    break;
                case HowToFishStationKind.Sell:
                    if (scene.Player.HeldItem != null && scene.Player.HeldItem.TrySell(out int value))
                        Notify("售出鱼获 +$" + value);
                    else
                        Notify("拿着处理好的鱼获来出售。");
                    break;
                case HowToFishStationKind.Keeper:
                    TalkToKeeper();
                    break;
                case HowToFishStationKind.ForestLady:
                    if (!TryDeliverToForestLady(scene.Player.HeldItem))
                        Notify(State.Session.State.unlockedIsland >= 2 ? "湖畔女士：沙漠在西北方，坐标已经给你。" : $"湖畔女士：带给我三条草地上的水蛭，我给你改造鱼饵。已收到 {State.Session.State.forestLeeches}/3；打败巨型食人鱼后，把骨架带回来。");
                    break;
                case HowToFishStationKind.BoatWheel:
                    if (!State.Session.State.hasBoatKey)
                    {
                        Notify("先帮灯塔看守人拿到船钥匙。");
                        break;
                    }

                    scene.Player.Board(scene.Boat.Seat);
                    scene.Boat.SetDriver(scene.Input);
                    break;
                case HowToFishStationKind.Grill:
                    Notify(State.Session.State.hasGrill ? "把鱼获放到或拿在烤架上方持续加热；熟成后及时取走，继续加热会烧焦。" : "先向烧烤师交付一条死蓝鲨或哥布林鲨，再领取打火机。");
                    break;
                case HowToFishStationKind.GrillMaster:
                    if (TryDeliverToGrillMaster(scene.Player.HeldItem))
                        break;
                    if (State.Session.State.hasGrill)
                    {
                        Notify("烤炉已经可以使用，留意鱼获颜色和售价，别烤焦了。");
                        break;
                    }

                    if (!State.Session.State.completedQuests.Contains("GrillSharkDelivered"))
                    {
                        Notify("带来一条你拿过的死蓝鲨或哥布林鲨，我就给你打火机。");
                        break;
                    }

                    if (State.Session.Count("Lighter") >= 100000)
                    {
                        Notify("打火机已达到携带上限，奖励暂时保留。");
                        break;
                    }

                    State.Session.GrantItem("Lighter");
                    State.Session.State.hasGrill = true;
                    if (!State.Session.State.completedQuests.Contains("DesertGrill"))
                        State.Session.State.completedQuests.Add("DesertGrill");
                    if (TrySave())
                        Notify("获得打火机，烤炉已解锁。");
                    break;
                case HowToFishStationKind.Tourist:
                    if (!TryDeliverToTourist(scene.Player.HeldItem))
                        Notify(State.Session.State.unlockedIsland >= 3 ? "游客：岩石岛的坐标已经给你，祝你一路顺风。" : "游客：把处理好的濒危鱼带给我，颌针鱼、海马或鱼缸鱼都可以。我给你萝卜去钓河豚，之后请把鱼鳍带回来。");
                    break;
                case HowToFishStationKind.Anvil:
                case HowToFishStationKind.AmmoUpgrade:
                    var weaponKind = station.Kind == HowToFishStationKind.Anvil ? HowToFishItemKind.Melee : HowToFishItemKind.Gun;
                    if (scene.Player.Equipment?.Kind != weaponKind)
                    {
                        Notify(weaponKind == HowToFishItemKind.Melee ? "请先手持近战武器。" : "请先手持枪械。");
                        break;
                    }

                    Notify(State.Session.TryUpgrade(scene.Player.Equipment?.Id, station.Island, out var upgradeError) ? "武器升级完成。" : upgradeError);
                    break;
                case HowToFishStationKind.Attachment:
                    Notify(State.Session.TryBuyAttachment(scene.Player.Equipment?.Id, station.Attachment, station.Island, out var attachmentError) ? "已安装" + station.Label : attachmentError);
                    break;
                case HowToFishStationKind.InventoryUpgrade:
                    Notify(State.Session.TryExpandInventory(station.Island, out var inventoryError) ? "装备栏已扩容。" : inventoryError);
                    break;
                case HowToFishStationKind.MotorUpgrade:
                    Notify(State.Session.TryBuyMotor(station.MotorTier, station.Island, out var motorError) ? "船只马达已升级。" : motorError);
                    break;
                case HowToFishStationKind.BoatRadar:
                    Notify(State.Session.TryBuyBoatRadar(station.Island, out var radarError) ? "船载雷达已安装。" : radarError);
                    break;
                case HowToFishStationKind.Scientist:
                    TalkToScientist();
                    break;
                case HowToFishStationKind.MilitaryDeparture:
                    if (!State.Session.State.hasMilitaryBoatKey)
                    {
                        Notify("需要科学家交给你的军用船钥匙。");
                        break;
                    }

                    bool wasFinished = State.Session.State.hasFinished;
                    State.Session.State.hasFinished = true;
                    bool newScientist = State.Session.UnlockOutfit("Scientist");
                    // 旧档未统计暂停时间，不能据不完整时钟补授限时奖励；小于一小时的边界为推定。
                    bool newBean = !wasFinished && State.Session.State.tracksPausedPlaytime && State.Session.State.playedSeconds < 3600 && State.Session.UnlockOutfit("Bean");
                    if (TrySave())
                    {
                        State.ShowEnding = true;
                        State.ShowJournal = false;
                        SetPaused(true);
                        if (!wasFinished)
                            scene.RequestSound(HowToFishSound.Ending, scene.Player.transform.position);
                        Notify("你乘军用船回到了大陆。航程已保存，可以继续探索群岛。");
                    }
                    else
                    {
                        State.Session.State.hasFinished = wasFinished;
                        if (newScientist)
                            State.Session.State.unlockedOutfits.Remove("Scientist");
                        if (newBean)
                            State.Session.State.unlockedOutfits.Remove("Bean");
                    }

                    break;
                case HowToFishStationKind.Islander:
                    if (!TryDeliverToIslander(scene.Player.HeldItem))
                        Notify(State.Session.State.unlockedIsland >= 4 ? "岛民：火山的坐标已经给你，准备好再出发。" : "岛民：用专业首领饵钓金枪鱼，打倒后把完整生鱼放在岸上引鸟。屋顶能挡落物，请把鸟头带回来。");
                    break;
            }

            if (State.Session.State.money != moneyBefore)
                scene.RequestSound(HowToFishSound.Trade, station.transform.position);
            if (State.Session.State.unlockedOutfits.Count > outfitsBefore && (station.Kind == HowToFishStationKind.MotorUpgrade || station.Kind == HowToFishStationKind.Attachment || station.Kind == HowToFishStationKind.AmmoUpgrade || station.Kind == HowToFishStationKind.Anvil))
                SaveOutfitProgress();
        }

        private void TalkToKeeper()
        {
            if (TryDeliverToKeeper(scene.Player.HeldItem))
                return;
            if (State.Session.State.hasBoatKey)
            {
                Notify("看守人：钥匙交给你了，去探索下一座岛吧。");
                return;
            }

            if (State.Session.TryConsume("CrabMeat"))
            {
                GiveBoatKey();
                return;
            }

            if (State.Session.Count("CrabRod") == 0)
            {
                Notify("看守人：把蛤蜊交给我，攒够 $3 就去木门买钓竿。蛤蜊也能填饱肚子。");
                return;
            }

            if (State.Session.Count("EmptyBeerCan") > 0)
            {
                Notify("看守人：用空啤酒罐作饵，打败蜘蛛蟹，把蟹壳带回来。");
                return;
            }

            if (State.Session.TryConsume("Beer"))
            {
                GiveEmptyCan();
            }
            else
                Notify("看守人：先给我一罐啤酒，我有办法帮你离开这里。");
        }

        private void DeliverToStation(HowToFishStation station, HowToFishWorldItem item)
        {
            if (State.Session == null || State.IsPaused || State.IsExiting)
                return;
            if (station.Kind == HowToFishStationKind.Keeper)
                TryDeliverToKeeper(item);
            else if (station.Kind == HowToFishStationKind.ForestLady)
                TryDeliverToForestLady(item);
            else if (station.Kind == HowToFishStationKind.Tourist)
                TryDeliverToTourist(item);
            else if (station.Kind == HowToFishStationKind.Islander)
                TryDeliverToIslander(item);
            else if (station.Kind == HowToFishStationKind.Scientist)
                TryDeliverToScientist(item);
            else if (station.Kind == HowToFishStationKind.GrillMaster)
                TryDeliverToGrillMaster(item);
        }

        private bool TryDeliverToGrillMaster(HowToFishWorldItem item)
        {
            if (item == null || item.IsConsumed || item.IsAlive || !item.HasBeenHeld || State.Session.State.hasGrill || State.Session.State.completedQuests.Contains("GrillSharkDelivered") || (item.DefinitionId != "BlueShark" && item.DefinitionId != "GoblinShark"))
                return false;
            return item.TryConsume(() =>
            {
                State.Session.State.completedQuests.Add("GrillSharkDelivered");
                if (TrySave())
                    Notify("烧烤师收下了鲨鱼，再交谈领取打火机。");
            });
        }

        private void ContinueAfterEnding()
        {
            if (!State.ShowEnding)
                return;
            State.ShowEnding = false;
            SetPaused(false);
        }

        private void TalkToScientist()
        {
            if (TryDeliverToScientist(scene.Player.HeldItem))
                return;
            if (State.Session.State.hasMilitaryBoatKey)
            {
                Notify("科学家：军用船钥匙交给你了。登上黑色充气艇，在船舵处启程吧。");
                return;
            }

            if (State.Session.State.volcanoFish < 5)
            {
                Notify($"科学家：给我五条处理好的鱼，我来准备鱼桶。已收到 {State.Session.State.volcanoFish}/5。");
                return;
            }

            if (State.Session.Count("FishBucket") > 0)
            {
                Notify("科学家：用鱼桶钓鲸鱼；把完整生鲸尸投入火山，再把变异鲸鱼鳍交给我。");
                return;
            }

            State.Session.GrantItem("FishBucket");
            if (!State.Session.State.completedQuests.Contains("VolcanoFishBucket"))
                State.Session.State.completedQuests.Add("VolcanoFishBucket");
            if (TrySave())
                Notify("领到鱼桶。用普通鱼竿在火山周围水域钓出弓头鲸。");
        }

        private bool TryDeliverToScientist(HowToFishWorldItem item)
        {
            if (item == null || item.IsConsumed || item.IsAlive || State.Session.State.unlockedIsland < 4)
                return false;
            if (item.DefinitionId == "WhaleFin" && !State.Session.State.hasMilitaryBoatKey)
                return item.TryConsume(() =>
                {
                    State.Session.GrantItem("MilitaryBoatKey");
                    State.Session.State.hasMilitaryBoatKey = true;
                    if (!State.Session.State.completedQuests.Contains("VolcanoWhale"))
                        State.Session.State.completedQuests.Add("VolcanoWhale");
                    if (TrySave())
                        Notify("科学家：拿好军用船钥匙，你可以回家了！");
                });
            var creature = item.Creature;
            if (State.Session.State.volcanoFish >= 5 || creature == null || creature.IsBoss || creature.IsGroundPickup || creature.Island == 0)
                return false;
            return item.TryConsume(() =>
            {
                State.Session.State.volcanoFish++;
                if (TrySave())
                    Notify(State.Session.State.volcanoFish == 5 ? "科学家：鱼桶准备好了，再与我交谈领取。" : $"科学家：已经收到 {State.Session.State.volcanoFish}/5 条鱼。");
            });
        }

        private bool TryDeliverToIslander(HowToFishWorldItem item)
        {
            if (item == null || item.IsConsumed || item.IsAlive || item.DefinitionId != "AlbatrossHead" || State.Session.State.unlockedIsland < 3)
                return false;
            return item.TryConsume(() =>
            {
                State.Session.State.unlockedIsland = 4;
                if (!State.Session.State.completedQuests.Contains("RocksAlbatross"))
                    State.Session.State.completedQuests.Add("RocksAlbatross");
                if (TrySave())
                    Notify("岛民：终于清静了！这是北方火山岛的坐标。");
            });
        }

        private bool TryDeliverToTourist(HowToFishWorldItem item)
        {
            if (item == null || item.IsConsumed || item.IsAlive || State.Session.State.unlockedIsland < 2)
                return false;
            if (item.DefinitionId == "PufferfishFin")
                return item.TryConsume(() =>
                {
                    State.Session.State.unlockedIsland = Mathf.Max(State.Session.State.unlockedIsland, 3);
                    if (!State.Session.State.completedQuests.Contains("DesertPufferfish"))
                        State.Session.State.completedQuests.Add("DesertPufferfish");
                    if (TrySave())
                        Notify("游客：谢谢！这是西北方向岩石岛的坐标。");
                });
            if (item.Creature?.IsEndangered != true)
                return false;
            if (State.Session.Count("Carrot") >= 100000)
            {
                Notify("萝卜已达到携带上限。");
                return true;
            }

            return item.TryConsume(() =>
            {
                State.Session.GrantItem("Carrot");
                if (!State.Session.State.completedQuests.Contains("DesertCarrot"))
                    State.Session.State.completedQuests.Add("DesertCarrot");
                if (TrySave())
                    Notify("游客：萝卜给你了。用普通鱼竿钓出河豚，记得把鱼鳍交回来。");
            });
        }

        private bool TryDeliverToForestLady(HowToFishWorldItem item)
        {
            if (item == null || item.IsConsumed || State.Session.State.unlockedIsland < 1)
                return false;
            if (item.DefinitionId == "Leech" && State.Session.State.forestLeeches == 2 && State.Session.Count("ModifiedLeech") >= 100000)
            {
                Notify("改造水蛭已达到携带上限，请先使用一些。");
                return true;
            }

            if (item.DefinitionId == "Leech")
                return item.TryConsume(() =>
                {
                    if (State.Session.State.forestLeeches == 2)
                    {
                        State.Session.GrantItem("ModifiedLeech");
                        State.Session.State.forestLeeches = 0;
                        if (!State.Session.State.completedQuests.Contains("ForestLeeches"))
                            State.Session.State.completedQuests.Add("ForestLeeches");
                    }
                    else
                        State.Session.State.forestLeeches++;
                    if (TrySave())
                        Notify(State.Session.State.forestLeeches == 0 ? "湖畔女士：改造水蛭给你了，在湖边用它钓出巨型食人鱼！" : $"湖畔女士：已收到 {State.Session.State.forestLeeches}/3 条水蛭。");
                });
            if (item.DefinitionId == "PiranhaSkeleton")
                return item.TryConsume(() =>
                {
                    State.Session.State.unlockedIsland = Mathf.Max(State.Session.State.unlockedIsland, 2);
                    if (!State.Session.State.completedQuests.Contains("ForestPiranha"))
                        State.Session.State.completedQuests.Add("ForestPiranha");
                    if (TrySave())
                        Notify("湖畔女士：谢谢！这是西北方沙漠岛的坐标。");
                });
            return false;
        }

        private bool TryDeliverToKeeper(HowToFishWorldItem item)
        {
            if (item == null || item.IsConsumed || item.IsAlive)
                return false;
            if (item.DefinitionId == "CrabMeat" && !State.Session.State.hasBoatKey)
                return item.TryConsume(GiveBoatKey);
            if (item.DefinitionId == "Beer")
                return item.TryConsume(GiveEmptyCan);
            if (item.Creature != null && !item.Creature.IsBoss && item.TrySell(out int money))
            {
                Notify("看守人收下鱼获，付给你 $" + money);
                return true;
            }

            return false;
        }

        private void GiveBoatKey()
        {
            State.Session.State.hasBoatKey = true;
            State.Session.State.unlockedIsland = Mathf.Max(State.Session.State.unlockedIsland, 1);
            if (!State.Session.State.completedQuests.Contains("KeeperCrab"))
                State.Session.State.completedQuests.Add("KeeperCrab");
            if (TrySave())
                Notify("看守人：船钥匙给你了。带上雷达，向西北寻找森林岛。");
        }

        private void GiveEmptyCan()
        {
            State.Session.GrantItem("EmptyBeerCan");
            if (!State.Session.State.completedQuests.Contains("KeeperBeer"))
                State.Session.State.completedQuests.Add("KeeperBeer");
            if (TrySave())
                Notify("看守人：谢了！空罐拿去钓蜘蛛蟹吧。");
        }

        private bool Damage(float amount)
        {
            if (State.Session == null || State.Session.State.health <= 0 || amount <= 0 || float.IsNaN(amount) || float.IsInfinity(amount))
                return false;
            State.Session.State.health = Mathf.Max(0, State.Session.State.health - amount);
            if (State.Session.State.health > 0)
                return false;
            State.Session.State.poisonSeconds = State.Session.State.burningSeconds = 0;
            State.Session.State.poisonDamagePerSecond = State.Session.State.burningDamagePerSecond = 0;
            return true;
        }

        private void ApplyDamageStatus(HowToFishDamageStatusAction action)
        {
            if (State.Session == null || !(action.Seconds > 0 && action.Seconds <= 60) || !(action.DamagePerSecond > 0 && action.DamagePerSecond <= 1000))
                return;
            var state = State.Session.State;
            if (action.Burning)
            {
                state.burningSeconds = Mathf.Max(state.burningSeconds, action.Seconds);
                state.burningDamagePerSecond = Mathf.Max(state.burningDamagePerSecond, action.DamagePerSecond);
            }
            else
            {
                state.poisonSeconds = Mathf.Max(state.poisonSeconds, action.Seconds);
                state.poisonDamagePerSecond = Mathf.Max(state.poisonDamagePerSecond, action.DamagePerSecond);
            }
        }

        private void TickDamageStatus(HowToFishDamageStatusTickAction action)
        {
            var state = State.Session.State;
            action.Damage = Mathf.Min(action.Delta, state.poisonSeconds) * state.poisonDamagePerSecond + Mathf.Min(action.Delta, state.burningSeconds) * state.burningDamagePerSecond;
            state.poisonSeconds = Mathf.Max(0, state.poisonSeconds - action.Delta);
            state.burningSeconds = Mathf.Max(0, state.burningSeconds - action.Delta);
            if (state.poisonSeconds == 0)
                state.poisonDamagePerSecond = 0;
            if (state.burningSeconds == 0)
                state.burningDamagePerSecond = 0;
            action.Lethal = action.Damage >= state.health;
        }

        private void RestoreFood(HowToFishRestoreFoodAction action)
        {
            State.Session.State.hunger = Mathf.Min(100, State.Session.State.hunger + action.Hunger);
            State.Session.State.health = Mathf.Min(100, State.Session.State.health + action.Health);
        }

        private void Ammo(HowToFishAmmoAction action)
        {
            var item = State.Session.State.inventory.Find(value => value.id == action.ItemId);
            if (item == null)
                return;
            switch (action.Operation)
            {
                case HowToFishAmmoOperation.Initialize:
                    item.ammo = item.ammo < 0 ? action.Capacity : Mathf.Min(item.ammo, action.Capacity);
                    break;
                case HowToFishAmmoOperation.Reload:
                    item.ammo = action.Capacity;
                    break;
                case HowToFishAmmoOperation.Consume:
                    if (item.ammo > 0)
                        item.ammo--;
                    break;
            }
        }

        private void Cooking(HowToFishCookingAction action)
        {
            var item = State.Session.State.inventory.Find(value => value.id == action.ItemId);
            if (item != null)
                item.cooking = action.Extinguish ? 0 : Mathf.Clamp01(item.cooking + action.Heat);
        }

        private void Equip(string id)
        {
            State.Session.State.equippedItemId = id;
            if (id == null)
                return;
            int slot = State.Session.State.equipmentSlots.IndexOf(id);
            if (slot >= 0)
                State.Session.State.selectedEquipmentSlot = slot;
        }

        private void StartSlot(int index, bool newGame, bool recoverBackup = false)
        {
            if (State.Session != null)
            {
                Notify("请先返回 Hub，再切换存档。");
                return;
            }

            try
            {
                if (recoverBackup)
                    scene.SaveStore.RestoreBackup(index);
                var loaded = scene.SaveStore.Load(index);
                if (loaded.Status != HowToFishLoadStatus.Ready && loaded.Status != HowToFishLoadStatus.Empty)
                {
                    Notify(loaded.Error);
                    return;
                }

                if (!newGame && loaded.Status != HowToFishLoadStatus.Ready)
                {
                    Notify("此槽还没有存档。");
                    return;
                }

                var state = newGame ? new HowToFishSaveData
                {
                    safePosition = scene.StartPoint.position,
                    safeYaw = scene.StartPoint.eulerAngles.y,
                    boatPosition = scene.Boat.transform.position,
                    boatYaw = scene.Boat.transform.eulerAngles.y,
                    tracksPausedPlaytime = true
                }

                : loaded.Data;
                var profile = scene.SaveStore.LoadSharedSkins(state);
                State.SelectedOutfitId = profile.selectedOutfitId;
                string profileNotice = scene.SaveStore.SkinProfileNotice;
                var nextSession = new HowToFishSession(scene.Catalog, state);
                if (state.unlockedOutfits.Count != profile.unlockedOutfits.Count)
                    scene.SaveStore.LoadSharedSkins(state);
                if (State.Session != null)
                    State.Session.Changed -= OnRulesChanged;
                State.Version++;
                State.Slot = index;
                State.Session = nextSession;
                State.Session.Changed += OnRulesChanged;
                if (State.Session.Count("FreeLure") == 0)
                    State.Session.GrantItem("FreeLure");
                scene.BuildSession(newGame, state, profileNotice);
            }
            catch (Exception exception)
            {
                Notify("无法开始游戏：" + exception.Message);
                Debug.LogException(exception, scene);
            }
        }

        private void Respawn()
        {
            if (State.Session == null)
                return;
            State.Session.State.health = 100;
            State.Session.State.hunger = Mathf.Max(35, State.Session.State.hunger);
            scene.RespawnPresentation();
        }

        private void RefreshSlots()
        {
            if (scene.SaveStore == null)
                return;
            summaryStore = scene.SaveStore;
            for (int i = 0; i < State.SlotInfos.Length; i++)
                State.SlotInfos[i] = scene.SaveStore.Load(i);
        }

        private void Tick(HowToFishTickAction action)
        {
            if (State.Notice != null && Time.unscaledTime > State.NoticeUntil)
                State.Notice = null;
            if (State.Session == null)
                return;
            if (!State.ShowEnding)
                State.Session.State.playedSeconds += action.UnscaledDelta;
            if (State.IsPaused)
                return;
            State.Session.State.hunger = Mathf.Max(0, State.Session.State.hunger - action.Delta * .06f);
            if (State.Session.State.hunger <= 0)
                scene.Player.Damage(action.Delta);
        }

        private void Ui(HowToFishUiAction action)
        {
            if (State.IsExiting && action.Command != HowToFishUiCommand.CancelSettings)
                return;
            switch (action.Command)
            {
                case HowToFishUiCommand.ToggleJournal:
                    State.ShowJournal = !State.ShowJournal;
                    SetPaused(State.ShowJournal);
                    break;
                case HowToFishUiCommand.OpenSettings:
                case HowToFishUiCommand.OpenOutfits:
                    if (State.MainPage is HowToFishPage.None or HowToFishPage.Journal)
                        return;
                    if (action.Command == HowToFishUiCommand.OpenOutfits && State.Session == null)
                        return;
                    scene.UI.CaptureReturnFocus();
                    State.RequestedOverlay = action.Command == HowToFishUiCommand.OpenSettings ? HowToFishPage.Settings : HowToFishPage.Outfits;
                    break;
                case HowToFishUiCommand.CloseSettings:
                    if (State.RequestedOverlay == HowToFishPage.Settings)
                        State.RequestedOverlay = HowToFishPage.None;
                    break;
                case HowToFishUiCommand.CloseOutfits:
                    if (State.RequestedOverlay == HowToFishPage.Outfits)
                        State.RequestedOverlay = HowToFishPage.None;
                    break;
                case HowToFishUiCommand.Exit:
                    State.RequestedOverlay = HowToFishPage.None;
                    scene.ExitScene();
                    break;
                case HowToFishUiCommand.ContinueSlot:
                    var inspected = scene.SaveStore.Load(action.Slot);
                    StartSlot(action.Slot, inspected.Status == HowToFishLoadStatus.Empty, inspected.Status == HowToFishLoadStatus.RecoveryAvailable);
                    break;
                case HowToFishUiCommand.NewSlot:
                    var saved = scene.SaveStore.Load(action.Slot);
                    if (saved.Status == HowToFishLoadStatus.Ready && State.ConfirmNewSlot != action.Slot)
                    {
                        State.ConfirmNewSlot = action.Slot;
                        Notify("再次点击此槽的“重开”，确认覆盖当前航程；上一份进度会保留为备份。");
                        return;
                    }

                    StartSlot(action.Slot, true);
                    break;
                case HowToFishUiCommand.BeginSettings:
                    State.SettingsSnapshot = scene.GetPreferences();
                    State.SettingsOpen = true;
                    SetEditingSettings(true);
                    break;
                case HowToFishUiCommand.CancelSettings:
                    scene.Input.CancelRebind();
                    if (State.SettingsOpen && State.SettingsSnapshot != null)
                        ApplyPreferences(State.SettingsSnapshot);
                    State.SettingsOpen = false;
                    State.SettingsSnapshot = null;
                    SetEditingSettings(false);
                    break;
                case HowToFishUiCommand.SaveSettings:
                    if (!SavePreferences())
                        return;
                    State.SettingsSnapshot = null;
                    State.SettingsOpen = false;
                    SetEditingSettings(false);
                    break;
            }

            action.Success = true;
        }

        internal void DetachSession()
        {
            if (State.Session != null)
                State.Session.Changed -= OnRulesChanged;
            summaryStore = null;
            sampleAt = 0;
        }

        public void Dispose()
        {
            if (State.Session != null)
                State.Session.Changed -= OnRulesChanged;
            State.Version++;
        }
    }
}
