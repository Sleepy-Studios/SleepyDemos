using Cysharp.Threading.Tasks;
using Hotfix.SceneManagement;
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
        private DroneResetHoldTracker resetHoldTracker;
        private bool isReloadingScene;

        /// 长按 R 的归一化复位进度。
        internal float ResetProgress => resetHoldTracker?.Progress ?? 0f;

        private void Awake()
        {
            controller = GetComponent<DroneFlightController>();
            var holdSeconds = controller != null && controller.Config != null
                ? controller.Config.ResetHoldSeconds
                : 5f;
            resetHoldTracker = new DroneResetHoldTracker(holdSeconds);
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
            }

            if (keyboard.rKey.isPressed
                && resetHoldTracker.Step(Time.unscaledDeltaTime))
            {
                ResetBufferedInput();
                ReloadCurrentSceneAsync().Forget();
            }

            if (keyboard.rKey.wasReleasedThisFrame
                && resetHoldTracker.Release() == DroneResetReleaseResult.ShortPress)
            {
                controller.SetArmed(!controller.IsArmed);
            }
        }

        private async UniTaskVoid ReloadCurrentSceneAsync()
        {
            if (isReloadingScene)
            {
                return;
            }

            var navigator = GameSceneNavigator.Instance;
            if (navigator == null)
            {
                Debug.LogError("[DroneFlight] 全局场景导航尚未初始化，无法重新运行场景。", this);
                return;
            }

            isReloadingScene = true;
            var result = await navigator.ReloadCurrentAsync();
            if (result.Status == GameSceneSwitchStatus.Failed)
            {
                isReloadingScene = false;
                Debug.LogError($"[DroneFlight] 重新运行场景失败：{result.Error}", this);
            }
            else if (result.Status is GameSceneSwitchStatus.Busy or GameSceneSwitchStatus.Ignored)
            {
                isReloadingScene = false;
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
