using Hotfix.DroneFlight;
using NUnit.Framework;

namespace Tests.Demo
{
    /*
     * 测试说明：验证玩家 HUD、操作提示和当前飞控告警，确保不同机型只显示适用的信息。
     */
    public sealed class DroneHudFormatterTests
    {
        [Test]
        public void PlayerHud_ContainsFlightAndCameraTelemetry()
        {
            var snapshot = CreateSnapshot(saturated: false);

            StringAssert.Contains("普通（Normal）", DroneHudFormatter.FormatFlight(snapshot));
            StringAssert.Contains("H 1.5 m", DroneHudFormatter.FormatFlight(snapshot));
            StringAssert.Contains("Gimbal", DroneHudFormatter.FormatCamera(snapshot));
        }

        [Test]
        public void Controls_ContainsFlightCameraEquipmentAndDebugBindings()
        {
            var controls = DroneHudFormatter.FormatControls(DroneEquipmentKind.Grapple, Key);

            StringAssert.Contains("长按重新运行场景", controls);
            StringAssert.DoesNotContain("F  开始控制", controls);
            StringAssert.DoesNotContain("抓斗收放", controls);
            StringAssert.Contains("LandingGear / 操作面板  起落架收放", controls);
            StringAssert.Contains("Equipment  四爪开合", controls);
                        StringAssert.Contains("SwitchCamera  切换视角", controls);
            StringAssert.Contains("DebugDraw  动力矢量", controls);
            StringAssert.Contains("DebugPanel  调试面板", controls);
            StringAssert.Contains("CopyTelemetry  复制遥测", controls);
            StringAssert.Contains("Exit  返回主界面", controls);
        }

        [Test]
        public void PlainDroneControls_HideEquipmentBindings()
        {
            var controls = DroneHudFormatter.FormatControls(DroneEquipmentKind.None, Key);

            StringAssert.Contains("LandingGear / 操作面板  起落架收放", controls);
            StringAssert.DoesNotContain("抓斗", controls);
            StringAssert.DoesNotContain("渔叉", controls);
            StringAssert.DoesNotContain("J / K", controls);
            StringAssert.DoesNotContain("四爪开合", controls);
        }

        [Test]
        public void Warning_ReportsMotorSaturation()
        {
            var saturation = CreateSnapshot(saturated: true);
            StringAssert.Contains("电机输出饱和", DroneHudFormatter.FormatWarning(saturation));
        }

        [Test]
        public void Controls_AreSplitIntoDeterministicSections()
        {
            StringAssert.Contains("Move", DroneHudFormatter.FormatFlightControls(Key));
            StringAssert.Contains("SwitchCamera  切换视角", DroneHudFormatter.FormatCameraControls(Key));
            StringAssert.Contains("DebugDraw  动力矢量", DroneHudFormatter.FormatSystemControls(Key));
            StringAssert.Contains("Equipment  四爪开合",
                DroneHudFormatter.FormatEquipmentControls(DroneEquipmentKind.Grapple, Key));
        }

        [Test]
        public void HarpoonControls_ContainAimAndDedicatedLineBindings()
        {
            var controls = DroneHudFormatter.FormatControls(DroneEquipmentKind.Harpoon, Key);

            StringAssert.Contains("Aim  机腹瞄准", controls);
            StringAssert.Contains("Line  收线 / 放线", controls);
        }

        private static string Key(string name) => name;

        private static DroneHudSnapshot CreateSnapshot(bool saturated)
        {
            return new DroneHudSnapshot(
                DroneFlightOperationState.Flying,
                DroneResponseProfile.Normal,
                true,
                0.25f,
                1.5f,
                2f,
                -0.1f,
                8f,
                saturated,
                DroneCameraMode.Gimbal,
                10f,
                -30f,
                45f);
        }
    }
}
