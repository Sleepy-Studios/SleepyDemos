using System.Collections;
using System.Linq;
using Core.Runtime;
using Core.Runtime.Inputs;
using NUnit.Framework;
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
    public sealed class UnifiedInputPlayModeTests
    {
        [UnityTest]
        public IEnumerator ConnectedPadDoesNotChangeActualDeviceAndUsageBindingResolvesSwitchA()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var pad = (Gamepad)InputSystem.AddDevice("SwitchProControllerHID");
            var action = new InputAction("Confirm", InputActionType.Button, "<Gamepad>/{Submit}");
            var cancel = new InputAction("Back", InputActionType.Button, "<Gamepad>/{Cancel}");
            try
            {
                InputDeviceState.Initialize(); InputDeviceState.Notify(keyboard);
                Assert.That(InputDeviceState.ActiveKind, Is.EqualTo(InputDeviceKind.KeyboardMouse));
                InputDeviceState.Notify(pad); InputDeviceState.Notify(keyboard);
                Assert.That(InputDeviceState.PromptDevice, Is.SameAs(pad));
                Assert.That(InputDeviceState.ActiveDevice, Is.SameAs(keyboard));
                Assert.That(InputDeviceState.PromptStyle, Is.EqualTo(GamepadStyle.Switch));
                action.Enable(); cancel.Enable();
                Assert.That(action.controls.Contains(pad.buttonEast), Is.True);
                Assert.That(action.controls.Contains(pad.buttonSouth), Is.False);
                Assert.That(cancel.controls.Contains(pad.buttonSouth), Is.True);
                Assert.That(cancel.controls.Contains(pad.buttonEast), Is.False);
                Assert.That(InputBindingDisplay.Get(action, out var path), Is.EqualTo("A"));
                Assert.That(path, Is.EqualTo("buttonEast"));
                InputDeviceState.Notify(pad);
                InputSystem.DisableDevice(pad);
                Assert.That(InputDeviceState.PromptDevice, Is.Not.SameAs(pad));
                Assert.That(InputDeviceState.ActiveDevice, Is.Not.SameAs(pad));
            }
            finally
            {
                action.Dispose(); cancel.Dispose();
                InputSystem.RemoveDevice(pad); InputSystem.RemoveDevice(keyboard);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator EnablingMenuButtonRefreshesNavigationAndExcludesNestedScope()
        {
            var original = EventSystem.current;
            var root = new GameObject("Nav", typeof(EventSystem), typeof(InputSystemUIInputModule));
            EventSystem.current = root.GetComponent<EventSystem>();
            var panel = new GameObject("Panel", typeof(RectTransform)); panel.transform.SetParent(root.transform);
            var first = new GameObject("First", typeof(RectTransform), typeof(Button)); first.transform.SetParent(panel.transform);
            var next = new GameObject("Next", typeof(RectTransform), typeof(Button)); next.transform.SetParent(panel.transform);
            next.transform.position = new Vector3(100, 0, 0); next.SetActive(false);
            var child = new GameObject("Child", typeof(RectTransform)); child.transform.SetParent(panel.transform);
            child.AddComponent<UIMenuScope>();
            var nested = new GameObject("Nested", typeof(RectTransform), typeof(Button)); nested.transform.SetParent(child.transform);
            nested.transform.position = new Vector3(10, 0, 0);
            try
            {
                panel.AddComponent<UIMenuScope>(); yield return null;
                Assert.That(first.GetComponent<Button>().navigation.selectOnRight, Is.Null);
                next.SetActive(true); yield return null;
                Assert.That(first.GetComponent<Button>().navigation.selectOnRight, Is.SameAs(next.GetComponent<Button>()));
                next.GetComponent<Button>().interactable = false; yield return null;
                Assert.That(first.GetComponent<Button>().navigation.selectOnRight, Is.Null);
            }
            finally { Object.Destroy(root); if (original != null) EventSystem.current = original; }
            yield return null;
        }

        [UnityTest]
        public IEnumerator MultiplePadsKeepLastUsedPromptAndIgnoreStickDrift()
        {
            var first = InputSystem.AddDevice<Gamepad>(); var second = InputSystem.AddDevice<Gamepad>();
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                InputDeviceState.Notify(first); InputDeviceState.Notify(keyboard);
                InputSystem.QueueStateEvent(second, new GamepadState { leftStick = new Vector2(.05f, .05f) }); yield return null;
                Assert.That(InputDeviceState.PromptDevice, Is.SameAs(first));
                InputDeviceState.Notify(second); InputDeviceState.Notify(keyboard);
                Assert.That(InputDeviceState.PromptDevice, Is.SameAs(second));
                InputSystem.DisableDevice(second);
                Assert.That(InputDeviceState.PromptDevice, Is.Not.SameAs(second));
                Assert.That(InputDeviceState.ActiveKind, Is.EqualTo(InputDeviceKind.KeyboardMouse));
            }
            finally { InputSystem.RemoveDevice(first); InputSystem.RemoveDevice(second); InputSystem.RemoveDevice(keyboard); }
        }

#if UNITY_EDITOR
        [UnityTest]
        public IEnumerator SavedDlssActionsSupportGamepadAndRequireNeutralAfterMenu()
        {
            var pad = InputSystem.AddDevice<Gamepad>();
            var source = UnityEditor.AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/LoadResources/Demos/dlss/Data/Dlss.inputactions");
            using var session = new InputActionSession(source);
            session.Asset.devices = new InputDevice[] { pad }; session.SetMap("Observe");
            try
            {
                InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = new Vector2(0, .8f), rightStick = new Vector2(.7f, 0) });
                yield return null; yield return null;
                Assert.That(session.ReadVector("Move").y, Is.GreaterThan(0));
                Assert.That(session.ReadVector("Look").x, Is.GreaterThan(0));
                session.SetMap("Menu"); session.SetMap("Observe"); yield return null;
                Assert.That(session.ReadVector("Move"), Is.EqualTo(Vector2.zero));
                InputSystem.QueueStateEvent(pad, new GamepadState()); yield return null;
                session.ReadVector("Move"); session.ReadVector("Look");
                InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = new Vector2(0, .8f) }.WithButton(GamepadButton.West)); yield return null;
                Assert.That(session.ReadVector("Move").y, Is.GreaterThan(0));
                Assert.That(session.Pressed("Reset"), Is.True);
            }
            finally { InputSystem.RemoveDevice(pad); }
        }
