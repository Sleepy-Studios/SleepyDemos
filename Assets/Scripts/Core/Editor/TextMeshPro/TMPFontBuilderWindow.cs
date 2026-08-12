using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;
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
        private string fileNameSuffix = string.Empty;
        private string additionalCharacters = string.Empty;
        private bool preserveExistingFallbackWhenEmpty = true;
        private int samplingPointSize = 90;
        private int atlasPadding = 9;
        private int atlasSize = 4096;
        private bool useOptimalPacking = true;
        private bool exportExternalAtlas = true;
        private bool useAstcPlatformSettings = true;
        private TMPFontBuilderEditorLanguage editorLanguage = TMPFontBuilderEditorLanguage.Chinese;
        private Vector2 scrollPosition;
        private string lastActionMessage = string.Empty;
        private MessageType lastActionMessageType = MessageType.Info;

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
                FileNameSuffix = "_EN",
                PreserveExistingFallbackWhenEmpty = true,
                SamplingPointSize = 90,
                AtlasPadding = 9,
                AtlasSize = 1024,
                UseOptimalPacking = true,
                ExportExternalAtlas = true,
                UseAstcPlatformSettings = true
            });

            var cnAsset = Build(new BuildRequest
            {
                SourceFontPath = DefaultCnSource,
                CharacterSetPath = DefaultCnCharacters,
                OutputDirectory = "CN",
                FileNameSuffix = "_CN",
                FallbackFontAsset = enAsset,
                PreserveExistingFallbackWhenEmpty = true,
                SamplingPointSize = 90,
                AtlasPadding = 9,
                AtlasSize = 4096,
                UseOptimalPacking = true,
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
                EditorGUILayout.HelpBox(lastActionMessage, lastActionMessageType);
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField(Text(TMPFontBuilderText.Source), EditorStyles.boldLabel);
            sourceFont = (Font)EditorGUILayout.ObjectField(Text(TMPFontBuilderText.FontFile), sourceFont, typeof(Font), false);
            var outputDirectoryContent = new GUIContent(
                Text(TMPFontBuilderText.OutputDirectory),
                Text(TMPFontBuilderText.OutputDirectoryTooltip));
            outputDirectory = EditorGUILayout.TextField(outputDirectoryContent, outputDirectory);
            var fileNameSuffixContent = new GUIContent(
                Text(TMPFontBuilderText.FileNameSuffix),
                Text(TMPFontBuilderText.FileNameSuffixTooltip));
            fileNameSuffix = EditorGUILayout.TextField(fileNameSuffixContent, fileNameSuffix);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField(Text(TMPFontBuilderText.Characters), EditorStyles.boldLabel);
            characterSetAsset = (TextAsset)EditorGUILayout.ObjectField(Text(TMPFontBuilderText.CharacterSetText), characterSetAsset, typeof(TextAsset), false);
            EditorGUILayout.LabelField(Text(TMPFontBuilderText.AdditionalCharacters));
            additionalCharacters = EditorGUILayout.TextArea(additionalCharacters, GUILayout.MinHeight(72));
            if (GUILayout.Button(Text(TMPFontBuilderText.ExtractCharacters)))
            {
                ExtractExistingFontCharacters();
                GUI.FocusControl(null);
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField(Text(TMPFontBuilderText.Fallback), EditorStyles.boldLabel);
            fallbackFontAsset = (TMP_FontAsset)EditorGUILayout.ObjectField(Text(TMPFontBuilderText.FallbackFont), fallbackFontAsset, typeof(TMP_FontAsset), false);
            var preserveFallbackContent = new GUIContent(
                Text(TMPFontBuilderText.PreserveExistingFallback),
                Text(TMPFontBuilderText.PreserveExistingFallbackTooltip));
            preserveExistingFallbackWhenEmpty = EditorGUILayout.Toggle(preserveFallbackContent, preserveExistingFallbackWhenEmpty);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField(Text(TMPFontBuilderText.Generation), EditorStyles.boldLabel);
            samplingPointSize = EditorGUILayout.IntField(
                new GUIContent(Text(TMPFontBuilderText.SamplingPointSize), Text(TMPFontBuilderText.SamplingPointSizeTooltip)),
                samplingPointSize);
            atlasPadding = EditorGUILayout.IntField(
                new GUIContent(Text(TMPFontBuilderText.AtlasPadding), Text(TMPFontBuilderText.AtlasPaddingTooltip)),
                atlasPadding);
            atlasSize = EditorGUILayout.IntField(
                new GUIContent(Text(TMPFontBuilderText.AtlasSize), Text(TMPFontBuilderText.AtlasSizeTooltip)),
                atlasSize);
            useOptimalPacking = EditorGUILayout.Toggle(
                new GUIContent(Text(TMPFontBuilderText.OptimalPacking), Text(TMPFontBuilderText.OptimalPackingTooltip)),
                useOptimalPacking);
            exportExternalAtlas = EditorGUILayout.Toggle(
                new GUIContent(Text(TMPFontBuilderText.ExportExternalAtlas), Text(TMPFontBuilderText.ExportExternalAtlasTooltip)),
                exportExternalAtlas);
            useAstcPlatformSettings = EditorGUILayout.Toggle(
                new GUIContent(Text(TMPFontBuilderText.AstcPlatformSettings), Text(TMPFontBuilderText.AstcPlatformSettingsTooltip)),
                useAstcPlatformSettings);

            DrawOutputPreview();

            EditorGUILayout.Space(16);
            if (GUILayout.Button(Text(TMPFontBuilderText.BuildFontAsset), GUILayout.Height(36)))
            {
                BuildFromWindow();
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawOutputPreview()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField(Text(TMPFontBuilderText.OutputPreview), EditorStyles.boldLabel);
            if (TryGetOutputPaths(out var assetPath, out var atlasPath, out _))
            {
                EditorGUILayout.SelectableLabel(assetPath, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                if (exportExternalAtlas)
                {
                    EditorGUILayout.SelectableLabel(atlasPath, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                }
            }
            else
            {
                EditorGUILayout.HelpBox(Text(TMPFontBuilderText.OutputPreviewHint), MessageType.Info);
            }
        }

        private bool ValidateCurrentSettings(out string error)
        {
            if (sourceFont == null)
            {
                error = Text(TMPFontBuilderText.SelectSourceFont);
                return false;
            }

            if (characterSetAsset == null)
            {
                error = Text(TMPFontBuilderText.SelectCharacterSet);
                return false;
            }

            var draft = CaptureDraft();
            if (!TMPFontBuilderPresetCollection.TryNormalizeOutputDirectory(draft.OutputDirectory, out _))
            {
                error = string.IsNullOrWhiteSpace(draft.OutputDirectory)
                    ? Text(TMPFontBuilderText.EmptyOutputDirectory)
                    : Text(TMPFontBuilderText.InvalidOutputDirectory);
                return false;
            }

            if (!TMPFontBuilderPresetCollection.TryNormalizeFileNameSuffix(draft.FileNameSuffix, out _))
            {
                error = Text(TMPFontBuilderText.InvalidFileNameSuffix);
                return false;
            }

            if (draft.SamplingPointSize <= 0)
            {
                error = Text(TMPFontBuilderText.InvalidSamplingPointSize);
                return false;
            }

            if (draft.AtlasPadding < 0)
            {
                error = Text(TMPFontBuilderText.InvalidAtlasPadding);
                return false;
            }

            if (draft.AtlasSize < 256 || draft.AtlasSize > 4096 || !Mathf.IsPowerOfTwo(draft.AtlasSize))
            {
                error = Text(TMPFontBuilderText.InvalidAtlasSize);
                return false;
            }

            error = null;
            return true;
        }

        private bool TryGetOutputPaths(out string assetPath, out string atlasPath, out string targetName)
        {
            assetPath = null;
            atlasPath = null;
            targetName = null;
            if (sourceFont == null
                || !TMPFontBuilderPresetCollection.TryNormalizeOutputDirectory(outputDirectory, out var normalizedOutputDirectory)
                || !TMPFontBuilderPresetCollection.TryNormalizeFileNameSuffix(fileNameSuffix, out var normalizedSuffix))
            {
                return false;
            }

            targetName = GetTargetAssetName(AssetDatabase.GetAssetPath(sourceFont), normalizedSuffix);
            if (string.IsNullOrEmpty(targetName))
            {
                return false;
            }

            var targetFolder = $"{FontAssetRoot}/{normalizedOutputDirectory}";
            assetPath = $"{targetFolder}/{targetName}.asset";
            atlasPath = $"{targetFolder}/{GetTargetAtlasAssetName(targetName)}.png";
            return true;
        }

        private bool TryMergeAndLoadCharacters(out string status, out string error)
        {
            var characterSetPath = AssetDatabase.GetAssetPath(characterSetAsset);
            if (string.IsNullOrEmpty(characterSetPath) || !characterSetPath.StartsWith("Assets/", StringComparison.Ordinal))
            {
                status = null;
                error = Text(TMPFontBuilderText.SelectCharacterSet);
                return false;
            }

            var originalCharacters = NormalizeCharacters(characterSetAsset.text);
            var mergedCharacters = NormalizeCharacters(originalCharacters + additionalCharacters);
            status = string.Empty;
            error = null;
            if (string.IsNullOrEmpty(mergedCharacters))
            {
                error = Text(TMPFontBuilderText.EmptyCharacterSet);
                return false;
            }

            if (string.IsNullOrEmpty(additionalCharacters))
            {
                return true;
            }

            if (mergedCharacters == originalCharacters)
            {
                additionalCharacters = string.Empty;
                status = Text(TMPFontBuilderText.CharacterSetAlreadyContained);
                return true;
            }

            try
            {
                var fullPath = ToFullPath(characterSetPath);
                var temporaryPath = fullPath + ".tmp";
                try
                {
                    File.WriteAllText(temporaryPath, mergedCharacters, new UTF8Encoding(false));
                    File.Copy(temporaryPath, fullPath, true);
                }
                finally
                {
                    if (File.Exists(temporaryPath))
                    {
                        File.Delete(temporaryPath);
                    }
                }
                AssetDatabase.ImportAsset(characterSetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                additionalCharacters = string.Empty;
                status = Format(TMPFontBuilderText.CharacterSetMerged, characterSetPath);
                return true;
            }
            catch (Exception exception)
            {
                error = Format(TMPFontBuilderText.CharacterSetWriteFailed, exception.Message);
                return false;
            }
        }

        private void ExtractExistingFontCharacters()
        {
            if (characterSetAsset == null)
            {
                SetStatus(Text(TMPFontBuilderText.SelectCharacterSet), MessageType.Error);
                return;
            }

            TMP_FontAsset targetFontAsset = null;
            if (TryGetOutputPaths(out var targetAssetPath, out _, out _))
            {
                targetFontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(targetAssetPath);
            }

            if (targetFontAsset == null && fallbackFontAsset == null)
            {
                SetStatus(Text(TMPFontBuilderText.NoExistingFont), MessageType.Warning);
                return;
            }

            var visitedFonts = new HashSet<TMP_FontAsset>();
            var fontCodePoints = new HashSet<int>();
            CollectFontCharacters(targetFontAsset, visitedFonts, fontCodePoints);
            CollectFontCharacters(fallbackFontAsset, visitedFonts, fontCodePoints);

            var characterSetCodePoints = new HashSet<int>(EnumerateCodePoints(characterSetAsset.text));
            var missingCodePoints = fontCodePoints.Where(codePoint => !characterSetCodePoints.Contains(codePoint)).ToList();
            missingCodePoints.Sort();
            var extractedCharacters = new StringBuilder(missingCodePoints.Count);
            foreach (var codePoint in missingCodePoints)
            {
                extractedCharacters.Append(char.ConvertFromUtf32(codePoint));
            }

            additionalCharacters = NormalizeCharacters(additionalCharacters + extractedCharacters);
            SetStatus(
                missingCodePoints.Count > 0
                    ? Format(TMPFontBuilderText.ExtractCompleted, visitedFonts.Count, missingCodePoints.Count)
                    : Format(TMPFontBuilderText.ExtractNoMissing, visitedFonts.Count),
                MessageType.Info);
        }

        private void BuildFromWindow()
        {
            if (!ValidateCurrentSettings(out var validationError))
            {
                SetStatus(validationError, MessageType.Error);
                return;
            }

            if (!TryGetOutputPaths(out var targetAssetPath, out _, out _))
            {
                SetStatus(Text(TMPFontBuilderText.InvalidPreset), MessageType.Error);
                return;
            }

            var existingFontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(targetAssetPath);
            if (existingFontAsset != null && !EditorUtility.DisplayDialog(
                    Text(TMPFontBuilderText.UpdateFontTitle),
                    Format(TMPFontBuilderText.UpdateFontMessage, targetAssetPath),
                    Text(TMPFontBuilderText.Update),
                    Text(TMPFontBuilderText.Cancel)))
            {
                return;
            }

            if (!TryMergeAndLoadCharacters(out var characterSetStatus, out var mergeError))
            {
                SetStatus(mergeError, MessageType.Error);
                return;
            }

            TMP_FontAsset result;
            string missingCharacters;
            try
            {
                result = Build(new BuildRequest
                {
                    SourceFontPath = AssetDatabase.GetAssetPath(sourceFont),
                    CharacterSetPath = AssetDatabase.GetAssetPath(characterSetAsset),
                    OutputDirectory = outputDirectory,
                    FileNameSuffix = fileNameSuffix,
                    FallbackFontAsset = GetValidatedFallback(),
                    PreserveExistingFallbackWhenEmpty = preserveExistingFallbackWhenEmpty,
                    SamplingPointSize = samplingPointSize,
                    AtlasPadding = atlasPadding,
                    AtlasSize = atlasSize,
                    UseOptimalPacking = useOptimalPacking,
                    ExportExternalAtlas = exportExternalAtlas,
                    UseAstcPlatformSettings = useAstcPlatformSettings,
                    ProgressTitle = Text(TMPFontBuilderText.GenerationProgressTitle),
                    ProgressMessage = Text(TMPFontBuilderText.GenerationProgressMessage)
                }, out missingCharacters);
            }
            catch (Exception exception)
            {
                SetStatus(Format(TMPFontBuilderText.BuildFailed, exception.Message), MessageType.Error);
                Debug.LogException(exception);
                return;
            }

            if (result == null)
            {
                SetStatus(Format(TMPFontBuilderText.BuildFailed, targetAssetPath), MessageType.Error);
                return;
            }

            Selection.activeObject = result;
            EditorGUIUtility.PingObject(result);
            if (string.IsNullOrEmpty(missingCharacters))
            {
                SetStatus(Format(TMPFontBuilderText.BuildCompleted, targetAssetPath, characterSetStatus), MessageType.Info);
            }
            else
            {
                SetStatus(Format(TMPFontBuilderText.BuildCompletedMissing, missingCharacters, characterSetStatus), MessageType.Warning);
            }
        }

        private static TMP_FontAsset Build(BuildRequest request)
        {
            return Build(request, out _);
        }

        private static TMP_FontAsset Build(BuildRequest request, out string missingCharacters)
        {
            missingCharacters = string.Empty;
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

            var targetName = GetTargetAssetName(request.SourceFontPath, request.FileNameSuffix);
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
            var existingFontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(targetAssetPath);
            var existingFallbacks = existingFontAsset != null
                ? new List<TMP_FontAsset>(existingFontAsset.fallbackFontAssetTable)
                : null;

            TMP_FontAsset fontAsset;
            try
            {
                if (request.UseOptimalPacking)
                {
                    EditorUtility.DisplayProgressBar(
                        request.ProgressTitle ?? "TMP Font Builder",
                        request.ProgressMessage ?? "Optimal packing...",
                        0.4f);
                }

                fontAsset = CreateGeneratedFontAsset(request, sourceFont, characters);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

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

            fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;
            ApplyFallback(fontAsset, request, existingFallbacks);
            missingCharacters = GetMissingCharacters(characters, fontAsset);

            var generatedFontAsset = fontAsset;
            fontAsset = SaveFontAsset(generatedFontAsset, targetAssetPath, request.ExportExternalAtlas);
            SaveExternalMaterial(fontAsset, targetMaterialPath);

            if (request.ExportExternalAtlas)
            {
                ExportAndBindAtlas(fontAsset, targetAssetPath, targetAtlasPath, request.AtlasSize, request.UseAstcPlatformSettings);
            }

            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (fontAsset != generatedFontAsset)
            {
                DestroyTransient(generatedFontAsset.material);
                DestroyTransient(generatedFontAsset);
            }

            if (!string.IsNullOrEmpty(missingCharacters))
            {
                Debug.LogWarning($"[TMPFontBuilder] Generated {targetAssetPath}, but missing characters: {missingCharacters}");
            }
            else
            {
                Debug.Log($"[TMPFontBuilder] Generated {targetAssetPath}");
            }

            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(targetAssetPath);
        }

        private static TMP_FontAsset CreateGeneratedFontAsset(BuildRequest request, Font sourceFont, string characters)
        {
            if (request.UseOptimalPacking)
            {
                return CreateOptimallyPackedFontAsset(request, sourceFont, characters);
            }

            var fontAsset = TMP_FontAsset.CreateFontAsset(
                sourceFont,
                request.SamplingPointSize,
                request.AtlasPadding,
                GlyphRenderMode.SDFAA,
                request.AtlasSize,
                request.AtlasSize,
                AtlasPopulationMode.Dynamic,
                false);
            if (fontAsset != null)
            {
                fontAsset.TryAddCharacters(characters, out _);
            }

            return fontAsset;
        }

        private static TMP_FontAsset CreateOptimallyPackedFontAsset(BuildRequest request, Font sourceFont, string characters)
        {
            FontEngine.InitializeFontEngine();
            if (FontEngine.LoadFontFace(sourceFont, request.SamplingPointSize) != FontEngineError.Success)
            {
                return null;
            }

            var glyphIndices = new List<uint>();
            var glyphUnicodeMap = new Dictionary<uint, List<uint>>();
            var characterLookup = new HashSet<uint>();
            foreach (var codePoint in EnumerateCodePoints(characters))
            {
                var unicode = (uint)codePoint;
                if (!characterLookup.Add(unicode) || !TryGetGlyphIndex(unicode, out var glyphIndex))
                {
                    continue;
                }

                if (!glyphUnicodeMap.TryGetValue(glyphIndex, out var unicodes))
                {
                    unicodes = new List<uint>();
                    glyphUnicodeMap.Add(glyphIndex, unicodes);
                    glyphIndices.Add(glyphIndex);
                }

                unicodes.Add(unicode);
            }

            var glyphsToPack = new List<Glyph>(glyphIndices.Count);
            var glyphsPacked = new List<Glyph>(glyphIndices.Count);
            var glyphLoadFlags = GlyphLoadFlags.LOAD_RENDER | GlyphLoadFlags.LOAD_NO_HINTING;
            foreach (var glyphIndex in glyphIndices)
            {
                if (!FontEngine.TryGetGlyphWithIndexValue(glyphIndex, glyphLoadFlags, out var glyph))
                {
                    continue;
                }

                if (glyph.glyphRect.width > 0 && glyph.glyphRect.height > 0)
                {
                    glyphsToPack.Add(glyph);
                }
                else
                {
                    glyphsPacked.Add(glyph);
                }
            }

            var freeGlyphRects = new List<GlyphRect>
            {
                new GlyphRect(0, 0, request.AtlasSize - 1, request.AtlasSize - 1)
            };
            var usedGlyphRects = new List<GlyphRect>();
            InvokeTryPackGlyphsInAtlas(
                glyphsToPack,
                glyphsPacked,
                request.AtlasPadding,
                GlyphPackingMode.ContactPointRule,
                GlyphRenderMode.SDFAA,
                request.AtlasSize,
                request.AtlasSize,
                freeGlyphRects,
                usedGlyphRects);

            var glyphsToRender = new List<Glyph>(glyphsPacked.Count);
            var glyphTable = new List<Glyph>(glyphsPacked.Count);
            var characterTable = new List<TMP_Character>(characterLookup.Count);
            foreach (var glyph in glyphsPacked)
            {
                glyphTable.Add(glyph);
                if (glyph.glyphRect.width > 0 && glyph.glyphRect.height > 0)
                {
                    glyphsToRender.Add(glyph);
                }

                if (!glyphUnicodeMap.TryGetValue(glyph.index, out var unicodes))
                {
                    continue;
                }

                foreach (var unicode in unicodes)
                {
                    characterTable.Add(new TMP_Character(unicode, glyph));
                }
            }

            var atlasBuffer = new byte[request.AtlasSize * request.AtlasSize];
            if (glyphsToRender.Count > 0)
            {
                InvokeRenderGlyphsToTexture(
                    glyphsToRender,
                    request.AtlasPadding,
                    GlyphRenderMode.SDFAA,
                    atlasBuffer,
                    request.AtlasSize,
                    request.AtlasSize);
            }

            var atlas = new Texture2D(request.AtlasSize, request.AtlasSize, TextureFormat.Alpha8, false, true);
            var colors = new Color32[atlasBuffer.Length];
            for (var index = 0; index < colors.Length; index++)
            {
                var alpha = atlasBuffer[index];
                colors[index] = new Color32(alpha, alpha, alpha, alpha);
            }

            atlas.SetPixels32(colors);
            atlas.Apply(false, false);

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
                DestroyImmediate(atlas);
                return null;
            }

            DestroyImmediate(fontAsset.atlasTextures[0]);
            fontAsset.atlasTextures = new[] { atlas };
            fontAsset.material.mainTexture = atlas;
            fontAsset.faceInfo = FontEngine.GetFaceInfo();
            fontAsset.glyphTable.Clear();
            fontAsset.glyphTable.AddRange(glyphTable);
            fontAsset.glyphTable.Sort((left, right) => left.index.CompareTo(right.index));
            fontAsset.characterTable.Clear();
            fontAsset.characterTable.AddRange(characterTable);
            fontAsset.characterTable.Sort((left, right) => left.unicode.CompareTo(right.unicode));
            fontAsset.ReadFontAssetDefinition();
            return fontAsset;
        }

        private static void InvokeTryPackGlyphsInAtlas(
            List<Glyph> glyphsToPack,
            List<Glyph> glyphsPacked,
            int padding,
            GlyphPackingMode packingMode,
            GlyphRenderMode renderMode,
            int atlasWidth,
            int atlasHeight,
            List<GlyphRect> freeGlyphRects,
            List<GlyphRect> usedGlyphRects)
        {
            var parameterTypes = new[]
            {
                typeof(List<Glyph>), typeof(List<Glyph>), typeof(int), typeof(GlyphPackingMode),
                typeof(GlyphRenderMode), typeof(int), typeof(int), typeof(List<GlyphRect>), typeof(List<GlyphRect>)
            };
            var method = typeof(FontEngine).GetMethod(
                "TryPackGlyphsInAtlas",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
                null,
                parameterTypes,
                null);
            if (method == null)
            {
                throw new MissingMethodException("Unity TextCore does not provide TryPackGlyphsInAtlas.");
            }

            method.Invoke(null, new object[]
            {
                glyphsToPack, glyphsPacked, padding, packingMode, renderMode,
                atlasWidth, atlasHeight, freeGlyphRects, usedGlyphRects
            });
        }

        private static void InvokeRenderGlyphsToTexture(
            List<Glyph> glyphsToRender,
            int padding,
            GlyphRenderMode renderMode,
            byte[] atlasBuffer,
            int atlasWidth,
            int atlasHeight)
        {
            var parameterTypes = new[]
            {
                typeof(List<Glyph>), typeof(int), typeof(GlyphRenderMode), typeof(byte[]), typeof(int), typeof(int)
            };
            var method = typeof(FontEngine).GetMethod(
                "RenderGlyphsToTexture",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
                null,
                parameterTypes,
                null);
            if (method == null)
            {
                throw new MissingMethodException("Unity TextCore does not provide RenderGlyphsToTexture.");
            }

            method.Invoke(null, new object[] { glyphsToRender, padding, renderMode, atlasBuffer, atlasWidth, atlasHeight });
        }

        private static bool TryGetGlyphIndex(uint unicode, out uint glyphIndex)
        {
            if (FontEngine.TryGetGlyphIndex(unicode, out glyphIndex) && glyphIndex != 0)
            {
                return true;
            }

            uint replacementUnicode;
            switch (unicode)
            {
                case 0xA0:
                    replacementUnicode = 0x20;
                    break;
                case 0xAD:
                case 0x2011:
                    replacementUnicode = 0x2D;
                    break;
                default:
                    glyphIndex = 0;
                    return false;
            }

            return FontEngine.TryGetGlyphIndex(replacementUnicode, out glyphIndex) && glyphIndex != 0;
        }

        private static void ApplyFallback(
            TMP_FontAsset fontAsset,
            BuildRequest request,
            List<TMP_FontAsset> existingFallbacks)
        {
            if (request.FallbackFontAsset != null)
            {
                fontAsset.fallbackFontAssetTable = new List<TMP_FontAsset> { request.FallbackFontAsset };
            }
            else if (request.PreserveExistingFallbackWhenEmpty && existingFallbacks != null)
            {
                fontAsset.fallbackFontAssetTable = existingFallbacks;
            }
            else
            {
                fontAsset.fallbackFontAssetTable = new List<TMP_FontAsset>();
            }
        }

        private static string GetMissingCharacters(string characters, TMP_FontAsset fontAsset)
        {
            var visitedFonts = new HashSet<TMP_FontAsset>();
            var availableCodePoints = new HashSet<int>();
            CollectFontCharacters(fontAsset, visitedFonts, availableCodePoints);
            var missingCodePoints = new HashSet<int>();
            var result = new StringBuilder();
            foreach (var codePoint in EnumerateCodePoints(characters))
            {
                if (!availableCodePoints.Contains(codePoint) && missingCodePoints.Add(codePoint))
                {
                    result.Append(char.ConvertFromUtf32(codePoint));
                }
            }

            return result.ToString();
        }

        private static void CollectFontCharacters(
            TMP_FontAsset fontAsset,
            HashSet<TMP_FontAsset> visitedFonts,
            HashSet<int> codePoints)
        {
            if (fontAsset == null || !visitedFonts.Add(fontAsset))
            {
                return;
            }

            foreach (var character in fontAsset.characterTable)
            {
                var codePoint = (int)character.unicode;
                if (codePoint <= char.MaxValue && (char.IsControl((char)codePoint) || char.IsSurrogate((char)codePoint)))
                {
                    continue;
                }

                if (codePoint <= 0x10FFFF)
                {
                    codePoints.Add(codePoint);
                }
            }

            foreach (var fallback in fontAsset.fallbackFontAssetTable)
            {
                CollectFontCharacters(fallback, visitedFonts, codePoints);
            }
        }

        private static TMP_FontAsset SaveFontAsset(TMP_FontAsset fontAsset, string targetAssetPath, bool exportExternalAtlas)
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(targetAssetPath);
            if (existing != null)
            {
                var oldEmbeddedAtlas = existing.atlasTextures != null && existing.atlasTextures.Length > 0
                    ? existing.atlasTextures[0]
                    : null;
                if (oldEmbeddedAtlas != null && AssetDatabase.GetAssetPath(oldEmbeddedAtlas) != targetAssetPath)
                {
                    oldEmbeddedAtlas = null;
                }

                var generatedAtlas = fontAsset.atlasTextures != null && fontAsset.atlasTextures.Length > 0
                    ? fontAsset.atlasTextures[0]
                    : null;
                EditorUtility.CopySerialized(fontAsset, existing);
                existing.name = fontAsset.name;
                if (!exportExternalAtlas && generatedAtlas != null)
                {
                    generatedAtlas.name = $"{fontAsset.name} Atlas";
                    AssetDatabase.AddObjectToAsset(generatedAtlas, existing);
                    existing.atlasTextures[0] = generatedAtlas;
                }

                if (oldEmbeddedAtlas != null && oldEmbeddedAtlas != generatedAtlas)
                {
                    AssetDatabase.RemoveObjectFromAsset(oldEmbeddedAtlas);
                    DestroyImmediate(oldEmbeddedAtlas);
                }

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

            return fontAsset;
        }

        private static void SaveExternalMaterial(TMP_FontAsset fontAsset, string targetMaterialPath)
        {
            if (fontAsset.material == null)
            {
                return;
            }

            var generatedMaterial = fontAsset.material;
            var existingMaterial = AssetDatabase.LoadAssetAtPath<Material>(targetMaterialPath);
            if (existingMaterial != null)
            {
                EditorUtility.CopySerialized(generatedMaterial, existingMaterial);
                existingMaterial.name = $"{fontAsset.name} Material";
                EditorUtility.SetDirty(existingMaterial);
                fontAsset.material = existingMaterial;
                DestroyTransient(generatedMaterial);
                return;
            }

            var material = new Material(generatedMaterial)
            {
                name = $"{fontAsset.name} Material"
            };
            AssetDatabase.CreateAsset(material, targetMaterialPath);
            fontAsset.material = material;
            DestroyTransient(generatedMaterial);
        }

        private static void ExportAndBindAtlas(
            TMP_FontAsset fontAsset,
            string fontAssetPath,
            string atlasPath,
            int atlasSize,
            bool useAstcPlatformSettings)
        {
            if (fontAsset.atlasTextures == null || fontAsset.atlasTextures.Length == 0 || fontAsset.atlasTextures[0] == null)
            {
                Debug.LogWarning($"[TMPFontBuilder] Font asset has no atlas texture: {fontAsset.name}");
                return;
            }

            var atlas = fontAsset.atlasTextures[0];
            var png = atlas.EncodeToPNG();
            if (png == null || png.Length == 0)
            {
                throw new InvalidOperationException("TMP atlas cannot be encoded as PNG.");
            }

            var fullPath = ToFullPath(atlasPath);
            var temporaryPath = fullPath + ".tmp";
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            try
            {
                File.WriteAllBytes(temporaryPath, png);
                File.Copy(temporaryPath, fullPath, true);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }

            AssetDatabase.ImportAsset(atlasPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            ApplyAtlasImporterSettings(atlasPath, atlasSize, useAstcPlatformSettings);

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

            if (string.IsNullOrEmpty(embeddedAtlasPath) || embeddedAtlasPath == fontAssetPath)
            {
                DestroyImmediate(atlas);
            }
        }

        private static void ApplyAtlasImporterSettings(string atlasPath, int atlasSize, bool useAstcPlatformSettings)
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
            importer.maxTextureSize = atlasSize;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;

            if (useAstcPlatformSettings)
            {
                SetPlatformSettings(importer, "Android", atlasSize);
                SetPlatformSettings(importer, "iPhone", atlasSize);
            }

            importer.SaveAndReimport();
        }

        private static void SetPlatformSettings(TextureImporter importer, string platform, int atlasSize)
        {
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name = platform,
                overridden = true,
                maxTextureSize = atlasSize,
                format = TextureImporterFormat.ASTC_6x6,
                textureCompression = TextureImporterCompression.CompressedHQ,
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

            return NormalizeCharacters(builder.ToString());
        }

        internal static string NormalizeCharacters(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return string.Empty;
            }

            var result = new StringBuilder(input.Length);
            var codePoints = new HashSet<int>();
            foreach (var codePoint in EnumerateCodePoints(input))
            {
                if (codePoints.Add(codePoint))
                {
                    result.Append(char.ConvertFromUtf32(codePoint));
                }
            }

            return result.ToString();
        }

        internal static IEnumerable<int> EnumerateCodePoints(string sourceText)
        {
            if (string.IsNullOrEmpty(sourceText))
            {
                yield break;
            }

            for (var index = 0; index < sourceText.Length; index++)
            {
                int codePoint;
                if (char.IsHighSurrogate(sourceText[index]))
                {
                    if (index + 1 >= sourceText.Length || !char.IsLowSurrogate(sourceText[index + 1]))
                    {
                        continue;
                    }

                    codePoint = char.ConvertToUtf32(sourceText[index], sourceText[index + 1]);
                    index++;
                }
                else if (char.IsLowSurrogate(sourceText[index]))
                {
                    continue;
                }
                else
                {
                    codePoint = sourceText[index];
                }

                if (codePoint <= char.MaxValue && char.IsControl((char)codePoint))
                {
                    continue;
                }

                yield return codePoint;
            }
        }

        private static string GetTargetAssetName(string sourceFontPath, string suffix)
        {
            var name = Path.GetFileNameWithoutExtension(sourceFontPath);
            if (string.IsNullOrEmpty(name))
            {
                Debug.LogError($"[TMPFontBuilder] 无法解析源字体文件名: {sourceFontPath}");
                return null;
            }

            var normalizedSuffix = suffix?.Trim() ?? string.Empty;
            return !string.IsNullOrEmpty(normalizedSuffix) && !name.EndsWith(normalizedSuffix, StringComparison.OrdinalIgnoreCase)
                ? name + normalizedSuffix
                : name;
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

        private static void DestroyTransient(UnityEngine.Object target)
        {
            if (target != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(target)))
            {
                DestroyImmediate(target);
            }
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

                using (new EditorGUI.DisabledScope(selectedPresetIndex <= 0))
                {
                    var moveUpContent = new GUIContent("↑", Text(TMPFontBuilderText.MovePresetUpTooltip));
                    if (GUILayout.Button(moveUpContent, GUILayout.Width(28)))
                    {
                        MoveSelectedPreset(selectedPresetIndex - 1);
                    }
                }

                using (new EditorGUI.DisabledScope(selectedPresetIndex >= presetCollection.Presets.Count - 1))
                {
                    var moveDownContent = new GUIContent("↓", Text(TMPFontBuilderText.MovePresetDownTooltip));
                    if (GUILayout.Button(moveDownContent, GUILayout.Width(28)))
                    {
                        MoveSelectedPreset(selectedPresetIndex + 1);
                    }
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
                FileNameSuffix = isChinese ? "_CN" : "_EN",
                PreserveExistingFallbackWhenEmpty = true,
                SamplingPointSize = 90,
                AtlasPadding = 9,
                AtlasSize = isChinese ? 4096 : 1024,
                UseOptimalPacking = true,
                ExportExternalAtlas = true,
                UseAstcPlatformSettings = true
            };
        }

        private void AddPreset()
        {
            Undo.RecordObject(presetCollection, "Add TMP Font Preset");
            var addedPreset = presetCollection.AddCopy(CaptureDraft());
            SavePresetCollection();
            selectedPresetIndex = presetCollection.Presets.Count - 1;
            LoadSelectedPreset();
            additionalCharacters = string.Empty;
            lastActionMessage = Format(TMPFontBuilderText.AddedPreset, addedPreset.PresetName);
            lastActionMessageType = MessageType.Info;
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
            if (!confirmed)
            {
                return;
            }

            Undo.RecordObject(presetCollection, "Remove TMP Font Preset");
            if (!presetCollection.RemoveAt(selectedPresetIndex))
            {
                return;
            }

            SavePresetCollection();
            selectedPresetIndex = Mathf.Clamp(selectedPresetIndex, 0, presetCollection.Presets.Count - 1);
            LoadSelectedPreset();
            additionalCharacters = string.Empty;
            lastActionMessage = Format(TMPFontBuilderText.RemovedPreset, removedName);
            lastActionMessageType = MessageType.Info;
        }

        private void MoveSelectedPreset(int targetIndex)
        {
            if (!ConfirmUnsavedChanges())
            {
                return;
            }

            Undo.RecordObject(presetCollection, "Move TMP Font Preset");
            if (!presetCollection.Move(selectedPresetIndex, targetIndex))
            {
                return;
            }

            selectedPresetIndex = targetIndex;
            additionalCharacters = string.Empty;
            SavePresetCollection();
            LoadSelectedPreset();
        }

        private void ReloadSelectedPreset()
        {
            if (!ConfirmUnsavedChanges())
            {
                return;
            }

            LoadSelectedPreset();
            additionalCharacters = string.Empty;
            lastActionMessage = Format(TMPFontBuilderText.LoadedPreset, presetName);
            lastActionMessageType = MessageType.Info;
        }

        private bool SaveSelectedPreset()
        {
            if (!presetCollection.TryUpdateAt(selectedPresetIndex, CaptureDraft(), out var error))
            {
                lastActionMessage = GetValidationMessage(error);
                lastActionMessageType = MessageType.Error;
                return false;
            }

            SavePresetCollection();
            LoadSelectedPreset();
            lastActionMessage = Format(TMPFontBuilderText.SavedPreset, presetName);
            lastActionMessageType = MessageType.Info;
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
            additionalCharacters = string.Empty;
            lastActionMessage = Format(TMPFontBuilderText.LoadedPreset, presetName);
            lastActionMessageType = MessageType.Info;
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
            fileNameSuffix = preset.FileNameSuffix;
            preserveExistingFallbackWhenEmpty = preset.PreserveExistingFallbackWhenEmpty;
            samplingPointSize = preset.SamplingPointSize;
            atlasPadding = preset.AtlasPadding;
            atlasSize = preset.AtlasSize;
            useOptimalPacking = preset.UseOptimalPacking;
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
                FileNameSuffix = fileNameSuffix,
                PreserveExistingFallbackWhenEmpty = preserveExistingFallbackWhenEmpty,
                SamplingPointSize = samplingPointSize,
                AtlasPadding = atlasPadding,
                AtlasSize = atlasSize,
                UseOptimalPacking = useOptimalPacking,
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
                case TMPFontBuilderPresetValidationError.InvalidFileNameSuffix:
                    return Text(TMPFontBuilderText.InvalidFileNameSuffix);
                case TMPFontBuilderPresetValidationError.InvalidSamplingPointSize:
                    return Text(TMPFontBuilderText.InvalidSamplingPointSize);
                case TMPFontBuilderPresetValidationError.InvalidAtlasPadding:
                    return Text(TMPFontBuilderText.InvalidAtlasPadding);
                case TMPFontBuilderPresetValidationError.InvalidAtlasSize:
                    return Text(TMPFontBuilderText.InvalidAtlasSize);
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

        private void SetStatus(string message, MessageType messageType)
        {
            lastActionMessage = message;
            lastActionMessageType = messageType;
            Repaint();
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
            var targetName = GetTargetAssetName(AssetDatabase.GetAssetPath(sourceFont), fileNameSuffix);
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
            public string OutputDirectory;
            public string FileNameSuffix;
            public TMP_FontAsset FallbackFontAsset;
            public bool PreserveExistingFallbackWhenEmpty;
            public int SamplingPointSize;
            public int AtlasPadding;
            public int AtlasSize;
            public bool UseOptimalPacking;
            public bool ExportExternalAtlas;
            public bool UseAstcPlatformSettings;
            public string ProgressTitle;
            public string ProgressMessage;
        }
    }
}
