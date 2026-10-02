using UnityEngine;
using UnityEngine.InputSystem;

namespace Hotfix.JinxCasino.Adapters.Input
{
    /// Demo专属Action Asset定义。Editor保存ToJson为inputactions，运行时Router仅克隆保存资产。
    public static class JinxCasinoInputAsset
    {
        /// 创建尚未保存且禁用的输入资产；调用者负责保存ToJson和销毁此临时对象。
        public static InputActionAsset Create()
        {
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            asset.name = "JinxCasinoImmersion";
            var exploration = asset.AddActionMap("Exploration");
            AddKeyboardVector(exploration.AddAction("Move", InputActionType.Value, expectedControlLayout: "Vector2"), true);
            exploration.AddAction("PadMove", InputActionType.Value, "<Gamepad>/leftStick", expectedControlLayout: "Vector2");
            exploration.AddAction("MouseLook", InputActionType.Value, "<Mouse>/delta", expectedControlLayout: "Vector2");
            AddButton(exploration, "LookHold", "<Mouse>/rightButton");
            exploration.AddAction("PadLook", InputActionType.Value, "<Gamepad>/rightStick", expectedControlLayout: "Vector2");
            AddButton(exploration, "Interact", "<Keyboard>/e", "<Gamepad>/buttonSouth");
            AddButton(exploration, "Pause", "<Keyboard>/escape", "<Gamepad>/start");
            var table = asset.AddActionMap("Table");
            AddKeyboardVector(table.AddAction("Navigate", InputActionType.Value, expectedControlLayout: "Vector2"), false);
            var padNavigate = table.AddAction("PadNavigate", InputActionType.Value, expectedControlLayout: "Vector2");
            padNavigate.AddBinding("<Gamepad>/leftStick"); padNavigate.AddBinding("<Gamepad>/dpad");
            AddButton(table, "Confirm", "<Keyboard>/enter", "<Keyboard>/space", "<Gamepad>/buttonSouth");
            AddButton(table, "Back", "<Keyboard>/escape", "<Gamepad>/buttonEast");
            AddButton(table, "Secondary", "<Keyboard>/r", "<Gamepad>/buttonWest");
            AddButton(table, "Help", "<Keyboard>/h", "<Gamepad>/buttonNorth");
            AddButton(table, "PreviousGroup", "<Keyboard>/q", "<Gamepad>/leftShoulder");
            AddButton(table, "NextGroup", "<Keyboard>/e", "<Gamepad>/rightShoulder");
            AddButton(table, "Pause", "<Gamepad>/start");
            var menu = asset.AddActionMap("Menu");
            // Menu的Submit/Cancel/Navigate由Core现有InputSystemUIInputModule唯一处理。
            AddButton(menu, "Pause", "<Gamepad>/start");
            return asset;
        }

        private static void AddButton(InputActionMap map, string name, params string[] paths)
        {
            var action = map.AddAction(name, InputActionType.Button, interactions: "Press(behavior=0)", expectedControlLayout: "Button");
            foreach (string path in paths) action.AddBinding(path);
        }

        private static void AddKeyboardVector(InputAction action, bool wasd)
        {
            var binding = action.AddCompositeBinding("2DVector");
            binding.With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            if (wasd) binding.With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
        }
    }
}
