using System.Collections.Generic;
using Core.Runtime;
using UnityEngine;

namespace Hotfix.HowToFish
{
    /// 渔力全开的业务命令入口。
    public abstract class HowToFishAction : IAction
    {
    }

    /// 消费一份已有道具，失败时不扣除库存。
    public sealed class HowToFishTryConsumeAction : HowToFishAction
    {
        /// 发起命令的场景或规则实例；其它会话的命令被忽略。
        internal HowToFishSession Source;

        /// 本次操作的道具、装备或服装标识，按具体动作解释。
        public string Id;

        /// 同步处理结果；未接受请求时保持失败或零值。
        public bool Result { get; internal set; }

        /// <summary>
        /// 消费一份已有道具，失败时不扣除库存。构造后需通过 GlobalData.Dispatch 派发。
        /// </summary>
        /// <param name="source">发起命令的场景或规则实例；其它会话的命令被忽略。</param>
        /// <param name="id">本次操作的道具、装备或服装标识，按具体动作解释。</param>
        public HowToFishTryConsumeAction(HowToFishSession source, string id)
        {
            Source = source;
            Id = id;
        }
    }

    /// 增加背包中的消耗品数量。
    public sealed class HowToFishGrantItemAction : HowToFishAction
    {
        /// 发起命令的场景或规则实例；其它会话的命令被忽略。
        internal HowToFishSession Source;

        /// 本次操作的道具、装备或服装标识，按具体动作解释。
        public string Id;

        /// 增加的道具数量，默认为 1。
        public int Count;

        /// <summary>
        /// 增加背包中的消耗品数量。构造后需通过 GlobalData.Dispatch 派发。
        /// </summary>
        /// <param name="source">发起命令的场景或规则实例；其它会话的命令被忽略。</param>
        /// <param name="id">本次操作的道具、装备或服装标识，按具体动作解释。</param>
        /// <param name="count">增加的道具数量，默认为 1。</param>
        public HowToFishGrantItemAction(HowToFishSession source, string id, int count = 1)
        {
            Source = source;
            Id = id;
            Count = count;
        }
    }

    /// 把同一份装备记录加入本场背包。
    public sealed class HowToFishGrantEquipmentAction : HowToFishAction
    {
        /// 发起命令的场景或规则实例；其它会话的命令被忽略。
        internal HowToFishSession Source;

        /// 要加入背包的实际装备记录。
        public HowToFishOwnedItem Equipment;

        /// <summary>
        /// 把同一份装备记录加入本场背包。构造后需通过 GlobalData.Dispatch 派发。
        /// </summary>
        /// <param name="source">发起命令的场景或规则实例；其它会话的命令被忽略。</param>
        /// <param name="equipment">要加入背包的实际装备记录。</param>
        public HowToFishGrantEquipmentAction(HowToFishSession source, HowToFishOwnedItem equipment)
        {
            Source = source;
            Equipment = equipment;
        }
    }

    /// 尝试把手持装备放入指定装备槽。
    public sealed class HowToFishTryStoreEquipmentAction : HowToFishAction
    {
        /// 发起命令的场景或规则实例；其它会话的命令被忽略。
        internal HowToFishSession Source;

        /// 本次操作的道具、装备或服装标识，按具体动作解释。
        public string Id;

        /// 目标存档或装备槽；存档索引约定由对应 Demo 保持。
        public int Slot;

        /// 同步处理结果；未接受请求时保持失败或零值。
        public bool Result { get; internal set; }

        /// <summary>
        /// 尝试把手持装备放入指定装备槽。构造后需通过 GlobalData.Dispatch 派发。
        /// </summary>
        /// <param name="source">发起命令的场景或规则实例；其它会话的命令被忽略。</param>
        /// <param name="id">本次操作的道具、装备或服装标识，按具体动作解释。</param>
        /// <param name="slot">从零开始的装备槽索引，范围由当前装备容量决定。</param>
        public HowToFishTryStoreEquipmentAction(HowToFishSession source, string id, int slot)
        {
            Source = source;
            Id = id;
            Slot = slot;
        }
    }

    /// 按当前鱼获及倍率同步结算售价。
    public sealed class HowToFishSellCatchAction : HowToFishAction
    {
        /// 发起命令的场景或规则实例；其它会话的命令被忽略。
        internal HowToFishSession Source;

        /// 当前鱼获或图鉴生物标识。
        public string CreatureId;

