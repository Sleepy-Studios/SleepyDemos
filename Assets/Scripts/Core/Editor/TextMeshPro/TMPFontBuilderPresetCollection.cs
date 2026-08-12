using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;

namespace Core.Editor.TextMeshPro
{
    internal enum TMPFontBuilderPresetValidationError
    {
        None,
        InvalidIndex,
        EmptyName,
        DuplicateName,
        EmptyOutputDirectory,
        InvalidOutputDirectory,
        InvalidFileNameSuffix,
        InvalidSamplingPointSize,
        InvalidAtlasPadding,
        InvalidAtlasSize
    }

    [Serializable]
    public sealed class TMPFontBuilderPreset
    {
        [SerializeField] private string presetName = string.Empty;
        [SerializeField] private string outputDirectory = "CN";
        [SerializeField] private Font sourceFont;
        [SerializeField] private TextAsset characterSetAsset;
        [SerializeField] private TMP_FontAsset fallbackFontAsset;
        [SerializeField] private string fileNameSuffix = string.Empty;
        [SerializeField] private bool preserveExistingFallbackWhenEmpty = true;
        [SerializeField] private int samplingPointSize = 90;
        [SerializeField] private int atlasPadding = 9;
        [SerializeField] private int atlasSize = 4096;
        [SerializeField] private bool useOptimalPacking = true;
        [SerializeField] private bool exportExternalAtlas = true;
        [SerializeField] private bool useAstcPlatformSettings = true;

        internal string PresetName
        {
            get => presetName;
            set => presetName = value;
        }

        internal string OutputDirectory
        {
            get => outputDirectory;
            set => outputDirectory = value;
        }

        internal Font SourceFont
        {
            get => sourceFont;
            set => sourceFont = value;
        }

        internal TextAsset CharacterSetAsset
        {
            get => characterSetAsset;
            set => characterSetAsset = value;
        }

        internal TMP_FontAsset FallbackFontAsset
        {
            get => fallbackFontAsset;
            set => fallbackFontAsset = value;
        }

        internal string FileNameSuffix
        {
            get => fileNameSuffix;
            set => fileNameSuffix = value ?? string.Empty;
        }

        internal bool PreserveExistingFallbackWhenEmpty
        {
            get => preserveExistingFallbackWhenEmpty;
            set => preserveExistingFallbackWhenEmpty = value;
        }

        internal int SamplingPointSize
        {
            get => samplingPointSize;
            set => samplingPointSize = value;
        }

        internal int AtlasPadding
        {
            get => atlasPadding;
            set => atlasPadding = value;
        }

        internal int AtlasSize
        {
            get => atlasSize;
            set => atlasSize = value;
        }

        internal bool UseOptimalPacking
        {
            get => useOptimalPacking;
            set => useOptimalPacking = value;
        }

        internal bool ExportExternalAtlas
        {
            get => exportExternalAtlas;
            set => exportExternalAtlas = value;
        }

        internal bool UseAstcPlatformSettings
        {
            get => useAstcPlatformSettings;
            set => useAstcPlatformSettings = value;
        }

        internal TMPFontBuilderPreset Clone()
        {
            return new TMPFontBuilderPreset
            {
                presetName = presetName,
                outputDirectory = outputDirectory,
                sourceFont = sourceFont,
                characterSetAsset = characterSetAsset,
                fallbackFontAsset = fallbackFontAsset,
                fileNameSuffix = fileNameSuffix,
                preserveExistingFallbackWhenEmpty = preserveExistingFallbackWhenEmpty,
                samplingPointSize = samplingPointSize,
                atlasPadding = atlasPadding,
                atlasSize = atlasSize,
                useOptimalPacking = useOptimalPacking,
                exportExternalAtlas = exportExternalAtlas,
                useAstcPlatformSettings = useAstcPlatformSettings
            };
        }

        internal bool HasSameSettings(TMPFontBuilderPreset other)
        {
            return other != null
                && string.Equals(presetName, other.presetName, StringComparison.Ordinal)
                && string.Equals(outputDirectory, other.outputDirectory, StringComparison.Ordinal)
                && sourceFont == other.sourceFont
                && characterSetAsset == other.characterSetAsset
                && fallbackFontAsset == other.fallbackFontAsset
                && string.Equals(fileNameSuffix, other.fileNameSuffix, StringComparison.Ordinal)
                && preserveExistingFallbackWhenEmpty == other.preserveExistingFallbackWhenEmpty
                && samplingPointSize == other.samplingPointSize
                && atlasPadding == other.atlasPadding
                && atlasSize == other.atlasSize
                && useOptimalPacking == other.useOptimalPacking
                && exportExternalAtlas == other.exportExternalAtlas
                && useAstcPlatformSettings == other.useAstcPlatformSettings;
        }
    }

