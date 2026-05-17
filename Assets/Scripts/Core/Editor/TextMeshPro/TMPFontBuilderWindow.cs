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

        private const string DefaultCnSource = SourceRoot + "/CN/HarmonyOS.ttf";
        private const string DefaultEnSource = SourceRoot + "/EN/BebasNeue_EN.OTF";
        private const string DefaultCnCharacters = FallbackRoot + "/Default_CN_Characters.txt";
        private const string DefaultEnCharacters = FallbackRoot + "/Default_EN_Characters.txt";

        private Font sourceFont;
        private TextAsset characterSetAsset;
        private TMP_FontAsset fallbackFontAsset;
        private FontLanguage language = FontLanguage.CN;
        private string inlineCharacters = string.Empty;
        private int samplingPointSize = 90;
        private int atlasPadding = 9;
        private int cnAtlasSize = 4096;
        private int enAtlasSize = 1024;
        private bool exportExternalAtlas = true;
        private bool useAstcPlatformSettings = true;

        private enum FontLanguage
        {
            CN,
            EN
        }

        [MenuItem("Tools/SleepyDemos/TextMesh Pro/Font Builder")]
        public static void Open()
        {
            var window = GetWindow<TMPFontBuilderWindow>("TMP Font Builder");
            window.minSize = new Vector2(480, 560);
            window.LoadDefaults();
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
                Language = FontLanguage.EN,
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
                Language = FontLanguage.CN,
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
            LoadDefaults();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Source", EditorStyles.boldLabel);
            sourceFont = (Font)EditorGUILayout.ObjectField("Font File", sourceFont, typeof(Font), false);
            language = (FontLanguage)EditorGUILayout.EnumPopup("Language", language);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Characters", EditorStyles.boldLabel);
            characterSetAsset = (TextAsset)EditorGUILayout.ObjectField("Character Set Text", characterSetAsset, typeof(TextAsset), false);
            EditorGUILayout.LabelField("Inline Characters");
            inlineCharacters = EditorGUILayout.TextArea(inlineCharacters, GUILayout.MinHeight(120));

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Fallback", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(language != FontLanguage.CN))
            {
                fallbackFontAsset = (TMP_FontAsset)EditorGUILayout.ObjectField("CN Fallback Font", fallbackFontAsset, typeof(TMP_FontAsset), false);
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Generation", EditorStyles.boldLabel);
            samplingPointSize = EditorGUILayout.IntField("Sampling Point Size", samplingPointSize);
            atlasPadding = EditorGUILayout.IntField("Atlas Padding", atlasPadding);
            cnAtlasSize = EditorGUILayout.IntField("CN Atlas Size", cnAtlasSize);
            enAtlasSize = EditorGUILayout.IntField("EN Atlas Size", enAtlasSize);
            exportExternalAtlas = EditorGUILayout.Toggle("Export External Atlas", exportExternalAtlas);
            useAstcPlatformSettings = EditorGUILayout.Toggle("ASTC Platform Settings", useAstcPlatformSettings);

            EditorGUILayout.Space(16);
            using (new EditorGUI.DisabledScope(sourceFont == null))
            {
                if (GUILayout.Button("Build TMP Font Asset", GUILayout.Height(36)))
                {
                    BuildFromWindow();
                }
            }

            if (GUILayout.Button("Build Default CN EN Fonts", GUILayout.Height(28)))
            {
                BuildDefaultProjectFonts();
            }
        }

        private void LoadDefaults()
        {
            EnsureFolders();

            sourceFont = AssetDatabase.LoadAssetAtPath<Font>(language == FontLanguage.CN ? DefaultCnSource : DefaultEnSource);
            characterSetAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(language == FontLanguage.CN ? DefaultCnCharacters : DefaultEnCharacters);
            fallbackFontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetRoot + "/EN/BebasNeue_EN.asset");
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
                Language = language,
                FallbackFontAsset = language == FontLanguage.CN ? fallbackFontAsset : null,
                SamplingPointSize = samplingPointSize,
                AtlasPadding = atlasPadding,
                AtlasSize = language == FontLanguage.CN ? cnAtlasSize : enAtlasSize,
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

            var targetName = GetTargetAssetName(request.SourceFontPath, request.Language);
            var targetFolder = $"{FontAssetRoot}/{request.Language}";
            EnsureFolder(targetFolder);

            var targetAssetPath = $"{targetFolder}/{targetName}.asset";
            var targetMaterialPath = $"{MaterialRoot}/{targetName}.mat";
            var targetAtlasPath = $"{targetFolder}/{targetName}.png";

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
            fontAsset.fallbackFontAssetTable = request.Language == FontLanguage.CN && request.FallbackFontAsset != null
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

        private static string GetTargetAssetName(string sourceFontPath, FontLanguage language)
        {
            var name = Path.GetFileNameWithoutExtension(sourceFontPath);
            var suffix = "_" + language;

            if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return name;
            }

            if (name.EndsWith(" SDF", StringComparison.OrdinalIgnoreCase))
            {
                name = name.Substring(0, name.Length - 4);
            }

            return name + suffix;
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

        private sealed class BuildRequest
        {
            public string SourceFontPath;
            public string CharacterSetPath;
            public string InlineCharacters;
            public FontLanguage Language;
            public TMP_FontAsset FallbackFontAsset;
            public int SamplingPointSize;
            public int AtlasPadding;
            public int AtlasSize;
            public bool ExportExternalAtlas;
            public bool UseAstcPlatformSettings;
        }
    }
}
