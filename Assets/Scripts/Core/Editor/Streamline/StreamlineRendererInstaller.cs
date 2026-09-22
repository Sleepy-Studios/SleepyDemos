using Core.Runtime.Rendering.Streamline;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Core.Editor.Streamline
{
    /// 为项目已有 Renderer 补齐功能，不替换 Renderer 或后处理配置。
    public static class StreamlineRendererInstaller
    {
        [MenuItem("Tools/Rendering/Streamline/配置Windows图形后端")]
        public static void ConfigureWindowsGraphics()
        {
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,
                new[] { GraphicsDeviceType.Direct3D12, GraphicsDeviceType.Vulkan });
            AssetDatabase.SaveAssets();
            Debug.Log("[Streamline] Windows 默认使用 D3D12，保留 Vulkan。当前 Editor 后端在重启后生效。");
        }

        [MenuItem("Tools/Rendering/Streamline/配置项目Renderer")]
        public static void Install()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:UniversalRendererData", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
                if (!renderer.rendererFeatures.Exists(feature => feature is StreamlineCaptureFeature))
                {
                    var feature = ScriptableObject.CreateInstance<StreamlineCaptureFeature>();
                    feature.name = "Streamline";
                    AssetDatabase.AddObjectToAsset(feature, renderer);
                    renderer.rendererFeatures.Add(feature);
                }
                // 与 URP Inspector 一样同步持久化本地 ID，保证重新导入后 Feature 引用可恢复。
                var serialized = new SerializedObject(renderer);
                var map = serialized.FindProperty("m_RendererFeatureMap");
                map.arraySize = renderer.rendererFeatures.Count;
                for (int index = 0; index < renderer.rendererFeatures.Count; index++)
                {
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(renderer.rendererFeatures[index], out string _, out long localId);
                    map.GetArrayElementAtIndex(index).longValue = localId;
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
                renderer.SetDirty();
                EditorUtility.SetDirty(renderer);
            }
            AssetDatabase.SaveAssets();
        }
    }
}
