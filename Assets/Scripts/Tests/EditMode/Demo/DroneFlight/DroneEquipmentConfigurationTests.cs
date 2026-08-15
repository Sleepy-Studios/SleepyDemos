using Hotfix.DroneFlight;
using Hotfix.Editor.DroneFlight;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.Demo
{
    /*
     * 测试说明：验证紧凑抓斗、渔叉的默认配置、质量守恒、装备宿主边界以及双语配置 Inspector 页面。
     */
    public sealed class DroneEquipmentConfigurationTests
    {
        [Test]
        public void Grapple_DefaultsAreCompactFourClawValues()
        {
            var config = ScriptableObject.CreateInstance<DroneGrappleConfig>();
            try
            {
                Assert.That(config.TryValidate(out var diagnostic), Is.True, diagnostic);
                Assert.That(config.HardwareMassKilograms, Is.EqualTo(0.05f).Within(0.0001f));
                Assert.That(config.StowedDistanceMeters, Is.EqualTo(0.08f).Within(0.0001f));
                Assert.That(config.DeployedDistanceMeters, Is.EqualTo(0.26f).Within(0.0001f));
                Assert.That(config.TwistLimitDegrees, Is.EqualTo(25f));
                Assert.That(config.SwingLimitDegrees, Is.EqualTo(35f));
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void Harpoon_DefaultsHaveConservedHardwareMassAndBreakableTension()
        {
            var config = ScriptableObject.CreateInstance<DroneHarpoonConfig>();
            try
            {
                Assert.That(config.TryValidate(out var diagnostic), Is.True, diagnostic);
                Assert.That(config.HardwareMassKilograms, Is.GreaterThan(config.ProjectileMassKilograms));
                Assert.That(config.RopeBreakForceNewtons, Is.GreaterThan(config.MaximumTensionNewtons));
                Assert.That(config.MaximumRopeLengthMeters, Is.GreaterThan(config.MinimumRopeLengthMeters));
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void EquipmentInspectors_UseExpectedCustomEditorTypesAndSharedLanguageState()
        {
            Assert.That(typeof(DroneGrappleConfigEditor).GetCustomAttributes(typeof(CustomEditor), false), Is.Not.Empty);
            Assert.That(typeof(DroneHarpoonConfigEditor).GetCustomAttributes(typeof(CustomEditor), false), Is.Not.Empty);
            Assert.That(DroneConfigInspectorUi.IsChineseSelection(0), Is.True);
            Assert.That(DroneConfigInspectorUi.IsChineseSelection(1), Is.False);
        }
    }
}
