using Core.Runtime;
using Hotfix.JinxCasino.Rules;

namespace Hotfix.JinxCasino
{
    /// 赌场的业务命令入口。
    public abstract class JinxCasinoAction : IAction
    {
    }

    /// 更新当前冒险的用户反馈。
    public sealed class JinxCasinoSetStatusAction : JinxCasinoAction
    {
        /// 发起命令的场景或规则实例；其它会话的命令被忽略。
        internal JinxCasinoGame Source;

        /// 需要显示的业务反馈，null 表示清除。
        public string Message;

        /// <summary>
        /// 更新当前冒险的用户反馈。构造后需通过 GlobalData.Dispatch 派发。
        /// </summary>
        /// <param name="source">发起命令的场景或规则实例；其它会话的命令被忽略。</param>
        /// <param name="message">需要显示的业务反馈，null 表示清除。</param>
        public JinxCasinoSetStatusAction(JinxCasinoGame source, string message)
        {
            Source = source;
            Message = message;
        }
    }

    /// 按当前配置开始一次冒险。
    public sealed class JinxCasinoStartAdventureAction : JinxCasinoAction
    {
        /// 发起命令的场景或规则实例；其它会话的命令被忽略。
        internal JinxCasinoGame Source;

        /// 本次冒险模式。
        public CasinoAdventureMode Mode;

        /// 当前场景创建的规则配置。
        public CasinoAdventureConfig Config;

        /// 可选规则随机种子，null 使用现有随机来源。
        public uint? Seed;

        /// <summary>
        /// 按当前配置开始一次冒险。构造后需通过 GlobalData.Dispatch 派发。
        /// </summary>
        /// <param name="source">发起命令的场景或规则实例；其它会话的命令被忽略。</param>
        /// <param name="mode">本次冒险模式。</param>
        /// <param name="config">当前场景创建的规则配置。</param>
        /// <param name="seed">可选规则随机种子，null 使用现有随机来源。</param>
        public JinxCasinoStartAdventureAction(JinxCasinoGame source, CasinoAdventureMode mode, CasinoAdventureConfig config, uint? seed = null)
        {
            Source = source;
            Mode = mode;
            Config = config;
            Seed = seed;
        }
    }

    /// 开始教学冒险，按请求决定是否替换旅程。
    public sealed class JinxCasinoStartTutorialAdventureAction : JinxCasinoAction
    {
        /// 发起命令的场景或规则实例；其它会话的命令被忽略。
        internal JinxCasinoGame Source;

        /// 当前场景创建的规则配置。
        public CasinoAdventureConfig Config;

        /// 是否替换当前旅程，默认为 false。
        public bool ReplaceCurrentRun;

        /// 可选规则随机种子，null 使用现有随机来源。
        public uint? Seed;

        /// 同步处理结果；未接受请求时保持失败或零值。
        public CasinoAdventureResult Result { get; internal set; } = new CasinoAdventureResult
        {
            Error = "Inactive",
            Description = "当前旅程已经关闭。"
        };

        /// <summary>
        /// 开始教学冒险，按请求决定是否替换旅程。构造后需通过 GlobalData.Dispatch 派发。
        /// </summary>
        /// <param name="source">发起命令的场景或规则实例；其它会话的命令被忽略。</param>
        /// <param name="config">当前场景创建的规则配置。</param>
        /// <param name="replaceCurrentRun">是否替换当前旅程，默认为 false。</param>
        /// <param name="seed">可选规则随机种子，null 使用现有随机来源。</param>
        public JinxCasinoStartTutorialAdventureAction(JinxCasinoGame source, CasinoAdventureConfig config, bool replaceCurrentRun = false, uint? seed = null)
        {
            Source = source;
            Config = config;
            ReplaceCurrentRun = replaceCurrentRun;
            Seed = seed;
        }
    }

    /// 按现有规则频率推进冒险时间。
    public sealed class JinxCasinoTickAction : JinxCasinoAction
    {
        /// 发起命令的场景或规则实例；其它会话的命令被忽略。
        internal JinxCasinoGame Source;

        /// 按现有时钟采集的规则时间增量。
        public float DeltaSeconds;