        /// 当前鱼获的实际烹饪进度。
        public float Cooking;

        /// 是否为本场实际产生的 Drip 个体。
        public bool Drip;

        /// 实际样式倍率，默认为 1。
        public float StyleMultiplier;

        /// 实际下注倍率，默认为 1。
        public float BettingMultiplier;

        /// 实际重量倍率，默认为 1。
        public float WeightMultiplier;

        /// 同步处理结果；未接受请求时保持失败或零值。
        public int Result { get; internal set; }

        /// <summary>
        /// 按当前鱼获及倍率同步结算售价。构造后需通过 GlobalData.Dispatch 派发。
        /// </summary>
        /// <param name="source">发起命令的场景或规则实例；其它会话的命令被忽略。</param>
        /// <param name="creatureId">当前鱼获或图鉴生物标识。</param>
        /// <param name="cooking">当前鱼获的实际烹饪进度。</param>
        /// <param name="drip">是否为本场实际产生的 Drip 个体。</param>
        /// <param name="styleMultiplier">实际样式倍率，默认为 1。</param>
        /// <param name="bettingMultiplier">实际下注倍率，默认为 1。</param>
        /// <param name="weightMultiplier">实际重量倍率，默认为 1。</param>
        public HowToFishSellCatchAction(HowToFishSession source, string creatureId, float cooking, bool drip, float styleMultiplier = 1, float bettingMultiplier = 1, float weightMultiplier = 1)
        {
            Source = source;
            CreatureId = creatureId;
            Cooking = cooking;
            Drip = drip;
            StyleMultiplier = styleMultiplier;
            BettingMultiplier = bettingMultiplier;
            WeightMultiplier = weightMultiplier;
        }
    }

    /// 记录发现或击败生物的图鉴进度。
    public sealed class HowToFishRegisterCreatureAction : HowToFishAction
    {
        /// 发起命令的场景或规则实例；其它会话的命令被忽略。
        internal HowToFishSession Source;

        /// 当前鱼获或图鉴生物标识。
        public string CreatureId;

        /// 是否已在本场实际击败该生物。
        public bool Defeated;

        /// 是否为本场实际产生的 Drip 个体。
        public bool Drip;

        /// <summary>
        /// 记录发现或击败生物的图鉴进度。构造后需通过 GlobalData.Dispatch 派发。
        /// </summary>
        /// <param name="source">发起命令的场景或规则实例；其它会话的命令被忽略。</param>
        /// <param name="creatureId">当前鱼获或图鉴生物标识。</param>
        /// <param name="defeated">是否已在本场实际击败该生物。</param>
        /// <param name="drip">是否为本场实际产生的 Drip 个体。</param>
        public HowToFishRegisterCreatureAction(HowToFishSession source, string creatureId, bool defeated, bool drip)
        {
            Source = source;
            CreatureId = creatureId;
            Defeated = defeated;
            Drip = drip;
        }
    }

    /// 开始或恢复一个存档槽的航程。
    public sealed class HowToFishStartSlotAction : HowToFishAction
    {
        /// 发起命令的场景或规则实例；其它会话的命令被忽略。
        internal HowToFishWorld Source;

        /// 从零开始的航程存档索引，范围为 0 到 2。
        public int Index;

        /// true 开始新航程，false 恢复已保存航程。
        public bool NewGame;

        /// 是否先恢复该槽备份，默认为 false。
        public bool RecoverBackup;

        /// <summary>
        /// 开始或恢复一个存档槽的航程。构造后需通过 GlobalData.Dispatch 派发。
        /// </summary>
        /// <param name="source">发起命令的场景或规则实例；其它会话的命令被忽略。</param>
        /// <param name="index">从零开始的航程存档索引，范围为 0 到 2。</param>
        /// <param name="newGame">true 开始新航程，false 恢复已保存航程。</param>
        /// <param name="recoverBackup">是否先恢复该槽备份，默认为 false。</param>
        public HowToFishStartSlotAction(HowToFishWorld source, int index, bool newGame, bool recoverBackup = false)
        {
            Source = source;
            Index = index;
            NewGame = newGame;
            RecoverBackup = recoverBackup;
        }
    }

    /// 保存当前航程，保留现有原子保存与备份规则。
    public sealed class HowToFishSaveAction : HowToFishAction
    {
        /// 发起命令的场景或规则实例；其它会话的命令被忽略。
        internal HowToFishWorld Source;

