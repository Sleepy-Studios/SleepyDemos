using System;
using Core.Editor.TextMeshPro;
using NUnit.Framework;
using UnityEngine;

namespace Core.Tests.TextMeshPro
{
    public sealed class TMPFontBuilderPresetCollectionTests
    {
        private TMPFontBuilderPresetCollection collection;

        [SetUp]
        public void SetUp()
        {
            collection = ScriptableObject.CreateInstance<TMPFontBuilderPresetCollection>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(collection);
        }

        [Test]
        public void InitializeDefaults_WhenEmpty_AddsChineseAndEnglishPresets()
        {
            InitializeDefaults();

            Assert.That(collection.Presets, Has.Count.EqualTo(2));
            Assert.That(collection.Presets[0].PresetName, Is.EqualTo("Default CN"));
            Assert.That(collection.Presets[1].PresetName, Is.EqualTo("Default EN"));
            Assert.That(collection.Presets[0].OutputDirectory, Is.EqualTo("CN"));
            Assert.That(collection.Presets[1].OutputDirectory, Is.EqualTo("EN"));
        }

        [Test]
        public void AddCopy_UsesUniqueGeneratedNamesAndCopiesSettings()
        {
            InitializeDefaults();
            var source = CreatePreset("Draft", "Japanese", 2048);

            var first = collection.AddCopy(source);
            var second = collection.AddCopy(source);

            Assert.That(first.PresetName, Is.EqualTo("Preset 1"));
            Assert.That(second.PresetName, Is.EqualTo("Preset 2"));
            Assert.That(first.AtlasSize, Is.EqualTo(2048));
            Assert.That(first.OutputDirectory, Is.EqualTo("Japanese"));
        }

        [Test]
        public void RemoveAt_DoesNotRemoveLastPreset()
        {
            InitializeDefaults();

            Assert.That(collection.RemoveAt(1), Is.True);
            Assert.That(collection.Presets, Has.Count.EqualTo(1));
            Assert.That(collection.RemoveAt(0), Is.False);
            Assert.That(collection.Presets, Has.Count.EqualTo(1));
        }

        [Test]
        public void TryUpdateAt_RejectsEmptyAndDuplicateNames()
        {
            InitializeDefaults();
            var emptyName = CreatePreset("  ", "CN", 4096);
            var duplicateName = CreatePreset("default en", "CN", 4096);

            Assert.That(collection.TryUpdateAt(0, emptyName, out var emptyError), Is.False);
            Assert.That(emptyError, Is.EqualTo(TMPFontBuilderPresetValidationError.EmptyName));
            Assert.That(collection.TryUpdateAt(0, duplicateName, out var duplicateError), Is.False);
            Assert.That(duplicateError, Is.EqualTo(TMPFontBuilderPresetValidationError.DuplicateName));
        }

        [Test]
        public void TryUpdateAt_RejectsEmptyAndInvalidOutputDirectories()
        {
            InitializeDefaults();
            var emptyDirectory = CreatePreset("Chinese", "  ", 4096);
            var invalidDirectory = CreatePreset("Chinese", "../CN", 4096);

            Assert.That(collection.TryUpdateAt(0, emptyDirectory, out var emptyError), Is.False);
            Assert.That(emptyError, Is.EqualTo(TMPFontBuilderPresetValidationError.EmptyOutputDirectory));
            Assert.That(collection.TryUpdateAt(0, invalidDirectory, out var invalidError), Is.False);
            Assert.That(invalidError, Is.EqualTo(TMPFontBuilderPresetValidationError.InvalidOutputDirectory));
        }

        [Test]
        public void Localization_AllKeysHaveChineseAndEnglishText()
        {
            foreach (TMPFontBuilderText key in Enum.GetValues(typeof(TMPFontBuilderText)))
            {
                Assert.That(TMPFontBuilderLocalization.HasTranslation(key, TMPFontBuilderEditorLanguage.Chinese), Is.True, key.ToString());
                Assert.That(TMPFontBuilderLocalization.HasTranslation(key, TMPFontBuilderEditorLanguage.English), Is.True, key.ToString());
            }
        }

        private void InitializeDefaults()
        {
            collection.InitializeDefaults(
                CreatePreset("Default CN", "CN", 4096),
                CreatePreset("Default EN", "EN", 1024));
        }

        private static TMPFontBuilderPreset CreatePreset(string name, string outputDirectory, int atlasSize)
        {
            return new TMPFontBuilderPreset
            {
                PresetName = name,
                OutputDirectory = outputDirectory,
                AtlasSize = atlasSize,
                SamplingPointSize = 90,
                AtlasPadding = 9,
                ExportExternalAtlas = true,
                UseAstcPlatformSettings = true
            };
        }
    }
}
