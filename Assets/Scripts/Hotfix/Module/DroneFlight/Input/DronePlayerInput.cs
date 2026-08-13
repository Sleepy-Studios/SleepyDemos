using UnityEngine;
using UnityEngine.InputSystem;

namespace Hotfix.DroneFlight
{
    /// <summary>
    /// MVP 键盘和手柄输入适配器，只向飞控提交统一归一化输入。
    /// </summary>
    [RequireComponent(typeof(DroneFlightController))]
    public sealed class DronePlayerInput : MonoBehaviour
    {
        [SerializeField] private float keyboardRiseRate = 3f;
        [SerializeField] private float keyboardFallRate = 5f;

        private DroneFlightController controller;
        private Vector4 smoothedKeyboardInput;

        private void Awake()
        {
            controller = GetComponent<DroneFlightController>();
        }

        private void Update()
        {
            if (controller == null)
            {
                return;
            }

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
            {
                controller.SetArmed(!controller.IsArmed);
            }

            var targetKeyboard = ReadKeyboard(keyboard);
            var riseRate = controller.InputRiseRate > 0f ? controller.InputRiseRate : keyboardRiseRate;
            var rate = targetKeyboard == Vector4.zero ? keyboardFallRate : riseRate;
            smoothedKeyboardInput = Vector4.MoveTowards(
                smoothedKeyboardInput,
                targetKeyboard,
                Mathf.Max(0f, rate) * Time.unscaledDeltaTime);

            var gamepadInput = ReadGamepad(Gamepad.current);
            var input = gamepadInput.sqrMagnitude > 0.0001f ? gamepadInput : smoothedKeyboardInput;
            controller.SetControlInput(DroneControlInput.Create(input.x, input.y, input.z, input.w));

            if (keyboard == null)
            {
                return;
            }

            if (keyboard.tKey.wasPressedThisFrame)
            {
                controller.BeginAutomaticTakeoff();
            }
            else if (keyboard.gKey.wasPressedThisFrame)
            {
                controller.BeginAutomaticLanding();
            }

            if (keyboard.digit1Key.wasPressedThisFrame)
            {
                controller.SetResponseProfile(DroneResponseProfile.Cine);
            }
            else if (keyboard.digit2Key.wasPressedThisFrame)
            {
                controller.SetResponseProfile(DroneResponseProfile.Normal);
            }
            else if (keyboard.digit3Key.wasPressedThisFrame)
            {
                controller.SetResponseProfile(DroneResponseProfile.Sport);
            }
        }

        private static Vector4 ReadKeyboard(Keyboard keyboard)
        {
            if (keyboard == null)
            {
                return Vector4.zero;
            }

            var lift = ReadButtonAxis(keyboard.leftCtrlKey.isPressed, keyboard.spaceKey.isPressed);
            var yaw = ReadButtonAxis(keyboard.qKey.isPressed, keyboard.eKey.isPressed);
            var forward = ReadButtonAxis(keyboard.sKey.isPressed, keyboard.wKey.isPressed);
            var right = ReadButtonAxis(keyboard.aKey.isPressed, keyboard.dKey.isPressed);
            return new Vector4(lift, yaw, forward, right);
        }

        private static Vector4 ReadGamepad(Gamepad gamepad)
        {
            if (gamepad == null)
            {
                return Vector4.zero;
            }

            var leftStick = gamepad.leftStick.ReadValue();
            var rightStick = gamepad.rightStick.ReadValue();
            return new Vector4(leftStick.y, leftStick.x, rightStick.y, rightStick.x);
        }

        private static float ReadButtonAxis(bool negative, bool positive)
        {
            return (positive ? 1f : 0f) - (negative ? 1f : 0f);
        }
    }
}