        /// <summary>
        /// 保存当前航程，保留现有原子保存与备份规则。构造后需通过 GlobalData.Dispatch 派发。
        /// </summary>
        /// <param name="source">发起命令的场景或规则实例；其它会话的命令被忽略。</param>
        public HowToFishSaveAction(HowToFishWorld source)
        {
            Source = source;
        }
    }

    /// 预览输入偏好，不写入持久存档。
    public sealed class HowToFishApplyPreferencesAction : HowToFishAction
    {
        /// 发起命令的场景或规则实例；其它会话的命令被忽略。
        internal HowToFishWorld Source;

        /// 当前真实偏好或教学事实对应的值。
        public HowToFishLocalPreferences Value;

        /// <summary>
        /// 预览输入偏好，不写入持久存档。构造后需通过 GlobalData.Dispatch 派发。
        /// </summary>
        /// <param name="source">发起命令的场景或规则实例；其它会话的命令被忽略。</param>
        /// <param name="value">要预览的独立输入偏好；必须通过有效性检查。</param>
        public HowToFishApplyPreferencesAction(HowToFishWorld source, HowToFishLocalPreferences value)
        {
            Source = source;
            Value = value;
        }
    }

    /// 把当前输入偏好写入本地设置。
    public sealed class HowToFishSavePreferencesAction : HowToFishAction
    {
        /// 发起命令的场景或规则实例；其它会话的命令被忽略。
        internal HowToFishWorld Source;

        /// 同步处理结果；未接受请求时保持失败或零值。
        public bool Result { get; internal set; }

        /// <summary>
        /// 把当前输入偏好写入本地设置。构造后需通过 GlobalData.Dispatch 派发。
        /// </summary>
        /// <param name="source">发起命令的场景或规则实例；其它会话的命令被忽略。</param>
        public HowToFishSavePreferencesAction(HowToFishWorld source)
        {
            Source = source;
        }
    }

    /// 同步设置或服装页面的输入编辑状态。
    public sealed class HowToFishSetEditingSettingsAction : HowToFishAction
    {
        /// 发起命令的场景或规则实例；其它会话的命令被忽略。
        internal HowToFishWorld Source;

        /// 是否进入输入编辑状态，结束时恢复松键门禁。
        public bool Editing;

        /// <summary>
        /// 同步设置或服装页面的输入编辑状态。构造后需通过 GlobalData.Dispatch 派发。
        /// </summary>
        /// <param name="source">发起命令的场景或规则实例；其它会话的命令被忽略。</param>
        /// <param name="editing">是否进入输入编辑状态，结束时恢复松键门禁。</param>
        public HowToFishSetEditingSettingsAction(HowToFishWorld source, bool editing)
        {
            Source = source;
            Editing = editing;
        }
    }

    /// 尝试保存并应用已解锁的服装。
    public sealed class HowToFishTrySelectOutfitAction : HowToFishAction
    {
        /// 发起命令的场景或规则实例；其它会话的命令被忽略。
        internal HowToFishWorld Source;

        /// 本次操作的道具、装备或服装标识，按具体动作解释。
        public string Id;

        /// 同步处理结果；未接受请求时保持失败或零值。
        public bool Result { get; internal set; }

        /// <summary>
        /// 尝试保存并应用已解锁的服装。构造后需通过 GlobalData.Dispatch 派发。
        /// </summary>
        /// <param name="source">发起命令的场景或规则实例；其它会话的命令被忽略。</param>
        /// <param name="id">需要穿戴的已解锁服装标识。</param>
        public HowToFishTrySelectOutfitAction(HowToFishWorld source, string id)
        {
            Source = source;
            Id = id;
        }
    }

    /// 尝试用当前鱼获兑换老虎机奖励。
    public sealed class HowToFishTryPlaySlotMachineAction : HowToFishAction
    {
        /// 发起命令的场景或规则实例；其它会话的命令被忽略。
        internal HowToFishWorld Source;

        /// 老虎机所在岛屿索引，从零开始。
        public int Island;

        /// 当前实际世界物件，必须仍属于本场景。
        public HowToFishWorldItem Item;

        /// 同步处理结果；未接受请求时保持失败或零值。
        public bool Result { get; internal set; }

        /// 本次结算的用户反馈。
        public string Message { get; internal set; }

