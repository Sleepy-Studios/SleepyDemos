using Hotfix.DroneFlight;
using NUnit.Framework;

namespace Hotfix.Tests
{
    /*
     * 测试说明：验证玩家 HUD、操作提示、中文调试文本和告警优先级，确保不同机型只显示适用的信息。
     */
    public sealed class DroneHudFormatterTests
    {
        [Test]
        public void PlayerHud_ContainsFlightCameraAndPayloadTelemetry()
        {
            var snapshot = CreateSnapshot(hasPayload: true, PayloadReleaseReason.None, saturated: false);

            StringAssert.Contains("普通（Normal）", DroneHudFormatter.FormatFlight(snapshot));
            StringAssert.Contains("高度 1.5 m", DroneHudFormatter.FormatFlight(snapshot));
            StringAssert.Contains("Gimbal", DroneHudFormatter.FormatCamera(snapshot));
            StringAssert.Contains("BaitDropper", DroneHudFormatter.FormatPayload(snapshot));
            StringAssert.Contains("0.35 kg", DroneHudFormatter.FormatPayload(snapshot));
        }

        [Test]
        public void Controls_ContainsFlightCameraResetWinchAndDebugBindings()
        {
            var controls = DroneHudFormatter.FormatControls();

            StringAssert.Contains("长按重新运行场景", controls);
            StringAssert.Contains("J  抓斗收放", controls);
            StringAssert.Contains("L  起落架收放", controls);
            StringAssert.Contains("H  四爪开合", controls);
            StringAssert.DoesNotContain("RT", controls);
            StringAssert.Contains("C  切换视角", controls);
            StringAssert.Contains("F3  调试面板", controls);
            StringAssert.Contains("Backspace  返回主界面", controls);
        }

        [Test]
        public void PlainDroneControls_HideEquipmentBindings()
        {
            var controls = DroneHudFormatter.FormatControls(DroneEquipmentKind.None);

            StringAssert.Contains("L  起落架收放", controls);
            StringAssert.DoesNotContain("抓斗", controls);
            StringAssert.DoesNotContain("渔叉", controls);
            StringAssert.DoesNotContain("J / K", controls);
            StringAssert.DoesNotContain("四爪开合", controls);
        }

        [Test]
        public void DebugText_UsesChinesePrimaryLabelsAndBilingualAbbreviations()
        {
            var text = DroneDebugFormatter.Format(
                default,
                default,
                default,
                default,
                12f,
                0.02f,
                DroneLandingGearState.Deployed,
                DroneWinchState.Stowed,
                DroneGrappleState.Open,
                0);

            StringAssert.Contains("电机 左前(FL)", text);
            StringAssert.Contains("横滚(Roll) 误差", text);
            StringAssert.Contains("比例P", text);
            StringAssert.Contains("姿态缩放", text);
            StringAssert.Contains("物理步长", text);
            StringAssert.Contains("有效接触", text);
            StringAssert.Contains("动力模式", text);
            StringAssert.Contains("额定载重", text);
            StringAssert.Contains("当前真实载荷", text);
            StringAssert.Contains("飞控承载载荷", text);
            StringAssert.Contains("载荷支撑", text);
            StringAssert.Contains("整机恒定总质量", text);
            StringAssert.Contains("机载抓斗设备", text);
            StringAssert.Contains("地面支持力", text);
            StringAssert.Contains("软约束接入", text);
            StringAssert.Contains("单摆吊索", text);
            StringAssert.Contains("长度", text);
            StringAssert.Contains("主动防摆", text);
            StringAssert.Contains("被动阻尼扭矩", text);
            StringAssert.Contains("理论悬停指令", text);
            StringAssert.Contains("动力余量", text);
            StringAssert.DoesNotContain("Motor FL", text);
            StringAssert.DoesNotContain("fixedDeltaTime", text);
        }

        [Test]
        public void Warning_PrioritizesFaultThenOverloadThenMotorSaturation()
        {
            var overload = CreateSnapshot(false, PayloadReleaseReason.Overload, saturated: true);
            StringAssert.Contains("载荷超重", DroneHudFormatter.FormatWarning(overload));

            var saturation = CreateSnapshot(false, PayloadReleaseReason.None, saturated: true);
            StringAssert.Contains("电机输出饱和", DroneHudFormatter.FormatWarning(saturation));
        }

        private static DroneHudSnapshot CreateSnapshot(
            bool hasPayload,
            PayloadReleaseReason releaseReason,
            bool saturated)
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
                45f,
                hasPayload,
                "BaitDropper",
                0.35f,
                releaseReason);
        }
    }
}