#endif

        [UnityTest]
        public IEnumerator MapActivationKeepsAbsolutePointerWithoutRequiringScreenOrigin()
        {
            var mouse = InputSystem.AddDevice<Mouse>();
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            asset.AddActionMap("Observe").AddAction("Point", InputActionType.Value, "<Mouse>/position");
            using var session = new InputActionSession(asset);
            session.Asset.devices = new InputDevice[] { mouse };
            try
            {
                InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(350, 220) }); yield return null;
                session.SetMap("Observe"); yield return null;
                Assert.That(session.Read<Vector2>("Point"), Is.EqualTo(new Vector2(350, 220)));
            }
            finally { InputSystem.RemoveDevice(mouse); Object.Destroy(asset); }
        }

        [UnityTest]
        public IEnumerator NestedMenuRestoresParentAndTouchSuppressesInteractionFeedback()
        {
            var original = EventSystem.current;
            var root = new GameObject("UnifiedInputTest", typeof(EventSystem), typeof(InputSystemUIInputModule));
            var system = root.GetComponent<EventSystem>();
            EventSystem.current = system;
            var first = new GameObject("First", typeof(RectTransform), typeof(Button), typeof(UIStateInteraction));
            var second = new GameObject("Second", typeof(RectTransform), typeof(Button));
            var keyboard = InputSystem.AddDevice<Keyboard>();
            first.transform.SetParent(root.transform); second.transform.SetParent(root.transform);
            MenuInputScope parent = null, child = null;
            try
            {
                parent = new MenuInputScope(system);
                parent.SetContext(GameplayInputContext.Menu, first);
                yield return null; parent.Update();
                Assert.That(system.currentSelectedGameObject, Is.SameAs(first));
                child = new MenuInputScope(system);
                child.SetContext(GameplayInputContext.Menu, second);
                yield return null; child.Update(); parent.Update();
                Assert.That(system.currentSelectedGameObject, Is.SameAs(second));
                child.Dispose(); child = null;
                Assert.That(system.currentSelectedGameObject, Is.SameAs(first));
                InputDeviceState.Notify(keyboard);
                yield return null;
                Assert.That(first.GetComponent<UIStateInteraction>().InteractionState, Is.EqualTo("Focused"));
                InputDeviceState.NotifyTouch();
                first.GetComponent<UIStateInteraction>().OnSubmit(new BaseEventData(system));
                Assert.That(first.GetComponent<UIStateInteraction>().InteractionState, Is.EqualTo("Normal"));
                first.GetComponent<Button>().interactable = false;
                Assert.That(first.GetComponent<Button>().IsInteractable(), Is.False);
            }
            finally
            {
                child?.Dispose(); parent?.Dispose();
                InputSystem.RemoveDevice(keyboard);
                Object.Destroy(root);
                if (original != null) EventSystem.current = original;
            }
            yield return null;
        }
    }
}