        /// <summary>
        /// 尝试用当前鱼获兑换老虎机奖励。构造后需通过 GlobalData.Dispatch 派发。
        /// </summary>
        /// <param name="source">发起命令的场景或规则实例；其它会话的命令被忽略。</param>
        /// <param name="island">老虎机所在岛屿索引，从零开始。</param>
        /// <param name="item">当前实际世界物件，必须仍属于本场景。</param>
        public HowToFishTryPlaySlotMachineAction(HowToFishWorld source, int island, HowToFishWorldItem item)
        {
            Source = source;
            Island = island;
            Item = item;
        }
    }

    /// 切换当前装备或船只支持的皮肤。
    public sealed class HowToFishChangeSkinAction : HowToFishAction
    {
        /// 发起命令的场景或规则实例；其它会话的命令被忽略。
        internal HowToFishWorld Source;

        /// <summary>
        /// 切换当前装备或船只支持的皮肤。构造后需通过 GlobalData.Dispatch 派发。
        /// </summary>
        /// <param name="source">发起命令的场景或规则实例；其它会话的命令被忽略。</param>
        public HowToFishChangeSkinAction(HowToFishWorld source)
        {
            Source = source;
        }
    }

    /// 按实际下注和开奖结果同步结算轮盘。
    public sealed class HowToFishTrySettleRouletteAction : HowToFishAction
    {
        /// 发起命令的场景或规则实例；其它会话的命令被忽略。
        internal HowToFishWorld Source;

        /// 本次结算的实际轮盘对象。
        public HowToFishRoulette Roulette;

        /// 实际下注物件及其押注颜色，保持现有所有权检查。
        public IReadOnlyDictionary<HowToFishWorldItem, HowToFishRouletteColor> Bets;

        /// 本次实际开奖颜色。
        public HowToFishRouletteColor WinningColor;

        /// 同步处理结果；未接受请求时保持失败或零值。
        public bool Result { get; internal set; }

        /// 本次轮盘结算的公告。
        public string Announcement { get; internal set; }

        /// <summary>
        /// 按实际下注和开奖结果同步结算轮盘。构造后需通过 GlobalData.Dispatch 派发。
        /// </summary>
        /// <param name="source">发起命令的场景或规则实例；其它会话的命令被忽略。</param>
        /// <param name="roulette">本次结算的实际轮盘对象。</param>
        /// <param name="bets">实际下注物件及其押注颜色，保持现有所有权检查。</param>
        /// <param name="winningColor">本次实际开奖颜色。</param>
        public HowToFishTrySettleRouletteAction(HowToFishWorld source, HowToFishRoulette roulette, IReadOnlyDictionary<HowToFishWorldItem, HowToFishRouletteColor> bets, HowToFishRouletteColor winningColor)
        {
            Source = source;
            Roulette = roulette;
            Bets = bets;
            WinningColor = winningColor;
        }
    }

    /// 设置本场暂停状态并同步物理表现。
    public sealed class HowToFishSetPausedAction : HowToFishAction
    {
        /// 发起命令的场景或规则实例；其它会话的命令被忽略。
        internal HowToFishWorld Source;

        /// 是否暂停本场玩法。
        public bool Paused;

        /// <summary>
        /// 设置本场暂停状态并同步物理表现。构造后需通过 GlobalData.Dispatch 派发。
        /// </summary>
        /// <param name="source">发起命令的场景或规则实例；其它会话的命令被忽略。</param>
        /// <param name="paused">是否暂停本场玩法。</param>
        public HowToFishSetPausedAction(HowToFishWorld source, bool paused)
        {
            Source = source;
            Paused = paused;
        }
    }

    /// 更新本场用户反馈及显示时限。
    public sealed class HowToFishNotifyAction : HowToFishAction
    {
        /// 发起命令的场景或规则实例；其它会话的命令被忽略。
        internal HowToFishWorld Source;

        /// 需要显示的业务反馈，null 表示清除。
        public string Message;

        /// <summary>
        /// 更新本场用户反馈及显示时限。构造后需通过 GlobalData.Dispatch 派发。
        /// </summary>
        /// <param name="source">发起命令的场景或规则实例；其它会话的命令被忽略。</param>
        /// <param name="message">需要显示的业务反馈，null 表示清除。</param>
        public HowToFishNotifyAction(HowToFishWorld source, string message)
        {
            Source = source;
            Message = message;
        }
    }

    /// 执行当前交互目标的业务操作。
    public sealed class HowToFishInteractAction : HowToFishAction
    {
        /// 发起命令的场景或规则实例；其它会话的命令被忽略。
        internal HowToFishWorld Source;

