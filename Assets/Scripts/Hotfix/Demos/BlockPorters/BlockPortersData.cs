using System.Collections.Generic;
using Core.Runtime;

namespace Hotfix.BlockPorters
{
    /// 本场玩法的唯一业务状态，场景和页面共享此实例。
    public sealed class BlockPortersData : IData
    {
        /// 本场注册的命令处理器，列表身份在整个会话中保持不变。
        public List<IHandler> Handlers
        {
            get;
        }

        internal BlockPortersHandler Handler
        {
            get;
        }

        internal BlockPortersScheduler Scheduler
        {
            get; set;
        }

        internal int Version
        {
            get; set;
        }

        internal bool Ready
        {
            get; set;
        }

        internal bool SettingsRequested
        {
            get; set;
        }

        internal bool WasPaused
        {
            get; set;
        }

        internal BlockPortersSession SettingsSession
        {
            get; set;
        }

        internal BlockPortersLevel[] Levels
        {
            get; set;
        }

        /// 当前关卡的规则真源；清理后为 null。
        public BlockPortersSession Session
        {
            get; internal set;
        }

        /// 当前场景配置的 UI 样式。
        public BlockPortersUiStyle Style
        {
            get;
        }

        /// 当前关卡索引，从零开始。
        public int LevelIndex
        {
            get; internal set;
        }

        /// 当前目录包含的关卡数量。
        public int LevelCount => Levels?.Length ?? 0;

        /// 当前关卡配置，仅在关卡已加载时读取。
        public BlockPortersLevel CurrentLevel => Levels[LevelIndex];

        /// 玩法是否暂停，不包含应用后台暂停。
        public bool IsPaused
        {
            get; internal set;
        }

        /// 当前场景音效是否静音。
        public bool IsMuted
        {
            get; internal set;
        }

        /// 是否正在等待奖励服务，等待期间禁止再次请求。
        public bool IsRewardPending
        {
            get; internal set;
        }

        /// 是否正在关闭会话并返回大厅。
        public bool IsExiting
        {
            get; internal set;
        }

        internal BlockPortersData(BlockPortersController scene, BlockPortersUiStyle style)
        {
            Style = style;
            Handler = new BlockPortersHandler(scene);
            Handlers = new() { Handler };
        }

        /// 使旧关卡结果失效，取消奖励并重置本场状态。
        public void ClearData()
        {
            // 先失效版本，再取消任务，取消回调不能重新写入本关。
            Version++;
            Handler.CancelReward();
            Session = null;
            Scheduler = null;
            SettingsSession = null;
            Levels = null;
            LevelIndex = 0;
            WasPaused = false;
            Ready = SettingsRequested = IsPaused = IsMuted = IsRewardPending = IsExiting = false;
        }
    }
}
