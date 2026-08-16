using System;
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
        [SerializeField, InspectorName("输入配置")]
        [Tooltip("集中管理键盘平滑和长按复位参数。")]
        private DroneInputConfig config;

        private DroneFlightController controller;
        private Vector4 smoothedKeyboardInput;
        private DroneResetHoldTracker resetHoldTracker;
        private bool reloadRequestSent;

        /// 长按达到配置时间后发送一次；输入组件不直接切换场景。
        internal event Action ReloadRequested;

        /// 长按 R 的归一化复位进度。
        internal float ResetProgress => resetHoldTracker?.Progress ?? 0f;

        /// 长按重载所需时间，单位秒。
        internal float ResetHoldSeconds => config != null ? config.ResetHoldSeconds : 5f;

        private void Awake()
        {
            controller = GetComponent<DroneFlightController>();
            resetHoldTracker = new DroneResetHoldTracker(ResetHoldSeconds);
        }

        private void Update()
        {
            if (controller == null)
            {
                return;
            }

            var keyboard = Keyboard.current;
            HandleResetInput(keyboard);

            var targetKeyboard = ReadKeyboard(keyboard);
            var fallbackRiseRate = config != null ? config.KeyboardFallbackRiseRate : 3f;
            var riseRate = controller.InputRiseRate > 0f ? controller.InputRiseRate : fallbackRiseRate;
            smoothedKeyboardInput = new Vector4(
                StepKeyboardAxis(smoothedKeyboardInput.x, targetKeyboard.x, riseRate),
                StepKeyboardAxis(smoothedKeyboardInput.y, targetKeyboard.y, riseRate),
                StepKeyboardAxis(smoothedKeyboardInput.z, targetKeyboard.z, riseRate),
                StepKeyboardAxis(smoothedKeyboardInput.w, targetKeyboard.w, riseRate));

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

        /// 清空键盘平滑输入，避免复位后残留移动命令。
        internal void ResetBufferedInput()
        {
            smoothedKeyboardInput = Vector4.zero;
            controller?.SetControlInput(default);
        }

        private void HandleResetInput(Keyboard keyboard)
        {
            if (keyboard == null || resetHoldTracker == null)
            {
                return;
            }

            if (keyboard.rKey.wasPressedThisFrame)
            {
                resetHoldTracker.Begin();
                reloadRequestSent = false;
            }

            if (keyboard.rKey.isPressed
                && resetHoldTracker.Step(Time.unscaledDeltaTime))
            {
                if (!reloadRequestSent)
                {
                    reloadRequestSent = true;
                    ResetBufferedInput();
                    ReloadRequested?.Invoke();
                }
            }

            if (keyboard.rKey.wasReleasedThisFrame
                && resetHoldTracker.Release() == DroneResetReleaseResult.ShortPress)
            {
                controller.SetArmed(!controller.IsArmed);
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

        private float StepKeyboardAxis(float current, float target, float riseRate)
        {
            var fallRate = config != null ? config.KeyboardFallRate : 5f;
            var rate = Mathf.Approximately(target, 0f) ? fallRate : riseRate;
            return Mathf.MoveTowards(current, target, Mathf.Max(0f, rate) * Time.unscaledDeltaTime);
        }
    }
}
