using UnityEditor;

namespace Core.Editor.Streamline
{
    internal sealed class StreamlinePluginImporter : AssetPostprocessor
    {
        private const string PluginPath = "Assets/Plugins/Streamline/x86_64/GfxPluginSleepyStreamline.dll";

        [InitializeOnLoadMethod]
        private static void EnsureExistingImportSettings()
        {
            // 首次部署时 DLL 可能早于本导入器编译完成，需要补齐平台约束。
            EditorApplication.delayCall += () =>
            {
                var importer = AssetImporter.GetAtPath(PluginPath) as PluginImporter;
                if (importer == null) return;
                if (importer.GetCompatibleWithAnyPlatform() || !importer.GetCompatibleWithEditor()
                    || importer.GetEditorData("OS") != "Windows" || importer.GetEditorData("CPU") != "x86_64"
                    || !importer.GetCompatibleWithPlatform(BuildTarget.StandaloneWindows64) || !importer.isPreloaded)
                {
                    Configure(importer);
                    importer.SaveAndReimport();
                }
            };
        }

        private void OnPreprocessAsset()
        {
            if (assetPath != PluginPath) return;
            var importer = assetImporter as PluginImporter;
            if (importer == null) return;
            Configure(importer);
        }

        private static void Configure(PluginImporter importer)
        {
            importer.SetCompatibleWithAnyPlatform(false);
            importer.SetCompatibleWithEditor(true);
            importer.SetEditorData("OS", "Windows");
            importer.SetEditorData("CPU", "x86_64");
            importer.SetCompatibleWithPlatform(BuildTarget.StandaloneWindows64, true);
            importer.SetPlatformData(BuildTarget.StandaloneWindows64, "CPU", "x86_64");
            importer.isPreloaded = true;
        }
    }
}