        /// 本次实际检测到的交互碰撞体。
        public Collider Collider;

        /// <summary>
        /// 执行当前交互目标的业务操作。构造后需通过 GlobalData.Dispatch 派发。
        /// </summary>
        /// <param name="source">发起命令的场景或规则实例；其它会话的命令被忽略。</param>
        /// <param name="collider">本次实际检测到的交互碰撞体。</param>
        public HowToFishInteractAction(HowToFishWorld source, Collider collider)
        {
            Source = source;
            Collider = collider;
        }
    }

    /// 把实际鱼获提交给任务交付站。
    public sealed class HowToFishDeliverToStationAction : HowToFishAction
    {
        /// 发起命令的场景或规则实例；其它会话的命令被忽略。
        internal HowToFishWorld Source;

        /// 接收交付的实际任务站。
        public HowToFishStation Station;

        /// 当前实际世界物件，必须仍属于本场景。
        public HowToFishWorldItem Item;

        /// <summary>
        /// 把实际鱼获提交给任务交付站。构造后需通过 GlobalData.Dispatch 派发。
        /// </summary>
        /// <param name="source">发起命令的场景或规则实例；其它会话的命令被忽略。</param>
        /// <param name="station">接收交付的实际任务站。</param>
        /// <param name="item">当前实际世界物件，必须仍属于本场景。</param>
        public HowToFishDeliverToStationAction(HowToFishWorld source, HowToFishStation station, HowToFishWorldItem item)
        {
            Source = source;
            Station = station;
            Item = item;
        }
    }

    /// 结局后继续当前航程。
    public sealed class HowToFishContinueAfterEndingAction : HowToFishAction
    {
        /// 发起命令的场景或规则实例；其它会话的命令被忽略。
        internal HowToFishWorld Source;

        /// <summary>
        /// 结局后继续当前航程。构造后需通过 GlobalData.Dispatch 派发。
        /// </summary>
        /// <param name="source">发起命令的场景或规则实例；其它会话的命令被忽略。</param>
        public HowToFishContinueAfterEndingAction(HowToFishWorld source)
        {
            Source = source;
        }
    }

    /// 恢复死亡后的规则状态，再请求场景复位。
    public sealed class HowToFishRespawnAction : HowToFishAction
    {
        /// 发起命令的场景或规则实例；其它会话的命令被忽略。
        internal HowToFishWorld Source;

        /// <summary>
        /// 恢复死亡后的规则状态，再请求场景复位。构造后需通过 GlobalData.Dispatch 派发。
        /// </summary>
        /// <param name="source">发起命令的场景或规则实例；其它会话的命令被忽略。</param>
        public HowToFishRespawnAction(HowToFishWorld source)
        {
            Source = source;
        }
    }

    /// 接收真实进食完成事件并检查服装奖励。
    public sealed class HowToFishOnCreatureEatenAction : HowToFishAction
    {
        /// 发起命令的场景或规则实例；其它会话的命令被忽略。
        internal HowToFishWorld Source;

        /// 当前实际世界物件，必须仍属于本场景。
        public HowToFishWorldItem Item;

        /// <summary>
        /// 接收真实进食完成事件并检查服装奖励。构造后需通过 GlobalData.Dispatch 派发。
        /// </summary>
        /// <param name="source">发起命令的场景或规则实例；其它会话的命令被忽略。</param>
        /// <param name="item">当前实际世界物件，必须仍属于本场景。</param>
        public HowToFishOnCreatureEatenAction(HowToFishWorld source, HowToFishWorldItem item)
        {
            Source = source;
            Item = item;
        }
    }

    /// 保存本场已获得的共享服装进度。
    public sealed class HowToFishSaveOutfitProgressAction : HowToFishAction
    {
        /// 发起命令的场景或规则实例；其它会话的命令被忽略。
        internal HowToFishWorld Source;

        /// <summary>
        /// 保存本场已获得的共享服装进度。构造后需通过 GlobalData.Dispatch 派发。
        /// </summary>
        /// <param name="source">发起命令的场景或规则实例；其它会话的命令被忽略。</param>
        public HowToFishSaveOutfitProgressAction(HowToFishWorld source)
        {
            Source = source;
        }
    }

    /// 同步结算本场生命损失，调用方按死亡结果执行物理表现。
    public sealed class HowToFishDamageAction : HowToFishAction
    {
        internal HowToFishSession Source;

