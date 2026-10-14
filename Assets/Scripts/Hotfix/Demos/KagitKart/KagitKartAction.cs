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
        SkipIntro,
        Settings,
        Controls,
        ChangeSetting,
        CloseOptions = 22,
        MarkTutorialSeen,
        ShowStandings,
        PlayModes,
        SinglePlayer,
        Online,
        CreateRoom,
        JoinRoom,
        Room,
        LeaveRoom,
        Ready,
        StartOnlineRace,
        ChooseOnlineCharacter,
        CopyAddress,
        ReturnRoom
    }

    /// 页面与输入提交的本场流程请求。
    public class KagitKartAction : IAction
    {
        public KagitKartWorld Owner;
        public KagitKartCommand Command;
        public int Number;
        public string Id;
        /// UI 确认保留点击音；原作全局快捷键可明确不播放额外 click。
        public bool PlayClick;

        /// <summary>构造后通过 GlobalData.Dispatch 提交，不在构造时执行操作。</summary>
        /// <param name="owner">当前场景宿主，用于拒绝上一场页面的请求。</param>
        /// <param name="command">比赛流程或菜单选择操作。</param>
        /// <param name="number">选择模式时为模式枚举值，选择速度时为 80、120 或 200；其他操作忽略。</param>
        /// <param name="id">选择角色或赛道时使用原作稳定标识；其他操作忽略。</param>
        /// <param name="playClick">是否保留 UI 点击音；源全局 P/R/M 不追加 click。</param>
        public KagitKartAction(KagitKartWorld owner, KagitKartCommand command, int number = 0, string id = null, bool playClick = true)
        {
            Owner = owner;
            Command = command;
            Number = number;
            Id = id;
            PlayClick = playClick;
        }
    }

    /// 创建或修改房间表单的完整候选。
    public sealed class KagitKartRoomConfigurationAction : KagitKartAction
    {
        public string Name;
        public int Capacity;
        public KagitKartRaceMode Mode;
        public int SpeedClass;
        public string TrackId;
        public bool UpdateExistingRoom;

        /// <summary>提交房间候选，不在构造时创建连接。</summary>
        /// <param name="world">当前场景宿主。</param>
        /// <param name="name">非空房间名称，最多32字符。</param>
        /// <param name="capacity">2、4或8。</param>
        /// <param name="mode">单场或大奖赛。</param>
        /// <param name="speedClass">80、120或200。</param>
        /// <param name="trackId">原作赛道。</param>
        /// <param name="updateExistingRoom">是否修改已加入的大厅。</param>
        public KagitKartRoomConfigurationAction(KagitKartWorld world, string name, int capacity,
            KagitKartRaceMode mode, int speedClass, string trackId, bool updateExistingRoom)
            : base(world, KagitKartCommand.CreateRoom)
        {
            Name = name;
            Capacity = capacity;
            Mode = mode;
            SpeedClass = speedClass;
            TrackId = trackId;
            UpdateExistingRoom = updateExistingRoom;
        }
    }

    /// 连接玩家房主的地址表单。
    public sealed class KagitKartJoinRoomAction : KagitKartAction
    {
        public string Address;
        public ushort Port;
        public string DisplayName;

        /// <summary>提交加入请求，身份由网络层分配。</summary>
        /// <param name="world">当前场景宿主。</param>
        /// <param name="address">房主IP或域名。</param>
        /// <param name="port">有效端口，默认表单为7777。</param>
        /// <param name="displayName">仅用于显示。</param>
        public KagitKartJoinRoomAction(KagitKartWorld world, string address, ushort port, string displayName)
            : base(world, KagitKartCommand.JoinRoom)
        {
            Address = address;
            Port = port;
            DisplayName = displayName;
        }
    }
}
