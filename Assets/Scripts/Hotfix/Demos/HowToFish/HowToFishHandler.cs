using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Core.Runtime;
using UnityEngine;

namespace Hotfix.HowToFish
{
    internal sealed class HowToFishHandler : HandlerBase<HowToFishAction, HowToFishData>, IDisposable
    {
        private readonly HowToFishWorld scene;
        private int reducing;
        private float sampleAt;
        private HowToFishSaveStore summaryStore;
        internal HowToFishHandler(HowToFishWorld scene) => this.scene = scene;
        private HowToFishSession session => State.Session;
        private HowToFishCatalog catalog => scene.Catalog;
        private HowToFishPlayer player => scene.Player;
        private HowToFishBoat boat => scene.Boat;
        private HowToFishInput input => scene.Input;
        private HowToFishSaveStore saves => scene.SaveStore;
        private HowToFishLocalPreferencesStore preferencesStore => scene.PreferencesStore;
        private IReadOnlyList<HowToFishWorldItem> items => scene.Items;
        private bool IsPaused { get => State.IsPaused; set => State.IsPaused = value; }
        private bool ShowJournal { get => State.ShowJournal; set => State.ShowJournal = value; }
        private bool ShowEnding { get => State.ShowEnding; set => State.ShowEnding = value; }
        private bool IsEditingSettings { get => State.IsEditingSettings; set => State.IsEditingSettings = value; }
        private bool exiting => State.IsExiting;
        private int slot => State.Slot;
        private string SelectedOutfitId { get => State.SelectedOutfitId; set => State.SelectedOutfitId = value; }
        private HowToFishWorldItem Spawn(string id, Vector3 position, bool drip) => scene.Spawn(id, position, drip);
        internal void Initialize() { RefreshSlots(); Publish(); }
        private void OnRulesChanged() { scene.ApplyRulePresentation(); Publish(); }
        internal void Publish()
        {
            if (reducing != 0 || !ReferenceEquals(GlobalData.Get<HowToFishData>(), State)) return;
            if (session == null && !ReferenceEquals(summaryStore, saves)) RefreshSlots();
            if (State.MainPage == HowToFishPage.None) State.RequestedOverlay = HowToFishPage.None;
            ApplyState();
        }
        protected override void Reduce(HowToFishAction action)
        {
            if (!ReferenceEquals(GlobalData.Get<HowToFishData>(), State)) return;
            reducing++;
            try
            {
                switch (action)
                {
                    case HowToFishTryConsumeAction request when session != null && ReferenceEquals(request.Source, session):
                        request.Result = session.TryConsume(request.id); break;
                    case HowToFishGrantItemAction request when session != null && ReferenceEquals(request.Source, session):
                        session.GrantItem(request.id, request.count); break;
                    case HowToFishGrantEquipmentAction request when session != null && ReferenceEquals(request.Source, session):
                        session.GrantEquipment(request.equipment); break;
                    case HowToFishTryStoreEquipmentAction request when session != null && ReferenceEquals(request.Source, session):
                        request.Result = session.TryStoreEquipment(request.id, request.slot); break;
                    case HowToFishSellCatchAction request when session != null && ReferenceEquals(request.Source, session):
                        request.Result = session.SellCatch(request.creatureId, request.cooking, request.drip, request.styleMultiplier, request.bettingMultiplier, request.weightMultiplier); break;
                    case HowToFishRegisterCreatureAction request when session != null && ReferenceEquals(request.Source, session):
                        session.RegisterCreature(request.creatureId, request.defeated, request.drip); break;
                    case HowToFishStartSlotAction request when ReferenceEquals(request.Source, scene):
                        StartSlot(request.index, request.newGame, request.recoverBackup); break;
                    case HowToFishSaveAction request when ReferenceEquals(request.Source, scene):
                        TrySave(); break;
                    case HowToFishApplyPreferencesAction request when ReferenceEquals(request.Source, scene):
                        ApplyPreferences(request.value); break;
                    case HowToFishSavePreferencesAction request when ReferenceEquals(request.Source, scene):
                        request.Result = SavePreferences(); break;
                    case HowToFishSetEditingSettingsAction request when ReferenceEquals(request.Source, scene):
                        SetEditingSettings(request.editing); break;
                    case HowToFishTrySelectOutfitAction request when ReferenceEquals(request.Source, scene):
                        request.Result = TrySelectOutfit(request.id); break;
                    case HowToFishTryPlaySlotMachineAction request when ReferenceEquals(request.Source, scene):
                        request.Result = TryPlaySlotMachine(request.island, request.item, out request.result); break;
                    case HowToFishChangeSkinAction request when ReferenceEquals(request.Source, scene):
                        ChangeSkin(); break;
                    case HowToFishTrySettleRouletteAction request when ReferenceEquals(request.Source, scene):
                        request.Result = TrySettleRoulette(request.roulette, request.bets, request.result, out request.announcement); break;
                    case HowToFishSetPausedAction request when ReferenceEquals(request.Source, scene):
                        SetPaused(request.paused); break;
                    case HowToFishNotifyAction request when ReferenceEquals(request.Source, scene):
                        Notify(request.message); break;
                    case HowToFishInteractAction request when ReferenceEquals(request.Source, scene):
                        Interact(request.collider); break;
                    case HowToFishDeliverToStationAction request when ReferenceEquals(request.Source, scene):
                        DeliverToStation(request.station, request.item); break;
                    case HowToFishContinueAfterEndingAction request when ReferenceEquals(request.Source, scene):
                        ContinueAfterEnding(); break;
                    case HowToFishRespawnAction request when ReferenceEquals(request.Source, scene):
                        Respawn(); break;
                    case HowToFishOnCreatureEatenAction request when ReferenceEquals(request.Source, scene):
                        OnCreatureEaten(request.item); break;
                    case HowToFishSaveOutfitProgressAction request when ReferenceEquals(request.Source, scene):
                        SaveOutfitProgress(); break;
                    case HowToFishDamageAction request when session != null && ReferenceEquals(request.Source, session): request.Died = Damage(request.Amount); return;
                    case HowToFishDamageStatusAction request when session != null && ReferenceEquals(request.Source, session): ApplyDamageStatus(request); return;
                    case HowToFishDamageStatusTickAction request when session != null && ReferenceEquals(request.Source, session): TickDamageStatus(request); return;
                    case HowToFishRestoreFoodAction request when session != null && ReferenceEquals(request.Source, session): RestoreFood(request); return;
                    case HowToFishAmmoAction request when session != null && ReferenceEquals(request.Source, session): Ammo(request); return;
                    case HowToFishCookingAction request when session != null && ReferenceEquals(request.Source, session): Cooking(request); return;
                    case HowToFishEquipAction request when session != null && ReferenceEquals(request.Source, session): Equip(request.ItemId); return;
                    case HowToFishSelectSlotAction request when session != null && ReferenceEquals(request.Source, session): session.State.selectedEquipmentSlot = request.Slot; return;
                    case HowToFishTickAction request when ReferenceEquals(request.Source, scene):
                        Tick(request); if (Time.unscaledTime < sampleAt) return; sampleAt = Time.unscaledTime + .05f; break;
                    case HowToFishUiAction request when ReferenceEquals(request.Source, scene): Ui(request); break;
                    default: return;
                }
            }
            finally { reducing--; }
            Publish();
        }
        private void ApplyPreferences(HowToFishLocalPreferences value)
        {
            if (value == null || !value.IsValid) throw new ArgumentException("输入设置无效。", nameof(value));
            input.LoadBindings(value.Bindings);
            player.MouseSensitivity = value.MouseSensitivity;
            player.GamepadSensitivity = value.GamepadSensitivity;
            player.DeadZone = value.DeadZone;
            player.InvertY = value.InvertY;
        }

