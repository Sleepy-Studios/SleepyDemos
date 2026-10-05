using Core.Runtime;
using Hotfix.JinxCasino.Rules;

namespace Hotfix.JinxCasino
{
    public abstract class JinxCasinoAction : IAction { }
    public sealed class JinxCasinoSetStatusAction : JinxCasinoAction
    {
        internal JinxCasinoGame Source; internal string message;

        internal static void Send(JinxCasinoGame source, string message)
        {
            var request = new JinxCasinoSetStatusAction { Source = source, message = message };
            GlobalData.Dispatch(request);
        }
    }
    public sealed class JinxCasinoStartAdventureAction : JinxCasinoAction
    {
        internal JinxCasinoGame Source; internal CasinoAdventureMode mode; internal CasinoAdventureConfig config; internal uint? seed;

        internal static void Send(JinxCasinoGame source, CasinoAdventureMode mode, CasinoAdventureConfig config, uint? seed = null)
        {
            var request = new JinxCasinoStartAdventureAction { Source = source, mode = mode, config = config, seed = seed };
            GlobalData.Dispatch(request);
        }
    }
    public sealed class JinxCasinoStartTutorialAdventureAction : JinxCasinoAction
    {
        internal JinxCasinoGame Source; internal CasinoAdventureConfig config; internal bool replaceCurrentRun; internal uint? seed;
        public CasinoAdventureResult Result { get; internal set; } = new() { Error = "Inactive", Description = "当前旅程已经关闭。" };
        internal static CasinoAdventureResult Send(JinxCasinoGame source, CasinoAdventureConfig config, bool replaceCurrentRun = false, uint? seed = null)
        {
            var request = new JinxCasinoStartTutorialAdventureAction { Source = source, config = config, replaceCurrentRun = replaceCurrentRun, seed = seed };
            GlobalData.Dispatch(request); return request.Result;
        }
    }
    public sealed class JinxCasinoTickAction : JinxCasinoAction
    {
        internal JinxCasinoGame Source; internal float deltaSeconds;

