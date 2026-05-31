using UnityEditor;

namespace Core.Editor.AssetNaming
{
    public sealed class LoadResourcesAssetNamingPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            foreach (var assetPath in importedAssets)
            {
                ValidateImported(assetPath);
            }

            foreach (var assetPath in movedAssets)
            {
                ValidateImported(assetPath);
            }
        }

        private static void ValidateImported(string assetPath)
        {
            if (LoadResourcesAssetNamingRules.ShouldSkipAssetPath(assetPath))
            {
                return;
            }

            if (!LoadResourcesAssetNamingRules.TryValidate(assetPath, out var error))
            {
                UnityEngine.Debug.LogError($"[AssetNaming] {assetPath}: {error}");
            }
        }
    }
}