        private bool SavePreferences()
        {
            try { preferencesStore.Save(scene.GetPreferences()); Notify("输入设置已保存。"); return true; }
            catch (Exception exception) { Notify("输入设置未保存：" + exception.Message); return false; }
        }

        private void SetEditingSettings(bool editing) { State.IsEditingSettings = editing; if (!editing) scene.SettingsClosedGate(); }

        private bool TrySelectOutfit(string id)
        {
            if (session == null || exiting || !HowToFishOutfitCatalog.IsUnlocked(id, session.State.unlockedOutfits) ||
                catalog.FindOutfit(id)?.Prefab == null) return false;
            try
            {
                var profile = saves.LoadSkinProfile(out bool recovered);
                foreach (string unlocked in session.State.unlockedOutfits)
                    if (!profile.unlockedOutfits.Contains(unlocked)) profile.unlockedOutfits.Add(unlocked);
                profile.selectedOutfitId = id;
                saves.SaveSkinProfile(profile);
                SelectedOutfitId = id;
                player.SetOutfit(catalog.FindOutfit(id));
                Notify("当前服装：" + HowToFishOutfitCatalog.Find(id).Name + (recovered ? "；共享档案已从备份恢复，损坏原件已保留。" : ""));
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is NotSupportedException)
            { Notify("服装选择未保存，原外观已保留：" + exception.Message); return false; }
        }

        private void SaveOutfitProgress()
        {
            // 独立成就立即进入共享档案，不在死亡事件中抢拍仍等待帧末清理的世界实体。
            try
            {
                saves.LoadSharedSkins(session.State);
                if (!string.IsNullOrEmpty(saves.SkinProfileNotice)) Notify(saves.SkinProfileNotice);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is NotSupportedException)
            { Notify("服装奖励仍在本次航程中，共享保存失败，请再次保存进度：" + exception.Message); }
        }

        private void OnCreatureEaten(HowToFishWorldItem item)
        {
            if (item.IsBurnt && session.UnlockOutfit("KioskLady")) SaveOutfitProgress();
        }

