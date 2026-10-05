using Core.Runtime;
using System.Collections.Generic;
using UnityEngine;

namespace Hotfix.HowToFish
{
    public abstract class HowToFishAction : IAction { }
    public sealed class HowToFishTryConsumeAction : HowToFishAction
    {
        internal HowToFishSession Source;
        internal string id;
        public bool Result { get; internal set; }
        internal static bool Send(HowToFishSession source, string id)
        {
            var request = new HowToFishTryConsumeAction { Source = source, id = id };
            GlobalData.Dispatch(request);
            return request.Result;
        }
    }
    public sealed class HowToFishGrantItemAction : HowToFishAction
    {
        internal HowToFishSession Source;
        internal string id;
        internal int count;
        internal static void Send(HowToFishSession source, string id, int count = 1)
        {
            var request = new HowToFishGrantItemAction { Source = source, id = id, count = count };
            GlobalData.Dispatch(request);
        }
    }
    public sealed class HowToFishGrantEquipmentAction : HowToFishAction
    {
        internal HowToFishSession Source;
        internal HowToFishOwnedItem equipment;
        internal static void Send(HowToFishSession source, HowToFishOwnedItem equipment)
        {
            var request = new HowToFishGrantEquipmentAction { Source = source, equipment = equipment };
            GlobalData.Dispatch(request);
        }
    }
    public sealed class HowToFishTryStoreEquipmentAction : HowToFishAction
    {
        internal HowToFishSession Source;
        internal string id;
        internal int slot;
        public bool Result { get; internal set; }
        internal static bool Send(HowToFishSession source, string id, int slot)
        {
            var request = new HowToFishTryStoreEquipmentAction { Source = source, id = id, slot = slot };
            GlobalData.Dispatch(request);
            return request.Result;
        }
    }
    public sealed class HowToFishSellCatchAction : HowToFishAction
    {
        internal HowToFishSession Source;
        internal string creatureId;
        internal float cooking;
        internal bool drip;
        internal float styleMultiplier;
        internal float bettingMultiplier;
        internal float weightMultiplier;
        public int Result { get; internal set; }
        internal static int Send(HowToFishSession source, string creatureId, float cooking, bool drip, float styleMultiplier = 1, float bettingMultiplier = 1, float weightMultiplier = 1)
        {
            var request = new HowToFishSellCatchAction { Source = source, creatureId = creatureId, cooking = cooking, drip = drip, styleMultiplier = styleMultiplier, bettingMultiplier = bettingMultiplier, weightMultiplier = weightMultiplier };
            GlobalData.Dispatch(request);
            return request.Result;
        }
    }
    public sealed class HowToFishRegisterCreatureAction : HowToFishAction
    {
        internal HowToFishSession Source;
        internal string creatureId;
        internal bool defeated;
        internal bool drip;
        internal static void Send(HowToFishSession source, string creatureId, bool defeated, bool drip)
        {
            var request = new HowToFishRegisterCreatureAction { Source = source, creatureId = creatureId, defeated = defeated, drip = drip };
            GlobalData.Dispatch(request);
        }
    }
    public sealed class HowToFishStartSlotAction : HowToFishAction
    {
        internal HowToFishWorld Source;
        internal int index;
        internal bool newGame;
        internal bool recoverBackup;
        internal static void Send(HowToFishWorld source, int index, bool newGame, bool recoverBackup = false)
        {
            var request = new HowToFishStartSlotAction { Source = source, index = index, newGame = newGame, recoverBackup = recoverBackup };
            GlobalData.Dispatch(request);
        }
    }
    public sealed class HowToFishSaveAction : HowToFishAction
    {
        internal HowToFishWorld Source;
        internal static void Send(HowToFishWorld source)
        {
            var request = new HowToFishSaveAction { Source = source };
            GlobalData.Dispatch(request);
        }
    }
    public sealed class HowToFishApplyPreferencesAction : HowToFishAction
    {
        internal HowToFishWorld Source;
        internal HowToFishLocalPreferences value;
        internal static void Send(HowToFishWorld source, HowToFishLocalPreferences value)
        {
            var request = new HowToFishApplyPreferencesAction { Source = source, value = value };
            GlobalData.Dispatch(request);
        }
    }
    public sealed class HowToFishSavePreferencesAction : HowToFishAction
    {
        internal HowToFishWorld Source;
        public bool Result { get; internal set; }
        internal static bool Send(HowToFishWorld source)
        {
            var request = new HowToFishSavePreferencesAction { Source = source };
            GlobalData.Dispatch(request);
            return request.Result;
        }
    }
    public sealed class HowToFishSetEditingSettingsAction : HowToFishAction
    {
        internal HowToFishWorld Source;
        internal bool editing;
        internal static void Send(HowToFishWorld source, bool editing)
        {
            var request = new HowToFishSetEditingSettingsAction { Source = source, editing = editing };
            GlobalData.Dispatch(request);
        }
    }
    public sealed class HowToFishTrySelectOutfitAction : HowToFishAction
    {
        internal HowToFishWorld Source;
        internal string id;
        public bool Result { get; internal set; }
        internal static bool Send(HowToFishWorld source, string id)
        {
            var request = new HowToFishTrySelectOutfitAction { Source = source, id = id };
            GlobalData.Dispatch(request);
            return request.Result;
        }
    }
    public sealed class HowToFishTryPlaySlotMachineAction : HowToFishAction
    {
        internal HowToFishWorld Source;
        internal int island;
        internal HowToFishWorldItem item;
        internal string result;
        public bool Result { get; internal set; }
        internal static bool Send(HowToFishWorld source, int island, HowToFishWorldItem item, out string result)
        {
            var request = new HowToFishTryPlaySlotMachineAction { Source = source, island = island, item = item };
            GlobalData.Dispatch(request);
            result = request.result;
            return request.Result;
        }
    }
    public sealed class HowToFishChangeSkinAction : HowToFishAction
    {
        internal HowToFishWorld Source;
        internal static void Send(HowToFishWorld source)
        {
            var request = new HowToFishChangeSkinAction { Source = source };
            GlobalData.Dispatch(request);
        }
    }
    public sealed class HowToFishTrySettleRouletteAction : HowToFishAction
    {
        internal HowToFishWorld Source;
        internal HowToFishRoulette roulette;
        internal IReadOnlyDictionary<HowToFishWorldItem, HowToFishRouletteColor> bets;
        internal HowToFishRouletteColor result;
        internal string announcement;
        public bool Result { get; internal set; }
        internal static bool Send(HowToFishWorld source, HowToFishRoulette roulette, IReadOnlyDictionary<HowToFishWorldItem, HowToFishRouletteColor> bets,
            HowToFishRouletteColor result, out string announcement)
        {
            var request = new HowToFishTrySettleRouletteAction { Source = source, roulette = roulette, bets = bets, result = result };
            GlobalData.Dispatch(request);
            announcement = request.announcement;
            return request.Result;
        }
    }
    public sealed class HowToFishSetPausedAction : HowToFishAction
    {
        internal HowToFishWorld Source;
        internal bool paused;
        internal static void Send(HowToFishWorld source, bool paused)
        {
            var request = new HowToFishSetPausedAction { Source = source, paused = paused };
            GlobalData.Dispatch(request);
        }
    }
    public sealed class HowToFishNotifyAction : HowToFishAction
    {
        internal HowToFishWorld Source;
        internal string message;
        internal static void Send(HowToFishWorld source, string message)
        {
            var request = new HowToFishNotifyAction { Source = source, message = message };
            GlobalData.Dispatch(request);
        }
    }
    public sealed class HowToFishInteractAction : HowToFishAction
    {
        internal HowToFishWorld Source;
        internal Collider collider;
        internal static void Send(HowToFishWorld source, Collider collider)
        {
            var request = new HowToFishInteractAction { Source = source, collider = collider };
            GlobalData.Dispatch(request);
        }
    }
    public sealed class HowToFishDeliverToStationAction : HowToFishAction
    {
        internal HowToFishWorld Source;
        internal HowToFishStation station;
        internal HowToFishWorldItem item;
        internal static void Send(HowToFishWorld source, HowToFishStation station, HowToFishWorldItem item)
        {
            var request = new HowToFishDeliverToStationAction { Source = source, station = station, item = item };
            GlobalData.Dispatch(request);
        }
    }
    public sealed class HowToFishContinueAfterEndingAction : HowToFishAction
    {
        internal HowToFishWorld Source;
        internal static void Send(HowToFishWorld source)
        {
            var request = new HowToFishContinueAfterEndingAction { Source = source };
            GlobalData.Dispatch(request);
        }
    }
    public sealed class HowToFishRespawnAction : HowToFishAction
    {
        internal HowToFishWorld Source;
        internal static void Send(HowToFishWorld source)
        {
            var request = new HowToFishRespawnAction { Source = source };
            GlobalData.Dispatch(request);
        }
    }
    public sealed class HowToFishOnCreatureEatenAction : HowToFishAction
    {
        internal HowToFishWorld Source;
        internal HowToFishWorldItem item;
        internal static void Send(HowToFishWorld source, HowToFishWorldItem item)
        {
            var request = new HowToFishOnCreatureEatenAction { Source = source, item = item };
            GlobalData.Dispatch(request);
        }
    }
    public sealed class HowToFishSaveOutfitProgressAction : HowToFishAction
    {
        internal HowToFishWorld Source;
        internal static void Send(HowToFishWorld source)
        {
            var request = new HowToFishSaveOutfitProgressAction { Source = source };
            GlobalData.Dispatch(request);
        }
    }
    public sealed class HowToFishDamageAction : HowToFishAction
    {
        internal HowToFishSession Source; internal float Amount;
        public bool Died { get; internal set; }
        internal HowToFishDamageAction(HowToFishSession source, float amount) { Source = source; Amount = amount; }
    }
    public sealed class HowToFishDamageStatusAction : HowToFishAction
    {
        internal HowToFishSession Source; internal bool Burning; internal float Seconds, DamagePerSecond;
        internal HowToFishDamageStatusAction(HowToFishSession source, bool burning, float seconds, float damagePerSecond)
        { Source = source; Burning = burning; Seconds = seconds; DamagePerSecond = damagePerSecond; }
    }
    public sealed class HowToFishDamageStatusTickAction : HowToFishAction
    {
        internal HowToFishSession Source; internal float Delta;
        public float Damage { get; internal set; }
        public bool Lethal { get; internal set; }
        internal HowToFishDamageStatusTickAction(HowToFishSession source, float delta) { Source = source; Delta = delta; }
    }
    public sealed class HowToFishRestoreFoodAction : HowToFishAction
    {
        internal HowToFishSession Source; internal float Hunger, Health;
        internal HowToFishRestoreFoodAction(HowToFishSession source, float hunger, float health) { Source = source; Hunger = hunger; Health = health; }
    }
    public enum HowToFishAmmoOperation { Initialize, Reload, Consume }
    public sealed class HowToFishAmmoAction : HowToFishAction
    {
        internal HowToFishSession Source; internal string ItemId; internal int Capacity; internal HowToFishAmmoOperation Operation;
        internal HowToFishAmmoAction(HowToFishSession source, string id, int capacity, HowToFishAmmoOperation operation)
        { Source = source; ItemId = id; Capacity = capacity; Operation = operation; }
    }
    public sealed class HowToFishCookingAction : HowToFishAction
    {
        internal HowToFishSession Source; internal string ItemId; internal float Heat; internal bool Extinguish;
        internal HowToFishCookingAction(HowToFishSession source, string id, float heat, bool extinguish = false)
        { Source = source; ItemId = id; Heat = heat; Extinguish = extinguish; }
    }
    public sealed class HowToFishEquipAction : HowToFishAction
    {
        internal HowToFishSession Source; internal string ItemId;
        internal HowToFishEquipAction(HowToFishSession source, string id) { Source = source; ItemId = id; }
    }
    public sealed class HowToFishSelectSlotAction : HowToFishAction
    {
        internal HowToFishSession Source; internal int Slot;
        internal HowToFishSelectSlotAction(HowToFishSession source, int slot) { Source = source; Slot = slot; }
    }
    public sealed class HowToFishTickAction : HowToFishAction
    {
        internal HowToFishWorld Source; internal float Delta, UnscaledDelta;
        internal HowToFishTickAction(HowToFishWorld source, float delta, float unscaledDelta)
        { Source = source; Delta = delta; UnscaledDelta = unscaledDelta; }
    }
    public enum HowToFishUiCommand { ToggleJournal, OpenSettings, CloseSettings, OpenOutfits, CloseOutfits, Exit, NewSlot, BeginSettings, CancelSettings, SaveSettings }
    public sealed class HowToFishUiAction : HowToFishAction
    {
        internal HowToFishWorld Source; internal HowToFishUiCommand Command; internal int Slot;
        public bool Success { get; internal set; }
        internal HowToFishUiAction(HowToFishWorld source, HowToFishUiCommand command, int slot = 0)
        { Source = source; Command = command; Slot = slot; }
    }
}
