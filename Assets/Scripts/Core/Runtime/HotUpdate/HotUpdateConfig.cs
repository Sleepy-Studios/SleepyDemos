using UnityEngine;

namespace Core.Runtime
{
    [CreateAssetMenu(fileName = "HotUpdateConfig", menuName = "Sleepy/Hot Update Config")]
    public sealed class HotUpdateConfig : ScriptableObject
    {
        [Header("YooAssets")]
        public YooAssetPlayMode PlayMode = YooAssetPlayMode.EditorSimulateMode;
        public string PackageName = YooAssetResourceSystem.DefaultPackageName;
        public string BaseServerURL = string.Empty;
        public string LocalBundlePath = "Bundles/StandaloneWindows64/DefaultPackage";

        [Header("HybridCLR")]
        public string AotSourcePath = "HybridCLRData/AssembliesPostIl2CppStrip/StandaloneWindows64";
        public string AotStrippedSourcePath = "HybridCLRData/StrippedAOTAssembly2/StandaloneWindows64";
        public string AotTargetPath = "Assets/LoadResources/Codes/Aot";
        public string[] AotAssemblies = new string[0];
        public string HotUpdateSourcePath = "HybridCLRData/HotUpdateDlls/StandaloneWindows64";
        public string HotUpdateTargetPath = "Assets/LoadResources/Codes/HotUpdate";
        public string[] HotUpdateAssemblies = new string[0];
    }

    public enum YooAssetPlayMode
    {
        EditorSimulateMode,
        OfflinePlayMode,
        HostPlayMode
    }
}
