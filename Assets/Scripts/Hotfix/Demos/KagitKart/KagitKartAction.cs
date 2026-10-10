using Core.Runtime;

namespace Hotfix.KagitKart
{
    public enum KagitKartCommand
    {
        Pause,
        Resume,
        Restart,
        NextRound,
        Hub,
        Refresh,
        Modes,
        Classes,
        Characters,
        Tracks,
        Title,
        PreviewCharacter,
        PreviewTrack,
        ChooseMode,
        ChooseSpeedClass,
        ChooseCharacter,
        ChooseTrack,
        SkipIntro
    }

    /// 页面与输入提交的本场流程请求。
    public sealed class KagitKartAction : IAction
    {
        public KagitKartWorld Owner;
        public KagitKartCommand Command;
        public int Number;
        public string Id;

        /// <summary>构造后通过 GlobalData.Dispatch 提交，不在构造时执行操作。</summary>
        /// <param name="owner">当前场景宿主，用于拒绝上一场页面的请求。</param>
        /// <param name="command">比赛流程或菜单选择操作。</param>
        /// <param name="number">选择模式时为模式枚举值，选择速度时为 80、120 或 200；其他操作忽略。</param>
        /// <param name="id">选择角色或赛道时使用原作稳定标识；其他操作忽略。</param>
        public KagitKartAction(KagitKartWorld owner, KagitKartCommand command, int number = 0, string id = null)
        {
            Owner = owner;
            Command = command;
            Number = number;
            Id = id;
        }
    }
}