        /// <summary>
        /// 按现有规则频率推进冒险时间。构造后需通过 GlobalData.Dispatch 派发。
        /// </summary>
        /// <param name="source">发起命令的场景或规则实例；其它会话的命令被忽略。</param>
        /// <param name="deltaSeconds">按现有时钟采集的规则时间增量。</param>
        public JinxCasinoTickAction(JinxCasinoGame source, float deltaSeconds)
        {
            Source = source;
            DeltaSeconds = deltaSeconds;
        }
    }

    /// 尝试购买指定商店道具并返回实际交易结果。
    public sealed class JinxCasinoPurchaseItemAction : JinxCasinoAction
    {
        /// 发起命令的场景或规则实例；其它会话的命令被忽略。
        internal JinxCasinoGame Source;

        /// 本次操作的道具或装备标识。
        public string ItemId;

        /// 发起操作的帧标识，用于保持同帧去重。
        public int FrameId;

        /// 同步处理结果；未接受请求时保持失败或零值。
        public CasinoAdventureResult Result { get; internal set; } = new CasinoAdventureResult
        {
            Error = "Inactive",
            Description = "当前旅程已经关闭。"
        };

        /// <summary>
        /// 尝试购买指定商店道具并返回实际交易结果。构造后需通过 GlobalData.Dispatch 派发。
        /// </summary>
        /// <param name="source">发起命令的场景或规则实例；其它会话的命令被忽略。</param>
        /// <param name="itemId">本次操作的道具或装备标识。</param>
        /// <param name="frameId">发起操作的帧标识，用于保持同帧去重。</param>
        public JinxCasinoPurchaseItemAction(JinxCasinoGame source, string itemId, int frameId)
        {
            Source = source;
            ItemId = itemId;
            FrameId = frameId;
        }
    }

    /// 尝试对实际目标使用道具。
    public sealed class JinxCasinoUseItemAction : JinxCasinoAction
    {
        /// 发起命令的场景或规则实例；其它会话的命令被忽略。
        internal JinxCasinoGame Source;

        /// 本次操作的道具或装备标识。
        public string ItemId;

        /// 实际作用目标的标识，保留现有目标保护规则。
        public string TargetId;

        /// 发起操作的帧标识，用于保持同帧去重。
        public int FrameId;

        /// 同步处理结果；未接受请求时保持失败或零值。
        public CasinoAdventureResult Result { get; internal set; } = new CasinoAdventureResult
        {
            Error = "Inactive",
            Description = "当前旅程已经关闭。"
        };

        /// <summary>
        /// 尝试对实际目标使用道具。构造后需通过 GlobalData.Dispatch 派发。
        /// </summary>
        /// <param name="source">发起命令的场景或规则实例；其它会话的命令被忽略。</param>
        /// <param name="itemId">本次操作的道具或装备标识。</param>
        /// <param name="targetId">实际作用目标的标识，保留现有目标保护规则。</param>
        /// <param name="frameId">发起操作的帧标识，用于保持同帧去重。</param>
        public JinxCasinoUseItemAction(JinxCasinoGame source, string itemId, string targetId, int frameId)
        {
            Source = source;
            ItemId = itemId;
            TargetId = targetId;
            FrameId = frameId;
        }
    }

    /// 撤销已准备道具并恢复其规则状态。
    public sealed class JinxCasinoCancelPreparedItemAction : JinxCasinoAction
    {
        /// 发起命令的场景或规则实例；其它会话的命令被忽略。
        internal JinxCasinoGame Source;

        /// 本次操作的道具或装备标识。
        public string ItemId;

        /// 发起操作的帧标识，用于保持同帧去重。
        public int FrameId;

        /// 同步处理结果；未接受请求时保持失败或零值。
        public CasinoAdventureResult Result { get; internal set; } = new CasinoAdventureResult
        {
            Error = "Inactive",
            Description = "当前旅程已经关闭。"
        };