        public float Amount;

        /// 本次生命损失是否导致死亡，物理死亡表现由调用方处理。
        public bool Died { get; internal set; }

        /// <summary>
        /// 同步结算本场生命损失，调用方按死亡结果执行物理表现。构造后通过 GlobalData.Dispatch 派发。
        /// </summary>
        /// <param name="source">命令所属场景或规则实例；旧来源请求被忽略。</param>
        /// <param name="amount">实际非负生命损失；无效值不会改变生命。</param>
        public HowToFishDamageAction(HowToFishSession source, float amount)
        {
            Source = source;
            Amount = amount;
        }
    }

    /// 应用真实中毒或燃烧状态。
    public sealed class HowToFishDamageStatusAction : HowToFishAction
    {
        internal HowToFishSession Source;

        public bool Burning;

        public float Seconds, DamagePerSecond;

        /// <summary>
        /// 应用真实中毒或燃烧状态。构造后通过 GlobalData.Dispatch 派发。
        /// </summary>
        /// <param name="source">命令所属场景或规则实例；旧来源请求被忽略。</param>
        /// <param name="burning">true 为燃烧，false 为中毒。</param>
        /// <param name="seconds">实际状态持续秒数，有效范围大于 0 且不超过 60。</param>
        /// <param name="damagePerSecond">每秒实际伤害，有效范围大于 0 且不超过 1000。</param>
        public HowToFishDamageStatusAction(HowToFishSession source, bool burning, float seconds, float damagePerSecond)
        {
            Source = source;
            Burning = burning;
            Seconds = seconds;
            DamagePerSecond = damagePerSecond;
        }
    }

    /// 计算本帧持续伤害并返回伤害和致死判断。
    public sealed class HowToFishDamageStatusTickAction : HowToFishAction
    {
        internal HowToFishSession Source;

        public float Delta;

        /// 本帧规则计算的持续伤害值。
        public float Damage { get; internal set; }

        /// 本帧持续伤害是否达到当前生命。
        public bool Lethal { get; internal set; }

        /// <summary>
        /// 计算本帧持续伤害并返回伤害和致死判断。构造后通过 GlobalData.Dispatch 派发。
        /// </summary>
        /// <param name="source">命令所属场景或规则实例；旧来源请求被忽略。</param>
        /// <param name="delta">本帧实际时间增量。</param>
        public HowToFishDamageStatusTickAction(HowToFishSession source, float delta)
        {
            Source = source;
            Delta = delta;
        }
    }

    /// 恢复进食获得的饱食和生命。
    public sealed class HowToFishRestoreFoodAction : HowToFishAction
    {
        internal HowToFishSession Source;

        public float Hunger, Health;

        /// <summary>
        /// 恢复进食获得的饱食和生命。构造后通过 GlobalData.Dispatch 派发。
        /// </summary>
        /// <param name="source">命令所属场景或规则实例；旧来源请求被忽略。</param>
        /// <param name="hunger">本次进食恢复的饱食值。</param>
        /// <param name="health">本次进食恢复的生命值。</param>
        public HowToFishRestoreFoodAction(HowToFishSession source, float hunger, float health)
        {
            Source = source;
            Hunger = hunger;
            Health = health;
        }
    }

    public enum HowToFishAmmoOperation
    {
        Initialize,
        Reload,
        Consume
    }

    /// 按真实装备容量初始化、装填或消费弹药。
    public sealed class HowToFishAmmoAction : HowToFishAction
    {
        internal HowToFishSession Source;

        public string ItemId;

        public int Capacity;

        public HowToFishAmmoOperation Operation;

        /// <summary>
        /// 按真实装备容量初始化、装填或消费弹药。构造后通过 GlobalData.Dispatch 派发。
        /// </summary>
        /// <param name="source">命令所属场景或规则实例；旧来源请求被忽略。</param>
        /// <param name="id">当前操作的实际装备标识；卸下装备时可为空。</param>
        /// <param name="capacity">当前装备配置的弹匣容量。</param>
        /// <param name="operation">本次弹药或设置操作。</param>
        public HowToFishAmmoAction(HowToFishSession source, string id, int capacity, HowToFishAmmoOperation operation)
        {
            Source = source;
            ItemId = id;
            Capacity = capacity;
            Operation = operation;
        }
    }

    /// 更新当前装备记录的真实受热状态。
    public sealed class HowToFishCookingAction : HowToFishAction
    {
        internal HowToFishSession Source;

