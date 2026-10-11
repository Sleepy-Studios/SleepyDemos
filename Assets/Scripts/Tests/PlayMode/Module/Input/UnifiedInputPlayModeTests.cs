using System.Collections;
using System.Linq;
using Core.Runtime;
using Core.Runtime.Inputs;
using NUnit.Framework;
using TMPro;
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
        public IEnumerator CompoundKeycapsAndPlayStationPromptsResolveActualBindings()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var pad = InputSystem.AddDevice("DualSenseGamepadHID");
            var action = new InputAction("Reset", InputActionType.Button);
            action.AddCompositeBinding("ButtonWithOneModifier")
                .With("Modifier", "<Keyboard>/leftCtrl")
                .With("Button", "<Keyboard>/r");
            action.AddBinding("<Gamepad>/buttonNorth");
            try
            {
                action.Enable();
                InputDeviceState.Notify(keyboard);
                var parts = InputBindingDisplay.GetParts(action, out var separator);
                Assert.That(parts.Count, Is.EqualTo(2));
                Assert.That(separator, Is.EqualTo(" + "));
                Assert.That(parts[0].ControlPath, Is.EqualTo("leftCtrl"));
                Assert.That(parts[1].ControlPath, Is.EqualTo("r"));
                InputDeviceState.Notify(pad);
                parts = InputBindingDisplay.GetParts(action, out _);
                Assert.That(parts.Count, Is.EqualTo(1));
                Assert.That(parts[0].ControlPath, Is.EqualTo("buttonNorth"));
                Assert.That(InputDeviceState.PromptStyle, Is.EqualTo(GamepadStyle.PlayStation));
                InputDeviceState.Notify(keyboard);
                action.ApplyBindingOverride(2, "<Keyboard>/t");
                parts = InputBindingDisplay.GetParts(action, out _);
                Assert.That(parts[1].ControlPath, Is.EqualTo("t"));
            }
            finally
            {
                action.Dispose();
                InputSystem.RemoveDevice(pad);
                InputSystem.RemoveDevice(keyboard);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator MenuCancelFromSelectedButtonTargetsNearestScope()
        {
            var previous = EventSystem.current;
            var root = new GameObject("MenuCancelTest", typeof(EventSystem), typeof(InputSystemUIInputModule));
            EventSystem.current = root.GetComponent<EventSystem>();
            var parent = new GameObject("Parent", typeof(RectTransform));
            parent.transform.SetParent(root.transform);
            var parentButton = new GameObject("ParentButton", typeof(RectTransform), typeof(Button));
            parentButton.transform.SetParent(parent.transform);
            var parentMenu = parent.AddComponent<UIMenuScope>();
            int parentCanceled = 0;
            parentMenu.Canceled += () => parentCanceled++;
            var child = new GameObject("Child", typeof(RectTransform));
            child.transform.SetParent(parent.transform);
            var childButton = new GameObject("ChildButton", typeof(RectTransform), typeof(Button));
            childButton.transform.SetParent(child.transform);
            var dropdown = new GameObject("Dropdown", typeof(RectTransform), typeof(Dropdown));
            dropdown.transform.SetParent(child.transform);
            var childMenu = child.AddComponent<UIMenuScope>();
            int childCanceled = 0;
            childMenu.Canceled += () => childCanceled++;
            try
            {
                var module = root.GetComponent<InputSystemUIInputModule>();
                if (module.move?.action == null)
                {
                    module.AssignDefaultActions();
                }
                var promptObject = new GameObject("MenuPrompt", typeof(RectTransform), typeof(InputBindingPrompt));
                promptObject.transform.SetParent(child.transform, false);
                var prompt = promptObject.GetComponent<InputBindingPrompt>();
                prompt.Configure(null, null, null, null, null, menuAction: "Navigate");
                prompt.BindAsset(null);
                Assert.That(prompt.Action, Is.SameAs(module.move.action), "Navigate 必须使用公共 UI 的 move，而非 submit。");
                Assert.That(prompt.Action, Is.Not.SameAs(module.submit.action));
                prompt.Configure(null, null, null, null, null, menuAction: "Submit");
                prompt.BindAsset(null);
                Assert.That(prompt.Action, Is.SameAs(module.submit.action));
                prompt.Configure(null, null, null, null, null, menuAction: "Cancel");
                prompt.BindAsset(null);
                Assert.That(prompt.Action, Is.SameAs(module.cancel.action));
                yield return null;
                ExecuteEvents.Execute(childButton, new BaseEventData(EventSystem.current), ExecuteEvents.cancelHandler);
                Assert.That(childCanceled, Is.EqualTo(1));
                Assert.That(parentCanceled, Is.Zero);
                Assert.That(dropdown.GetComponent<UIMenuCancelRelay>(), Is.Null, "下拉框应保留自己的取消处理。");
                childButton.SetActive(false);
                yield return null;
                childButton.SetActive(true);
                yield return null;
                Assert.That(childButton.GetComponents<UIMenuCancelRelay>().Length, Is.EqualTo(1), "重新显示控件不重复添加取消转发。");
                child.SetActive(false);
                ExecuteEvents.Execute(parentButton, new BaseEventData(EventSystem.current), ExecuteEvents.cancelHandler);
                Assert.That(parentCanceled, Is.EqualTo(1));
            }
            finally
            {
                Object.Destroy(root);
                if (previous != null)
                {
                    EventSystem.current = previous;
                }
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator PromptsFollowActualDeviceAndUsageBindingResolvesSwitchA()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var pad = (Gamepad)InputSystem.AddDevice("SwitchProControllerHID");
            var action = new InputAction("Confirm", InputActionType.Button, "*/{Submit}");
            var cancel = new InputAction("Back", InputActionType.Button, "*/{Cancel}");
            try
            {
                InputDeviceState.Initialize(); InputDeviceState.Notify(keyboard);
                Assert.That(InputDeviceState.ActiveKind, Is.EqualTo(InputDeviceKind.KeyboardMouse));
                InputDeviceState.Notify(pad); InputDeviceState.Notify(keyboard);
                Assert.That(InputDeviceState.PromptDevice, Is.SameAs(keyboard));
                Assert.That(InputDeviceState.ActiveDevice, Is.SameAs(keyboard));
                InputDeviceState.Notify(pad);
                Assert.That(InputDeviceState.PromptStyle, Is.EqualTo(GamepadStyle.Switch));
                action.Enable(); cancel.Enable();
                Assert.That(action.controls.Contains(pad.buttonEast), Is.True);
                Assert.That(action.controls.Contains(pad.buttonSouth), Is.False);
                Assert.That(cancel.controls.Contains(pad.buttonSouth), Is.True);
                Assert.That(cancel.controls.Contains(pad.buttonEast), Is.False);
                Assert.That(InputBindingDisplay.Get(action, out var path), Is.EqualTo("A"));
                Assert.That(path, Is.EqualTo("buttonEast"));
                var parts = InputBindingDisplay.GetParts(action, out _, InputDeviceKind.KeyboardMouse, bindingIndex: 0);
                Assert.That(parts.Count, Is.EqualTo(1));
                Assert.That(parts[0].ControlPath, Is.EqualTo("enter"), "同一通配用途绑定的键鼠预览应显示实际确认键。");
                InputBindingDisplay.Get(cancel, out path, InputDeviceKind.KeyboardMouse, bindingIndex: 0);
                Assert.That(path, Is.EqualTo("escape"));
                InputBindingDisplay.Get(cancel, out path, InputDeviceKind.Gamepad, bindingIndex: 0);
                Assert.That(path, Is.EqualTo("buttonSouth"), "Switch 返回仍由原生 Cancel 用途解析。");
                Assert.That(InputDeviceState.PromptDevice, Is.SameAs(pad), "设备预览不能改变实际提示来源。");
                InputSystem.DisableDevice(pad);
                Assert.That(InputDeviceState.PromptDevice, Is.Not.SameAs(pad));
                Assert.That(InputDeviceState.ActiveDevice, Is.Not.SameAs(pad));
                InputSystem.RemoveDevice(pad);
                pad = null;
                parts = InputBindingDisplay.GetParts(action, out _, InputDeviceKind.Gamepad, bindingIndex: 0);
                Assert.That(parts.Count, Is.EqualTo(1), "未连接手柄时仍可按原生布局预览固定用途绑定。");
                Assert.That(parts[0].ControlPath, Is.Not.Empty);
                if (Gamepad.current == null)
                {
                    Assert.That(parts[0].ControlPath, Is.EqualTo("buttonSouth"), "没有实际手柄时使用原生通用 Gamepad 布局。");
                }
            }
            finally
            {
                action.Dispose(); cancel.Dispose();
                if (pad != null)
                {
                    InputSystem.RemoveDevice(pad);
                }
                InputSystem.RemoveDevice(keyboard);
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
                child.SetActive(false);
                EventSystem.current.SetSelectedGameObject(next);
                panel.SetActive(false);
                panel.SetActive(true);
                yield return null; yield return null;
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(next), "重新露出页面恢复原控件焦点");
                next.GetComponent<Button>().interactable = false; yield return null;
                Assert.That(first.GetComponent<Button>().navigation.selectOnRight, Is.Null);
                panel.SetActive(false); panel.SetActive(true);
                yield return null; yield return null;
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(first), "原控件失效时回到可用控件");
            }
            finally { Object.Destroy(root); if (original != null) EventSystem.current = original; }
            yield return null;
        }

        [UnityTest]
        public IEnumerator OrdinaryScrollRectRevealsMenuSelectionAndPreservesPointerScrolling()
        {
            var previous = EventSystem.current;
            var root = new GameObject("Scroll focus", typeof(EventSystem), typeof(InputSystemUIInputModule));
            EventSystem.current = root.GetComponent<EventSystem>();
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var mouse = InputSystem.AddDevice<Mouse>();
            var panel = new GameObject("Panel", typeof(RectTransform));
            panel.transform.SetParent(root.transform, false);
            var container = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect));
            container.transform.SetParent(panel.transform, false);
            var viewport = (RectTransform)container.transform;
            viewport.sizeDelta = new Vector2(200, 100);
            container.AddComponent<RectMask2D>();
            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport, false);
            var contentRect = (RectTransform)content.transform;
            contentRect.anchorMin = contentRect.anchorMax = new Vector2(0, 1);
            contentRect.pivot = new Vector2(0, 1);
            contentRect.sizeDelta = new Vector2(500, 500);
            var first = MakeButton("First", new Vector2(10, -10));
            var last = MakeButton("Last", new Vector2(440, -450));
            first.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnDown = last };
            last.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = first };
            var scroll = container.GetComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = contentRect;
            scroll.horizontal = scroll.vertical = true;
            scroll.inertia = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            try
            {
                InputDeviceState.Notify(keyboard);
                panel.AddComponent<UIMenuScope>();
                yield return null;
                yield return null;
                EventSystem.current.SetSelectedGameObject(first.gameObject);
                ExecuteEvents.Execute(first.gameObject, new AxisEventData(EventSystem.current) { moveDir = MoveDirection.Down }, ExecuteEvents.moveHandler);
                yield return null;
                yield return null;
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(last.gameObject));
                AssertInside(last);
                Assert.That(scroll.horizontalNormalizedPosition, Is.GreaterThan(0));
                Assert.That(scroll.verticalNormalizedPosition, Is.LessThan(1));
                ExecuteEvents.Execute(last.gameObject, new AxisEventData(EventSystem.current) { moveDir = MoveDirection.Up }, ExecuteEvents.moveHandler);
                yield return null;
                yield return null;
                AssertInside(first);
                InputDeviceState.Notify(mouse);
                scroll.normalizedPosition = Vector2.one * 0.5f;
                var pointerPosition = scroll.normalizedPosition;
                yield return null;
                yield return null;
                Assert.That(scroll.normalizedPosition, Is.EqualTo(pointerPosition), "指针滚动不能被仍保留的键盘焦点拉回。");
                InputDeviceState.Notify(keyboard);
                yield return null;
                yield return null;
                AssertInside(first);
            }
            finally
            {
                Object.Destroy(root);
                InputSystem.RemoveDevice(keyboard);
                InputSystem.RemoveDevice(mouse);
                if (previous != null)
                {
                    EventSystem.current = previous;
                }
            }
            yield return null;

            Button MakeButton(string name, Vector2 position)
            {
                var button = new GameObject(name, typeof(RectTransform), typeof(Button));
                button.transform.SetParent(content.transform, false);
                var rect = (RectTransform)button.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
                rect.sizeDelta = new Vector2(40, 30);
                rect.anchoredPosition = position;
                return button.GetComponent<Button>();
            }
            void AssertInside(Button button)
            {
                var corners = new Vector3[4];
                ((RectTransform)button.transform).GetWorldCorners(corners);
                foreach (var corner in corners)
                {
                    var point = viewport.InverseTransformPoint(corner);
                    Assert.That(point.x, Is.InRange(viewport.rect.xMin - 0.5f, viewport.rect.xMax + 0.5f));
                    Assert.That(point.y, Is.InRange(viewport.rect.yMin - 0.5f, viewport.rect.yMax + 0.5f));
                }
            }
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
                Assert.That(InputDeviceState.PromptDevice, Is.SameAs(keyboard));
                InputDeviceState.Notify(second);
                Assert.That(InputDeviceState.PromptDevice, Is.SameAs(second));
                InputDeviceState.Notify(keyboard);
                Assert.That(InputDeviceState.PromptDevice, Is.SameAs(keyboard));
                InputSystem.DisableDevice(second);
                Assert.That(InputDeviceState.PromptDevice, Is.Not.SameAs(second));
                Assert.That(InputDeviceState.ActiveKind, Is.EqualTo(InputDeviceKind.KeyboardMouse));
            }
            finally { InputSystem.RemoveDevice(first); InputSystem.RemoveDevice(second); InputSystem.RemoveDevice(keyboard); }
        }

        [UnityTest]
        public IEnumerator ExplicitMenuLinksSurviveAvailabilityChanges()
        {
            var original = EventSystem.current;
            var root = new GameObject("ExplicitNav", typeof(EventSystem), typeof(InputSystemUIInputModule));
            EventSystem.current = root.GetComponent<EventSystem>();
            var panel = new GameObject("Panel", typeof(RectTransform));
            panel.transform.SetParent(root.transform);
            var first = new GameObject("First", typeof(RectTransform), typeof(Button));
            first.transform.SetParent(panel.transform);
            var next = new GameObject("Next", typeof(RectTransform), typeof(Button));
            next.transform.SetParent(panel.transform);
            next.transform.position = new Vector3(100, 0, 0);
            var nearer = new GameObject("Nearer", typeof(RectTransform), typeof(Button));
            nearer.transform.SetParent(panel.transform);
            nearer.transform.position = new Vector3(10, 0, 0);
            first.GetComponent<Button>().navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = next.GetComponent<Button>() };
            try
            {
                panel.AddComponent<UIMenuScope>();
                yield return null;
                nearer.SetActive(false);
                yield return null;
                nearer.SetActive(true);
                yield return null;
                EventSystem.current.SetSelectedGameObject(first);
                ExecuteEvents.Execute(first, new AxisEventData(EventSystem.current) { moveDir = MoveDirection.Right }, ExecuteEvents.moveHandler);
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(next));
                panel.SetActive(false);
                Assert.That(first.GetComponent<Button>().navigation.selectOnRight, Is.SameAs(next.GetComponent<Button>()));
            }
            finally { Object.Destroy(root); if (original != null) EventSystem.current = original; }
            yield return null;
        }