        /// <summary>
        /// 撤销已准备道具并恢复其规则状态。构造后需通过 GlobalData.Dispatch 派发。
        /// </summary>
        /// <param name="source">发起命令的场景或规则实例；其它会话的命令被忽略。</param>
        /// <param name="itemId">本次操作的道具或装备标识。</param>
        /// <param name="frameId">发起操作的帧标识，用于保持同帧去重。</param>
        public JinxCasinoCancelPreparedItemAction(JinxCasinoGame source, string itemId, int frameId)
        {
            Source = source;
            ItemId = itemId;
            FrameId = frameId;
        }
    }

    /// 尝试通过出口完成当前冒险阶段。
    public sealed class JinxCasinoCompleteStageAction : JinxCasinoAction
    {
        /// 发起命令的场景或规则实例；其它会话的命令被忽略。
        internal JinxCasinoGame Source;

        /// 发起操作的帧标识，用于保持同帧去重。
        public int FrameId;

        /// 同步处理结果；未接受请求时保持失败或零值。
        public CasinoAdventureResult Result { get; internal set; } = new CasinoAdventureResult
        {
            Error = "Inactive",
            Description = "当前旅程已经关闭。"
        };

        /// <summary>
        /// 尝试通过出口完成当前冒险阶段。构造后需通过 GlobalData.Dispatch 派发。
        /// </summary>
        /// <param name="source">发起命令的场景或规则实例；其它会话的命令被忽略。</param>
        /// <param name="frameId">发起操作的帧标识，用于保持同帧去重。</param>
        public JinxCasinoCompleteStageAction(JinxCasinoGame source, int frameId)
        {
            Source = source;
            FrameId = frameId;
        }
    }

    /// 请求当前旅程的实际结局。
    public sealed class JinxCasinoChooseEndingAction : JinxCasinoAction
    {
        /// 发起命令的场景或规则实例；其它会话的命令被忽略。
        internal JinxCasinoGame Source;

        /// 请求的结局类型，由规则判断是否可执行。
        public CasinoAdventureEnding Ending;

        /// 发起操作的帧标识，用于保持同帧去重。
        public int FrameId;

        /// 同步处理结果；未接受请求时保持失败或零值。
        public CasinoAdventureResult Result { get; internal set; } = new CasinoAdventureResult
        {
            Error = "Inactive",
            Description = "当前旅程已经关闭。"
        };

        /// <summary>
        /// 请求当前旅程的实际结局。构造后需通过 GlobalData.Dispatch 派发。
        /// </summary>
        /// <param name="source">发起命令的场景或规则实例；其它会话的命令被忽略。</param>
        /// <param name="ending">请求的结局类型，由规则判断是否可执行。</param>
        /// <param name="frameId">发起操作的帧标识，用于保持同帧去重。</param>
        public JinxCasinoChooseEndingAction(JinxCasinoGame source, CasinoAdventureEnding ending, int frameId)
        {
            Source = source;
            Ending = ending;
            FrameId = frameId;
        }
    }

    /// 保存当前冒险到指定槽位。
    public sealed class JinxCasinoSaveAdventureAction : JinxCasinoAction
    {
        /// 发起命令的场景或规则实例；其它会话的命令被忽略。
        internal JinxCasinoGame Source;

        /// 目标存档或装备槽；存档索引约定由对应 Demo 保持。
        public int Slot;

        /// 同步处理结果；未接受请求时保持失败或零值。
        public bool Result { get; internal set; }

        /// <summary>
        /// 保存当前冒险到指定槽位。构造后需通过 GlobalData.Dispatch 派发。
        /// </summary>
        /// <param name="source">发起命令的场景或规则实例；其它会话的命令被忽略。</param>
        /// <param name="slot">目标存档槽，范围为 1 到 3。</param>
        public JinxCasinoSaveAdventureAction(JinxCasinoGame source, int slot)
        {
            Source = source;
            Slot = slot;
        }
    }

    /// 从指定槽位恢复冒险。
    public sealed class JinxCasinoLoadAdventureAction : JinxCasinoAction
    {
        /// 发起命令的场景或规则实例；其它会话的命令被忽略。
        internal JinxCasinoGame Source;

        /// 目标存档或装备槽；存档索引约定由对应 Demo 保持。
        public int Slot;

