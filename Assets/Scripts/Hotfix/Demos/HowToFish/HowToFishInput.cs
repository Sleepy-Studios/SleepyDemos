using System;
using Core.Runtime.Inputs;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Hotfix.HowToFish
{
    /// 一次重绑定的结果；冲突或失败均保留原绑定。
    public enum HowToFishRebindResult { Completed, Cancelled, Conflict, Failed }

    /// 键鼠和手柄共用的动作入口；每个 Demo 会话独占并释放其 ActionAsset。
    public sealed class HowToFishInput : IDisposable
    {
        private readonly InputActionSession session;
        private InputActionAsset asset => session.Asset;
        private readonly InputActionMap gameplay;
        private readonly InputActionMap boat;
        private readonly InputActionMap ui;
        private InputActionMap activeMap;
        private InputActionRebindingExtensions.RebindingOperation rebind;
        private bool disposed;
        private Action rebindEnd;
        private Action<HowToFishRebindResult> rebindFinished;
        /// 最近一次重绑定的失败或冲突说明；成功与取消时为空。
        public string RebindMessage { get; private set; }

        /// 当前是否由手柄操作，用于 HUD 提示。
        public bool IsGamepad => InputDeviceState.PromptKind == InputDeviceKind.Gamepad;
        /// 输入设备提示发生变化。
        public event Action DeviceChanged;
        /// UI 使用此资产的界面动作映射。
        public InputActionAsset Asset => asset;
        /// 当前有交互式重绑定时暂停普通菜单确认。
        public bool IsRebinding => rebind != null;

        /// <summary>从持久化模板创建独立会话，保持绑定 ID 稳定以支持跨启动重绑定。</summary>
        /// <param name="template">编辑期保存的输入模板。</param>
        public HowToFishInput(InputActionAsset template)
        {
            if (template == null) throw new ArgumentNullException(nameof(template));
            session = new InputActionSession(template);
            gameplay = asset.FindActionMap("Gameplay", true);
            boat = asset.FindActionMap("Boat", true);
            ui = asset.FindActionMap("UI", true);
            InputDeviceState.Changed += OnDeviceChanged;
            SetMode(false, false);
        }

        /// 创建供编辑器保存的默认输入模板；已有模板不应重复生成，以免改变绑定 ID。
        public static InputActionAsset CreateDefaultAsset()
        {
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            asset.name = "HowToFishInput";
            var gameplay = asset.AddActionMap("Gameplay");
            var boat = asset.AddActionMap("Boat");
            var ui = asset.AddActionMap("UI");
            AddMovement(gameplay);
            AddMovement(boat);
            AddButton(gameplay, "Jump", "<Keyboard>/space", "<Gamepad>/buttonSouth");
            AddButton(gameplay, "Sprint", "<Keyboard>/leftShift", "<Gamepad>/leftStickPress");
            AddButton(gameplay, "Use", "<Mouse>/leftButton", "<Gamepad>/rightTrigger");
            AddButton(gameplay, "Alternate", "<Mouse>/rightButton", "<Gamepad>/leftTrigger");
            AddButton(gameplay, "Throw", "<Keyboard>/g", "<Gamepad>/leftShoulder");
            AddButton(gameplay, "Reload", "<Keyboard>/r", "<Gamepad>/buttonNorth");
            AddButton(gameplay, "Next", "<Keyboard>/q", "<Gamepad>/dpad/right");
            AddButton(gameplay, "Previous", "<Keyboard>/z", "<Gamepad>/dpad/left");
            AddEquipmentBindings(asset);
            AddButton(gameplay, "Bait", "<Keyboard>/b", "<Gamepad>/dpad/up");
            AddButton(gameplay, "Style", "<Keyboard>/f", "<Gamepad>/rightShoulder");
            AddButton(ui, "Pause", "<Keyboard>/escape", "<Gamepad>/start");
            AddButton(ui, "Journal", "<Keyboard>/tab", "<Gamepad>/select");
            AddButton(ui, "Submit", "<Keyboard>/enter", "<Gamepad>/buttonSouth");
            AddButton(ui, "Cancel", "<Keyboard>/escape", "<Gamepad>/buttonEast");
            var navigate = ui.AddAction("Navigate", InputActionType.Value, expectedControlLayout: "Vector2");
            navigate.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow", groups: "KeyboardMouse").With("Down", "<Keyboard>/downArrow", groups: "KeyboardMouse")
                .With("Left", "<Keyboard>/leftArrow", groups: "KeyboardMouse").With("Right", "<Keyboard>/rightArrow", groups: "KeyboardMouse");
            navigate.AddBinding("<Gamepad>/dpad", groups: "Gamepad");
            navigate.AddBinding("<Gamepad>/leftStick", groups: "Gamepad");
            ui.AddAction("Point", InputActionType.PassThrough, "<Mouse>/position", expectedControlLayout: "Vector2");
            ui.AddAction("Click", InputActionType.PassThrough, "<Mouse>/leftButton", expectedControlLayout: "Button");
            ui.AddAction("ScrollWheel", InputActionType.PassThrough, "<Mouse>/scroll", expectedControlLayout: "Vector2");
            return asset;
        }

        /// <summary>补齐装备栏输入，保留既有动作和重绑定 ID。</summary>
        /// <param name="template">编辑器保存的输入模板。</param>
        public static void AddEquipmentBindings(InputActionAsset template)
        {
            var map = template.FindActionMap("Gameplay", true);
            foreach (var actionMap in new[] { map, template.FindActionMap("Boat", true) })
                if (actionMap.FindAction("ChangeSkin", false) == null)
                    AddButton(actionMap, "ChangeSkin", "<Keyboard>/c", "<Gamepad>/rightStickPress");
            if (map.FindAction("Holster", false) == null) AddButton(map, "Holster", "<Keyboard>/h", "<Gamepad>/dpad/down");
            for (int i = 1; i <= 8; i++)
                if (map.FindAction("Slot" + i, false) == null)
                    map.AddAction("Slot" + i, InputActionType.Button).AddBinding("<Keyboard>/" + i, groups: "KeyboardMouse");
        }

        /// <summary>切换陆地、驾驶或暂停状态，不叠加多个玩法输入映射。</summary>
        /// <param name="driving">是否正在驾驶。</param>
        /// <param name="paused">是否由菜单占用输入。</param>
        public void SetMode(bool driving, bool paused)
        {
            activeMap = driving ? boat : gameplay;
            session.SetMap(paused || IsRebinding ? null : activeMap.name);
            if (!IsRebinding) ui.Enable();
        }

        /// <summary>读取当前移动向量。</summary>
        /// <param name="deadZone">手柄移动死区。</param>
        public Vector2 ReadMove(float deadZone) => session.ReadVector("Move", deadZone);

        /// <summary>读取单帧视角变化；鼠标位移不重复乘帧时间。</summary>
        /// <param name="mouseSensitivity">鼠标每像素角度。</param>
        /// <param name="gamepadSpeed">手柄每秒最大角速度。</param>
        /// <param name="deadZone">摇杆死区。</param>
        /// <param name="invertY">是否反转纵向。</param>
        /// <param name="deltaTime">当前帧时长。</param>
        public Vector2 ReadLook(float mouseSensitivity, float gamepadSpeed, float deadZone, bool invertY, float deltaTime)
        {
            var action = activeMap.FindAction("Look");
            var value = session.ReadVector("Look", deadZone);
            value = action.activeControl?.device is Gamepad
                ? value * gamepadSpeed * deltaTime
                : value * mouseSensitivity;
            if (invertY) value.y = -value.y;
            return value;
        }

        /// <summary>读取本帧动作按下边沿。</summary>
        /// <param name="name">Gameplay/Boat 或 UI 动作名。</param>
        public bool Pressed(string name) => !IsRebinding && (activeMap.FindAction(name, false) != null
            ? session.Pressed(name) : ui.FindAction(name, false)?.WasPressedThisFrame() == true);

        /// <summary>读取动作是否持续按住。</summary>
        /// <param name="name">当前玩法或 UI 动作名。</param>
        public bool Held(string name)
        {
            if (IsRebinding) return false;
            if (activeMap.FindAction(name, false) != null) return session.Held(name);
            var action = ui.FindAction(name, false);
            if (action == null || !action.enabled) return false;
            // 绑定回滚会重置动作状态；与 Core 菜单相同，松键门闩读取实际按钮。
            foreach (var control in action.controls)
                if (control is ButtonControl button && button.isPressed) return true;
            return false;
        }

        /// <summary>按当前设备取得绑定提示。</summary>
        /// <param name="name">玩法或 UI 动作名。</param>
        public string BindingLabel(string name)
        {
            var action = activeMap.FindAction(name, false) ?? ui.FindAction(name, false);
            return action?.GetBindingDisplayString(group: IsGamepad ? "Gamepad" : "KeyboardMouse") ?? name;
        }

        /// 导出与设置文件一起保存的绑定覆盖。
        public string SaveBindings() => asset.SaveBindingOverridesAsJson();

        /// <summary>恢复之前保存的按键覆盖。</summary>
        /// <param name="json">Input System 生成的绑定覆盖 JSON。</param>
        public void LoadBindings(string json)
        {
            if (disposed) throw new ObjectDisposedException(nameof(HowToFishInput));
            if (IsRebinding) throw new InvalidOperationException("请先取消正在进行的重绑定。");
            if (!HowToFishLocalPreferences.AreBindingsValid(json)) throw new ArgumentException("绑定覆盖数据无效。", nameof(json));
            string previous = SaveBindings();
            try
            {
                asset.RemoveAllBindingOverrides();
                if (!string.IsNullOrWhiteSpace(json)) asset.LoadBindingOverridesFromJson(json);
            }
            catch
            {
                asset.RemoveAllBindingOverrides();
                if (!string.IsNullOrWhiteSpace(previous)) asset.LoadBindingOverridesFromJson(previous);
                throw;
            }
        }

        /// <summary>重新绑定指定动作的一个绑定，结束后由菜单恢复输入模式。</summary>
        /// <param name="actionPath">映射和动作路径。</param>
        /// <param name="bindingIndex">目标绑定索引，复合绑定应传具体分量。</param>
        /// <param name="finished">完成或取消后的回调。</param>
        public void Rebind(string actionPath, int bindingIndex, Action finished)
            => Rebind(actionPath, bindingIndex, _ => finished?.Invoke(), null, null);

        /// <summary>只捕获目标设备组的按钮；Esc 或手柄 B 取消。调用方负责真实菜单门闩与结束后的模式恢复。</summary>
        /// <param name="actionPath">Gameplay、Boat 或 UI/Pause、UI/Journal 动作路径。</param>
        /// <param name="bindingIndex">具体按钮或键盘移动分量索引，不接受复合根节点或连续轴。</param>
        /// <param name="finished">资源释放后报告结果；冲突及失败详情见 RebindMessage。</param>
        /// <param name="begin">开始捕获前关闭真实菜单导航及交互；不应创建新的 EventSystem。</param>
        /// <param name="end">所有结束路径恢复菜单门闩；应等松键再接受确认，随后由 finished 恢复输入模式。</param>
        public void Rebind(string actionPath, int bindingIndex, Action<HowToFishRebindResult> finished, Action begin, Action end)
        {
            if (disposed) throw new ObjectDisposedException(nameof(HowToFishInput));
            if (rebind != null) throw new InvalidOperationException("已有按键重绑定正在进行。");
            var action = asset.FindAction(actionPath, true);
            if (bindingIndex < 0 || bindingIndex >= action.bindings.Count || action.bindings[bindingIndex].isComposite)
                throw new ArgumentOutOfRangeException(nameof(bindingIndex));
            var binding = action.bindings[bindingIndex];
            bool keyboard = HasGroup(binding.groups, "KeyboardMouse");
            bool gamepad = HasGroup(binding.groups, "Gamepad");
            if (keyboard == gamepad || action.actionMap == ui && action.name != "Pause" && action.name != "Journal" ||
                action.type != InputActionType.Button && !(keyboard && action.name == "Move" && binding.isPartOfComposite))
                throw new ArgumentException("此绑定不支持按钮重绑定。", nameof(bindingIndex));
            RebindMessage = null;
            rebindEnd = end;
            rebindFinished = finished;
            var outcome = HowToFishRebindResult.Completed;
            try
            {
                begin?.Invoke();
                session.SetMap(null);
                rebind = action.PerformInteractiveRebinding(bindingIndex)
                    .WithExpectedControlType("Button")
                    .WithControlsExcluding("<Mouse>/position")
                    .WithControlsExcluding("<Mouse>/delta")
                    .WithControlsExcluding("<Mouse>/scroll")
                    .WithControlsExcluding("<Mouse>/scroll/*")
                    // MatchesPrefix 不支持局部名称通配；具体摇杆路径也排除其方向子控件。
                    .WithControlsExcluding("<Gamepad>/leftStick")
                    .WithControlsExcluding("<Gamepad>/rightStick")
                    .WithCancelingThrough("*/{Cancel}")
                    .WithTimeout(15)
                    .OnApplyBinding((operation, path) =>
                    {
                        try
                        {
                            string conflict = FindConflict(action, bindingIndex, operation.selectedControl, path);
                            if (conflict != null)
                            {
                                outcome = HowToFishRebindResult.Conflict;
                                RebindMessage = "该按键已用于 " + conflict + "，原绑定已保留。";
                                return;
                            }
                            action.ApplyBindingOverride(bindingIndex, path);
                        }
                        catch (Exception exception)
                        {
                            action.ApplyBindingOverride(bindingIndex, binding);
                            outcome = HowToFishRebindResult.Failed;
                            RebindMessage = "重绑定失败：" + exception.Message;
                        }
                    })
                    .OnCancel(_ => FinishRebind(HowToFishRebindResult.Cancelled))
                    .OnComplete(_ => FinishRebind(outcome));
                if (gamepad) rebind.WithControlsHavingToMatchPath("<Gamepad>");
                else
                {
                    rebind.WithControlsHavingToMatchPath("<Keyboard>");
                    if (!binding.isPartOfComposite) rebind.WithControlsHavingToMatchPath("<Mouse>");
                }
                rebind.Start();
            }
            catch (Exception exception)
            {
                RebindMessage = "无法开始重绑定：" + exception.Message;
                FinishRebind(HowToFishRebindResult.Failed);
            }
        }

        /// 取消未完成捕获；完成回调会收到 Cancelled，已完成的重绑定不受影响。
        public void CancelRebind() => rebind?.Cancel();

        /// 取消未完成绑定并释放本会话输入和菜单门闩。
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            try { if (rebind != null) FinishRebind(HowToFishRebindResult.Cancelled); }
            finally
            {
                InputDeviceState.Changed -= OnDeviceChanged;
                session.Dispose();
            }
        }

        private void FinishRebind(HowToFishRebindResult result)
        {
            var operation = rebind;
            var ended = rebindEnd;
            var finished = rebindFinished;
            rebind = null; rebindEnd = null; rebindFinished = null;
            try { operation?.Dispose(); }
            finally
            {
                if (!disposed) ui.Enable();
                try { ended?.Invoke(); }
                finally { finished?.Invoke(result); }
            }
        }

        private string FindConflict(InputAction target, int bindingIndex, InputControl control, string path)
        {
            string group = HasGroup(target.bindings[bindingIndex].groups, "Gamepad") ? "Gamepad" : "KeyboardMouse";
            foreach (var map in asset.actionMaps)
            {
                // 陆地与驾驶互斥；Pause/Journal 则与两者同时生效。
                if (target.actionMap != ui && map != target.actionMap && map != ui) continue;
                foreach (var action in map.actions)
                {
                    if (map == ui && target.actionMap != ui && action.name != "Pause" && action.name != "Journal") continue;
                    for (int i = 0; i < action.bindings.Count; i++)
                    {
                        var other = action.bindings[i];
                        if (action == target && i == bindingIndex || other.isComposite || !HasGroup(other.groups, group) ||
                            string.IsNullOrEmpty(other.effectivePath)) continue;
                        if (string.Equals(other.effectivePath, path, StringComparison.OrdinalIgnoreCase) ||
                            control != null && InputControlPath.Matches(other.effectivePath, control))
                            return map.name + "/" + action.name + (other.isPartOfComposite ? "/" + other.name : "");
                    }
                }
            }
            return null;
        }

        private static bool HasGroup(string groups, string group)
            => Array.Exists((groups ?? "").Split(';'), value => string.Equals(value, group, StringComparison.OrdinalIgnoreCase));

        private void OnDeviceChanged() => DeviceChanged?.Invoke();

        private static void AddMovement(InputActionMap map)
        {
            var movement = map.AddAction("Move", InputActionType.Value, expectedControlLayout: "Vector2");
            movement.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w", groups: "KeyboardMouse").With("Down", "<Keyboard>/s", groups: "KeyboardMouse")
                .With("Left", "<Keyboard>/a", groups: "KeyboardMouse").With("Right", "<Keyboard>/d", groups: "KeyboardMouse");
            movement.AddBinding("<Gamepad>/leftStick", groups: "Gamepad");
            var look = map.AddAction("Look", InputActionType.Value, expectedControlLayout: "Vector2");
            look.AddBinding("<Mouse>/delta", groups: "KeyboardMouse");
            look.AddBinding("<Gamepad>/rightStick", groups: "Gamepad");
            AddButton(map, "Interact", "<Keyboard>/e", "<Gamepad>/buttonWest");
        }

        private static void AddButton(InputActionMap map, string name, string keyboard, string controller)
        {
            var action = map.AddAction(name, InputActionType.Button);
            action.AddBinding(keyboard, groups: "KeyboardMouse");
            action.AddBinding(controller, groups: "Gamepad");
        }

    }
}
