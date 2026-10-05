using Core.Runtime;

namespace Hotfix.BlockPorters
{
    /// 搬豆工业务命令的共同入口。
    public abstract class BlockPortersAction : IAction
    {
    }

    /// 加载指定关卡，并决定是否重新选择主题。
    public sealed class BlockPortersLoadLevelAction : BlockPortersAction
    {
        /// 从零开始的关卡索引。
        public int Index;

        /// 是否重新选择主题；重开当前关卡时为 false。
        public bool ChooseTheme;

        /// <summary>
        /// 请求加载关卡；非法索引由 Handler 拒绝。
        /// </summary>
        /// <param name="index">从零开始的关卡索引，必须小于关卡数量。</param>
        /// <param name="chooseTheme">是否重新选择主题；默认为 true。</param>
        public BlockPortersLoadLevelAction(int index, bool chooseTheme = true)
        {
            Index = index;
            ChooseTheme = chooseTheme;
        }
    }

    /// 向指定列派出搬运队伍。
    public sealed class BlockPortersDispatchAction : BlockPortersAction
    {
        /// 从零开始的搬运列索引。
        public int Column;

        /// <summary>
        /// 请求派队；不可派队时不改变关卡状态。
        /// </summary>
        /// <param name="column">从零开始的列索引。</param>
        public BlockPortersDispatchAction(int column)
        {
            Column = column;
        }
    }

    /// 切换玩法暂停状态。
    public sealed class BlockPortersTogglePauseAction : BlockPortersAction
    {
    }

    /// 切换当前场景的静音状态。
    public sealed class BlockPortersToggleSoundAction : BlockPortersAction
    {
    }

    /// 重开当前关卡并保留主题。
    public sealed class BlockPortersRestartAction : BlockPortersAction
    {
    }

    /// 通关后进入下一关并重新选择主题。
    public sealed class BlockPortersNextLevelAction : BlockPortersAction
    {
    }

    /// 记录暂停来源并请求设置页。
    public sealed class BlockPortersOpenSettingsAction : BlockPortersAction
    {
    }

    /// 关闭设置页，按打开前状态恢复玩法。
    public sealed class BlockPortersCloseSettingsAction : BlockPortersAction
    {
    }

    /// 请求结束当前会话并返回大厅。
    public sealed class BlockPortersExitAction : BlockPortersAction
    {
    }

    /// 返回失败后恢复会话及奖励请求能力。
    internal sealed class BlockPortersRestoreAction : BlockPortersAction
    {
        internal BlockPortersData Source;

        internal BlockPortersRestoreAction(BlockPortersData source)
        {
            Source = source;
        }
    }

    /// 请求奖励服务解锁一侧额外搬运槽。
    public sealed class BlockPortersUnlockSlotAction : BlockPortersAction
    {
        /// 额外槽所在侧：0 左侧，1 右侧。
        public int Side;

        /// <summary>
        /// 请求奖励解锁；结果通过会话状态异步发布。
        /// </summary>
        /// <param name="side">0 为左侧额外槽，1 为右侧额外槽；其它值被忽略。</param>
        public BlockPortersUnlockSlotAction(int side)
        {
            Side = side;
        }
    }

    /// 接收当前关卡的异步奖励结果。
    internal sealed class BlockPortersRewardResultAction : BlockPortersAction
    {
        internal int Version;

        internal int Side;

        internal PorterRewardResult Result;

        internal BlockPortersRewardResultAction(int version, int side, PorterRewardResult result)
        {
            Version = version;
            Side = side;
            Result = result;
        }
    }

    /// 同步 HUD 准备或场景停用状态。
    internal sealed class BlockPortersReadyAction : BlockPortersAction
    {
        /// 拥有 HUD 的当前会话。
        internal BlockPortersData Source;

        /// HUD 已准备且场景启用时为 true。
        internal bool Ready;

        /// <summary>
        /// 同步 HUD 准备或场景停用状态。
        /// </summary>
        /// <param name="source">拥有 HUD 的当前会话。</param>
        /// <param name="ready">HUD 已准备且场景启用时为 true。</param>
        internal BlockPortersReadyAction(BlockPortersData source, bool ready)
        {
            Source = source;
            Ready = ready;
        }
    }
}