        /// 同步处理结果；未接受请求时保持失败或零值。
        public bool Result { get; internal set; }

        /// <summary>
        /// 从指定槽位恢复冒险。构造后需通过 GlobalData.Dispatch 派发。
        /// </summary>
        /// <param name="source">发起命令的场景或规则实例；其它会话的命令被忽略。</param>
        /// <param name="slot">目标存档槽，范围为 1 到 3。</param>
        public JinxCasinoLoadAdventureAction(JinxCasinoGame source, int slot)
        {
            Source = source;
            Slot = slot;
        }
    }

    /// 记录真实发生的教学事实。
    public sealed class JinxCasinoObserveTutorialAction : JinxCasinoAction
    {
        /// 发起命令的场景或规则实例；其它会话的命令被忽略。
        internal JinxCasinoGame Source;

        /// 本次真实发生的教学事实类型。
        public CasinoTutorialFact Fact;

        /// 当前真实偏好或教学事实对应的值。
        public int Value;

        /// 事实所属机台，可为空。
        public string StationId;

        /// 是否发布教学规则反馈，默认为 true。
        public bool Notify;

        /// 同步处理结果；未接受请求时保持失败或零值。
        public CasinoAdventureResult Result { get; internal set; } = new CasinoAdventureResult
        {
            Error = "Inactive",
            Description = "当前旅程已经关闭。"
        };

        /// <summary>
        /// 记录真实发生的教学事实。构造后需通过 GlobalData.Dispatch 派发。
        /// </summary>
        /// <param name="source">发起命令的场景或规则实例；其它会话的命令被忽略。</param>
        /// <param name="fact">本次真实发生的教学事实类型。</param>
        /// <param name="value">当前真实偏好或教学事实对应的值。</param>
        /// <param name="stationId">事实所属机台，可为空。</param>
        /// <param name="notify">是否发布教学规则反馈，默认为 true。</param>
        public JinxCasinoObserveTutorialAction(JinxCasinoGame source, CasinoTutorialFact fact, int value, string stationId = null, bool notify = true)
        {
            Source = source;
            Fact = fact;
            Value = value;
            StationId = stationId;
            Notify = notify;
        }
    }

    /// 跳过当前教学流程。
    public sealed class JinxCasinoSkipTutorialAction : JinxCasinoAction
    {
        /// 发起命令的场景或规则实例；其它会话的命令被忽略。
        internal JinxCasinoGame Source;

        /// 同步处理结果；未接受请求时保持失败或零值。
        public CasinoAdventureResult Result { get; internal set; } = new CasinoAdventureResult
        {
            Error = "Inactive",
            Description = "当前旅程已经关闭。"
        };

        /// <summary>
        /// 跳过当前教学流程。构造后需通过 GlobalData.Dispatch 派发。
        /// </summary>
        /// <param name="source">发起命令的场景或规则实例；其它会话的命令被忽略。</param>
        public JinxCasinoSkipTutorialAction(JinxCasinoGame source)
        {
            Source = source;
        }
    }

    /// 尝试完成当前教学并返回规则结果。
    public sealed class JinxCasinoCompleteTutorialAction : JinxCasinoAction
    {
        /// 发起命令的场景或规则实例；其它会话的命令被忽略。
        internal JinxCasinoGame Source;

        /// 同步处理结果；未接受请求时保持失败或零值。
        public CasinoAdventureResult Result { get; internal set; } = new CasinoAdventureResult
        {
            Error = "Inactive",
            Description = "当前旅程已经关闭。"
        };

        /// <summary>
        /// 尝试完成当前教学并返回规则结果。构造后需通过 GlobalData.Dispatch 派发。
        /// </summary>
        /// <param name="source">发起命令的场景或规则实例；其它会话的命令被忽略。</param>
        public JinxCasinoCompleteTutorialAction(JinxCasinoGame source)
        {
            Source = source;
        }
    }