        internal static void Send(JinxCasinoGame source, float deltaSeconds)
        {
            var request = new JinxCasinoTickAction { Source = source, deltaSeconds = deltaSeconds };
            GlobalData.Dispatch(request);
        }
    }
    public sealed class JinxCasinoPurchaseItemAction : JinxCasinoAction
    {
        internal JinxCasinoGame Source; internal string itemId; internal int frameId;
        public CasinoAdventureResult Result { get; internal set; } = new() { Error = "Inactive", Description = "当前旅程已经关闭。" };
        internal static CasinoAdventureResult Send(JinxCasinoGame source, string itemId, int frameId)
        {
            var request = new JinxCasinoPurchaseItemAction { Source = source, itemId = itemId, frameId = frameId };
            GlobalData.Dispatch(request); return request.Result;
        }
    }
    public sealed class JinxCasinoUseItemAction : JinxCasinoAction
    {
        internal JinxCasinoGame Source; internal string itemId; internal string targetId; internal int frameId;
        public CasinoAdventureResult Result { get; internal set; } = new() { Error = "Inactive", Description = "当前旅程已经关闭。" };
        internal static CasinoAdventureResult Send(JinxCasinoGame source, string itemId, string targetId, int frameId)
        {
            var request = new JinxCasinoUseItemAction { Source = source, itemId = itemId, targetId = targetId, frameId = frameId };
            GlobalData.Dispatch(request); return request.Result;
        }
    }
    public sealed class JinxCasinoCancelPreparedItemAction : JinxCasinoAction
    {
        internal JinxCasinoGame Source; internal string itemId; internal int frameId;
        public CasinoAdventureResult Result { get; internal set; } = new() { Error = "Inactive", Description = "当前旅程已经关闭。" };
        internal static CasinoAdventureResult Send(JinxCasinoGame source, string itemId, int frameId)
        {
            var request = new JinxCasinoCancelPreparedItemAction { Source = source, itemId = itemId, frameId = frameId };
            GlobalData.Dispatch(request); return request.Result;
        }
    }
    public sealed class JinxCasinoCompleteStageAction : JinxCasinoAction
    {
        internal JinxCasinoGame Source; internal int frameId;
        public CasinoAdventureResult Result { get; internal set; } = new() { Error = "Inactive", Description = "当前旅程已经关闭。" };
        internal static CasinoAdventureResult Send(JinxCasinoGame source, int frameId)
        {
            var request = new JinxCasinoCompleteStageAction { Source = source, frameId = frameId };
            GlobalData.Dispatch(request); return request.Result;
        }
    }
    public sealed class JinxCasinoChooseEndingAction : JinxCasinoAction
    {
        internal JinxCasinoGame Source; internal CasinoAdventureEnding ending; internal int frameId;
        public CasinoAdventureResult Result { get; internal set; } = new() { Error = "Inactive", Description = "当前旅程已经关闭。" };
        internal static CasinoAdventureResult Send(JinxCasinoGame source, CasinoAdventureEnding ending, int frameId)
        {
            var request = new JinxCasinoChooseEndingAction { Source = source, ending = ending, frameId = frameId };
            GlobalData.Dispatch(request); return request.Result;
        }
    }
    public sealed class JinxCasinoSaveAdventureAction : JinxCasinoAction
    {
        internal JinxCasinoGame Source; internal int slot;
        public bool Result { get; internal set; }
        internal static bool Send(JinxCasinoGame source, int slot)
        {
            var request = new JinxCasinoSaveAdventureAction { Source = source, slot = slot };
            GlobalData.Dispatch(request); return request.Result;
        }
    }
    public sealed class JinxCasinoLoadAdventureAction : JinxCasinoAction
    {
        internal JinxCasinoGame Source; internal int slot;
        public bool Result { get; internal set; }
        internal static bool Send(JinxCasinoGame source, int slot)
        {
            var request = new JinxCasinoLoadAdventureAction { Source = source, slot = slot };
            GlobalData.Dispatch(request); return request.Result;
        }
    }
    public sealed class JinxCasinoObserveTutorialAction : JinxCasinoAction
    {
        internal JinxCasinoGame Source; internal CasinoTutorialFact fact; internal int value; internal string stationId; internal bool notify;
        public CasinoAdventureResult Result { get; internal set; } = new() { Error = "Inactive", Description = "当前旅程已经关闭。" };
        internal static CasinoAdventureResult Send(JinxCasinoGame source, CasinoTutorialFact fact, int value, string stationId = null, bool notify = true)
        {
            var request = new JinxCasinoObserveTutorialAction { Source = source, fact = fact, value = value, stationId = stationId, notify = notify };
            GlobalData.Dispatch(request); return request.Result;
        }
    }
    public sealed class JinxCasinoSkipTutorialAction : JinxCasinoAction
    {
        internal JinxCasinoGame Source;
        public CasinoAdventureResult Result { get; internal set; } = new() { Error = "Inactive", Description = "当前旅程已经关闭。" };
        internal static CasinoAdventureResult Send(JinxCasinoGame source)
        {
            var request = new JinxCasinoSkipTutorialAction { Source = source };
            GlobalData.Dispatch(request); return request.Result;
        }
    }
    public sealed class JinxCasinoCompleteTutorialAction : JinxCasinoAction
    {
        internal JinxCasinoGame Source;
        public CasinoAdventureResult Result { get; internal set; } = new() { Error = "Inactive", Description = "当前旅程已经关闭。" };
        internal static CasinoAdventureResult Send(JinxCasinoGame source)
        {
            var request = new JinxCasinoCompleteTutorialAction { Source = source };
            GlobalData.Dispatch(request); return request.Result;
        }
    }
    public enum JinxCasinoUiCommand { Pause, Resume, Interact, ExitTable, Exit, Quit, OpenSettings, CloseSettings, OpenSaveLoad, OpenSaveWrite, ChooseSaveSlot, ConfirmSaveOperation, CancelSaveWindow, StartTeaching, SkipTeaching, CompleteTeaching, RetryTeaching, RequestTeachingStandard, DeferTeachingCompletion, ContinueTeachingPractice, ReopenTeachingChoice, ResumeTeachingView, ConfirmTeachingReplacement, CancelTutorialWindow, CancelWindow }
    public sealed class JinxCasinoUiAction : JinxCasinoAction
    {
        internal JinxCasinoController Source; internal JinxCasinoUiCommand Command; internal int Slot;
        /// <summary>请求当前场景的菜单或玩法操作，由 Handler 同步处理。</summary>
        /// <param name="source">拥有本次请求的场景。</param>
        /// <param name="command">具体操作。</param>
        /// <param name="slot">存档槽；其它命令保持默认零。</param>
        public JinxCasinoUiAction(JinxCasinoController source, JinxCasinoUiCommand command, int slot = 0) { Source = source; Command = command; Slot = slot; }
    }
    public enum JinxCasinoSettingsOperation { Begin, Preview, Save, Cancel }
    public sealed class JinxCasinoSettingsAction : JinxCasinoAction
    {
        internal JinxCasinoController Source; internal JinxCasinoSettingsOperation Operation;
        internal Hotfix.JinxCasino.Persistence.CasinoLocalPreferences Candidate;
        public bool Success { get; internal set; }
        public string Error { get; internal set; }
        /// <summary>开始设置草稿、预览、保存或撤销，通过结果反馈实际写盘状态。</summary>
        /// <param name="source">当前场景。</param>
        /// <param name="operation">设置操作。</param>
        /// <param name="candidate">预览时提供独立偏好副本；其它操作可为空。</param>
        public JinxCasinoSettingsAction(JinxCasinoController source, JinxCasinoSettingsOperation operation, Hotfix.JinxCasino.Persistence.CasinoLocalPreferences candidate = null)
        { Source = source; Operation = operation; Candidate = candidate; }
    }
    public sealed class JinxCasinoTableActionRequest : JinxCasinoAction
    {
        internal JinxCasinoController Source;
        internal Hotfix.JinxCasino.Interaction.JinxCasinoTableSession Table;
        internal Hotfix.JinxCasino.Interaction.JinxCasinoTableAction Action;
        internal int Value, Frame;
        public Hotfix.JinxCasino.Interaction.JinxCasinoTableResult Result { get; internal set; }
        internal JinxCasinoTableActionRequest(JinxCasinoController source, Hotfix.JinxCasino.Interaction.JinxCasinoTableSession table, Hotfix.JinxCasino.Interaction.JinxCasinoTableAction action, int value, int frame)
        { Source = source; Table = table; Action = action; Value = value; Frame = frame; }
    }
}
