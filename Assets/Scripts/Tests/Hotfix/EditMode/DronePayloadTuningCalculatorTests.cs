using System.IO;
using Hotfix.DroneFlight;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Hotfix.Tests
{
    /*
     * 测试说明：验证载荷质量、悬停指令、动力储备、最大载荷门禁及自动/手动调参映射和中文诊断。
     */
    public sealed class DronePayloadTuningCalculatorTests
    {
        [Test]
        public void DefaultOneKilogramConfiguration_DerivesExpectedMassGateAndHover()
        {
            var result = Calculate(1f, 1.25f, 0.9f);

            Assert.That(result.IsValid, Is.True, result.Diagnostic);
            Assert.That(result.BodyMassKilograms, Is.EqualTo(1.2f).Within(0.0001f));
            Assert.That(result.MaximumPayloadKilograms, Is.EqualTo(1.25f).Within(0.0001f));
            Assert.That(result.RatedOperatingMassKilograms, Is.EqualTo(2.2f).Within(0.0001f));
            Assert.That(result.ThrustCoefficient, Is.GreaterThan(0f));
            Assert.That(result.RatedPowerReserve, Is.EqualTo(0.1f).Within(0.0001f));
        }

        [Test]
        public void RatedOperatingMass_HoverCommandEqualsConfiguredCommand()
        {
            var result = Calculate(1f, 1.25f, 0.9f);
            var command = DronePayloadTuningCalculator.CalculateHoverCommand(
                result.RatedOperatingMassKilograms, 9.81f, 10000f, result.ThrustCoefficient);

            Assert.That(command, Is.EqualTo(0.9f).Within(0.0001f));
        }

        [Test]
        public void SameConfiguration_HeavierPayloadNeedsMoreCommand()
        {
            var result = Calculate(1f, 1.25f, 0.9f);
            var light = Hover(result, 1.2f + 0.25f);
            var heavy = Hover(result, 1.2f + 0.95f);

            Assert.That(heavy, Is.GreaterThan(light));
            Assert.That(heavy, Is.GreaterThan(0.85f));
        }

        [Test]
        public void TenKilogramRatedPayload_HasMuchMoreReserveForSameCargo()
        {
            var one = Calculate(1f, 1.25f, 0.9f);
            var ten = Calculate(10f, 1.25f, 0.9f);
            var oneCommand = Hover(one, one.BodyMassKilograms + 0.95f);
            var tenCommand = Hover(ten, ten.BodyMassKilograms + 0.95f);

            Assert.That(oneCommand - tenCommand, Is.GreaterThan(0.15f));
        }

        [Test]
        public void MaximumPayloadMultiplier_ChangesGateButNotRatedPhysics()
        {
            var low = Calculate(1f, 1f, 0.9f);
            var high = Calculate(1f, 1.5f, 0.9f);

            Assert.That(high.MaximumPayloadKilograms, Is.GreaterThan(low.MaximumPayloadKilograms));
            Assert.That(high.BodyMassKilograms, Is.EqualTo(low.BodyMassKilograms));
            Assert.That(high.ThrustCoefficient, Is.EqualTo(low.ThrustCoefficient));
        }

        [TestCase(0f, "额定载重")]
        [TestCase(float.NaN, "额定载重")]
        public void InvalidRatedPayload_ReturnsChineseDiagnostic(float ratedPayload, string expected)
        {
            var result = Calculate(ratedPayload, 1.25f, 0.9f);

            Assert.That(result.IsValid, Is.False);
            StringAssert.Contains(expected, result.Diagnostic);
        }

        [Test]
        public void MotorResponsivenessMapping_IsBounded()
        {
            Assert.That(DronePayloadTuningCalculator.MapMotorResponsivenessToResponseTime(100f), Is.GreaterThan(0f));
            Assert.That(
                DronePayloadTuningCalculator.MapMotorResponsivenessToResponseTime(100f),
                Is.LessThan(DronePayloadTuningCalculator.MapMotorResponsivenessToResponseTime(0f)));
        }

        [Test]
        public void Inspector_ContainsBasicAdvancedLanguageAndAllNewFriendlyFields()
        {
            const string path = "Assets/Scripts/Hotfix/Editor/DroneFlight/DroneFlightConfigEditor.cs";
            var source = File.ReadAllText(Path.GetFullPath(path));

            StringAssert.Contains("普通设置", source);
            StringAssert.Contains("高级设置", source);
            StringAssert.Contains("中文", source);
            StringAssert.Contains("English", source);
            foreach (var field in new[]
                     {
                         "ratedPayloadKilograms", "maximumPayloadMultiplier", "ratedPayloadHoverCommand",
                         "motorResponsiveness", "bodyMassMultiplier", "automaticTakeoffHeightMeters",
                         "automaticLandingSpeedMetersPerSecond", "defaultResponseProfile", "resetHoldSeconds"
                     })
            {
                StringAssert.Contains(field, source, $"Inspector 缺少字段 {field} 的双层展示契约。");
            }

            Assert.That(AssetDatabase.LoadAssetAtPath<MonoScript>(path), Is.Not.Null);
        }

        [Test]
        public void Config_AutomaticAndManualModesUseExplicitEffectiveParametersWithoutChangingProfiles()
        {
            var config = ScriptableObject.CreateInstance<DroneFlightConfig>();
            try
            {
                var profile = config.GetProfile(DroneResponseProfile.Normal);
                config.ConfigureAutomaticPayloadTuning(10f, 1.25f, 0.9f);
                Assert.That(config.BodyMassKilograms, Is.EqualTo(12f).Within(0.0001f));
                Assert.That(config.MaximumPayloadMassKilograms, Is.EqualTo(12.5f).Within(0.0001f));

                config.ConfigureManualPhysics(10f, 3f, 8000f, 0.0000002f, 0.12f);
                Assert.That(config.BodyMassKilograms, Is.EqualTo(3f));
                Assert.That(config.MaximumRpm, Is.EqualTo(8000f));
                Assert.That(config.ThrustCoefficient, Is.EqualTo(0.0000002f));
                Assert.That(config.MotorResponseTimeSeconds, Is.EqualTo(0.12f));
                Assert.That(config.GetProfile(DroneResponseProfile.Normal).MaximumHorizontalSpeed,
                    Is.EqualTo(profile.MaximumHorizontalSpeed));
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        private static DronePayloadTuningResult Calculate(float ratedPayload, float maximumMultiplier, float hover)
        {
            return DronePayloadTuningCalculator.Calculate(new DronePayloadTuningInput(
                ratedPayload,
                1.2f,
                maximumMultiplier,
                hover,
                10000f,
                9.81f));
        }

        private static float Hover(DronePayloadTuningResult result, float totalMass)
        {
            return DronePayloadTuningCalculator.CalculateHoverCommand(
                totalMass, 9.81f, 10000f, result.ThrustCoefficient);
        }
    }
}