    public enum JinxCasinoUiCommand
    {
        Pause,
        Resume,
        Interact,
        ExitTable,
        Exit,
        Quit,
        OpenSettings,
        CloseSettings,
        OpenSaveLoad,
        OpenSaveWrite,
        ChooseSaveSlot,
        ConfirmSaveOperation,
        CancelSaveWindow,
        StartTeaching,
        SkipTeaching,
        CompleteTeaching,
        RetryTeaching,
        RequestTeachingStandard,
        DeferTeachingCompletion,
        ContinueTeachingPractice,
        ReopenTeachingChoice,
        ResumeTeachingView,
        ConfirmTeachingReplacement,
        CancelTutorialWindow,
        CancelWindow
    }

    /// 请求本场菜单、存档确认或教学操作。
    public sealed class JinxCasinoUiAction : JinxCasinoAction
    {
        internal JinxCasinoController Source;

        public JinxCasinoUiCommand Command;

        public int Slot;

        /// <summary>请求当前场景的菜单或玩法操作，由 Handler 同步处理。</summary>
        /// <param name="source">拥有本次请求的场景。</param>
        /// <param name="command">具体操作。</param>
        /// <param name="slot">存档槽；其它命令保持默认零。</param>
        public JinxCasinoUiAction(JinxCasinoController source, JinxCasinoUiCommand command, int slot = 0)
        {
            Source = source;
            Command = command;
            Slot = slot;
        }
    }

    public enum JinxCasinoSettingsOperation
    {
        Begin,
        Preview,
        Save,
        Cancel
    }

    /// 请求设置草稿的预览、保存或撤销。
    public sealed class JinxCasinoSettingsAction : JinxCasinoAction
    {
        internal JinxCasinoController Source;

        public JinxCasinoSettingsOperation Operation;

        public Hotfix.JinxCasino.Persistence.CasinoLocalPreferences Candidate;

        /// 请求是否被成功接受。
        public bool Success { get; internal set; }

        /// 请求失败时的用户可读原因。
        public string Error { get; internal set; }

        /// <summary>开始设置草稿、预览、保存或撤销，通过结果反馈实际写盘状态。</summary>
        /// <param name="source">当前场景。</param>
        /// <param name="operation">设置操作。</param>
        /// <param name="candidate">预览时提供独立偏好副本；其它操作可为空。</param>
        public JinxCasinoSettingsAction(JinxCasinoController source, JinxCasinoSettingsOperation operation, Hotfix.JinxCasino.Persistence.CasinoLocalPreferences candidate = null)
        {
            Source = source;
            Operation = operation;
            Candidate = candidate;
        }
    }

    /// 请求当前实际机台的业务操作并返回规则结果。
    public sealed class JinxCasinoTableActionRequest : JinxCasinoAction
    {
        internal JinxCasinoController Source;

        public Hotfix.JinxCasino.Interaction.JinxCasinoTableSession Table;

        public Hotfix.JinxCasino.Interaction.JinxCasinoTableAction Action;

        public int Value, Frame;

        public Hotfix.JinxCasino.Interaction.JinxCasinoTableResult Result { get; internal set; }

        /// <summary>
        /// 请求当前实际机台的业务操作并返回规则结果。构造后通过 GlobalData.Dispatch 派发。
        /// </summary>
        /// <param name="source">命令所属场景或规则实例；旧来源请求被忽略。</param>
        /// <param name="table">本次实际交互的机台会话。</param>
        /// <param name="action">机台支持的业务操作。</param>
        /// <param name="value">操作对应的选择索引或业务值。</param>
        /// <param name="frame">操作发生的帧标识，保持同帧去重。</param>
        public JinxCasinoTableActionRequest(JinxCasinoController source, Hotfix.JinxCasino.Interaction.JinxCasinoTableSession table, Hotfix.JinxCasino.Interaction.JinxCasinoTableAction action, int value, int frame)
        {
            Source = source;
            Table = table;
            Action = action;
            Value = value;
            Frame = frame;
        }
    }

    /// 同步本场离场或导航恢复状态。
    internal sealed class JinxCasinoExitingAction : JinxCasinoAction
    {
        internal JinxCasinoController Source;

        internal bool Exiting;

        public JinxCasinoExitingAction(JinxCasinoController source, bool exiting)
        {
            Source = source;
            Exiting = exiting;
        }
    }
}
