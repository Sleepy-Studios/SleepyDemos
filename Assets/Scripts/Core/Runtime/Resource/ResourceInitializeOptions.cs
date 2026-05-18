namespace Core.Runtime
{
    public readonly struct ResourceInitializeOptions
    {
        public const string DefaultPackageName = "DefaultPackage";

        public ResourceInitializeOptions(string packageName, ResourcePlayMode playMode, string hostServerURL)
        {
            PackageName = string.IsNullOrWhiteSpace(packageName) ? DefaultPackageName : packageName;
            PlayMode = playMode;
            HostServerURL = hostServerURL ?? string.Empty;
        }

        public string PackageName { get; }
        public ResourcePlayMode PlayMode { get; }
        public string HostServerURL { get; }

        public static ResourceInitializeOptions Default =>
            new ResourceInitializeOptions(DefaultPackageName, ResourcePlayMode.EditorSimulateMode, string.Empty);
    }

    public enum ResourcePlayMode
    {
        EditorSimulateMode,
        OfflinePlayMode,
        HostPlayMode
    }
}
