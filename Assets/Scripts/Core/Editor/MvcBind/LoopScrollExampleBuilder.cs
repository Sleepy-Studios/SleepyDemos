using System;
using System.Linq;
using SleepyStudios.LoopScroll;
using SleepyStudios.LoopScroll.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Core.Editor.MvcBind
{
    public static class LoopScrollExampleBuilder
    {
        [MenuItem("Tools/Sleepy Loop Scroll/Build MvcBind Example")]
        public static void BuildExample()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请在 Edit Mode 构建 MvcBind 示例。");
            const string folder = "Assets/LoadResources/Demos/loop_scroll/Scenes";
            System.IO.Directory.CreateDirectory(folder);
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                var canvas = new GameObject("LoopScrollCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                SceneManager.MoveGameObjectToScene(canvas, scene); canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(960, 720); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
                var list = LoopScrollViewEditor.CreateHierarchy(canvas.transform);
                list.GetComponent<RectTransform>().sizeDelta = new Vector2(800, 500); list.ScrollRect.viewport.sizeDelta = Vector2.zero;
                var template = list.transform.Find("CellTemplate");
                var labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text)); labelObject.transform.SetParent(template, false);
                var labelRect = labelObject.GetComponent<RectTransform>(); labelRect.anchorMin = Vector2.zero; labelRect.anchorMax = Vector2.one; labelRect.offsetMin = new Vector2(12, 2); labelRect.offsetMax = new Vector2(-12, -2);
                var label = labelObject.GetComponent<Text>(); label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); label.fontSize = 20; label.alignment = TextAnchor.MiddleLeft; label.color = Color.white; label.raycastTarget = false;
                var driver = AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType("Hotfix.Demos.LoopScroll.LoopScrollMvcExample")).FirstOrDefault(type => type != null);
                if (driver == null) throw new InvalidOperationException("Hotfix LoopScroll 示例尚未编译。");
                list.gameObject.AddComponent(driver);
                var events = new GameObject("EventSystem", typeof(EventSystem)); SceneManager.MoveGameObjectToScene(events, scene);
                var input = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
                if (input != null) events.AddComponent(input); else events.AddComponent<StandaloneInputModule>();
                var camera = new GameObject("Camera", typeof(Camera)); SceneManager.MoveGameObjectToScene(camera, scene); camera.GetComponent<Camera>().clearFlags = CameraClearFlags.SolidColor;
                if (!EditorSceneManager.SaveScene(scene, folder + "/LoopScrollMvcExample.unity"))
                    throw new System.IO.IOException("MvcBind 示例场景保存失败。");
            }
            finally { EditorSceneManager.CloseScene(scene, true); if (previous.IsValid()) SceneManager.SetActiveScene(previous); }
        }
    }
}