    public sealed class TMPFontBuilderPresetCollection : ScriptableObject
    {
        [SerializeField] private List<TMPFontBuilderPreset> presets = new List<TMPFontBuilderPreset>();

        internal IReadOnlyList<TMPFontBuilderPreset> Presets => presets;

        internal void InitializeDefaults(TMPFontBuilderPreset cnPreset, TMPFontBuilderPreset enPreset)
        {
            if (presets.Count > 0)
            {
                return;
            }

            presets.Add(cnPreset.Clone());
            presets.Add(enPreset.Clone());
        }

        internal TMPFontBuilderPreset AddCopy(TMPFontBuilderPreset source)
        {
            var copy = source.Clone();
            copy.PresetName = GetUniquePresetName();
            presets.Add(copy);
            return copy;
        }

        internal bool RemoveAt(int index)
        {
            if (presets.Count <= 1 || index < 0 || index >= presets.Count)
            {
                return false;
            }

            presets.RemoveAt(index);
            return true;
        }

        internal bool Move(int sourceIndex, int targetIndex)
        {
            if (sourceIndex < 0 || sourceIndex >= presets.Count
                || targetIndex < 0 || targetIndex >= presets.Count
                || sourceIndex == targetIndex)
            {
                return false;
            }

            var preset = presets[sourceIndex];
            presets.RemoveAt(sourceIndex);
            presets.Insert(targetIndex, preset);
            return true;
        }

        internal bool TryUpdateAt(int index, TMPFontBuilderPreset source, out TMPFontBuilderPresetValidationError error)
        {
            if (index < 0 || index >= presets.Count)
            {
                error = TMPFontBuilderPresetValidationError.InvalidIndex;
                return false;
            }

            var trimmedName = source.PresetName?.Trim();
            if (string.IsNullOrEmpty(trimmedName))
            {
                error = TMPFontBuilderPresetValidationError.EmptyName;
                return false;
            }

            for (var presetIndex = 0; presetIndex < presets.Count; presetIndex++)
            {
                if (presetIndex != index && string.Equals(presets[presetIndex].PresetName, trimmedName, StringComparison.OrdinalIgnoreCase))
                {
                    error = TMPFontBuilderPresetValidationError.DuplicateName;
                    return false;
                }
            }

            if (!TryNormalizeOutputDirectory(source.OutputDirectory, out var normalizedOutputDirectory))
            {
                error = string.IsNullOrWhiteSpace(source.OutputDirectory)
                    ? TMPFontBuilderPresetValidationError.EmptyOutputDirectory
                    : TMPFontBuilderPresetValidationError.InvalidOutputDirectory;
                return false;
            }

            if (!TryNormalizeFileNameSuffix(source.FileNameSuffix, out var normalizedFileNameSuffix))
            {
                error = TMPFontBuilderPresetValidationError.InvalidFileNameSuffix;
                return false;
            }

            if (source.SamplingPointSize <= 0)
            {
                error = TMPFontBuilderPresetValidationError.InvalidSamplingPointSize;
                return false;
            }

            if (source.AtlasPadding < 0)
            {
                error = TMPFontBuilderPresetValidationError.InvalidAtlasPadding;
                return false;
            }

            if (source.AtlasSize < 256 || source.AtlasSize > 4096 || !Mathf.IsPowerOfTwo(source.AtlasSize))
            {
                error = TMPFontBuilderPresetValidationError.InvalidAtlasSize;
                return false;
            }

            var replacement = source.Clone();
            replacement.PresetName = trimmedName;
            replacement.OutputDirectory = normalizedOutputDirectory;
            replacement.FileNameSuffix = normalizedFileNameSuffix;
            presets[index] = replacement;
            error = TMPFontBuilderPresetValidationError.None;
            return true;
        }

        private string GetUniquePresetName()
        {
            var suffix = 1;
            while (presets.Exists(preset => string.Equals(preset.PresetName, $"Preset {suffix}", StringComparison.OrdinalIgnoreCase)))
            {
                suffix++;
            }

            return $"Preset {suffix}";
        }

        internal static bool TryNormalizeOutputDirectory(string value, out string normalizedValue)
        {
            normalizedValue = value?.Trim();
            if (string.IsNullOrEmpty(normalizedValue)
                || normalizedValue == "."
                || normalizedValue == ".."
                || normalizedValue.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
                || normalizedValue.Contains("/")
                || normalizedValue.Contains("\\"))
            {
                normalizedValue = string.Empty;
                return false;
            }

            return true;
        }

        internal static bool TryNormalizeFileNameSuffix(string value, out string normalizedValue)
        {
            normalizedValue = value?.Trim() ?? string.Empty;
            if (normalizedValue.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
                || normalizedValue.Contains("/")
                || normalizedValue.Contains("\\"))
            {
                normalizedValue = string.Empty;
                return false;
            }

            return true;
        }
    }
}
