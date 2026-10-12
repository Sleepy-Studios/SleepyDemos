using Hotfix.DroneFlight;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Demo
{
    /*
     * 测试说明：验证非法装备配置被拒绝并提供中英双语诊断。
     */
    public sealed class DroneConfigInspectorTests
    {
        [Test]
        public void InvalidEquipmentConfigs_ReturnChineseAndEnglishDiagnostics()
        {
            var grapple = ScriptableObject.CreateInstance<DroneGrappleConfig>();
            var harpoon = ScriptableObject.CreateInstance<DroneHarpoonConfig>();
            try
            {
                var serializedGrapple = new UnityEditor.SerializedObject(grapple);
                serializedGrapple.FindProperty("maximumLiftTravelMeters").floatValue = 0f;
                serializedGrapple.ApplyModifiedPropertiesWithoutUndo();
                var grappleResult = grapple.Validate();
                Assert.That(grappleResult.IsValid, Is.False);
                Assert.That(grappleResult.ChineseMessage, Is.Not.Empty);
                Assert.That(grappleResult.EnglishMessage, Is.Not.Empty);

                var serializedHarpoon = new UnityEditor.SerializedObject(harpoon);
                serializedHarpoon.FindProperty("projectileMassKilograms").floatValue = 0f;
                serializedHarpoon.ApplyModifiedPropertiesWithoutUndo();
                var harpoonResult = harpoon.Validate();
                Assert.That(harpoonResult.IsValid, Is.False);
                Assert.That(harpoonResult.ChineseMessage, Is.Not.Empty);
                Assert.That(harpoonResult.EnglishMessage, Is.Not.Empty);
            }
            finally
            {
                Object.DestroyImmediate(grapple);
                Object.DestroyImmediate(harpoon);
            }
        }

    }
}
