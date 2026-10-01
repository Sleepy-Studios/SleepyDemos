using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Core.Editor.MvcBind
{
    public static class LoopScrollExampleBuilder
    {
        [MenuItem("Tools/Sleepy Loop Scroll/Build MvcBind Example")]
        public static void BuildExample()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请在 Edit Mode 构建 MvcBind 示例。");
            const string folder = "Assets/LoadResources/Demos/loop_scroll/Scenes";
            Directory.CreateDirectory(folder);
            var catalogPath = AssetDatabase.FindAssets("Catalog t:ScriptableObject", new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(path => path.EndsWith("/SleepyLoopScrollSamples/Catalog.asset"));
            var catalog = catalogPath == null ? null : AssetDatabase.LoadAssetAtPath<ScriptableObject>(catalogPath);
            if (catalog == null) throw new InvalidOperationException("请先构建完整 Loop Scroll Showcase。");
            var font = AssetDatabase.LoadAssetAtPath<Font>(Path.GetDirectoryName(catalogPath).Replace('\\', '/') + "/NotoSansSC-Regular.otf");
            var driver = AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType("Hotfix.Demos.LoopScroll.LoopScrollMvcExample")).FirstOrDefault(type => type != null);
            if (driver == null) throw new InvalidOperationException("Hotfix LoopScroll 示例尚未编译。");
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var open = SceneManager.GetSceneAt(i);
                if (open.isDirty || string.IsNullOrEmpty(open.path))
                    throw new InvalidOperationException("请先保存当前场景或打开一个已保存场景，再重建示例。");
            }
            var serialized = new SerializedObject(catalog); var entries = serialized.FindProperty("entries"); var slot = entries.arraySize;
            for (var i = 0; i < entries.arraySize; i++) if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("id").stringValue == "mvc") { slot = i; break; }
            if (slot == entries.arraySize) entries.arraySize++;
            var entry = entries.GetArrayElementAtIndex(slot); entry.FindPropertyRelative("id").stringValue = "mvc";
            entry.FindPropertyRelative("titleKey").stringValue = "mvc"; entry.FindPropertyRelative("descriptionKey").stringValue = "mvcDesc";
            entry.FindPropertyRelative("scenePath").stringValue = folder + "/LoopScrollMvcExample.unity";
            serialized.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets();
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            try
            {
                var root = new GameObject("LoopScrollMvcExample", typeof(RectTransform)); SceneManager.MoveGameObjectToScene(root, scene);
                var page = root.AddComponent(driver); driver.GetMethod("ConfigureAssets").Invoke(page, new object[] { catalog, font });
                var camera = new GameObject("Camera", typeof(Camera)); SceneManager.MoveGameObjectToScene(camera, scene);
                camera.GetComponent<Camera>().clearFlags = CameraClearFlags.SolidColor; camera.GetComponent<Camera>().backgroundColor = Color.black;
                if (!EditorSceneManager.SaveScene(scene, folder + "/LoopScrollMvcExample.unity")) throw new IOException("MvcBind 示例场景保存失败。");
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }

        }
    }
}
