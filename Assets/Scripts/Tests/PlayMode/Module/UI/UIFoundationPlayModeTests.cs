#if UNITY_EDITOR
using System.Collections;
using System.IO;
using Core.Runtime;
using Core.Runtime.Inputs;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Tests.Module
{
    public sealed class UIFoundationPlayModeTests
    {
        [UnityTest, Timeout(120000)]
        public IEnumerator CommonButton_RealPointerKeyboardGamepadAndDisableRestoreDistinctVisuals()
        {
            var originalSettings = InputSystem.settings;
            var settings = Object.Instantiate(originalSettings);
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = settings;
            var originalEditorSelection = UnityEditor.Selection.activeObject;
            var original = EventSystem.current;
            bool previousEnabled = original != null && original.enabled;
            if (original != null) original.enabled = false;
            var backgroundCamera = new GameObject("FoundationBackground", typeof(Camera));
            backgroundCamera.GetComponent<Camera>().clearFlags = CameraClearFlags.SolidColor;
            backgroundCamera.GetComponent<Camera>().backgroundColor = new Color(.04f, .05f, .07f);
            backgroundCamera.GetComponent<Camera>().cullingMask = 0;
            var canvas = new GameObject("FoundationCanvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.GetComponent<Canvas>().sortingOrder = 30000;
            var events = new GameObject("FoundationEvents", typeof(EventSystem), typeof(InputSystemUIInputModule));
            var system = events.GetComponent<EventSystem>(); EventSystem.current = system;
            events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/LoadResources/UI/Common/_TemplateInstantiatePrefab/Btns/CommonButton.prefab");
            var obj = Object.Instantiate(source, canvas.transform);
            var button = obj.GetComponent<Button>();
            var interaction = obj.GetComponent<UIStateInteraction>();
            var mouse = InputSystem.AddDevice<Mouse>();
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var gamepad = InputSystem.AddDevice<Gamepad>();
            int clicks = 0; button.onClick.AddListener(() => clicks++);
            var background = button.targetGraphic;
            var normal = background.color;
            Vector2 inside = RectTransformUtility.WorldToScreenPoint(null, obj.transform.position);
            Vector2 outside = inside + new Vector2(400, 180);
            try
            {
                InputDeviceState.Notify(mouse);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = outside });
                yield return null; yield return null;
                Assert.That(interaction.InteractionState, Is.EqualTo("Normal"));
                yield return Capture("Normal");
                InputSystem.QueueStateEvent(mouse, new MouseState { position = inside });
                yield return null; yield return null;
                Assert.That(interaction.InteractionState, Is.EqualTo("Hover"));
                Assert.That(background.color, Is.Not.EqualTo(normal));
                yield return Capture("Hover");
                InputSystem.QueueStateEvent(mouse, new MouseState { position = inside }.WithButton(MouseButton.Left));
                yield return null; yield return null;
                Assert.That(interaction.InteractionState, Is.EqualTo("Pressed"));
                Assert.That(obj.transform.localScale.x, Is.EqualTo(.96f).Within(.001f));
                yield return Capture("Pressed");
                InputSystem.QueueStateEvent(mouse, new MouseState { position = inside });
                yield return null; yield return null;
                Assert.That(clicks, Is.EqualTo(1));
                Assert.That(obj.transform.localScale, Is.EqualTo(Vector3.one));
                InputSystem.QueueStateEvent(mouse, new MouseState { position = outside });
                system.SetSelectedGameObject(null);
                yield return null; yield return null;
                Assert.That(interaction.InteractionState, Is.EqualTo("Normal"));
                Assert.That(background.color, Is.EqualTo(normal));
                system.SetSelectedGameObject(obj); InputDeviceState.Notify(keyboard);
                yield return null; yield return null;
                Assert.That(interaction.InteractionState, Is.EqualTo("Focused"));
                Assert.That(obj.transform.Find("InteractionFeedback").GetComponent<CanvasGroup>().alpha, Is.EqualTo(1));
                yield return Capture("Focused");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter));
                yield return null; yield return null;
                Assert.That(clicks, Is.EqualTo(2));
                Assert.That(interaction.InteractionState, Is.EqualTo("Pressed"));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return new WaitForSecondsRealtime(.2f);
                InputDeviceState.Notify(gamepad);
                InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.South));
                yield return null; yield return null;
                Assert.That(clicks, Is.EqualTo(3));
                InputSystem.QueueStateEvent(gamepad, new GamepadState());
                button.interactable = false;
                yield return null; yield return null;
                Assert.That(interaction.InteractionState, Is.EqualTo("Disabled"));
                Assert.That(obj.transform.Find("InteractionFeedback").GetComponent<CanvasGroup>().alpha, Is.Zero);
                Assert.That(obj.transform.Find("InteractionFeedback").gameObject.activeSelf, Is.False);
                system.SetSelectedGameObject(null);
                yield return Capture("Disabled");
                InputDeviceState.NotifyTouch();
                yield return null; yield return null;
                Assert.That(interaction.InteractionState, Is.EqualTo("Disabled"));
                Assert.That(background.color, Is.Not.EqualTo(normal));
                InputDeviceState.Notify(gamepad);
                InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.South));
                yield return null; yield return null;
                Assert.That(clicks, Is.EqualTo(3));
                button.interactable = true; InputSystem.QueueStateEvent(gamepad, new GamepadState());
                for (int i = 0; i < 3; i++) { obj.SetActive(false); yield return null; obj.SetActive(true); yield return null; }
                InputDeviceState.Notify(mouse); system.SetSelectedGameObject(null);
                yield return null; yield return null;
                Assert.That(obj.transform.localScale, Is.EqualTo(Vector3.one));
                Assert.That(background.color, Is.EqualTo(normal));
                Directory.CreateDirectory("Library/UIRefactor/Evidence");
                ScreenCapture.CaptureScreenshot("Library/UIRefactor/Evidence/CommonButton.png");
                yield return null; yield return null;
            }
            finally
            {
                InputSystem.RemoveDevice(mouse); InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(gamepad);
                UnityEditor.Selection.activeObject = originalEditorSelection;
                InputSystem.settings = originalSettings; Object.Destroy(settings);
                Object.Destroy(backgroundCamera); Object.Destroy(canvas); Object.Destroy(events);
                if (original != null) { original.enabled = previousEnabled; EventSystem.current = original; }
            }
        }

        private static IEnumerator Capture(string state)
        {
            UnityEditor.Selection.activeObject = null;
            yield return new WaitForEndOfFrame();
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                Assert.That(texture, Is.Not.Null);
                var pixels = texture.GetPixels32();
                Assert.That(System.Array.Exists(pixels, pixel => pixel.r > 80 && pixel.g > 80 && pixel.b > 80), Is.True, "截图没有实际 UI。");
                Directory.CreateDirectory("Library/UIRefactor/Evidence");
                File.WriteAllBytes("Library/UIRefactor/Evidence/CommonButton-" + state + ".png", texture.EncodeToPNG());
            }
            finally { Object.Destroy(texture); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator ProgressBar_ActualFilledMeshTracksValueAndKeepsColor()
        {
            var canvas = new GameObject("ProgressCanvas", typeof(RectTransform), typeof(Canvas));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var obj = new GameObject("Progress", typeof(RectTransform), typeof(Image), typeof(UIProgressBar));
            obj.transform.SetParent(canvas.transform, false);
            ((RectTransform)obj.transform).sizeDelta = new Vector2(300, 24);
            var fill = obj.GetComponent<Image>();
            fill.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/LoadResources/UI/Common/Sprites/White.png");
            fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal;
            var bar = obj.GetComponent<UIProgressBar>();
            try
            {
                bar.SetColor(Color.green);
                foreach (float value in new[] { 0f, .01f, .5f, 1f })
                {
                    bar.SetValue(value); Canvas.ForceUpdateCanvases(); yield return null;
                    var mesh = fill.canvasRenderer.GetMesh();
                    if (value == 0) Assert.That(mesh == null || mesh.vertexCount == 0, Is.True);
                    else Assert.That(mesh.bounds.size.x, Is.EqualTo(300 * value).Within(.05f));
                    Assert.That(fill.color, Is.EqualTo(Color.green));
                }
                bar.SetValue(float.NaN); Assert.That(bar.Value, Is.Zero);
                bar.SetValue(-1); Assert.That(bar.Value, Is.Zero);
                bar.SetValue(2); Assert.That(bar.Value, Is.EqualTo(1));
            }
            finally { Object.Destroy(canvas); }
        }
    }
}
#endif
