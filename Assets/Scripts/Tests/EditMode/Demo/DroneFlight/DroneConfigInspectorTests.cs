using System.Linq;
using System.Collections.Generic;
using Hotfix.DroneFlight;
using Hotfix.Editor.DroneFlight;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Demo
{
    /*
     * 测试说明：验证三套 DroneFlight 配置的互斥语言状态、装备字段双语覆盖和结构化双语诊断。
     */
    public sealed class DroneConfigInspectorTests
    {
        [Test]
        public void LanguageSelection_IsMutuallyExclusiveAndDeterministic()
        {
            Assert.That(DroneConfigInspectorUi.IsChineseSelection(0), Is.True);
            Assert.That(DroneConfigInspectorUi.IsChineseSelection(1), Is.False);
        }

        [Test]
        public void EquipmentFields_HaveUniqueCompleteBilingualLabels()
        {
            AssertFieldCoverage(
                DroneGrappleConfigEditor.BasicSerializedFields,
                DroneGrappleConfigEditor.AllSerializedFields,
                DroneGrappleConfigEditor.SerializedFieldLabels,
                DroneGrappleConfigEditor.GetSectionLabel);
            AssertFieldCoverage(
                DroneHarpoonConfigEditor.BasicSerializedFields,
                DroneHarpoonConfigEditor.AllSerializedFields,
                DroneHarpoonConfigEditor.SerializedFieldLabels,
                DroneHarpoonConfigEditor.GetSectionLabel);
        }

        [Test]
        public void FlightConfig_AllVisibleSerializedFieldsHaveBilingualLabels()
        {
            var config = ScriptableObject.CreateInstance<DroneFlightConfig>();
            try
            {
                var serializedObject = new UnityEditor.SerializedObject(config);
                var iterator = serializedObject.GetIterator();
                var fieldNames = new HashSet<string>();
                for (var enterChildren = true; iterator.NextVisible(enterChildren); enterChildren = true)
                {
                    if (iterator.propertyPath != "m_Script")
                    {
                        fieldNames.Add(iterator.name);
                    }
                }

                foreach (var fieldName in fieldNames)
                {
                    Assert.That(
                        DroneFlightConfigEditor.SerializedFieldLabels.TryGetValue(fieldName, out var label),
                        Is.True,
                        $"主飞控配置字段 {fieldName} 缺少双语标签。");
                    AssertComplete(label);
                }
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void RemovedPrototypeFields_AreNotSerializedAnymore()
        {
            var flight = ScriptableObject.CreateInstance<DroneFlightConfig>();
            var grapple = ScriptableObject.CreateInstance<DroneGrappleConfig>();
            try
            {
                var flightObject = new UnityEditor.SerializedObject(flight);
                Assert.That(flightObject.FindProperty("maximumVerticalSpeedMetersPerSecond"), Is.Null);
                Assert.That(flightObject.FindProperty("maximumHorizontalSpeedMetersPerSecond"), Is.Null);
                Assert.That(flightObject.FindProperty("maximumHorizontalAccelerationMetersPerSecondSquared"), Is.Null);

                var grappleObject = new UnityEditor.SerializedObject(grapple);
                Assert.That(grappleObject.FindProperty("clawMaximumForce"), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(flight);
                Object.DestroyImmediate(grapple);
            }
        }

        [Test]
        public void InvalidEquipmentConfigs_ReturnChineseAndEnglishDiagnostics()
        {
            var grapple = ScriptableObject.CreateInstance<DroneGrappleConfig>();
            var harpoon = ScriptableObject.CreateInstance<DroneHarpoonConfig>();
            try
            {
                var serializedGrapple = new UnityEditor.SerializedObject(grapple);
                serializedGrapple.FindProperty("hardwareMassKilograms").floatValue = 0f;
                serializedGrapple.ApplyModifiedPropertiesWithoutUndo();
                var grappleResult = grapple.Validate();
                Assert.That(grappleResult.IsValid, Is.False);
                Assert.That(grappleResult.ChineseMessage, Is.Not.Empty);
                Assert.That(grappleResult.EnglishMessage, Is.Not.Empty);

                var serializedHarpoon = new UnityEditor.SerializedObject(harpoon);
                serializedHarpoon.FindProperty("projectileMassKilograms").floatValue = 1f;
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

        private static void AssertFieldCoverage(
            System.Collections.Generic.IReadOnlyList<string> basic,
            System.Collections.Generic.IReadOnlyList<string> all,
            System.Collections.Generic.IReadOnlyDictionary<string, DroneInspectorLabel> labels,
            System.Func<string, DroneInspectorLabel> resolveSection)
        {
            Assert.That(all, Is.Unique);
            Assert.That(basic, Is.Unique);
            Assert.That(basic.All(all.Contains), Is.True);
            Assert.That(labels.Keys.OrderBy(value => value), Is.EqualTo(all.OrderBy(value => value)));
            foreach (var label in labels.Values)
            {
                AssertComplete(label);
            }

            foreach (var fieldName in all)
            {
                AssertComplete(resolveSection(fieldName));
            }
        }

        private static void AssertComplete(DroneInspectorLabel label)
        {
            Assert.That(label.Chinese, Is.Not.Empty);
            Assert.That(label.English, Is.Not.Empty);
            Assert.That(label.ChineseTooltip, Is.Not.Empty);
            Assert.That(label.EnglishTooltip, Is.Not.Empty);
        }
    }
}
