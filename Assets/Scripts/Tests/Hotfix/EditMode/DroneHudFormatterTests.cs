using Hotfix.DroneFlight;
using NUnit.Framework;

namespace Hotfix.Tests
{
    public sealed class DroneHudFormatterTests
    {
        [Test]
        public void PlayerHud_ContainsFlightCameraAndPayloadTelemetry()
        {
            var snapshot = CreateSnapshot(hasPayload: true, PayloadReleaseReason.None, saturated: false);

            StringAssert.Contains("Normal", DroneHudFormatter.FormatFlight(snapshot));
            StringAssert.Contains("高度 1.5 m", DroneHudFormatter.FormatFlight(snapshot));
            StringAssert.Contains("Gimbal", DroneHudFormatter.FormatCamera(snapshot));
            StringAssert.Contains("BaitDropper", DroneHudFormatter.FormatPayload(snapshot));
            StringAssert.Contains("0.35 kg", DroneHudFormatter.FormatPayload(snapshot));
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