        public string ItemId;

        public float Heat;

        public bool Extinguish;

        /// <summary>
        /// 更新当前装备记录的真实受热状态。构造后通过 GlobalData.Dispatch 派发。
        /// </summary>
        /// <param name="source">命令所属场景或规则实例；旧来源请求被忽略。</param>
        /// <param name="id">当前操作的实际装备标识；卸下装备时可为空。</param>
        /// <param name="heat">本帧实际烹饪进度增量。</param>
        /// <param name="extinguish">是否清除受热进度，默认为 false。</param>
        public HowToFishCookingAction(HowToFishSession source, string id, float heat, bool extinguish = false)
        {
            Source = source;
            ItemId = id;
            Heat = heat;
            Extinguish = extinguish;
        }
    }

    /// 同步实际穿戴的装备标识。
    public sealed class HowToFishEquipAction : HowToFishAction
    {
        internal HowToFishSession Source;

        public string ItemId;

        /// <summary>
        /// 同步实际穿戴的装备标识。构造后通过 GlobalData.Dispatch 派发。
        /// </summary>
        /// <param name="source">命令所属场景或规则实例；旧来源请求被忽略。</param>
        /// <param name="id">当前操作的实际装备标识；卸下装备时可为空。</param>
        public HowToFishEquipAction(HowToFishSession source, string id)
        {
            Source = source;
            ItemId = id;
        }
    }

    /// 同步当前选中的装备槽。
    public sealed class HowToFishSelectSlotAction : HowToFishAction
    {
        internal HowToFishSession Source;

        public int Slot;

        /// <summary>
        /// 同步当前选中的装备槽。构造后通过 GlobalData.Dispatch 派发。
        /// </summary>
        /// <param name="source">命令所属场景或规则实例；旧来源请求被忽略。</param>
        /// <param name="slot">目标存档或装备槽索引；其它命令保持默认值。</param>
        public HowToFishSelectSlotAction(HowToFishSession source, int slot)
        {
            Source = source;
            Slot = slot;
        }
    }

    /// 按现有规则和 UI 频率推进本场时间。
    public sealed class HowToFishTickAction : HowToFishAction
    {
        internal HowToFishWorld Source;

        public float Delta, UnscaledDelta;

        /// <summary>
        /// 按现有规则和 UI 频率推进本场时间。构造后通过 GlobalData.Dispatch 派发。
        /// </summary>
        /// <param name="source">命令所属场景或规则实例；旧来源请求被忽略。</param>
        /// <param name="delta">本帧实际时间增量。</param>
        /// <param name="unscaledDelta">保留暂停期间计时语义的真实时间增量。</param>
        public HowToFishTickAction(HowToFishWorld source, float delta, float unscaledDelta)
        {
            Source = source;
            Delta = delta;
            UnscaledDelta = unscaledDelta;
        }
    }

    public enum HowToFishUiCommand
    {
        ToggleJournal,
        OpenSettings,
        CloseSettings,
        OpenOutfits,
        CloseOutfits,
        Exit,
        NewSlot,
        ContinueSlot,
        BeginSettings,
        CancelSettings,
        SaveSettings
    }

    /// 请求本场菜单、设置或服装操作。
    public sealed class HowToFishUiAction : HowToFishAction
    {
        internal HowToFishWorld Source;

        public HowToFishUiCommand Command;

        public int Slot;

        /// 请求是否被成功接受。
        public bool Success { get; internal set; }

        /// <summary>
        /// 请求本场菜单、设置或服装操作。构造后通过 GlobalData.Dispatch 派发。
        /// </summary>
        /// <param name="source">命令所属场景或规则实例；旧来源请求被忽略。</param>
        /// <param name="command">本次已定义的页面操作。</param>
        /// <param name="slot">目标存档或装备槽索引；其它命令保持默认值。</param>
        public HowToFishUiAction(HowToFishWorld source, HowToFishUiCommand command, int slot = 0)
        {
            Source = source;
            Command = command;
            Slot = slot;
        }
    }

    /// 同步本场离场或导航恢复状态。
    internal sealed class HowToFishExitingAction : HowToFishAction
    {
        internal HowToFishWorld Source;

        internal bool Exiting;

        public HowToFishExitingAction(HowToFishWorld source, bool exiting)
        {
            Source = source;
            Exiting = exiting;
        }
    }
}
