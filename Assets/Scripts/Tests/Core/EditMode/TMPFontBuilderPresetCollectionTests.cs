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
            Assert.That(first.FileNameSuffix, Is.EqualTo("_JP"));
            Assert.That(first.PreserveExistingFallbackWhenEmpty, Is.True);
            Assert.That(first.UseOptimalPacking, Is.True);
        }

        [Test]
        public void Move_ReordersPresetsAndRejectsInvalidIndices()
        {
            InitializeDefaults();

            Assert.That(collection.Move(1, 0), Is.True);
            Assert.That(collection.Presets[0].PresetName, Is.EqualTo("Default EN"));
            Assert.That(collection.Move(-1, 0), Is.False);
            Assert.That(collection.Move(0, 2), Is.False);
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
        public void TryUpdateAt_RejectsInvalidGenerationSettings()
        {
            InitializeDefaults();

            var invalidSuffix = CreatePreset("Chinese", "CN", 4096);
            invalidSuffix.FileNameSuffix = "../CN";
            Assert.That(collection.TryUpdateAt(0, invalidSuffix, out var suffixError), Is.False);
            Assert.That(suffixError, Is.EqualTo(TMPFontBuilderPresetValidationError.InvalidFileNameSuffix));

            var invalidPointSize = CreatePreset("Chinese", "CN", 4096);
            invalidPointSize.SamplingPointSize = 0;
            Assert.That(collection.TryUpdateAt(0, invalidPointSize, out var pointSizeError), Is.False);
            Assert.That(pointSizeError, Is.EqualTo(TMPFontBuilderPresetValidationError.InvalidSamplingPointSize));

            var invalidPadding = CreatePreset("Chinese", "CN", 4096);
            invalidPadding.AtlasPadding = -1;
            Assert.That(collection.TryUpdateAt(0, invalidPadding, out var paddingError), Is.False);
            Assert.That(paddingError, Is.EqualTo(TMPFontBuilderPresetValidationError.InvalidAtlasPadding));

            var invalidAtlas = CreatePreset("Chinese", "CN", 1000);
            Assert.That(collection.TryUpdateAt(0, invalidAtlas, out var atlasError), Is.False);
            Assert.That(atlasError, Is.EqualTo(TMPFontBuilderPresetValidationError.InvalidAtlasSize));
        }

        [Test]
        public void NormalizeCharacters_DeduplicatesUnicodeCodePointsAndSkipsControls()
        {
            var result = TMPFontBuilderWindow.NormalizeCharacters("A😀A\n😀B");

            Assert.That(result, Is.EqualTo("A😀B"));
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
                FileNameSuffix = outputDirectory == "Japanese" ? "_JP" : string.Empty,
                PreserveExistingFallbackWhenEmpty = true,
                AtlasSize = atlasSize,
                SamplingPointSize = 90,
                AtlasPadding = 9,
                UseOptimalPacking = true,
                ExportExternalAtlas = true,
                UseAstcPlatformSettings = true
            };
        }
    }
}
