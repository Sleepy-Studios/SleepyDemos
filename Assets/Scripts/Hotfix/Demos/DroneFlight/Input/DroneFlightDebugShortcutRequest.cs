namespace Hotfix.DroneFlight
{
    /// <summary>单帧 F2/F3 调试快捷键请求，避免两个显示开关互相耦合。</summary>
    internal readonly struct DroneFlightDebugShortcutRequest
    {
        private DroneFlightDebugShortcutRequest(bool toggleDraw, bool togglePanel)
        {
            ToggleDraw = toggleDraw;
            TogglePanel = togglePanel;
        }

        /// 是否切换 Game View 动力矢量。
        internal bool ToggleDraw { get; }

        /// 是否切换调试数据面板。
        internal bool TogglePanel { get; }

        /// <summary>把本帧按键边沿转换为两个互不影响的调试请求。</summary>
        /// <param name="f2Pressed">本帧是否按下 F2。</param>
        /// <param name="f3Pressed">本帧是否按下 F3。</param>
        internal static DroneFlightDebugShortcutRequest FromPressedKeys(bool f2Pressed, bool f3Pressed) =>
            new(f2Pressed, f3Pressed);
    }
}
