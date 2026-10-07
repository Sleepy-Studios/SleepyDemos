namespace Hotfix.SceneManagement
{
    /// 全局业务场景标识；启动壳使用 Hub，具体 Demo 在目录中映射资源地址。
    public enum GameSceneId
    {
        Hub = 0,
        DroneFlight = 1,
        Dlss = 2,
        BlockPorters = 3,
        JinxCasino = 4,
        HowToFish = 5,
        WallSqueeze = 6
    }

    /// 场景切换结果状态。
    public enum GameSceneSwitchStatus
    {
        Succeeded = 1,
        Ignored = 2,
        Busy = 3,
        Failed = 4
    }

    /// 场景切换结果。
    public readonly struct GameSceneSwitchResult
    {
        private GameSceneSwitchResult(GameSceneSwitchStatus status, GameSceneId sceneId, string error)
        {
            Status = status;
            SceneId = sceneId;
            Error = error;
        }

        /// 切换结果状态。
        public GameSceneSwitchStatus Status { get; }

        /// 请求的目标场景。
        public GameSceneId SceneId { get; }

        /// 失败时的诊断信息。
        public string Error { get; }

        internal static GameSceneSwitchResult Succeeded(GameSceneId sceneId) =>
            new GameSceneSwitchResult(GameSceneSwitchStatus.Succeeded, sceneId, null);

        internal static GameSceneSwitchResult Ignored(GameSceneId sceneId) =>
            new GameSceneSwitchResult(GameSceneSwitchStatus.Ignored, sceneId, null);

        internal static GameSceneSwitchResult Busy(GameSceneId sceneId) =>
            new GameSceneSwitchResult(GameSceneSwitchStatus.Busy, sceneId, null);

        internal static GameSceneSwitchResult Failed(GameSceneId sceneId, string error) =>
            new GameSceneSwitchResult(GameSceneSwitchStatus.Failed, sceneId, error);
    }

    internal readonly struct GameSceneDefinition
    {
        internal GameSceneDefinition(GameSceneId id, string displayName, string address)
        {
            Id = id;
            DisplayName = displayName;
            Address = address;
        }

        internal GameSceneId Id { get; }
        internal string DisplayName { get; }
        internal string Address { get; }
        internal bool IsHub => Id == GameSceneId.Hub;
    }

    internal static class GameSceneCatalog
    {
        internal const string DroneFlightAddress =
            "Assets/LoadResources/Demos/drone_flight/Scenes/Main.unity";
        internal const string DlssAddress = "Assets/LoadResources/Demos/dlss/Scenes/Main.unity";
        internal const string BlockPortersAddress = "Assets/LoadResources/Demos/block_porters/Scenes/Main.unity";
        internal const string JinxCasinoAddress = "Assets/LoadResources/Demos/jinx_casino/Scenes/Immersion.unity";

        internal const string HowToFishAddress = "Assets/LoadResources/Demos/how_to_fish/Scenes/Main.unity";
        internal const string WallSqueezeAddress = "Assets/LoadResources/Demos/wall_squeeze/Scenes/Main.unity";

        internal static bool TryFindSceneId(string scenePath, out GameSceneId sceneId)
        {
            foreach (GameSceneId candidate in System.Enum.GetValues(typeof(GameSceneId)))
            {
                if (candidate != GameSceneId.Hub && TryGet(candidate, out var definition)
                    && string.Equals(definition.Address, scenePath, System.StringComparison.Ordinal))
                {
                    sceneId = candidate;
                    return true;
                }
            }
            sceneId = GameSceneId.Hub;
            return false;
        }

        internal static bool TryGet(GameSceneId sceneId, out GameSceneDefinition definition)
        {
            switch (sceneId)
            {
                case GameSceneId.Hub:
                    definition = new GameSceneDefinition(GameSceneId.Hub, "主界面", null);
                    return true;
                case GameSceneId.DroneFlight:
                    definition = new GameSceneDefinition(
                        GameSceneId.DroneFlight,
                        "无人机飞行",
                        DroneFlightAddress);
                    return true;
                case GameSceneId.Dlss:
                    definition = new GameSceneDefinition(GameSceneId.Dlss, "DLSS 实验室", DlssAddress);
                    return true;
                case GameSceneId.BlockPorters:
                    definition = new GameSceneDefinition(GameSceneId.BlockPorters, "小小搬豆工", BlockPortersAddress);
                    return true;
                case GameSceneId.JinxCasino:
                    definition = new GameSceneDefinition(GameSceneId.JinxCasino, "倒霉蛋俱乐部", JinxCasinoAddress);
                    return true;
                case GameSceneId.HowToFish:
                    definition = new GameSceneDefinition(GameSceneId.HowToFish, "渔力全开 · 单人", HowToFishAddress);
                    return true;
                case GameSceneId.WallSqueeze:
                    definition = new GameSceneDefinition(GameSceneId.WallSqueeze, "夹爆它", WallSqueezeAddress);
                    return true;
                default:
                    definition = default;
                    return false;
            }
        }
    }
}