#if UNITY_EDITOR
        [UnityTest]
        public IEnumerator SavedKartBindingRowsShowSelectedAliasAndClearSelectionWhenReused()
        {
            var source = UnityEditor.AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/LoadResources/Demos/kagit_kart/Data/PlayerInput.asset");
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/LoadResources/UI/Common/Input/InputPrompt.prefab");
            var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<InputGlyphCatalog>("Assets/LoadResources/UI/Common/Input/InputGlyphCatalog.asset");
            Assert.That(source, Is.Not.Null);
            Assert.That(prefab, Is.Not.Null);
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var candidate = Object.Instantiate(source);
            var root = Object.Instantiate(prefab);
            string original = source.SaveBindingOverridesAsJson();
            try
            {
                InputDeviceState.Notify(keyboard);
                var throttle = candidate.FindAction("Drive/KeyThrottle", true);
                var steer = candidate.FindAction("Drive/KeySteer", true);
                throttle.Enable();
                steer.Enable();
                int arrow = Enumerable.Range(0, throttle.bindings.Count).Single(index => throttle.bindings[index].path == "<Keyboard>/upArrow");
                var parts = InputBindingDisplay.GetParts(throttle, out _, InputDeviceKind.KeyboardMouse, bindingIndex: arrow);
                Assert.That(parts.Count, Is.EqualTo(1));
                Assert.That(parts[0].ControlPath, Is.EqualTo("upArrow"), "同动作的第二个别名不能显示成第一个 W。");
                InputBindingDisplay.Get(throttle, out var path, InputDeviceKind.KeyboardMouse, bindingIndex: arrow);
                Assert.That(path, Is.EqualTo("upArrow"));
                var prompt = root.GetComponent<InputBindingPrompt>();
                var image = root.transform.Find("Glyphs/Glyph0/Icon0").GetComponent<Image>();
                prompt.SetDisplayDevice(InputDeviceKind.KeyboardMouse);
                prompt.BindBinding(throttle, arrow);
                Assert.That(image.sprite, Is.SameAs(catalog.Get(InputDeviceKind.KeyboardMouse, GamepadStyle.Generic, "upArrow")));
                throttle.ApplyBindingOverride(arrow, "<Keyboard>/t");
                prompt.Refresh();
                Assert.That(image.sprite, Is.SameAs(catalog.Get(InputDeviceKind.KeyboardMouse, GamepadStyle.Generic, "t")));
                prompt.Bind(throttle);
                Assert.That(image.sprite, Is.SameAs(catalog.Get(InputDeviceKind.KeyboardMouse, GamepadStyle.Generic, "w")), "复用为完整动作时清除精确绑定选择。");
                int right = Enumerable.Range(0, steer.bindings.Count).Single(index => steer.bindings[index].path == "<Keyboard>/rightArrow");
                prompt.BindBinding(steer, right);
                Assert.That(image.sprite, Is.SameAs(catalog.Get(InputDeviceKind.KeyboardMouse, GamepadStyle.Generic, "rightArrow")));
                prompt.BindPart(steer, "Positive");
                Assert.That(image.sprite, Is.SameAs(catalog.Get(InputDeviceKind.KeyboardMouse, GamepadStyle.Generic, "d")), "复合分组预览也应清除精确绑定选择。");
                Assert.That(source.SaveBindingOverridesAsJson(), Is.EqualTo(original));
            }
            finally
            {
                candidate.Disable();
                Object.Destroy(root);
                Object.Destroy(candidate);
                InputSystem.RemoveDevice(keyboard);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator SavedKenneyKeyboardPromptsUseActualSpritesAfterRebinding()
        {
            var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<InputGlyphCatalog>("Assets/LoadResources/UI/Common/Input/InputGlyphCatalog.asset");
            Assert.That(catalog, Is.Not.Null);
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var root = new GameObject("KeyboardPrompt", typeof(RectTransform));
            var glyphRoot = new GameObject("Glyph", typeof(RectTransform), typeof(Image));
            glyphRoot.transform.SetParent(root.transform, false);
            var textRoot = new GameObject("KeyText", typeof(RectTransform), typeof(TextMeshProUGUI));
            textRoot.transform.SetParent(glyphRoot.transform, false);
            var captionRoot = new GameObject("Caption", typeof(RectTransform), typeof(TextMeshProUGUI));
            captionRoot.transform.SetParent(root.transform, false);
            var action = new InputAction("Prompt", InputActionType.Button, "<Keyboard>/r");
            try
            {
                InputDeviceState.Notify(keyboard);
                var image = glyphRoot.GetComponent<Image>();
                var keyText = textRoot.GetComponent<TMP_Text>();
                var prompt = root.AddComponent<InputBindingPrompt>();
                prompt.Configure(null, catalog, captionRoot.GetComponent<TMP_Text>(), new[]
                {
                    new InputBindingPrompt.GlyphSlot { root = glyphRoot, icon = image, keyText = keyText }
                }, null);
                prompt.Bind(action);
                foreach (string key in new[] { "r", "escape", "backspace", "leftCtrl", "upArrow", "f12", "9" })
                {
                    action.ApplyBindingOverride(0, "<Keyboard>/" + key);
                    prompt.Refresh();
                    var sprite = catalog.Get(InputDeviceKind.KeyboardMouse, GamepadStyle.Generic, key);
                    Assert.That(sprite, Is.Not.Null, "正式键盘图标缺失：" + key);
                    Assert.That(sprite, Is.Not.SameAs(catalog.KeyboardBlank));
                    Assert.That(image.sprite, Is.SameAs(sprite), "提示应使用真实 Kenney 键图形：" + key);
                    Assert.That(image.gameObject.activeSelf, Is.True);
                    Assert.That(keyText.gameObject.activeSelf, Is.False, "已有图形不叠加第二套文字。");
                }
                action.ApplyBindingOverride(0, "<Keyboard>/t");
                prompt.Refresh();
                Assert.That(image.sprite.name, Is.EqualTo("KeyT"), "候选绑定生效后图标也应更新。");
            }
            finally
            {
                Object.Destroy(root);
                action.Dispose();
                InputSystem.RemoveDevice(keyboard);
            }
            yield return null;
        }

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
        public IEnumerator ScalarGamepadAxesRequireNeutralWhenGameplayMapReopens()
        {
            var pad = InputSystem.AddDevice<Gamepad>();
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            var map = asset.AddActionMap("Drive");
            map.AddAction("Steer", InputActionType.Value, "<Gamepad>/leftStick/x", expectedControlLayout: "Axis");
            map.AddAction("Throttle", InputActionType.Value, "<Gamepad>/rightTrigger", expectedControlLayout: "Axis");
            using var session = new InputActionSession(asset);
            session.Asset.devices = new InputDevice[] { pad };
            session.SetMap("Drive");
            try
            {
                InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = new Vector2(0.8f, 0), rightTrigger = 0.1f });
                yield return null;
                yield return null;
                Assert.That(session.Read<float>("Steer"), Is.GreaterThan(0));
                Assert.That(session.Read<float>("Throttle"), Is.GreaterThan(0));
                session.SetMap(null);
                session.SetMap("Drive");
                yield return null;
                Assert.That(session.Read<float>("Steer"), Is.Zero);
                Assert.That(session.Read<float>("Throttle"), Is.Zero);
                InputSystem.QueueStateEvent(pad, new GamepadState());
                yield return null;
                session.Read<float>("Steer");
                session.Read<float>("Throttle");
                InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = new Vector2(-0.8f, 0), rightTrigger = 0.1f });
                yield return null;
                Assert.That(session.Read<float>("Steer"), Is.LessThan(0));
                Assert.That(session.Read<float>("Throttle"), Is.GreaterThan(0));
            }
            finally
            {
                InputSystem.RemoveDevice(pad);
                Object.Destroy(asset);
            }
        }

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
