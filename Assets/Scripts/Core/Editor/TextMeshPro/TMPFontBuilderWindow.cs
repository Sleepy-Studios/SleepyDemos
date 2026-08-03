using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Core.Editor.TextMeshPro
{
    public sealed class TMPFontBuilderWindow : EditorWindow
    {
        private const string FontRoot = "Assets/LoadResources/Fonts";
        private const string SourceRoot = FontRoot + "/Source";
        private const string FontAssetRoot = FontRoot + "/TMP_FontAssets";
        private const string MaterialRoot = FontRoot + "/Materials";
        private const string FallbackRoot = FontRoot + "/Fallbacks";

        private const string DefaultCnSource = SourceRoot + "/CN/HarmonyOS_CN.ttf";
        private const string DefaultEnSource = SourceRoot + "/EN/BebasNeue_EN.otf";
        private const string DefaultCnCharacters = FallbackRoot + "/Default_CN_Characters.txt";
        private const string DefaultEnCharacters = FallbackRoot + "/Default_EN_Characters.txt";
        private const string EditorPrefsRoot = "SleepyDemos.TMPFontBuilder";
        private const string PresetCollectionPath = "Assets/Settings/TMPFontBuilderPresets.asset";
        private const string EditorLanguagePrefsKey = EditorPrefsRoot + ".EditorLanguage";

        private TMPFontBuilderPresetCollection presetCollection;
        private int selectedPresetIndex;
        private string presetName = string.Empty;
        private Font sourceFont;
        private TextAsset characterSetAsset;
        private TMP_FontAsset fallbackFontAsset;
        private string outputDirectory = "CN";
        private string inlineCharacters = string.Empty;
        private int samplingPointSize = 90;
        private int atlasPadding = 9;
        private int atlasSize = 4096;
        private bool exportExternalAtlas = true;
        private bool useAstcPlatformSettings = true;
        private TMPFontBuilderEditorLanguage editorLanguage = TMPFontBuilderEditorLanguage.Chinese;
        private Vector2 scrollPosition;
        private string lastActionMessage = string.Empty;

        [MenuItem("Tools/SleepyDemos/TextMesh Pro/Font Builder")]
        public static void Open()
        {
            var window = GetWindow<TMPFontBuilderWindow>();
            window.minSize = new Vector2(480, 560);
            window.Show();
        }

        [MenuItem("Tools/SleepyDemos/TextMesh Pro/Build Default CN EN Fonts")]
        public static void BuildDefaultProjectFonts()
        {
            EnsureFolders();

            var enAsset = Build(new BuildRequest
            {
                SourceFontPath = DefaultEnSource,
                CharacterSetPath = DefaultEnCharacters,
                OutputDirectory = "EN",
                SamplingPointSize = 90,
                AtlasPadding = 9,
                AtlasSize = 1024,
                ExportExternalAtlas = true,
                UseAstcPlatformSettings = true
            });

            var cnAsset = Build(new BuildRequest
            {
                SourceFontPath = DefaultCnSource,
                CharacterSetPath = DefaultCnCharacters,
                OutputDirectory = "CN",
                FallbackFontAsset = enAsset,
                SamplingPointSize = 90,
                AtlasPadding = 9,
                AtlasSize = 4096,
                ExportExternalAtlas = true,
                UseAstcPlatformSettings = true
            });

            Selection.activeObject = cnAsset != null ? cnAsset : enAsset;
            if (Selection.activeObject != null)
            {
                EditorGUIUtility.PingObject(Selection.activeObject);
            }
        }

        private void OnEnable()
        {
            var savedLanguage = EditorPrefs.GetInt(EditorLanguagePrefsKey, (int)TMPFontBuilderEditorLanguage.Chinese);
            editorLanguage = (TMPFontBuilderEditorLanguage)Mathf.Clamp(
                savedLanguage,
                (int)TMPFontBuilderEditorLanguage.Chinese,
                (int)TMPFontBuilderEditorLanguage.English);
            EnsureFolders();
            LoadOrCreatePresetCollection();
            LoadSelectedPreset();
            UpdateWindowTitle();
        }

        private void OnGUI()
        {
            DrawLanguageSwitcher();
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            EditorGUILayout.LabelField(Text(TMPFontBuilderText.Presets), EditorStyles.boldLabel);
            DrawPresetControls();

            if (!string.IsNullOrEmpty(lastActionMessage))
            {
                EditorGUILayout.HelpBox(lastActionMessage, MessageType.Info);
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField(Text(TMPFontBuilderText.Source), EditorStyles.boldLabel);
            sourceFont = (Font)EditorGUILayout.ObjectField(Text(TMPFontBuilderText.FontFile), sourceFont, typeof(Font), false);
            var outputDirectoryContent = new GUIContent(
                Text(TMPFontBuilderText.OutputDirectory),
                Text(TMPFontBuilderText.OutputDirectoryTooltip));
            outputDirectory = EditorGUILayout.TextField(outputDirectoryContent, outputDirectory);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField(Text(TMPFontBuilderText.Characters), EditorStyles.boldLabel);
            characterSetAsset = (TextAsset)EditorGUILayout.ObjectField(Text(TMPFontBuilderText.CharacterSetText), characterSetAsset, typeof(TextAsset), false);
            EditorGUILayout.LabelField(Text(TMPFontBuilderText.InlineCharacters));
            inlineCharacters = EditorGUILayout.TextArea(inlineCharacters, GUILayout.MinHeight(120));

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField(Text(TMPFontBuilderText.Fallback), EditorStyles.boldLabel);
            fallbackFontAsset = (TMP_FontAsset)EditorGUILayout.ObjectField(Text(TMPFontBuilderText.FallbackFont), fallbackFontAsset, typeof(TMP_FontAsset), false);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField(Text(TMPFontBuilderText.Generation), EditorStyles.boldLabel);
            samplingPointSize = EditorGUILayout.IntField(Text(TMPFontBuilderText.SamplingPointSize), samplingPointSize);
            atlasPadding = EditorGUILayout.IntField(Text(TMPFontBuilderText.AtlasPadding), atlasPadding);
            atlasSize = EditorGUILayout.IntField(Text(TMPFontBuilderText.AtlasSize), atlasSize);
            exportExternalAtlas = EditorGUILayout.Toggle(Text(TMPFontBuilderText.ExportExternalAtlas), exportExternalAtlas);
            useAstcPlatformSettings = EditorGUILayout.Toggle(Text(TMPFontBuilderText.AstcPlatformSettings), useAstcPlatformSettings);

            EditorGUILayout.Space(16);
            using (new EditorGUI.DisabledScope(sourceFont == null))
            {
                if (GUILayout.Button(Text(TMPFontBuilderText.BuildFontAsset), GUILayout.Height(36)))
                {
                    BuildFromWindow();
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void BuildFromWindow()
        {
            var sourcePath = AssetDatabase.GetAssetPath(sourceFont);
            var characterSetPath = AssetDatabase.GetAssetPath(characterSetAsset);
            var characters = string.IsNullOrEmpty(inlineCharacters) ? string.Empty : inlineCharacters;

            var result = Build(new BuildRequest
            {
                SourceFontPath = sourcePath,
                CharacterSetPath = characterSetPath,
                InlineCharacters = characters,
                OutputDirectory = outputDirectory,
                FallbackFontAsset = GetValidatedFallback(),
                SamplingPointSize = samplingPointSize,
                AtlasPadding = atlasPadding,
                AtlasSize = atlasSize,
                ExportExternalAtlas = exportExternalAtlas,
                UseAstcPlatformSettings = useAstcPlatformSettings
            });

            if (result == null)
            {
                return;
            }

            Selection.activeObject = result;
            EditorGUIUtility.PingObject(result);
        }

        private static TMP_FontAsset Build(BuildRequest request)
        {
            EnsureFolders();

            var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(request.SourceFontPath);
            if (sourceFont == null)
            {
                Debug.LogError($"[TMPFontBuilder] Source font not found: {request.SourceFontPath}");
                return null;
            }

            var characters = LoadCharacterSet(request);
            if (string.IsNullOrEmpty(characters))
            {
                Debug.LogError("[TMPFontBuilder] Character set is empty.");
                return null;
            }

            var targetName = GetTargetAssetName(request.SourceFontPath);
            if (targetName == null)
            {
                return null;
            }

            if (!TMPFontBuilderPresetCollection.TryNormalizeOutputDirectory(request.OutputDirectory, out var normalizedOutputDirectory))
            {
                Debug.LogError($"[TMPFontBuilder] Invalid output directory: {request.OutputDirectory}");
                return null;
            }

            var targetFolder = $"{FontAssetRoot}/{normalizedOutputDirectory}";
            EnsureFolder(targetFolder);

            var targetAssetPath = $"{targetFolder}/{targetName}.asset";
            var targetMaterialPath = $"{MaterialRoot}/{GetTargetMaterialAssetName(targetName)}.mat";
            var targetAtlasPath = $"{targetFolder}/{GetTargetAtlasAssetName(targetName)}.png";

            var fontAsset = TMP_FontAsset.CreateFontAsset(
                sourceFont,
                request.SamplingPointSize,
                request.AtlasPadding,
                GlyphRenderMode.SDFAA,
                request.AtlasSize,
                request.AtlasSize,
                AtlasPopulationMode.Dynamic,
                false);

            if (fontAsset == null)
            {
                Debug.LogError($"[TMPFontBuilder] Failed to create TMP_FontAsset from {request.SourceFontPath}");
                return null;
            }

            fontAsset.name = targetName;
            if (fontAsset.material != null)
            {
                fontAsset.material.name = $"{targetName} Material";
            }

            var addedAll = fontAsset.TryAddCharacters(characters, out var missingCharacters);
            fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;
            fontAsset.fallbackFontAssetTable = request.FallbackFontAsset != null
                ? new List<TMP_FontAsset> { request.FallbackFontAsset }
                : new List<TMP_FontAsset>();

            fontAsset = SaveFontAsset(fontAsset, targetAssetPath);
            SaveExternalMaterial(fontAsset, targetMaterialPath);

            if (request.ExportExternalAtlas)
            {
                ExportAndBindAtlas(fontAsset, targetAssetPath, targetAtlasPath, request.UseAstcPlatformSettings);
            }

            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (!addedAll)
            {
                Debug.LogWarning($"[TMPFontBuilder] Generated {targetAssetPath}, but missing characters: {missingCharacters}");
            }
            else
            {
                Debug.Log($"[TMPFontBuilder] Generated {targetAssetPath}");
            }

            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(targetAssetPath);
        }

        private static TMP_FontAsset SaveFontAsset(TMP_FontAsset fontAsset, string targetAssetPath)
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(targetAssetPath);
            if (existing != null)
            {
                EditorUtility.CopySerialized(fontAsset, existing);
                existing.name = fontAsset.name;
                EditorUtility.SetDirty(existing);
                return existing;
            }

            AssetDatabase.CreateAsset(fontAsset, targetAssetPath);

            var atlasTexture = fontAsset.atlasTextures != null && fontAsset.atlasTextures.Length > 0
                ? fontAsset.atlasTextures[0]
                : null;

            if (atlasTexture != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(atlasTexture)))
            {
                atlasTexture.name = $"{fontAsset.name} Atlas";
                AssetDatabase.AddObjectToAsset(atlasTexture, fontAsset);
            }

            if (fontAsset.material != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(fontAsset.material)))
            {
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            }

            return fontAsset;
        }

        private static void SaveExternalMaterial(TMP_FontAsset fontAsset, string targetMaterialPath)
        {
            if (fontAsset.material == null)
            {
                return;
            }

            var material = new Material(fontAsset.material)
            {
                name = $"{fontAsset.name} Material"
            };

            if (AssetDatabase.LoadAssetAtPath<Material>(targetMaterialPath) != null)
            {
                AssetDatabase.DeleteAsset(targetMaterialPath);
            }

            AssetDatabase.CreateAsset(material, targetMaterialPath);
            fontAsset.material = material;
        }

        private static void ExportAndBindAtlas(TMP_FontAsset fontAsset, string fontAssetPath, string atlasPath, bool useAstcPlatformSettings)
        {
            if (fontAsset.atlasTextures == null || fontAsset.atlasTextures.Length == 0 || fontAsset.atlasTextures[0] == null)
            {
                Debug.LogWarning($"[TMPFontBuilder] Font asset has no atlas texture: {fontAsset.name}");
                return;
            }

            var atlas = fontAsset.atlasTextures[0];
            var png = atlas.EncodeToPNG();
            File.WriteAllBytes(ToFullPath(atlasPath), png);
            AssetDatabase.ImportAsset(atlasPath, ImportAssetOptions.ForceSynchronousImport);

            if (useAstcPlatformSettings)
            {
                ApplyAtlasImporterSettings(atlasPath);
            }

            var externalAtlas = AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath);
            if (externalAtlas == null)
            {
                Debug.LogWarning($"[TMPFontBuilder] Failed to import external atlas: {atlasPath}");
                return;
            }

            var embeddedAtlasPath = AssetDatabase.GetAssetPath(atlas);
            if (embeddedAtlasPath == fontAssetPath)
            {
                AssetDatabase.RemoveObjectFromAsset(atlas);
            }

            fontAsset.atlasTextures[0] = externalAtlas;
            if (fontAsset.material != null)
            {
                fontAsset.material.mainTexture = externalAtlas;
                EditorUtility.SetDirty(fontAsset.material);
            }
        }

        private static void ApplyAtlasImporterSettings(string atlasPath)
        {
            var importer = AssetImporter.GetAtPath(atlasPath) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.sRGBTexture = false;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;

            SetPlatformSettings(importer, "Android");
            SetPlatformSettings(importer, "iPhone");

            importer.SaveAndReimport();
        }

        private static void SetPlatformSettings(TextureImporter importer, string platform)
        {
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name = platform,
                overridden = true,
                maxTextureSize = 4096,
                format = TextureImporterFormat.ASTC_6x6,
                compressionQuality = 100
            });
        }

        private static string LoadCharacterSet(BuildRequest request)
        {
            var builder = new StringBuilder();

            if (!string.IsNullOrEmpty(request.CharacterSetPath))
            {
                var textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(request.CharacterSetPath);
                if (textAsset != null)
                {
                    builder.Append(textAsset.text);
                }
            }

            if (!string.IsNullOrEmpty(request.InlineCharacters))
            {
                builder.Append(request.InlineCharacters);
            }

            return NormalizeCharacters(builder.ToString());
        }

        private static string NormalizeCharacters(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return string.Empty;
            }

            return new string(input.Where(c => !char.IsControl(c) || c == '\n' || c == '\r' || c == '\t').Distinct().ToArray());
        }

        private static string GetTargetAssetName(string sourceFontPath)
        {
            var name = Path.GetFileNameWithoutExtension(sourceFontPath);
            if (string.IsNullOrEmpty(name))
            {
                Debug.LogError($"[TMPFontBuilder] 无法解析源字体文件名: {sourceFontPath}");
                return null;
            }

            return name;
        }

        private static string GetTargetAtlasAssetName(string targetFontAssetName)
        {
            return $"{targetFontAssetName}_Atlas";
        }

        private static string GetTargetMaterialAssetName(string targetFontAssetName)
        {
            return targetFontAssetName;
        }

        private static void EnsureFolders()
        {
            EnsureFolder(FontRoot);
            EnsureFolder(SourceRoot);
            EnsureFolder(SourceRoot + "/CN");
            EnsureFolder(SourceRoot + "/EN");
            EnsureFolder(FontAssetRoot);
            EnsureFolder(FontAssetRoot + "/CN");
            EnsureFolder(FontAssetRoot + "/EN");
            EnsureFolder(MaterialRoot);
            EnsureFolder(FallbackRoot);
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            var parent = Path.GetDirectoryName(folder)?.Replace("\\", "/");
            var name = Path.GetFileName(folder);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name))
            {
                return;
            }

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static string ToFullPath(string assetPath)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length)));
        }

        private void DrawLanguageSwitcher()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                var buttonLabel = editorLanguage == TMPFontBuilderEditorLanguage.Chinese ? "EN" : "中文";
                if (!GUILayout.Button(buttonLabel, GUILayout.Width(56)))
                {
                    return;
                }

                editorLanguage = editorLanguage == TMPFontBuilderEditorLanguage.Chinese
                    ? TMPFontBuilderEditorLanguage.English
                    : TMPFontBuilderEditorLanguage.Chinese;
                EditorPrefs.SetInt(EditorLanguagePrefsKey, (int)editorLanguage);
                UpdateWindowTitle();
            }
        }

        private void DrawPresetControls()
        {
            if (presetCollection == null || presetCollection.Presets.Count == 0)
            {
                EditorGUILayout.HelpBox(Text(TMPFontBuilderText.InvalidPreset), MessageType.Error);
                return;
            }

            var presetNames = presetCollection.Presets.Select(preset => preset.PresetName).ToArray();
            using (new EditorGUILayout.HorizontalScope())
            {
                var nextIndex = EditorGUILayout.Popup(Text(TMPFontBuilderText.Preset), selectedPresetIndex, presetNames);
                if (nextIndex != selectedPresetIndex)
                {
                    TrySelectPreset(nextIndex);
                }

                var addContent = new GUIContent("+", Text(TMPFontBuilderText.AddPresetTooltip));
                if (GUILayout.Button(addContent, GUILayout.Width(28)))
                {
                    AddPreset();
                }

                using (new EditorGUI.DisabledScope(presetCollection.Presets.Count <= 1))
                {
                    var removeContent = new GUIContent("-", Text(TMPFontBuilderText.RemovePresetTooltip));
                    if (GUILayout.Button(removeContent, GUILayout.Width(28)))
                    {
                        RemoveSelectedPreset();
                    }
                }
            }

            presetName = EditorGUILayout.TextField(Text(TMPFontBuilderText.PresetName), presetName);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(Text(TMPFontBuilderText.LoadPreset)))
                {
                    ReloadSelectedPreset();
                }

                if (GUILayout.Button(Text(TMPFontBuilderText.SavePreset)))
                {
                    SaveSelectedPreset();
                }
            }

            if (presetCollection.Presets.Count <= 1)
            {
                EditorGUILayout.HelpBox(Text(TMPFontBuilderText.CannotRemoveLastPreset), MessageType.None);
            }
        }

        private void LoadOrCreatePresetCollection()
        {
            presetCollection = AssetDatabase.LoadAssetAtPath<TMPFontBuilderPresetCollection>(PresetCollectionPath);
            if (presetCollection == null)
            {
                EnsureFolder("Assets/Settings");
                presetCollection = CreateInstance<TMPFontBuilderPresetCollection>();
                presetCollection.InitializeDefaults(CreateDefaultPreset("CN"), CreateDefaultPreset("EN"));
                AssetDatabase.CreateAsset(presetCollection, PresetCollectionPath);
                AssetDatabase.SaveAssets();
                return;
            }

            if (presetCollection.Presets.Count == 0)
            {
                presetCollection.InitializeDefaults(CreateDefaultPreset("CN"), CreateDefaultPreset("EN"));
                SavePresetCollection();
            }
        }

        private static TMPFontBuilderPreset CreateDefaultPreset(string presetOutputDirectory)
        {
            var isChinese = string.Equals(presetOutputDirectory, "CN", StringComparison.Ordinal);
            return new TMPFontBuilderPreset
            {
                PresetName = isChinese ? "Default CN" : "Default EN",
                OutputDirectory = presetOutputDirectory,
                SourceFont = AssetDatabase.LoadAssetAtPath<Font>(isChinese ? DefaultCnSource : DefaultEnSource),
                CharacterSetAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(isChinese ? DefaultCnCharacters : DefaultEnCharacters),
                FallbackFontAsset = isChinese
                    ? AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetRoot + "/EN/BebasNeue_EN.asset")
                    : null,
                InlineCharacters = string.Empty,
                SamplingPointSize = 90,
                AtlasPadding = 9,
                AtlasSize = isChinese ? 4096 : 1024,
                ExportExternalAtlas = true,
                UseAstcPlatformSettings = true
            };
        }

        private void AddPreset()
        {
            var addedPreset = presetCollection.AddCopy(CaptureDraft());
            SavePresetCollection();
            selectedPresetIndex = presetCollection.Presets.Count - 1;
            LoadSelectedPreset();
            lastActionMessage = Format(TMPFontBuilderText.AddedPreset, addedPreset.PresetName);
        }

        private void RemoveSelectedPreset()
        {
            if (presetCollection.Presets.Count <= 1)
            {
                lastActionMessage = Text(TMPFontBuilderText.CannotRemoveLastPreset);
                return;
            }

            var removedName = presetCollection.Presets[selectedPresetIndex].PresetName;
            var confirmed = EditorUtility.DisplayDialog(
                Text(TMPFontBuilderText.DeletePresetTitle),
                Format(TMPFontBuilderText.DeletePresetMessage, removedName),
                Text(TMPFontBuilderText.Delete),
                Text(TMPFontBuilderText.Cancel));
            if (!confirmed || !presetCollection.RemoveAt(selectedPresetIndex))
            {
                return;
            }

            SavePresetCollection();
            selectedPresetIndex = Mathf.Clamp(selectedPresetIndex, 0, presetCollection.Presets.Count - 1);
            LoadSelectedPreset();
            lastActionMessage = Format(TMPFontBuilderText.RemovedPreset, removedName);
        }

        private void ReloadSelectedPreset()
        {
            if (!ConfirmUnsavedChanges())
            {
                return;
            }

            LoadSelectedPreset();
            lastActionMessage = Format(TMPFontBuilderText.LoadedPreset, presetName);
        }

        private bool SaveSelectedPreset()
        {
            if (!presetCollection.TryUpdateAt(selectedPresetIndex, CaptureDraft(), out var error))
            {
                lastActionMessage = GetValidationMessage(error);
                return false;
            }

            SavePresetCollection();
            LoadSelectedPreset();
            lastActionMessage = Format(TMPFontBuilderText.SavedPreset, presetName);
            return true;
        }

        private void TrySelectPreset(int nextIndex)
        {
            if (!ConfirmUnsavedChanges())
            {
                return;
            }

            selectedPresetIndex = Mathf.Clamp(nextIndex, 0, presetCollection.Presets.Count - 1);
            LoadSelectedPreset();
            lastActionMessage = Format(TMPFontBuilderText.LoadedPreset, presetName);
        }

        private bool ConfirmUnsavedChanges()
        {
            if (!HasUnsavedChanges())
            {
                return true;
            }

            var choice = EditorUtility.DisplayDialogComplex(
                Text(TMPFontBuilderText.UnsavedChangesTitle),
                Format(TMPFontBuilderText.UnsavedChangesMessage, presetName),
                Text(TMPFontBuilderText.Save),
                Text(TMPFontBuilderText.Cancel),
                Text(TMPFontBuilderText.Discard));

            if (choice == 0)
            {
                return SaveSelectedPreset();
            }

            return choice == 2;
        }

        private bool HasUnsavedChanges()
        {
            return presetCollection != null
                && selectedPresetIndex >= 0
                && selectedPresetIndex < presetCollection.Presets.Count
                && !presetCollection.Presets[selectedPresetIndex].HasSameSettings(CaptureDraft());
        }

        private void LoadSelectedPreset()
        {
            if (presetCollection == null || presetCollection.Presets.Count == 0)
            {
                return;
            }

            selectedPresetIndex = Mathf.Clamp(selectedPresetIndex, 0, presetCollection.Presets.Count - 1);
            var preset = presetCollection.Presets[selectedPresetIndex];
            presetName = preset.PresetName;
            outputDirectory = preset.OutputDirectory;
            sourceFont = preset.SourceFont;
            characterSetAsset = preset.CharacterSetAsset;
            fallbackFontAsset = preset.FallbackFontAsset;
            inlineCharacters = preset.InlineCharacters;
            samplingPointSize = preset.SamplingPointSize;
            atlasPadding = preset.AtlasPadding;
            atlasSize = preset.AtlasSize;
            exportExternalAtlas = preset.ExportExternalAtlas;
            useAstcPlatformSettings = preset.UseAstcPlatformSettings;
        }

        private TMPFontBuilderPreset CaptureDraft()
        {
            return new TMPFontBuilderPreset
            {
                PresetName = presetName,
                OutputDirectory = outputDirectory,
                SourceFont = sourceFont,
                CharacterSetAsset = characterSetAsset,
                FallbackFontAsset = fallbackFontAsset,
                InlineCharacters = inlineCharacters,
                SamplingPointSize = samplingPointSize,
                AtlasPadding = atlasPadding,
                AtlasSize = atlasSize,
                ExportExternalAtlas = exportExternalAtlas,
                UseAstcPlatformSettings = useAstcPlatformSettings
            };
        }

        private void SavePresetCollection()
        {
            EditorUtility.SetDirty(presetCollection);
            AssetDatabase.SaveAssets();
        }

        private string GetValidationMessage(TMPFontBuilderPresetValidationError error)
        {
            switch (error)
            {
                case TMPFontBuilderPresetValidationError.EmptyName:
                    return Text(TMPFontBuilderText.EmptyPresetName);
                case TMPFontBuilderPresetValidationError.DuplicateName:
                    return Text(TMPFontBuilderText.DuplicatePresetName);
                case TMPFontBuilderPresetValidationError.EmptyOutputDirectory:
                    return Text(TMPFontBuilderText.EmptyOutputDirectory);
                case TMPFontBuilderPresetValidationError.InvalidOutputDirectory:
                    return Text(TMPFontBuilderText.InvalidOutputDirectory);
                default:
                    return Text(TMPFontBuilderText.InvalidPreset);
            }
        }

        private string Text(TMPFontBuilderText key)
        {
            return TMPFontBuilderLocalization.Get(key, editorLanguage);
        }

        private string Format(TMPFontBuilderText key, params object[] args)
        {
            return TMPFontBuilderLocalization.Format(key, editorLanguage, args);
        }

        private void UpdateWindowTitle()
        {
            titleContent = new GUIContent(Text(TMPFontBuilderText.WindowTitle));
        }

        private TMP_FontAsset GetValidatedFallback()
        {
            if (fallbackFontAsset == null || sourceFont == null)
            {
                return fallbackFontAsset;
            }

            var fallbackPath = AssetDatabase.GetAssetPath(fallbackFontAsset);
            var targetName = GetTargetAssetName(AssetDatabase.GetAssetPath(sourceFont));
            if (targetName == null)
            {
                return fallbackFontAsset;
            }

            if (!TMPFontBuilderPresetCollection.TryNormalizeOutputDirectory(outputDirectory, out var normalizedOutputDirectory))
            {
                return fallbackFontAsset;
            }

            var expectedTargetPath = $"{FontAssetRoot}/{normalizedOutputDirectory}/{targetName}.asset";

            if (string.Equals(fallbackPath, expectedTargetPath, StringComparison.OrdinalIgnoreCase))
            {
                lastActionMessage = Text(TMPFontBuilderText.FallbackIgnored);
                return null;
            }

            return fallbackFontAsset;
        }

        private sealed class BuildRequest
        {
            public string SourceFontPath;
            public string CharacterSetPath;
            public string InlineCharacters;
            public string OutputDirectory;
            public TMP_FontAsset FallbackFontAsset;
            public int SamplingPointSize;
            public int AtlasPadding;
            public int AtlasSize;
            public bool ExportExternalAtlas;
            public bool UseAstcPlatformSettings;
        }
    }
}
