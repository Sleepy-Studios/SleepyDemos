using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Core.Runtime;
using Core.Runtime.Inputs;
using Core.Runtime.Settings;
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
        [Test]
        public void UnconnectedStickAxisPreviewUsesParentGlyphAndCandidateBrandStaysLocal()
        {
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            var map = new InputActionMap("Preview");
            asset.AddActionMap(map);
            asset.devices = new InputDevice[0];
            var action = map.AddAction("Steer", InputActionType.Value, "<Gamepad>/leftStick/x");
            var reference = InputActionReference.Create(action);
            var catalog = ScriptableObject.CreateInstance<InputGlyphCatalog>();
            var sprites = new List<Sprite>();
            var entries = new List<InputGlyphCatalog.Entry>();
            foreach (GamepadStyle style in System.Enum.GetValues(typeof(GamepadStyle)))
            {
                var sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f);
                sprites.Add(sprite);
                entries.Add(new InputGlyphCatalog.Entry { style = style, controlPath = "leftStick", sprite = sprite });
            }
            catalog.Configure(entries.ToArray(), null, null);
            var root = new GameObject("CandidatePrompt", typeof(RectTransform), typeof(InputBindingPrompt));
            var glyph = new GameObject("Glyph", typeof(RectTransform), typeof(Image));
            glyph.transform.SetParent(root.transform);
            try
            {
                var parts = InputBindingDisplay.GetParts(action, out _, InputDeviceKind.Gamepad);
                Assert.That(parts.Count, Is.EqualTo(1));
                Assert.That(parts[0].ControlPath, Is.EqualTo("leftStick"));
                var prompt = root.GetComponent<InputBindingPrompt>();
                var image = glyph.GetComponent<Image>();
                prompt.Configure(reference, catalog, null, new[] { new InputBindingPrompt.GlyphSlot { root = glyph, icon = image } }, null);
                prompt.SetDisplayDevice(InputDeviceKind.Gamepad);
                var applied = GameSettingsService.CurrentGamepadStyle;
                Assert.That(image.sprite, Is.SameAs(catalog.Get(applied, "leftStick")));
                foreach (GamepadStyle style in System.Enum.GetValues(typeof(GamepadStyle)))
                {
                    prompt.SetDisplayGamepadStyle(style);
                    Assert.That(image.sprite, Is.SameAs(catalog.Get(style, "leftStick")));
                    Assert.That(GameSettingsService.CurrentGamepadStyle, Is.EqualTo(applied), "候选预览不能提前修改全局品牌。");
                }
                prompt.SetDisplayGamepadStyle(null);
                Assert.That(image.sprite, Is.SameAs(catalog.Get(applied, "leftStick")));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(reference);
                Object.DestroyImmediate(asset);
                Object.DestroyImmediate(catalog);
                foreach (var sprite in sprites)
                {
                    Object.DestroyImmediate(sprite);
                }
            }
        }

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
                Assert.That(InputDeviceState.PromptStyle, Is.EqualTo(GameSettingsService.CurrentGamepadStyle), "真实设备不自动切换玩家图标偏好。");
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
                Assert.That(InputDeviceState.PromptStyle, Is.EqualTo(GameSettingsService.CurrentGamepadStyle), "Switch用途仍读物理布局，图标风格保持玩家偏好。");
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
        public IEnumerator PromptVariantsKeepBindingOrderDuringFallbackAndShareWidthOwnership()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var gamepad = InputSystem.AddDevice<Gamepad>();
            var action = new InputAction("Prompt", InputActionType.Button);
            action.AddCompositeBinding("ButtonWithOneModifier")
                .With("Modifier", "<Keyboard>/leftCtrl")
                .With("Button", "<Keyboard>/k");
            var parent = new GameObject("PromptParent", typeof(RectTransform));
            parent.GetComponent<RectTransform>().sizeDelta = new Vector2(1200, 80);
            try
            {
                InputDeviceState.Notify(keyboard);
                foreach (string name in new[] { "InputPrompt", "InputPrompt2" })
                {
                    var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/LoadResources/UI/Common/Input/" + name + ".prefab");
                    Assert.That(prefab, Is.Not.Null);
                    var root = Object.Instantiate(prefab, parent.transform);
                    try
                    {
                        var prompt = root.GetComponent<InputBindingPrompt>();
                        var caption = root.transform.Find("Caption").GetComponent<TMP_Text>();
                        var rect = root.GetComponent<RectTransform>();
                        var fitter = root.GetComponent<ContentSizeFitter>();
                        var serialized = new UnityEditor.SerializedObject(prompt);
                        var authoredSlots = serialized.FindProperty("glyphSlots");
                        var realSlots = new InputBindingPrompt.GlyphSlot[authoredSlots.arraySize];
                        for (int index = 0; index < realSlots.Length; index++)
                        {
                            var authored = authoredSlots.GetArrayElementAtIndex(index);
                            realSlots[index] = new InputBindingPrompt.GlyphSlot
                            {
                                root = authored.FindPropertyRelative("root").objectReferenceValue as GameObject,
                                icon = authored.FindPropertyRelative("icon").objectReferenceValue as Image,
                                keyText = authored.FindPropertyRelative("keyText").objectReferenceValue as TMP_Text,
                                separator = authored.FindPropertyRelative("separator").objectReferenceValue as TMP_Text
                            };
                        }
                        var slot = root.transform.Find("Glyphs/Glyph0");
                        prompt.Configure(null, null, caption, new[]
                        {
                            new InputBindingPrompt.GlyphSlot
                            {
                                root = slot.gameObject,
                                icon = slot.Find("Icon0").GetComponent<Image>()
                            }
                        }, null, authorCaption: "执行");
                        prompt.SetDisplayDevice(InputDeviceKind.KeyboardMouse);
                        prompt.Bind(action);
                        string binding = InputBindingDisplay.Get(action, out _, InputDeviceKind.KeyboardMouse);
                        bool captionFirst = name == "InputPrompt2";
                        Assert.That(caption.text, Is.EqualTo(captionFirst ? "执行 " + binding : binding + " 执行"));
                        Assert.That(root.transform.Find("Glyphs").gameObject.activeSelf, Is.False);
                        prompt.BindBinding(action, 2);
                        string singleBinding = InputBindingDisplay.Get(action, out _, InputDeviceKind.KeyboardMouse, bindingIndex: 2);
                        Assert.That(caption.text, Is.EqualTo(captionFirst ? "执行 " + singleBinding : singleBinding + " 执行"), "缺图标及空白键帽时仍完整显示键名。");
                        prompt.Bind(action);
                        LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
                        float shortWidth = rect.rect.width;
                        float originalRight = rect.offsetMax.x;
                        prompt.SetCaption("执行一段更长的操作说明，保留完整真实组合键");
                        LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
                        Assert.That(rect.rect.width, Is.GreaterThan(shortWidth));
                        if (captionFirst)
                        {
                            Assert.That(rect.offsetMax.x, Is.EqualTo(originalRight).Within(0.1f), "文案变长仍保持右边界。");
                        }
                        var layout = parent.AddComponent<HorizontalLayoutGroup>();
                        layout.childControlWidth = true;
                        layout.childForceExpandWidth = false;
                        prompt.Refresh();
                        Assert.That(fitter.horizontalFit, Is.EqualTo(ContentSizeFitter.FitMode.Unconstrained));
                        Object.DestroyImmediate(layout);
                        prompt.Refresh();
                        Assert.That(fitter.horizontalFit, Is.EqualTo(ContentSizeFitter.FitMode.PreferredSize));
                        var hubCatalog = UnityEditor.AssetDatabase.LoadAssetAtPath<InputGlyphCatalog>(
                            "Assets/LoadResources/UI/Common/Input/HubInputGlyphCatalog.asset");
                        Assert.That(hubCatalog, Is.Not.Null);
                        Assert.That(hubCatalog.KeyboardBlank, Is.Not.Null);
                        prompt.Configure(null, hubCatalog, caption, realSlots, null, authorCaption: "执行");
                        prompt.Bind(action);
                        LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
                        var parts = InputBindingDisplay.GetParts(action, out _, InputDeviceKind.KeyboardMouse);
                        Assert.That(parts.Count, Is.EqualTo(2));
                        for (int index = 0; index < parts.Count; index++)
                        {
                            Assert.That(realSlots[index].icon.sprite, Is.SameAs(hubCatalog.KeyboardBlank));
                            Assert.That(realSlots[index].keyText.gameObject.activeSelf, Is.True);
                            Assert.That(realSlots[index].keyText.text, Is.EqualTo(parts[index].Text), "Hub 空键帽必须显示真实组合子键，顺序不反转。");
                        }
                        Assert.That(realSlots[0].root.transform.localPosition.x, Is.LessThan(realSlots[1].root.transform.localPosition.x),
                            "文案在前的提示也不能反转键帽组内的真实阅读顺序。");
                        float shortKeyWidth = realSlots[1].icon.rectTransform.rect.width;
                        float rightBeforeLongKey = rect.offsetMax.x;
                        action.ApplyBindingOverride(2, "<Keyboard>/backspace");
                        prompt.Refresh();
                        LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
                        var longParts = InputBindingDisplay.GetParts(action, out _, InputDeviceKind.KeyboardMouse);
                        Assert.That(realSlots[1].keyText.text, Is.EqualTo(longParts[1].Text));
                        Assert.That(realSlots[1].icon.rectTransform.rect.width, Is.GreaterThan(shortKeyWidth), "长键名应扩展键帽宽度。");
                        Assert.That(realSlots[1].icon.rectTransform.rect.width + 0.1f, Is.GreaterThanOrEqualTo(realSlots[1].keyText.preferredWidth),
                            "键帽不能裁掉实际显示键名。");
                        if (captionFirst)
                        {
                            Assert.That(rect.offsetMax.x, Is.EqualTo(rightBeforeLongKey).Within(0.1f));
                        }
                        action.RemoveBindingOverride(2);
                    }
                    finally
                    {
                        Object.DestroyImmediate(root);
                    }
                }
                var originalCatalog = UnityEditor.AssetDatabase.LoadAssetAtPath<InputGlyphCatalog>(
                    "Assets/LoadResources/UI/Common/Input/InputGlyphCatalog.asset");
                Assert.That(originalCatalog, Is.Not.Null);
                Assert.That(originalCatalog.KeyboardBlank.border, Is.EqualTo(new Vector4(32f, 32f, 32f, 32f)),
                    "正式空帽必须提供可横向扩展的九宫格边界。");
                using var gamepadAction = new InputAction("LongFallback", InputActionType.Button);
                gamepadAction.AddBinding("<Gamepad>/unmappedLongControlName");
                gamepadAction.AddBinding("<Gamepad>/buttonSouth");
                foreach (bool equipment in new[] { false, true })
                {
                    InputDeviceState.Notify(keyboard);
                    string path = "Assets/LoadResources/Demos/drone_flight/Prefabs/UI/"
                        + (equipment ? "Parts/TelemetryRoot.prefab" : "Foundation/DroneInputPrompt.prefab");
                    var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    Assert.That(prefab, Is.Not.Null);
                    var root = Object.Instantiate(prefab, parent.transform);
                    try
                    {
                        if (equipment)
                        {
                            // 正式页面按装备状态显示此区域；TMP 和原生布局须在真实显示生命周期中验收。
                            root.transform.Find("EquipmentPanel").gameObject.SetActive(true);
                        }
                        var prompt = equipment
                            ? root.transform.Find("EquipmentPanel/EquipmentOperationPrompts/EquipmentAimPrompt").GetComponent<InputBindingPrompt>()
                            : root.GetComponent<InputBindingPrompt>();
                        if (equipment)
                        {
                            prompt.gameObject.SetActive(true);
                        }
                        Assert.That(prompt.gameObject.activeInHierarchy, Is.True);
                        prompt.enabled = false;
                        var image = prompt.transform.Find("Glyphs/Glyph0/Icon0").GetComponent<Image>();
                        var keyText = image.GetComponentInChildren<TMP_Text>(true);
                        var element = image.GetComponent<LayoutElement>();
                        var layout = image.GetComponent<HorizontalLayoutGroup>();
                        var slotLayout = image.transform.parent.GetComponent<HorizontalLayoutGroup>();
                        var promptElement = prompt.GetComponent<LayoutElement>();
                        int authoredPromptPriority = promptElement != null ? promptElement.layoutPriority : 0;
                        float authoredWidth = element.preferredWidth;
                        float authoredHeight = element.preferredHeight;
                        float authoredMultiplier = image.pixelsPerUnitMultiplier;
                        float authoredFlexibleHeight = element.flexibleHeight;
                        bool authoredForceExpandHeight = slotLayout.childForceExpandHeight;
                        int authoredPadding = layout.padding.horizontal;
                        Color authoredColor = image.color;
                        Assert.That(authoredHeight, Is.EqualTo(equipment ? 24f : 36f));
                        prompt.enabled = true;
                        InputDeviceState.Notify(gamepad);
                        prompt.SetDisplayDevice(InputDeviceKind.Gamepad);
                        prompt.SetDisplayGamepadStyle(GamepadStyle.Xbox);
                        prompt.BindBinding(gamepadAction, 0);
                        // Equipment 的父布局拥有提示宽度；必须完成整个正式宿主布局周期再验收键帽。
                        yield return null;
                        Canvas.ForceUpdateCanvases();
                        LayoutRebuilder.ForceRebuildLayoutImmediate(root.GetComponent<RectTransform>());
                        var parts = InputBindingDisplay.GetParts(gamepadAction, out _, InputDeviceKind.Gamepad, bindingIndex: 0);
                        Assert.That(parts.Count, Is.EqualTo(1));
                        Assert.That(originalCatalog.Get(InputDeviceKind.Gamepad, GamepadStyle.Xbox, parts[0].ControlPath), Is.Null);
                        Assert.That(keyText.text, Is.EqualTo(parts[0].Text), "缺失图标仍显示真实长控制名称。");
                        Assert.That(keyText.preferredWidth, Is.GreaterThan(authoredHeight), "夹具必须覆盖方形短帽放不下的真实名称。");
                        Assert.That(image.sprite, Is.SameAs(originalCatalog.KeyboardBlank));
                        Assert.That(image.type, Is.EqualTo(Image.Type.Sliced));
                        Assert.That(image.preserveAspect, Is.False, "空帽应包住长名称，不能仍绘制成方形。");
                        Assert.That(image.fillCenter, Is.False);
                        Assert.That(image.rectTransform.rect.height, Is.EqualTo(authoredHeight).Within(0.1f));
                        Assert.That(image.pixelsPerUnitMultiplier * image.pixelsPerUnit * authoredHeight,
                            Is.EqualTo(image.sprite.rect.height).Within(0.1f), "短帽仍保持原整张图的显示比例。");
                        Assert.That(layout.padding.left, Is.GreaterThan(0));
                        Assert.That(layout.padding.right, Is.GreaterThan(0));
                        Assert.That(image.rectTransform.rect.width + 0.1f,
                            Is.GreaterThanOrEqualTo(keyText.preferredWidth + layout.padding.horizontal),
                            $"equipment={equipment}; icon={image.rectTransform.rect.width}; text={keyText.preferredWidth}; padding={layout.padding.horizontal}; "
                            + $"layoutPreferred={layout.preferredWidth}; elementMin={element.minWidth}; elementPreferred={element.preferredWidth}; "
                            + $"slotWidth={image.transform.parent.GetComponent<RectTransform>().rect.width}; promptWidth={prompt.GetComponent<RectTransform>().rect.width}; "
                            + $"glyphWidth={image.transform.parent.parent.GetComponent<RectTransform>().rect.width}");
                        float longWidth = image.rectTransform.rect.width;
                        prompt.Refresh();
                        yield return null;
                        Canvas.ForceUpdateCanvases();
                        LayoutRebuilder.ForceRebuildLayoutImmediate(root.GetComponent<RectTransform>());
                        Assert.That(image.rectTransform.rect.width, Is.EqualTo(longWidth).Within(0.1f), "反复刷新不能累积内边距。");
                        prompt.BindBinding(gamepadAction, 1);
                        yield return null;
                        Canvas.ForceUpdateCanvases();
                        LayoutRebuilder.ForceRebuildLayoutImmediate(root.GetComponent<RectTransform>());
                        Assert.That(image.sprite, Is.SameAs(originalCatalog.Get(InputDeviceKind.Gamepad, GamepadStyle.Xbox, "buttonSouth")));
                        Assert.That(image.type, Is.EqualTo(Image.Type.Simple));
                        Assert.That(image.preserveAspect, Is.True);
                        Assert.That(keyText.gameObject.activeSelf, Is.False);
                        Assert.That(element.preferredWidth, Is.EqualTo(authoredWidth));
                        Assert.That(layout.padding.horizontal, Is.EqualTo(authoredPadding));
                        Assert.That(image.pixelsPerUnitMultiplier, Is.EqualTo(authoredMultiplier));
                        Assert.That(element.flexibleHeight, Is.EqualTo(authoredFlexibleHeight));
                        Assert.That(slotLayout.childForceExpandHeight, Is.EqualTo(authoredForceExpandHeight));
                        if (promptElement != null)
                        {
                            Assert.That(promptElement.layoutPriority, Is.EqualTo(authoredPromptPriority), "切回图标应恢复作者容器的布局优先级。");
                        }
                        Assert.That(image.color, Is.EqualTo(authoredColor), "回退不能更改 Demo 的作者颜色。");
                    }
                    finally
                    {
                        Object.DestroyImmediate(root);
                    }
                }
            }
            finally
            {
                Object.Destroy(parent);
                action.Dispose();
                InputSystem.RemoveDevice(keyboard);
                InputSystem.RemoveDevice(gamepad);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator SavedKartBindingRowsShowSelectedAliasAndClearSelectionWhenReused()
        {
            var source = UnityEditor.AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/LoadResources/Demos/kagit_kart/Data/PlayerInput.asset");
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/LoadResources/Demos/kagit_kart/Prefabs/UI/Foundation/KagitKartInputPrompt.prefab");
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
                Assert.That(image.sprite, Is.SameAs(catalog.Get(InputDeviceKind.KeyboardMouse, GamepadStyle.Xbox, "upArrow")));
                throttle.ApplyBindingOverride(arrow, "<Keyboard>/t");
                prompt.Refresh();
                Assert.That(image.sprite, Is.SameAs(catalog.Get(InputDeviceKind.KeyboardMouse, GamepadStyle.Xbox, "t")));
                prompt.Bind(throttle);
                Assert.That(image.sprite, Is.SameAs(catalog.Get(InputDeviceKind.KeyboardMouse, GamepadStyle.Xbox, "w")), "复用为完整动作时清除精确绑定选择。");
                int right = Enumerable.Range(0, steer.bindings.Count).Single(index => steer.bindings[index].path == "<Keyboard>/rightArrow");
                prompt.BindBinding(steer, right);
                Assert.That(image.sprite, Is.SameAs(catalog.Get(InputDeviceKind.KeyboardMouse, GamepadStyle.Xbox, "rightArrow")));
                prompt.BindPart(steer, "Positive");
                Assert.That(image.sprite, Is.SameAs(catalog.Get(InputDeviceKind.KeyboardMouse, GamepadStyle.Xbox, "d")), "复合分组预览也应清除精确绑定选择。");
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
                    var sprite = catalog.Get(InputDeviceKind.KeyboardMouse, GamepadStyle.Xbox, key);
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
                session.SetMap(null); session.SetMap("Observe"); yield return null;
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
            var first = new GameObject("First", typeof(RectTransform), typeof(Button), typeof(Image), typeof(UIState));
            var second = new GameObject("Second", typeof(RectTransform), typeof(Button));
            var keyboard = InputSystem.AddDevice<Keyboard>();
            first.transform.SetParent(root.transform); second.transform.SetParent(root.transform);
            MenuInputScope parent = null, child = null;
            try
            {
                var state = first.GetComponent<UIState>();
                var image = first.GetComponent<Image>();
                var definitions = new List<UIStateInfo>(state.States);
                state.GetState("Normal").properties.Add(new UIStateProperty
                    { propertyType = UIStatePropertyType.GraphicColor, target = image, colorValue = Color.white });
                state.GetState("Normal").properties.Add(new UIStateProperty
                    { propertyType = UIStatePropertyType.TransformLocalScale, target = first.transform, vector3Value = Vector3.one });
                state.GetState("Pressed").properties.Add(new UIStateProperty
                    { propertyType = UIStatePropertyType.GraphicColor, target = image, colorValue = Color.black });
                state.GetState("Pressed").properties.Add(new UIStateProperty
                    { propertyType = UIStatePropertyType.TransformLocalScale, target = first.transform, vector3Value = Vector3.one * 0.8f });
                state.GetState("Disabled").properties.Add(new UIStateProperty
                    { propertyType = UIStatePropertyType.GraphicColor, target = image, colorValue = Color.gray });
                definitions.Add(new UIStateInfo { stateName = "Selected", properties = new List<UIStateProperty>
                    { new() { propertyType = UIStatePropertyType.GraphicColor, target = image, colorValue = Color.red } } });
                definitions.Add(new UIStateInfo { stateName = "On", properties = new List<UIStateProperty>
                    { new() { propertyType = UIStatePropertyType.GraphicColor, target = image, colorValue = Color.green } } });
                state.ConfigureStates(definitions);
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
                Assert.That(state.InteractionState, Is.EqualTo("Focused"));
                state.SetState("Selected");
                var pointer = new PointerEventData(system) { pointerId = -1, button = PointerEventData.InputButton.Left };
                state.OnPointerDown(pointer);
                Assert.That(state.InteractionState, Is.EqualTo("Pressed"));
                Assert.That(image.color, Is.EqualTo(Color.black));
                state.OnPointerUp(pointer);
                Assert.That(state.CurrentStateId, Is.EqualTo("Selected"));
                Assert.That(image.color, Is.EqualTo(Color.red), "交互退出必须恢复业务选择，不是清为未选择。");
                Assert.That(first.transform.localScale, Is.EqualTo(Vector3.one));
                InputDeviceState.NotifyTouch();
                state.OnSubmit(new BaseEventData(system));
                Assert.That(state.InteractionState, Is.EqualTo("Normal"));
                state.SetState("On");
                first.GetComponent<Button>().interactable = false;
                Assert.That(first.GetComponent<Button>().IsInteractable(), Is.False);
                yield return null;
                yield return null;
                Assert.That(state.InteractionState, Is.EqualTo("Disabled"));
                Assert.That(image.color, Is.EqualTo(Color.gray));
                first.GetComponent<Button>().interactable = true;
                yield return null;
                yield return null;
                Assert.That(state.CurrentStateId, Is.EqualTo("On"));
                Assert.That(image.color, Is.EqualTo(Color.green));
                first.SetActive(false);
                Assert.That(state.InteractionState, Is.EqualTo("Normal"));
                Assert.That(image.color, Is.EqualTo(Color.green), "禁用只清交互，不重置开关业务值。");
                first.SetActive(true);
                yield return null;
                Assert.That(image.color, Is.EqualTo(Color.green));
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
