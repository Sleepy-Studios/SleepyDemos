using UnityEngine;

namespace Core.Runtime
{
    [CreateAssetMenu(fileName = "HotfixConfig", menuName = "Sleepy/Hotfix Config")]
    public sealed class HotfixConfig : ScriptableObject
    {
        [Header("Resources")]
        public ResourcePlayMode PlayMode = ResourcePlayMode.EditorSimulateMode;
        public string PackageName = ResourceInitializeOptions.DefaultPackageName;
        public string BaseServerURL = string.Empty;
        public string RemoteBaseServerURL = string.Empty;
        public string LocalBundlePath = "Bundles/StandaloneWindows64/DefaultPackage";

        [Header("HybridCLR")]
        public string AotSourcePath = "HybridCLRData/AssembliesPostIl2CppStrip/StandaloneWindows64";
        public string AotStrippedSourcePath = "HybridCLRData/StrippedAOTAssembly2/StandaloneWindows64";
        public string AotTargetPath = "Assets/LoadResources/Codes/Aot";
        public string[] AotAssemblies = new string[0];
        public string HotfixSourcePath = "HybridCLRData/HotUpdateDlls/StandaloneWindows64";
        public string HotfixTargetPath = "Assets/LoadResources/Codes/Hotfix";
        public string[] HotfixAssemblies = new string[0];

        [Header("SSH")]
        public string SshHost = string.Empty;
        public int SshPort = 22;
        public string SshUser = "root";
        public string KeyFilePath = "Assets/Settings/Hotfix/key";
        public string ServerBasePath = string.Empty;

        [Header("Mock Remote Server")]
        public bool UseLocalMockServer = false;
        public string MockServerFolderPath = string.Empty;
        public string LocalServerHost = "127.0.0.1";
        public int LocalServerPort = 18080;
    }

}
