using UnityEngine;

namespace Core.Runtime
{
    [CreateAssetMenu(fileName = "HotUpdateConfig", menuName = "Sleepy/Hot Update Config")]
    public sealed class HotUpdateConfig : ScriptableObject
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
        public string HotUpdateSourcePath = "HybridCLRData/HotUpdateDlls/StandaloneWindows64";
        public string HotUpdateTargetPath = "Assets/LoadResources/Codes/HotUpdate";
        public string[] HotUpdateAssemblies = new string[0];

        [Header("SSH")]
        public string SshHost = string.Empty;
        public int SshPort = 22;
        public string SshUser = "root";
        public string KeyFilePath = "Assets/Settings/HotUpdate/key";
        public string ServerBasePath = string.Empty;

        [Header("Mock Remote Server")]
        public bool UseLocalMockServer = false;
        public string MockServerFolderPath = string.Empty;
        public string LocalServerHost = "127.0.0.1";
        public int LocalServerPort = 18080;
    }

}
