using System.Collections.Generic;
using Core.Runtime;

namespace Hotfix.BlockPorters
{
    /// 本场玩法的唯一业务状态，场景和页面共享此实例。
    public sealed class BlockPortersData : IData
    {
        public List<IHandler> Handlers { get; }
        internal BlockPortersHandler Handler { get; }
        internal BlockPortersScheduler Scheduler { get; set; }
        internal int Version { get; set; }
        internal bool Ready { get; set; }
        internal bool SettingsRequested { get; set; }
        internal bool WasPaused { get; set; }
        internal BlockPortersSession SettingsSession { get; set; }
        internal BlockPortersLevel[] Levels { get; set; }
        public BlockPortersSession Session { get; internal set; }
        public BlockPortersUiStyle Style { get; }
        public int LevelIndex { get; internal set; }
        public int LevelCount => Levels?.Length ?? 0;
        public BlockPortersLevel CurrentLevel => Levels[LevelIndex];
        public bool IsPaused { get; internal set; }
        public bool IsMuted { get; internal set; }
        public bool IsRewardPending { get; internal set; }
        public bool IsExiting { get; internal set; }
        internal BlockPortersData(BlockPortersController scene, BlockPortersUiStyle style)
        {
            Style = style;
            Handler = new BlockPortersHandler(scene);
            Handlers = new() { Handler };
        }
        public void ClearData()
        {
            Version++; Handler.CancelReward(); Session = null; Scheduler = null;
            SettingsSession = null; Levels = null;
            Ready = SettingsRequested = IsPaused = IsMuted = IsRewardPending = IsExiting = false;
        }
    }
}
