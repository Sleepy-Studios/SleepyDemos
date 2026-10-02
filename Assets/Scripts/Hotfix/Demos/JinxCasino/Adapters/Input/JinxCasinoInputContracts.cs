using System;
using UnityEngine;

namespace Hotfix.JinxCasino.Adapters.Input
{
    public enum JinxCasinoInputContext { Exploration = 0, Table = 1, Menu = 2 }
    public enum JinxCasinoInputDeviceKind { KeyboardMouse = 0, Gamepad = 1, Touch = 2 }

    [Flags]
    public enum JinxCasinoInputActions
    {
        None = 0, Interact = 1, Confirm = 2, Back = 4, Secondary = 8,
        Help = 16, PreviousGroup = 32, NextGroup = 64, Pause = 128
    }

    [Flags]
    public enum JinxCasinoPauseReason
    {
        None = 0, User = 1, FocusLost = 2, Background = 4, GamepadDisconnected = 8
    }

    /// 本帧连续量。LookDegrees已经换算为角度，宿主不可再次乘deltaTime或旧视角系数。
    public struct JinxCasinoInputFrame
    {
        public Vector2 Move;
        public Vector2 LookDegrees;
        public Vector2 TableNavigation;
        /// 桌面指针的屏幕像素位置，供本地相机生成真实物理射线。
        public Vector2 PointerPosition;
        /// 本次读取是否收到一次真实鼠标/触屏按下；读取后消费，不模拟菜单Submit。
        public bool PointerPressed;
        /// 此帧真实指针移动；静止鼠标不能覆盖键盘/手柄选中的物件。
        public bool PointerMoved;
        public JinxCasinoInputDeviceKind DeviceKind;
        public bool IsPaused;
    }

    /// 输入适配参数。持久化时并入既有本机偏好迁移，不建立第二套存档或PlayerPrefs键。
    [Serializable]
    public sealed class JinxCasinoInputSettings
    {
        public int SchemaVersion = 1;
        public float GamepadDeadzone = 0.2f;
        public float GamepadMaximum = 1;
        public float GamepadLookDegreesPerSecond = 90;
        public float GamepadLookMultiplier = 1;
        public bool GamepadInvertY;
        public bool RumbleEnabled = true;
        public float RumbleStrength = 1;
        public float MouseLookMultiplier = 1;
        public float TouchLookMultiplier = 1;

        /// 独立候选副本，设置预览不能修改已生效对象。
        public JinxCasinoInputSettings Copy() => (JinxCasinoInputSettings)MemberwiseClone();

        /// 有限值和版本校验。无效配置拒绝应用，宿主加载损坏记录时使用完整默认值。
        public bool IsValid => SchemaVersion == 1 && Range(GamepadDeadzone, 0, 0.45f)
            && Range(GamepadMaximum, 0.65f, 1) && GamepadMaximum > GamepadDeadzone
            && Range(GamepadLookDegreesPerSecond, 30, 240) && Range(GamepadLookMultiplier, 0.25f, 3)
            && Range(RumbleStrength, 0, 1) && Range(MouseLookMultiplier, 0.25f, 3) && Range(TouchLookMultiplier, 0.25f, 3);

        private static bool Range(float value, float minimum, float maximum)
            => !float.IsNaN(value) && !float.IsInfinity(value) && value >= minimum && value <= maximum;
    }

    /// 连续输入换算。鼠标/触控是位移增量，手柄是每秒角速度，绝不共用deltaTime语义。
    public static class JinxCasinoInputMath
    {
        /// <summary>对未经处理的二维摇杆应用一次径向死区，不改变InputSystem全局处理器。</summary>
        /// <param name="raw">从绑定的StickControl读取的未处理值。</param>
        /// <param name="minimum">0至0.45之间的死区半径。</param>
        /// <param name="maximum">大于minimum的最大半径，推荐1。</param>
        public static Vector2 ApplyDeadzone(Vector2 raw, float minimum, float maximum)
        {
            if (!Finite(raw.x) || !Finite(raw.y) || !Finite(minimum) || !Finite(maximum) || minimum < 0 || maximum <= minimum)
                throw new ArgumentException("摇杆与死区必须是有限的合法值。");
            float magnitude = raw.magnitude;
            if (magnitude <= minimum) return Vector2.zero;
            return raw / magnitude * Mathf.Clamp01((magnitude - minimum) / (maximum - minimum));
        }

        /// <summary>把独立设备的视角输入换算为本帧角度；不改变宿主yaw/pitch限制。</summary>
        /// <param name="mouseDelta">按住视角键时的鼠标像素增量。</param>
        /// <param name="touchDelta">已有TouchPad提供的720p参考像素增量。</param>
        /// <param name="gamepadAxis">已经过单次死区处理的归一化手柄轴。</param>
        /// <param name="deltaSeconds">非负有限的未缩放帧时长；只有手柄角速度乘此值。</param>
        /// <param name="mouseDegreesPerPixel">既有GameSettings基础系数，默认0.12。</param>
        /// <param name="settings">已通过IsValid的独立参数。</param>
        public static Vector2 LookDegrees(Vector2 mouseDelta, Vector2 touchDelta, Vector2 gamepadAxis,
            float deltaSeconds, float mouseDegreesPerPixel, JinxCasinoInputSettings settings)
        {
            if (settings == null || !settings.IsValid || !Finite(deltaSeconds) || deltaSeconds < 0
                || !Finite(mouseDegreesPerPixel) || mouseDegreesPerPixel < 0
                || !Finite(mouseDelta.x) || !Finite(mouseDelta.y) || !Finite(touchDelta.x) || !Finite(touchDelta.y)
                || !Finite(gamepadAxis.x) || !Finite(gamepadAxis.y)) throw new ArgumentException("视角参数非法。");
            if (settings.GamepadInvertY) gamepadAxis.y = -gamepadAxis.y;
            return (mouseDelta * settings.MouseLookMultiplier + touchDelta * settings.TouchLookMultiplier) * mouseDegreesPerPixel
                + gamepadAxis * (settings.GamepadLookDegreesPerSecond * settings.GamepadLookMultiplier * deltaSeconds);
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