        private bool TryPlaySlotMachine(int island, HowToFishWorldItem item, out string result)
        {
            result = null;
            if (session == null || IsPaused || exiting || island < 0 || island > 4 || island > session.State.unlockedIsland ||
                item == null || item.IsConsumed || item.IsHeld || item.IsAlive || item.Creature == null ||
                !item.IsDrip || !item.HasBeenHeld || !items.Contains(item)) return false;
            if (items.Any(value => value != null && !value.IsConsumed && value.Creature?.IsBoss == true && value.IsAlive))
            { Notify("首领战斗结束后才能投入老虎机。"); return false; }
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
                    isNew = session.UnlockSkin(reward.Id);
                    if (rarity == HowToFishSkinRarity.Legendary) newOutfit = session.UnlockOutfit("Jacob");
                    if (TrySave()) return;
                    if (isNew) session.State.unlockedSkins.Remove(reward.Id);
                    if (newOutfit) session.State.unlockedOutfits.Remove("Jacob");
                    throw new IOException("老虎机进度未能保存，鱼获已保留。");
                })) return false;
            }
            catch (IOException exception) { Notify(exception.Message); return false; }
            result = $"老虎机：{reward.ItemId} · {reward.Name}（{reward.Rarity}）" +
                (isNew ? "，已解锁。" : "，重复中奖，鱼获已消耗。") +
                (string.IsNullOrEmpty(saves.SkinProfileNotice) ? "" : "\n" + saves.SkinProfileNotice);
            Notify("老虎机转动中……");
            return true;
        }

        private void ChangeSkin()
        {
            if (session == null || IsPaused || exiting) return;
            string skinId;
            if (player.IsDriving)
            {
                session.ChangeBoatSkin();
                skinId = session.State.boatSkinId;
            }
            else if (player.HeldItem != null)
            {
                if (!player.HeldItem.ChangeSkin()) { Notify("手持物品没有可切换的皮肤。"); return; }
                skinId = player.HeldItem.SkinId;
            }
            else
            {
                if (player.Equipment == null || !session.ChangeEquipmentSkin(player.Equipment.Id))
                { Notify("请先手持有皮肤的装备或驾驶船只。"); return; }
                skinId = session.State.inventory.Find(item => item.id == player.Equipment.Id).skinId;
            }
            Notify("当前外观：" + (HowToFishSkinCatalog.Find(skinId)?.Name ?? "Default"));
        }

        private bool TrySettleRoulette(HowToFishRoulette roulette, IReadOnlyDictionary<HowToFishWorldItem, HowToFishRouletteColor> bets,
            HowToFishRouletteColor result, out string announcement)
        {
            announcement = null;
            if (session == null || IsPaused || exiting || roulette == null || roulette.gameObject.scene != scene.gameObject.scene ||
                roulette.Island < 0 || roulette.Island > session.State.unlockedIsland || bets == null || bets.Count == 0 || (uint)result > 2) return false;
            var settled = new Dictionary<HowToFishWorldItem, float>();
            int winners = 0;
            try
            {
                foreach (var bet in bets)
                {
                    if (bet.Key == null || !bet.Key.CanBet || !items.Contains(bet.Key) || (uint)bet.Value > 2)
                    { Notify("押注物品状态已变化，请重新放置。"); return false; }
                    float multiplier = bet.Key.BettingMultiplier * (bet.Value == HowToFishRouletteColor.Green ? 35 : 2);
                    // 每件物品按它所押颜色的最高可得值检查，不能靠抽到输局避开溢出检查。
                    bet.Key.ValueAfterRoulette(multiplier);
                    bool won = bet.Value == result;
                    settled.Add(bet.Key, won ? multiplier : 0);
                    if (won) winners++;
                }
            }
            catch (Exception exception) when (exception is ArgumentOutOfRangeException || exception is InvalidOperationException)
            { Notify("本轮潜在中奖价值超出当前金额数值范围，未扣物品；请取回高倍率鱼获。"); return false; }
            bool newOutfit = result == HowToFishRouletteColor.Green && winners > 0 && session.UnlockOutfit("Andrei");
            if (!TrySave(settled))
            {
                if (newOutfit) session.State.unlockedOutfits.Remove("Andrei");
                return false;
            }
            foreach (var entry in settled) entry.Key.ApplyRouletteResult(entry.Value);
            string color = result == HowToFishRouletteColor.Green ? "绿" : result == HowToFishRouletteColor.Red ? "红" : "黑";
            announcement = $"轮盘落在{color}色：{winners}件获胜，{settled.Count - winners}件失去。取回获胜鱼获后出售兑现。";
            return true;
        }

        private bool TrySave(IReadOnlyDictionary<HowToFishWorldItem, float> rouletteResults = null)
        {
            if (session == null) return false;
            if (items.Any(item => item != null && !item.IsConsumed && item.Creature?.IsBoss == true && item.IsAlive))
            { Notify("首领战斗结束后才能保存。"); return false; }
            try
            {
                var state = session.State;
                session.ReconcileOutfits();
                state.boatPosition = boat.transform.position; state.boatYaw = boat.transform.eulerAngles.y;
                state.isDriving = player.IsDriving;
                state.isOnBoat = player.IsDriving || (Physics.Raycast(player.transform.position + Vector3.up * .1f,
                    Vector3.down, out var floor, 2, ~0, QueryTriggerInteraction.Ignore) && floor.rigidbody == boat.GetComponent<Rigidbody>());
                state.boatLocalPosition = state.isOnBoat ? boat.transform.InverseTransformPoint(player.transform.position) : Vector3.zero;
                state.boatLocalYaw = state.isOnBoat ? Mathf.DeltaAngle(boat.transform.eulerAngles.y, player.transform.eulerAngles.y) : 0;
                if (!state.isOnBoat && player.GetComponent<CharacterController>().isGrounded && player.transform.position.y > .1f)
                { state.safePosition = player.transform.position; state.safeYaw = player.transform.eulerAngles.y; state.safeIsland = player.Island; }
                state.worldItems.Clear();
                foreach (var item in items)
                    if (item != null && !item.IsConsumed)
                    {
                        var snapshot = item.Snapshot();
                        if (rouletteResults != null && rouletteResults.TryGetValue(item, out float multiplier))
                        {
                            if (multiplier == 0) continue;
                            snapshot.bettingMultiplier = multiplier;
                        }
                        state.worldItems.Add(snapshot);
                    }
                saves.Save(slot, state);
                Notify("已保存到存档 " + (slot + 1) +
                    (string.IsNullOrEmpty(saves.SkinProfileNotice) ? "" : "\n" + saves.SkinProfileNotice));
                return true;
            }
            catch (Exception exception) { Notify("保存失败，原存档已保留：" + exception.Message); Debug.LogException(exception, scene); return false; }
        }

        private void SetPaused(bool paused)
        {
            State.IsPaused = paused || session == null || State.ShowEnding;
            if (!State.IsPaused) State.ShowJournal = false;
            scene.ApplyPauseState();
        }

        private void Notify(string message) { State.Notice = message; State.NoticeUntil = Time.unscaledTime + 6; }

        private void Interact(Collider collider)
        {
            if (player.IsDriving) { boat.SetDriver(null); player.Teleport(boat.ExitPosition, boat.transform.eulerAngles.y); return; }
            if (collider == null) return;
            var roulette = collider.GetComponentInParent<HowToFishRoulette>();
            if (roulette != null && collider.GetComponentInParent<HowToFishWorldItem>() == null) { roulette.TrySpin(); return; }
            var station = collider.GetComponentInParent<HowToFishStation>();
            if (station == null) { player.PickUp(collider.GetComponentInParent<HowToFishWorldItem>()); return; }
            int outfitsBefore = session.State.unlockedOutfits.Count;
            int moneyBefore = session.State.money;
            switch (station.Kind)
            {
                case HowToFishStationKind.Product:
                    Notify(session.TryBuy(station.ItemId, station.Island, out var reason) ? "已购买 " + catalog.FindItem(station.ItemId).DisplayName : reason);
                    break;
                case HowToFishStationKind.Sell:
                    if (player.HeldItem != null && player.HeldItem.TrySell(out int value)) Notify("售出鱼获 +$" + value);
                    else Notify("拿着处理好的鱼获来出售。");
                    break;
                case HowToFishStationKind.Keeper: TalkToKeeper(); break;
                case HowToFishStationKind.ForestLady:
                    if (!TryDeliverToForestLady(player.HeldItem))
                        Notify(session.State.unlockedIsland >= 2 ? "湖畔女士：沙漠在西北方，坐标已经给你。" :
                            $"湖畔女士：带给我三条草地上的水蛭，我给你改造鱼饵。已收到 {session.State.forestLeeches}/3；打败巨型食人鱼后，把骨架带回来。");
                    break;
                case HowToFishStationKind.BoatWheel:
                    if (!session.State.hasBoatKey) { Notify("先帮灯塔看守人拿到船钥匙。"); break; }
                    player.Board(boat.Seat); boat.SetDriver(input); break;
                case HowToFishStationKind.Grill:
                    Notify(session.State.hasGrill ? "把鱼获放到或拿在烤架上方持续加热；熟成后及时取走，继续加热会烧焦。" :
                        "先向烧烤师交付一条死蓝鲨或哥布林鲨，再领取打火机。"); break;
                case HowToFishStationKind.GrillMaster:
                    if (TryDeliverToGrillMaster(player.HeldItem)) break;
                    if (session.State.hasGrill) { Notify("烤炉已经可以使用，留意鱼获颜色和售价，别烤焦了。"); break; }
                    if (!session.State.completedQuests.Contains("GrillSharkDelivered")) { Notify("带来一条你拿过的死蓝鲨或哥布林鲨，我就给你打火机。"); break; }
                    if (session.Count("Lighter") >= 100000) { Notify("打火机已达到携带上限，奖励暂时保留。"); break; }
                    session.GrantItem("Lighter");
                    session.State.hasGrill = true;
                    if (!session.State.completedQuests.Contains("DesertGrill")) session.State.completedQuests.Add("DesertGrill");
                    if (TrySave()) Notify("获得打火机，烤炉已解锁。");
                    break;
                case HowToFishStationKind.Tourist:
                    if (!TryDeliverToTourist(player.HeldItem))
                        Notify(session.State.unlockedIsland >= 3 ? "游客：岩石岛的坐标已经给你，祝你一路顺风。" :
                            "游客：把处理好的濒危鱼带给我，颌针鱼、海马或鱼缸鱼都可以。我给你萝卜去钓河豚，之后请把鱼鳍带回来。");
                    break;
                case HowToFishStationKind.Anvil:
                case HowToFishStationKind.AmmoUpgrade:
                    var weaponKind = station.Kind == HowToFishStationKind.Anvil ? HowToFishItemKind.Melee : HowToFishItemKind.Gun;
                    if (player.Equipment?.Kind != weaponKind) { Notify(weaponKind == HowToFishItemKind.Melee ? "请先手持近战武器。" : "请先手持枪械。"); break; }
                    Notify(session.TryUpgrade(player.Equipment?.Id, station.Island, out var upgradeError) ? "武器升级完成。" : upgradeError); break;
                case HowToFishStationKind.Attachment:
                    Notify(session.TryBuyAttachment(player.Equipment?.Id, station.Attachment, station.Island, out var attachmentError)
                        ? "已安装" + station.Label : attachmentError); break;
                case HowToFishStationKind.InventoryUpgrade:
                    Notify(session.TryExpandInventory(station.Island, out var inventoryError) ? "装备栏已扩容。" : inventoryError); break;
                case HowToFishStationKind.MotorUpgrade:
                    Notify(session.TryBuyMotor(station.MotorTier, station.Island, out var motorError) ? "船只马达已升级。" : motorError); break;
                case HowToFishStationKind.BoatRadar:
                    Notify(session.TryBuyBoatRadar(station.Island, out var radarError) ? "船载雷达已安装。" : radarError); break;
                case HowToFishStationKind.Scientist: TalkToScientist(); break;
                case HowToFishStationKind.MilitaryDeparture:
                    if (!session.State.hasMilitaryBoatKey) { Notify("需要科学家交给你的军用船钥匙。"); break; }
                    bool wasFinished = session.State.hasFinished;
                    session.State.hasFinished = true;
                    bool newScientist = session.UnlockOutfit("Scientist");
                    // 旧档未统计暂停时间，不能据不完整时钟补授限时奖励；小于一小时的边界为推定。
                    bool newBean = !wasFinished && session.State.tracksPausedPlaytime && session.State.playedSeconds < 3600 && session.UnlockOutfit("Bean");
                    if (TrySave()) { ShowEnding = true; ShowJournal = false; SetPaused(true); if (!wasFinished) scene.RequestSound(HowToFishSound.Ending, player.transform.position); Notify("你乘军用船回到了大陆。航程已保存，可以继续探索群岛。"); }
                    else
                    {
                        session.State.hasFinished = wasFinished;
                        if (newScientist) session.State.unlockedOutfits.Remove("Scientist");
                        if (newBean) session.State.unlockedOutfits.Remove("Bean");
                    }
                    break;
                case HowToFishStationKind.Islander:
                    if (!TryDeliverToIslander(player.HeldItem))
                        Notify(session.State.unlockedIsland >= 4 ? "岛民：火山的坐标已经给你，准备好再出发。" :
                            "岛民：用专业首领饵钓金枪鱼，打倒后把完整生鱼放在岸上引鸟。屋顶能挡落物，请把鸟头带回来。");
                    break;
            }
            if (session.State.money != moneyBefore) scene.RequestSound(HowToFishSound.Trade, station.transform.position);
            if (session.State.unlockedOutfits.Count > outfitsBefore &&
                (station.Kind == HowToFishStationKind.MotorUpgrade || station.Kind == HowToFishStationKind.Attachment ||
                 station.Kind == HowToFishStationKind.AmmoUpgrade || station.Kind == HowToFishStationKind.Anvil)) SaveOutfitProgress();
        }

        private void TalkToKeeper()
        {
            if (TryDeliverToKeeper(player.HeldItem)) return;
            if (session.State.hasBoatKey) { Notify("看守人：钥匙交给你了，去探索下一座岛吧。"); return; }
            if (session.TryConsume("CrabMeat"))
            {
                GiveBoatKey(); return;
            }
            if (session.Count("CrabRod") == 0) { Notify("看守人：把蛤蜊交给我，攒够 $3 就去木门买钓竿。蛤蜊也能填饱肚子。"); return; }
            if (session.Count("EmptyBeerCan") > 0) { Notify("看守人：用空啤酒罐作饵，打败蜘蛛蟹，把蟹壳带回来。"); return; }
            if (session.TryConsume("Beer"))
            {
                GiveEmptyCan();
            }
            else Notify("看守人：先给我一罐啤酒，我有办法帮你离开这里。");
        }

        private void DeliverToStation(HowToFishStation station, HowToFishWorldItem item)
        {
            if (session == null || IsPaused || exiting) return;
            if (station.Kind == HowToFishStationKind.Keeper) TryDeliverToKeeper(item);
            else if (station.Kind == HowToFishStationKind.ForestLady) TryDeliverToForestLady(item);
            else if (station.Kind == HowToFishStationKind.Tourist) TryDeliverToTourist(item);
            else if (station.Kind == HowToFishStationKind.Islander) TryDeliverToIslander(item);
            else if (station.Kind == HowToFishStationKind.Scientist) TryDeliverToScientist(item);
            else if (station.Kind == HowToFishStationKind.GrillMaster) TryDeliverToGrillMaster(item);
        }

        private bool TryDeliverToGrillMaster(HowToFishWorldItem item)
        {
            if (item == null || item.IsConsumed || item.IsAlive || !item.HasBeenHeld || session.State.hasGrill ||
                session.State.completedQuests.Contains("GrillSharkDelivered") ||
                (item.DefinitionId != "BlueShark" && item.DefinitionId != "GoblinShark")) return false;
            return item.TryConsume(() =>
            {
                session.State.completedQuests.Add("GrillSharkDelivered");
                if (TrySave()) Notify("烧烤师收下了鲨鱼，再交谈领取打火机。");
            });
        }

        private void ContinueAfterEnding()
        {
            if (!ShowEnding) return;
            ShowEnding = false;
            SetPaused(false);
        }

        private void TalkToScientist()
        {
            if (TryDeliverToScientist(player.HeldItem)) return;
            if (session.State.hasMilitaryBoatKey) { Notify("科学家：军用船钥匙交给你了。登上黑色充气艇，在船舵处启程吧。"); return; }
            if (session.State.volcanoFish < 5)
            { Notify($"科学家：给我五条处理好的鱼，我来准备鱼桶。已收到 {session.State.volcanoFish}/5。"); return; }
            if (session.Count("FishBucket") > 0)
            { Notify("科学家：用鱼桶钓鲸鱼；把完整生鲸尸投入火山，再把变异鲸鱼鳍交给我。"); return; }
            session.GrantItem("FishBucket");
            if (!session.State.completedQuests.Contains("VolcanoFishBucket")) session.State.completedQuests.Add("VolcanoFishBucket");
            if (TrySave()) Notify("领到鱼桶。用普通鱼竿在火山周围水域钓出弓头鲸。");
        }

        private bool TryDeliverToScientist(HowToFishWorldItem item)
        {
            if (item == null || item.IsConsumed || item.IsAlive || session.State.unlockedIsland < 4) return false;
            if (item.DefinitionId == "WhaleFin" && !session.State.hasMilitaryBoatKey)
                return item.TryConsume(() =>
                {
                    session.GrantItem("MilitaryBoatKey");
                    session.State.hasMilitaryBoatKey = true;
                    if (!session.State.completedQuests.Contains("VolcanoWhale")) session.State.completedQuests.Add("VolcanoWhale");
                    if (TrySave()) Notify("科学家：拿好军用船钥匙，你可以回家了！");
                });
            var creature = item.Creature;
            if (session.State.volcanoFish >= 5 || creature == null || creature.IsBoss || creature.IsGroundPickup || creature.Island == 0) return false;
            return item.TryConsume(() =>
            {
                session.State.volcanoFish++;
                if (TrySave()) Notify(session.State.volcanoFish == 5 ? "科学家：鱼桶准备好了，再与我交谈领取。" : $"科学家：已经收到 {session.State.volcanoFish}/5 条鱼。");
            });
        }

        private bool TryDeliverToIslander(HowToFishWorldItem item)
        {
            if (item == null || item.IsConsumed || item.IsAlive || item.DefinitionId != "AlbatrossHead" ||
                session.State.unlockedIsland < 3) return false;
            return item.TryConsume(() =>
            {
                session.State.unlockedIsland = 4;
                if (!session.State.completedQuests.Contains("RocksAlbatross")) session.State.completedQuests.Add("RocksAlbatross");
                if (TrySave()) Notify("岛民：终于清静了！这是北方火山岛的坐标。");
            });
        }

        private bool TryDeliverToTourist(HowToFishWorldItem item)
        {
            if (item == null || item.IsConsumed || item.IsAlive || session.State.unlockedIsland < 2) return false;
            if (item.DefinitionId == "PufferfishFin") return item.TryConsume(() =>
            {
                session.State.unlockedIsland = Mathf.Max(session.State.unlockedIsland, 3);
                if (!session.State.completedQuests.Contains("DesertPufferfish")) session.State.completedQuests.Add("DesertPufferfish");
                if (TrySave()) Notify("游客：谢谢！这是西北方向岩石岛的坐标。");
            });
            if (item.Creature?.IsEndangered != true) return false;
            if (session.Count("Carrot") >= 100000) { Notify("萝卜已达到携带上限。"); return true; }
            return item.TryConsume(() =>
            {
                session.GrantItem("Carrot");
                if (!session.State.completedQuests.Contains("DesertCarrot")) session.State.completedQuests.Add("DesertCarrot");
                if (TrySave()) Notify("游客：萝卜给你了。用普通鱼竿钓出河豚，记得把鱼鳍交回来。");
            });
        }

        private bool TryDeliverToForestLady(HowToFishWorldItem item)
        {
            if (item == null || item.IsConsumed || session.State.unlockedIsland < 1) return false;
            if (item.DefinitionId == "Leech" && session.State.forestLeeches == 2 && session.Count("ModifiedLeech") >= 100000)
            { Notify("改造水蛭已达到携带上限，请先使用一些。"); return true; }
            if (item.DefinitionId == "Leech") return item.TryConsume(() =>
            {
                if (session.State.forestLeeches == 2)
                {
                    session.GrantItem("ModifiedLeech");
                    session.State.forestLeeches = 0;
                    if (!session.State.completedQuests.Contains("ForestLeeches")) session.State.completedQuests.Add("ForestLeeches");
                }
                else session.State.forestLeeches++;
                if (TrySave()) Notify(session.State.forestLeeches == 0 ? "湖畔女士：改造水蛭给你了，在湖边用它钓出巨型食人鱼！" :
                    $"湖畔女士：已收到 {session.State.forestLeeches}/3 条水蛭。");
            });
            if (item.DefinitionId == "PiranhaSkeleton") return item.TryConsume(() =>
            {
                session.State.unlockedIsland = Mathf.Max(session.State.unlockedIsland, 2);
                if (!session.State.completedQuests.Contains("ForestPiranha")) session.State.completedQuests.Add("ForestPiranha");
                if (TrySave()) Notify("湖畔女士：谢谢！这是西北方沙漠岛的坐标。");
            });
            return false;
        }

        private bool TryDeliverToKeeper(HowToFishWorldItem item)
        {
            if (item == null || item.IsConsumed || item.IsAlive) return false;
            if (item.DefinitionId == "CrabMeat" && !session.State.hasBoatKey)
                return item.TryConsume(GiveBoatKey);
            if (item.DefinitionId == "Beer") return item.TryConsume(GiveEmptyCan);
            if (item.Creature != null && !item.Creature.IsBoss && item.TrySell(out int money))
            { Notify("看守人收下鱼获，付给你 $" + money); return true; }
            return false;
        }

        private void GiveBoatKey()
        {
            session.State.hasBoatKey = true;
            session.State.unlockedIsland = Mathf.Max(session.State.unlockedIsland, 1);
            if (!session.State.completedQuests.Contains("KeeperCrab")) session.State.completedQuests.Add("KeeperCrab");
            if (TrySave()) Notify("看守人：船钥匙给你了。带上雷达，向西北寻找森林岛。");
        }

        private void GiveEmptyCan()
        {
            session.GrantItem("EmptyBeerCan");
            if (!session.State.completedQuests.Contains("KeeperBeer")) session.State.completedQuests.Add("KeeperBeer");
            if (TrySave()) Notify("看守人：谢了！空罐拿去钓蜘蛛蟹吧。");
        }

        private bool Damage(float amount)
        {
            if (session == null || session.State.health <= 0 || amount <= 0 || float.IsNaN(amount) || float.IsInfinity(amount)) return false;
            session.State.health = Mathf.Max(0, session.State.health - amount);
            if (session.State.health > 0) return false;
            session.State.poisonSeconds = session.State.burningSeconds = 0;
            session.State.poisonDamagePerSecond = session.State.burningDamagePerSecond = 0;
            return true;
        }
        private void ApplyDamageStatus(HowToFishDamageStatusAction action)
        {
            if (session == null || !(action.Seconds > 0 && action.Seconds <= 60) || !(action.DamagePerSecond > 0 && action.DamagePerSecond <= 1000)) return;
            var state = session.State;
            if (action.Burning)
            { state.burningSeconds = Mathf.Max(state.burningSeconds, action.Seconds); state.burningDamagePerSecond = Mathf.Max(state.burningDamagePerSecond, action.DamagePerSecond); }
            else
            { state.poisonSeconds = Mathf.Max(state.poisonSeconds, action.Seconds); state.poisonDamagePerSecond = Mathf.Max(state.poisonDamagePerSecond, action.DamagePerSecond); }
        }
        private void TickDamageStatus(HowToFishDamageStatusTickAction action)
        {
            var state = session.State;
            action.Damage = Mathf.Min(action.Delta, state.poisonSeconds) * state.poisonDamagePerSecond + Mathf.Min(action.Delta, state.burningSeconds) * state.burningDamagePerSecond;
            state.poisonSeconds = Mathf.Max(0, state.poisonSeconds - action.Delta);
            state.burningSeconds = Mathf.Max(0, state.burningSeconds - action.Delta);
            if (state.poisonSeconds == 0) state.poisonDamagePerSecond = 0;
            if (state.burningSeconds == 0) state.burningDamagePerSecond = 0;
            action.Lethal = action.Damage >= state.health;
        }
        private void RestoreFood(HowToFishRestoreFoodAction action)
        {
            session.State.hunger = Mathf.Min(100, session.State.hunger + action.Hunger);
            session.State.health = Mathf.Min(100, session.State.health + action.Health);
        }
        private void Ammo(HowToFishAmmoAction action)
        {
            var item = session.State.inventory.Find(value => value.id == action.ItemId);
            if (item == null) return;
            switch (action.Operation)
            {
                case HowToFishAmmoOperation.Initialize: item.ammo = item.ammo < 0 ? action.Capacity : Mathf.Min(item.ammo, action.Capacity); break;
                case HowToFishAmmoOperation.Reload: item.ammo = action.Capacity; break;
                case HowToFishAmmoOperation.Consume: if (item.ammo > 0) item.ammo--; break;
            }
        }
        private void Cooking(HowToFishCookingAction action)
        {
            var item = session.State.inventory.Find(value => value.id == action.ItemId);
            if (item != null) item.cooking = action.Extinguish ? 0 : Mathf.Clamp01(item.cooking + action.Heat);
        }
        private void Equip(string id)
        {
            session.State.equippedItemId = id;
            if (id == null) return;
            int slot = session.State.equipmentSlots.IndexOf(id);
            if (slot >= 0) session.State.selectedEquipmentSlot = slot;
        }
        private void StartSlot(int index, bool newGame, bool recoverBackup = false)
        {
            if (session != null) { Notify("请先返回 Hub，再切换存档。"); return; }
            try
            {
                if (recoverBackup) saves.RestoreBackup(index);
                var loaded = saves.Load(index);
                if (loaded.Status != HowToFishLoadStatus.Ready && loaded.Status != HowToFishLoadStatus.Empty)
                { Notify(loaded.Error); return; }
                if (!newGame && loaded.Status != HowToFishLoadStatus.Ready) { Notify("此槽还没有存档。"); return; }
                var state = newGame ? new HowToFishSaveData
                {
                    safePosition = scene.StartPoint.position, safeYaw = scene.StartPoint.eulerAngles.y,
                    boatPosition = boat.transform.position, boatYaw = boat.transform.eulerAngles.y,
                    tracksPausedPlaytime = true
                } : loaded.Data;
                var profile = saves.LoadSharedSkins(state);
                State.SelectedOutfitId = profile.selectedOutfitId;
                string profileNotice = saves.SkinProfileNotice;
                var nextSession = new HowToFishSession(catalog, state);
                if (state.unlockedOutfits.Count != profile.unlockedOutfits.Count) saves.LoadSharedSkins(state);
                if (State.Session != null) State.Session.Changed -= OnRulesChanged;
                State.Version++;
                State.Slot = index;
                State.Session = nextSession;
                session.Changed += OnRulesChanged;
                if (session.Count("FreeLure") == 0) session.GrantItem("FreeLure");
                scene.BuildSession(newGame, state, profileNotice);
            }
            catch (Exception exception) { Notify("无法开始游戏：" + exception.Message); Debug.LogException(exception, scene); }
        }
        private void Respawn()
        {
            if (session == null) return;
            session.State.health = 100;
            session.State.hunger = Mathf.Max(35, session.State.hunger);
            scene.RespawnPresentation();
        }
        private void RefreshSlots()
        {
            if (saves == null) return;
            summaryStore = saves;
            for (int i = 0; i < State.SlotInfos.Length; i++) State.SlotInfos[i] = saves.Load(i);
        }
        private void Tick(HowToFishTickAction action)
        {
            if (State.Notice != null && Time.unscaledTime > State.NoticeUntil) State.Notice = null;
            if (session == null) return;
            if (!State.ShowEnding) session.State.playedSeconds += action.UnscaledDelta;
            if (State.IsPaused) return;
            session.State.hunger = Mathf.Max(0, session.State.hunger - action.Delta * .06f);
            if (session.State.hunger <= 0) player.Damage(action.Delta);
        }
        internal void SetExiting(bool value) { State.IsExiting = value; Publish(); }
        private void Ui(HowToFishUiAction action)
        {
            if (State.IsExiting && action.Command != HowToFishUiCommand.CancelSettings) return;
            switch (action.Command)
            {
                case HowToFishUiCommand.ToggleJournal:
                    State.ShowJournal = !State.ShowJournal; SetPaused(State.ShowJournal); break;
                case HowToFishUiCommand.OpenSettings:
                case HowToFishUiCommand.OpenOutfits:
                    if (State.MainPage is HowToFishPage.None or HowToFishPage.Journal) return;
                    if (action.Command == HowToFishUiCommand.OpenOutfits && session == null) return;
                    scene.UI.CaptureReturnFocus();
                    State.RequestedOverlay = action.Command == HowToFishUiCommand.OpenSettings ? HowToFishPage.Settings : HowToFishPage.Outfits; break;
                case HowToFishUiCommand.CloseSettings:
                    if (State.RequestedOverlay == HowToFishPage.Settings) State.RequestedOverlay = HowToFishPage.None; break;
                case HowToFishUiCommand.CloseOutfits:
                    if (State.RequestedOverlay == HowToFishPage.Outfits) State.RequestedOverlay = HowToFishPage.None; break;
                case HowToFishUiCommand.Exit: State.RequestedOverlay = HowToFishPage.None; scene.ExitScene(); break;
                case HowToFishUiCommand.NewSlot:
                    var saved = saves.Load(action.Slot);
                    if (saved.Status == HowToFishLoadStatus.Ready && State.ConfirmNewSlot != action.Slot)
                    { State.ConfirmNewSlot = action.Slot; Notify("再次点击此槽的“重开”，确认覆盖当前航程；上一份进度会保留为备份。"); return; }
                    StartSlot(action.Slot, true); break;
                case HowToFishUiCommand.BeginSettings:
                    State.SettingsSnapshot = scene.GetPreferences(); State.SettingsOpen = true; SetEditingSettings(true); break;
                case HowToFishUiCommand.CancelSettings:
                    input.CancelRebind();
                    if (State.SettingsOpen && State.SettingsSnapshot != null) ApplyPreferences(State.SettingsSnapshot);
                    State.SettingsOpen = false; State.SettingsSnapshot = null; SetEditingSettings(false); break;
                case HowToFishUiCommand.SaveSettings:
                    if (!SavePreferences()) return;
                    State.SettingsSnapshot = null; State.SettingsOpen = false; SetEditingSettings(false); break;
            }
            action.Success = true;
        }
        internal void Clear()
        {
            if (State.Session != null) State.Session.Changed -= OnRulesChanged;
            State.Session = null; State.Version++; State.Slot = 0; State.ConfirmNewSlot = -1;
            State.ShowJournal = State.ShowEnding = State.IsEditingSettings = State.IsExiting = State.SettingsOpen = false;
            State.SelectedOutfitId = HowToFishOutfitCatalog.DefaultId; State.Notice = null; State.SettingsSnapshot = null;
            State.RequestedOverlay = HowToFishPage.None; State.IsPaused = true;
        }
        public void Dispose() { if (session != null) session.Changed -= OnRulesChanged; State.Version++; }
    }
}
