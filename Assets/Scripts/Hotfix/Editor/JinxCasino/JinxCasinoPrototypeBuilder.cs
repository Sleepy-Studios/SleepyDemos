using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Core.Editor.MvcBind;
using Core.Runtime;
using Core.Runtime.Networking;
using Hotfix.JinxCasino.Adapters;
using Hotfix.JinxCasino.Adapters.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.TextCore.LowLevel;
using Object = UnityEngine.Object;

namespace Hotfix.Editor.JinxCasino
{
    /// 保存 P0 赌场场景与 HUD；生成资产可在 Inspector 中继续维护，不在运行期搭建界面。
    public static partial class JinxCasinoPrototypeBuilder
    {
        private const string Root = "Assets/LoadResources/Demos/jinx_casino";
        private const string HudPath = Root + "/Prefabs/UI/JinxCasinoHudView.prefab";
        private const string HubPath = "Assets/LoadResources/UI/Hall/MainMenuView.prefab";
        private const string ModuleOutput = "Assets/Scripts/Hotfix/Demos/JinxCasino/Adapters/UI";
        private const string FontPath = Root + "/Art/Fonts/CasinoHudFont.asset";
        private const string SourceFontPath = "Assets/LoadResources/Fonts/Source/CN/HarmonyOS_CN.ttf";

        /// 在非 Play 模式生成正式保存的 P0 原型与 Hub 入口，不写网络应用 ID。
        [MenuItem("Tools/SleepyDemos/整蛊赌场/生成P0原型与Hub入口")]
        public static void BuildPrototypeAndHub()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("请在非 Play 模式装配 P0 原型。");
            if (EditorApplication.isCompiling) throw new InvalidOperationException("请等待脚本编译完成。");
            Scene original = SceneManager.GetActiveScene();
            Scene staging = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                // HUD 临时对象也属于本次装配场景；创建再销毁仍可能污染用户场景 dirty 标记。
                // NewScene 已可能把新场景设为 active；不把重复切换的 false 当作失败。
                if (SceneManager.GetActiveScene() != staging && !SceneManager.SetActiveScene(staging))
                    throw new InvalidOperationException("无法激活临时装配场景。");
                EnsureFolder(Root + "/Scenes");
                EnsureFolder(Root + "/Prefabs/UI");
                EnsureFolder(Root + "/Art/Materials");
                EnsureFolder(Root + "/Art/Fonts");
                EnsureFolder(Root + "/Data");
                var font = EnsurePrototypeFont();
                EnsureSharedChineseFallback();
                var settings = EnsureSessionSettings();
                BuildHud(font);
                BuildScene(settings, font, staging);
                AddHubButton(font);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("[倒霉蛋俱乐部] " + (buildFormalArt ? "P4正式资源" : buildFullRegions ? "P2四区" : buildAdventure ? "P1完整局" : "P0原型") + "场景、HUD与Hub入口已保存。当前运行单人离线规则。");
            }
            finally
            {
                if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
                if (staging.IsValid() && staging.isLoaded) EditorSceneManager.CloseScene(staging, true);
            }
        }

        private static NetworkSessionSettings EnsureSessionSettings()
        {
            string path = Root + "/Data/SessionSettings.asset";
            var settings = AssetDatabase.LoadAssetAtPath<NetworkSessionSettings>(path);
            if (settings != null) return settings;
            settings = ScriptableObject.CreateInstance<NetworkSessionSettings>();
            AssetDatabase.CreateAsset(settings, path);
            return settings;
        }

        private static TMP_FontAsset EnsurePrototypeFont()
        {
            string characters = CollectPrototypeCharacters();
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (existing != null && existing.atlasPopulationMode == AtlasPopulationMode.Static && existing.HasCharacters(characters) &&
                existing.atlasTextures != null && existing.atlasTextures.Length == 1 && existing.atlasTextures[0] != null &&
                AssetDatabase.GetAssetPath(existing.atlasTextures[0]) == FontPath)
                return PersistAndVerifyFontMaterial(existing);
            var source = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
            if (source == null) throw new InvalidOperationException("缺少赌场字库源字体：" + SourceFontPath);
            // 与现有 TMPFontBuilder 普通模式相同，先填充单张 SDF 图集，完成后冻结为 Static。
            var generated = TMP_FontAsset.CreateFontAsset(source, 48, 5, GlyphRenderMode.SDFAA,
                4096, 4096, AtlasPopulationMode.Dynamic, false);
            if (generated == null) throw new InvalidOperationException("赌场专用 TMP 字库创建失败。");
            generated.TryAddCharacters(characters, out string missing);
            if (!string.IsNullOrEmpty(missing))
            {
                var rejectedAtlases = generated.atlasTextures;
                var rejectedMaterial = generated.material;
                generated.atlasTextures = Array.Empty<Texture2D>();
                generated.material = null;
                foreach (var atlas in rejectedAtlases) if (atlas != null) Object.DestroyImmediate(atlas);
                if (rejectedMaterial != null) Object.DestroyImmediate(rejectedMaterial);
                Object.DestroyImmediate(generated);
                throw new InvalidOperationException("赌场专用字体存在缺字或图集容量不足：" + missing);
            }
            generated.name = "CasinoHudFont";
            generated.atlasPopulationMode = AtlasPopulationMode.Static;
            generated.fallbackFontAssetTable = new List<TMP_FontAsset>();
            var transientMaterial = generated.material;
            // CopySerialized 不保证目标的已缓存 atlas 数组指向新纹理，必须提前保存并显式转交。
            var generatedAtlases = (Texture2D[])generated.atlasTextures.Clone();
            // 独立保存材质参数模板，不能在字体 / atlas 持久化前序列化其瞬态纹理引用。
            var materialTemplate = new Material(transientMaterial);
            generated.material = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Art/Materials/CasinoHudFont.mat");
            var oldAtlases = new List<Texture2D>();
            if (existing != null)
            {
                // 包括此前失败留下的孤立 atlas 子资产，而不只检查可能已经为空的引用数组。
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(FontPath))
                    if (asset is Texture2D oldAtlas) oldAtlases.Add(oldAtlas);
            }
            var saved = generated;
            if (existing == null) AssetDatabase.CreateAsset(generated, FontPath);
            else
            {
                // 更新已有专用字库时保留 Font Asset / Material GUID，避免场景和 Prefab 引用漂移。
                EditorUtility.CopySerialized(generated, existing);
                existing.name = generated.name;
                saved = existing;
            }
            foreach (var atlas in generatedAtlases)
            {
                if (atlas == null) continue;
                atlas.name = "CasinoHudFont Atlas";
                if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(atlas))) AssetDatabase.AddObjectToAsset(atlas, saved);
            }
            saved.atlasTextures = generatedAtlases;
            if (saved != generated)
            {
                // TMP_FontAsset.OnDestroy 会直接销毁 atlas 与 material；转交后立即解除临时字体所有权。
                generated.atlasTextures = Array.Empty<Texture2D>();
                generated.material = null;
            }
            foreach (var oldAtlas in oldAtlases)
            {
                if (oldAtlas == null || Array.IndexOf(generatedAtlases, oldAtlas) >= 0 || AssetDatabase.GetAssetPath(oldAtlas) != FontPath) continue;
                AssetDatabase.RemoveObjectFromAsset(oldAtlas);
                Object.DestroyImmediate(oldAtlas);
            }
            EditorUtility.SetDirty(saved);
            try
            {
                // 先把 atlas 作为 font 子资产写入并重载，再把真实持久化引用写入外部材质。
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(FontPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                saved = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
                return PersistAndVerifyFontMaterial(saved, materialTemplate);
            }
            finally
            {
                Object.DestroyImmediate(materialTemplate);
                if (transientMaterial != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(transientMaterial)))
                    Object.DestroyImmediate(transientMaterial);
                if (saved != generated && generated != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(generated)))
                {
                    generated.atlasTextures = Array.Empty<Texture2D>();
                    generated.material = null;
                    Object.DestroyImmediate(generated);
                }
            }
        }

        private static TMP_FontAsset PersistAndVerifyFontMaterial(TMP_FontAsset font, Material template = null)
        {
            if (font == null || font.atlasTextures == null || font.atlasTextures.Length != 1 || font.atlasTextures[0] == null)
                throw new InvalidOperationException("赌场字体缺少单张持久化图集。");
            var atlas = font.atlasTextures[0];
            if (AssetDatabase.GetAssetPath(atlas) != FontPath)
                throw new InvalidOperationException("赌场字体图集尚未持久化为 Font Asset 子资产。");
            string materialPath = Root + "/Art/Materials/CasinoHudFont.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            var source = template != null ? template : font.material;
            if (material == null)
            {
                if (source == null) throw new InvalidOperationException("赌场字体缺少可恢复的 SDF 材质模板。");
                material = new Material(source) { name = "CasinoHudFont Material" };
                // atlas 已经成为真实子资产，此时 CreateAsset 才能保存有效纹理引用。
                material.SetTexture("_MainTex", atlas);
                AssetDatabase.CreateAsset(material, materialPath);
            }
            else if (template != null) EditorUtility.CopySerialized(template, material);
            material.name = "CasinoHudFont Material";
            if (!material.HasProperty("_MainTex")) throw new InvalidOperationException("赌场字体材质不是带 _MainTex 的 SDF 材质。");
            material.SetTexture("_MainTex", atlas);
            material.SetFloat("_TextureWidth", atlas.width);
            material.SetFloat("_TextureHeight", atlas.height);
            font.material = material;
            font.ReadFontAssetDefinition();
            EditorUtility.SetDirty(material);
            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(materialPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(FontPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var persistedFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            var persistedMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            var persistedTexture = persistedMaterial != null ? persistedMaterial.GetTexture("_MainTex") : null;
            if (persistedFont == null || persistedFont.material != persistedMaterial || persistedTexture == null ||
                persistedFont.atlasTextures == null || persistedFont.atlasTextures.Length != 1 ||
                persistedTexture != persistedFont.atlasTextures[0] || AssetDatabase.GetAssetPath(persistedTexture) != FontPath)
                throw new InvalidOperationException("赌场字体保存后重载校验失败：SDF 材质 _MainTex 必须指向同字体的持久化 atlas。");
            return persistedFont;
        }

        private static string CollectPrototypeCharacters()
        {
            var characters = new SortedSet<char>();
            for (char value = ' '; value <= '~'; value++) characters.Add(value);
            foreach (string directory in new[]
            {
                "Assets/Scripts/Hotfix/Demos/JinxCasino", "Assets/Scripts/Hotfix/Editor/JinxCasino",
                "Assets/Scripts/Core/Runtime/Networking"
            })
            {
                foreach (string file in Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories))
                foreach (char value in File.ReadAllText(file))
                {
                    // 所有 P0 中文及界面标点都静态入图，不收集源码控制字符，也不依赖共享字体的动态回退。
                    if ((value >= '\u00a0' && value <= '\u00ff') || (value >= '\u4e00' && value <= '\u9fff') || (value >= '\u3000' && value <= '\u303f') ||
                        (value >= '\uff00' && value <= '\uffef') || (value >= '\u2000' && value <= '\u22ff'))
                        characters.Add(value);
                }
            }
            var result = new StringBuilder(characters.Count);
            foreach (char value in characters) result.Append(value);
            return result.ToString();
        }

        private static void BuildHud(TMP_FontAsset font)
        {
            var root = new GameObject("JinxCasinoHudView", typeof(RectTransform), typeof(CanvasGroup));
            try
            {
                Stretch(root.GetComponent<RectTransform>());
                var presenter = root.AddComponent<JinxCasinoHudPresenter>();
                var serialized = new SerializedObject(presenter);
                var top = Panel("SessionPanel", root.transform, new Vector2(26, -24), new Vector2(835, 280),
                    new Vector2(0, 1), new Vector2(0, 1), new Color(0.025f, 0.04f, 0.07f, 0.88f));
                Label("Title", top, font, "倒霉蛋俱乐部 · P0 / 离线验证入口", new Vector2(20, -12), new Vector2(795, 44), 32);
                SetReference(serialized, "statusText", Label("Status", top, font, "请选择离线验证或互联网房间。", new Vector2(20, -60), new Vector2(795, 42), 28));
                SetReference(serialized, "roomText", Label("Room", top, font, "房间：未加入", new Vector2(20, -104), new Vector2(795, 42), 28));
                SetReference(serialized, "balanceText", Label("Balance", top, font, "筹码：等待会话", new Vector2(20, -148), new Vector2(795, 42), 28));
                SetReference(serialized, "resultText", Label("Result", top, font, "左侧移动，右侧转向；选择游戏桌下注。", new Vector2(20, -192), new Vector2(795, 80), 28));

                var lobby = Panel("LobbyPanel", root.transform, new Vector2(26, -320), new Vector2(835, 260),
                    new Vector2(0, 1), new Vector2(0, 1), new Color(0.025f, 0.04f, 0.07f, 0.88f));
                Label("LobbyTitle", lobby, font, "好友房间 · 互联网需 SDK 与 App ID", new Vector2(20, -12), new Vector2(795, 44), 28);
                SetReference(serialized, "nameInput", Input("NameInput", lobby, font, "玩家", "昵称", new Vector2(20, -64), new Vector2(235, 80)));
                SetReference(serialized, "codeInput", Input("CodeInput", lobby, font, string.Empty, "房间码 V1-hk-ABCDEFGH", new Vector2(270, -64), new Vector2(540, 80)));
                SetReference(serialized, "offlineButton", Button("OfflineButton", lobby, font, "单人离线验证", new Vector2(20, -165), new Vector2(185, 80)));
                SetReference(serialized, "createButton", Button("CreateButton", lobby, font, "创建互联网房", new Vector2(220, -165), new Vector2(185, 80)));
                SetReference(serialized, "joinButton", Button("JoinButton", lobby, font, "加入互联网房", new Vector2(420, -165), new Vector2(185, 80)));
                SetReference(serialized, "leaveButton", Button("LeaveButton", lobby, font, "离开房间", new Vector2(620, -165), new Vector2(185, 80)));

                var games = Panel("GamesPanel", root.transform, new Vector2(26, -594), new Vector2(835, 240),
                    new Vector2(0, 1), new Vector2(0, 1), new Color(0.025f, 0.04f, 0.07f, 0.9f));
                Label("StakeLabel", games, font, "下注", new Vector2(20, -30), new Vector2(105, 44), 28);
                SetReference(serialized, "stakeInput", Input("StakeInput", games, font, "100", "正整数", new Vector2(120, -14), new Vector2(240, 80), true));
                Label("ChoiceLabel", games, font, "选号 0–36", new Vector2(385, -30), new Vector2(195, 44), 28);
                SetReference(serialized, "choiceInput", Input("ChoiceInput", games, font, "0", "硬币 0 或 1", new Vector2(580, -14), new Vector2(230, 80), true));
                SetReference(serialized, "slotsButton", Button("SlotsButton", games, font, "老虎机", new Vector2(20, -108), new Vector2(250, 80)));
                SetReference(serialized, "rouletteButton", Button("RouletteButton", games, font, "轮盘", new Vector2(290, -108), new Vector2(250, 80)));
                SetReference(serialized, "coinButton", Button("CoinButton", games, font, "猜硬币", new Vector2(560, -108), new Vector2(250, 80)));
                Label("GameHelp", games, font, "P0：三款规则与会话验证，使用虚拟筹码。", new Vector2(20, -195), new Vector2(795, 40), 28);

                SetReference(serialized, "movePad", TouchPad("MovePad", root.transform, font, "移动", new Vector2(32, 30), new Vector2(0, 0), new Vector2(0, 0)));
                SetReference(serialized, "lookPad", TouchPad("LookPad", root.transform, font, "视角", new Vector2(-32, 30), new Vector2(1, 0), new Vector2(1, 0)));
                var back = Button("BackButton", root.transform, font, "返回 Hub", new Vector2(-26, -180), new Vector2(210, 80));
                var backRect = back.GetComponent<RectTransform>();
                backRect.anchorMin = backRect.anchorMax = new Vector2(1, 1);
                backRect.pivot = new Vector2(1, 1);
                SetReference(serialized, "backButton", back);
                if (buildAdventure)
                {
                    SetReference(serialized, "adventurePresenter", BuildAdventureHud(root.transform, font));
                    var panels = serialized.FindProperty("legacyPanels");
                    panels.arraySize = 4;
                    var legacyObjects = new[] { top.gameObject, lobby.gameObject, games.gameObject, back.gameObject };
                    for (int index = 0; index < legacyObjects.Length; index++) panels.GetArrayElementAtIndex(index).objectReferenceValue = legacyObjects[index];
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();

                var nodes = MvcPrefabScanner.Scan(root);
                foreach (var node in nodes)
                    if (node.gameObject == root) Select(node, typeof(JinxCasinoHudPresenter));
                var config = new MvcBindSettings
                {
                    prefabPath = HudPath, moduleName = "JinxCasino", viewName = "JinxCasinoHudView", namespaceName = "Hotfix",
                    useCustomModuleOutputDirectory = true, customModuleOutputDirectory = ModuleOutput,
                    address = MvcBindPathUtility.ToRuntimeAddress(HudPath), viewType = ViewType.View,
                    layer = UILayer.Base, viewMode = UIViewMode.Page, mask = MaskType.None,
                    isHotfix = true, isAsync = true, enableOnInit = true, destroyOnHide = true
                };
                config.outputFolder = MvcBindPathUtility.ToOutputFolder(config);
                if (!MvcBindComponentWindowBridge.GenerateAndBind(root, config, nodes, false, out _, out string message))
                    throw new InvalidOperationException(message);
                if (buildFormalArt) ApplyFormalHudAssets(root);
                PrefabUtility.SaveAsPrefabAsset(root, HudPath);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void BuildScene(NetworkSessionSettings settings, TMP_FontAsset font, Scene scene)
        {
            Scene previous = SceneManager.GetActiveScene();
            // HUD 临时对象已经回收，复用外层装配场景；Unity 禁止对未保存的临时场景再开 Additive。
            try
            {
                SceneManager.SetActiveScene(scene);
                var player = new GameObject("LocalPlayer", typeof(CharacterController));
                player.transform.position = new Vector3(0, 0.03f, -4.5f);
                var body = player.GetComponent<CharacterController>();
                body.height = 1.8f;
                body.center = new Vector3(0, 0.9f, 0);
                body.radius = 0.3f;
                body.stepOffset = 0.25f;
                var camera = new GameObject("MainCamera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
                camera.transform.SetParent(player.transform, false);
                camera.transform.localPosition = new Vector3(0, 1.6f, 0);
                camera.tag = "MainCamera";
                camera.nearClipPlane = 0.08f;
                camera.farClipPlane = 70;
                camera.fieldOfView = 65;
                camera.allowHDR = false;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.02f, 0.025f, 0.045f);
                var cameraData = camera.GetUniversalAdditionalCameraData();
                cameraData.renderPostProcessing = false;
                cameraData.requiresColorTexture = false;
                cameraData.requiresDepthTexture = false;
                var light = new GameObject("MainLight").AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.4f;
                light.shadows = LightShadows.None;
                light.transform.rotation = Quaternion.Euler(48, -30, 0);
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.42f, 0.44f, 0.5f);
                RenderSettings.skybox = null;

                var floor = Material("CasinoFloor", new Color(0.1f, 0.055f, 0.11f));
                var wall = Material("CasinoWall", new Color(0.12f, 0.18f, 0.24f));
                var gold = Material("CasinoGold", new Color(0.88f, 0.63f, 0.2f));
                var green = Material("TableFelt", new Color(0.035f, 0.36f, 0.25f));
                var dark = Material("MachineDark", new Color(0.045f, 0.055f, 0.075f));
                var bright = Material("MachineDisplay", new Color(0.32f, 0.72f, 0.86f));
                Primitive("Floor", PrimitiveType.Cube, new Vector3(0, -0.15f, 0), new Vector3(18, 0.3f, 15), floor);
                Primitive("BackWall", PrimitiveType.Cube, new Vector3(0, 2, 7.5f), new Vector3(18, 4, 0.25f), wall);
                Primitive("LeftWall", PrimitiveType.Cube, new Vector3(-9, 2, 0), new Vector3(0.25f, 4, 15), wall);
                Primitive("RightWall", PrimitiveType.Cube, new Vector3(9, 2, 0), new Vector3(0.25f, 4, 15), wall);
                Primitive("BackGoldTrim", PrimitiveType.Cube, new Vector3(0, 2.75f, 7.28f), new Vector3(17.7f, 0.15f, 0.12f), gold);
                WorldLabel("CasinoSign", font, "倒霉蛋俱乐部 · P0", new Vector3(0, 3.8f, 7.1f), 8);
                var anchors = new Transform[3];
                string[] stationNames = { "Slots", "Roulette", "CoinFlip" };
                string[] labels = { "老虎机", "轮盘", "猜硬币" };
                for (int i = 0; i < anchors.Length; i++)
                {
                    float x = (i - 1) * 5;
                    var station = new GameObject(stationNames[i] + "Station");
                    var anchor = new GameObject(stationNames[i] + "Anchor").transform;
                    anchor.SetParent(station.transform, false);
                    anchor.position = new Vector3(x, 0, 1.3f);
                    anchors[i] = anchor;
                    Primitive(stationNames[i] + "Leg", PrimitiveType.Cube, new Vector3(x, 0.45f, 3), new Vector3(1.2f, 0.9f, 1.1f), gold, station.transform);
                    Primitive(stationNames[i] + "Table", PrimitiveType.Cube, new Vector3(x, 0.96f, 3), new Vector3(3.1f, 0.16f, 2.1f), green, station.transform);
                    WorldLabel(stationNames[i] + "Label", font, labels[i], new Vector3(x, 2.7f, 3.45f), 3.8f, station.transform);
                    if (i == 0)
                    {
                        Primitive("SlotMachine", PrimitiveType.Cube, new Vector3(x, 1.72f, 3.4f), new Vector3(2, 1.5f, 0.6f), dark, station.transform);
                        for (int reel = 0; reel < 3; reel++)
                            Primitive("ReelWindow" + reel, PrimitiveType.Cube, new Vector3(x - 0.55f + reel * 0.55f, 1.8f, 3.05f), new Vector3(0.42f, 0.55f, 0.1f), bright, station.transform);
                    }
                    else if (i == 1)
                    {
                        Primitive("RouletteWheel", PrimitiveType.Cylinder, new Vector3(x, 1.12f, 3), new Vector3(1.5f, 0.07f, 1.5f), gold, station.transform);
                        Primitive("RouletteHub", PrimitiveType.Cylinder, new Vector3(x, 1.24f, 3), new Vector3(0.26f, 0.09f, 0.26f), dark, station.transform);
                        for (int number = 0; number < 8; number++)
                        {
                            float angle = number * Mathf.PI / 4;
                            Primitive("WheelMarker" + number, PrimitiveType.Cube, new Vector3(x + Mathf.Cos(angle) * 0.58f, 1.21f, 3 + Mathf.Sin(angle) * 0.58f), new Vector3(0.15f, 0.025f, 0.15f), number % 2 == 0 ? dark : bright, station.transform);
                        }
                    }
                    else
                    {
                        var coin = Primitive("CoinDisplay", PrimitiveType.Cylinder, new Vector3(x, 1.65f, 3), new Vector3(0.9f, 0.07f, 0.9f), gold, station.transform);
                        coin.transform.rotation = Quaternion.Euler(90, 0, 0);
                    }
                }
                var coordinator = new GameObject("JinxCasinoController").AddComponent<JinxCasinoController>();
                coordinator.Configure(camera, settings, anchors);
                if (buildAdventure) ConfigureAdventureWorld(coordinator, player.transform, anchors, font);
                if (!EditorSceneManager.SaveScene(scene, Root + "/Scenes/Main.unity"))
                    throw new InvalidOperationException("P0 场景保存失败。");
            }
            finally
            {
                if (previous != scene && previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                // 场景只由外层装配事务关闭，成功与失败均恢复用户原场景。
            }
        }

        private static void AddHubButton(TMP_FontAsset font)
        {
            var root = PrefabUtility.LoadPrefabContents(HubPath);
            try
            {
                Button template = null, target = null;
                foreach (var button in root.GetComponentsInChildren<Button>(true))
                {
                    if (button.name == "BlockPortersButton") template = button;
                    if (button.name == "JinxCasinoButton") target = button;
                }
                if (template == null) throw new InvalidOperationException("Hub 缺少 BlockPortersButton 模板。");
                if (target == null)
                {
                    target = Object.Instantiate(template, template.transform.parent);
                    target.name = "JinxCasinoButton";
                    target.transform.SetSiblingIndex(template.transform.GetSiblingIndex() + 1);
                    target.GetComponent<RectTransform>().anchoredPosition = template.GetComponent<RectTransform>().anchoredPosition + Vector2.down * 90;
                }
                var label = target.GetComponentInChildren<TextMeshProUGUI>(true);
                if (label != null) { label.font = font; label.text = buildAdventure ? "倒霉蛋俱乐部" : "倒霉蛋俱乐部 · P0"; }
                else
                {
                    var legacy = target.GetComponentInChildren<Text>(true);
                    if (legacy == null) throw new InvalidOperationException("Hub 按钮缺少文本。");
                    legacy.text = buildAdventure ? "倒霉蛋俱乐部" : "倒霉蛋俱乐部 · P0";
                }
                var nodes = MvcPrefabScanner.Scan(root);
                MvcBindComponentWindowBridge.RestoreComponentChoices(root, nodes);
                foreach (var node in nodes) if (node.name == "JinxCasinoButton") Select(node, typeof(Button), "OnJinxCasinoButtonClick");
                var settings = new MvcBindSettings
                {
                    prefabPath = HubPath, moduleName = "Main", viewName = "MainMenuView", namespaceName = "Hotfix",
                    address = MvcBindPathUtility.ToRuntimeAddress(HubPath), viewType = ViewType.View,
                    layer = UILayer.Base, viewMode = UIViewMode.Page, mask = MaskType.None,
                    isHotfix = true, isAsync = true, enableOnInit = true, destroyOnHide = true
                };
                settings.outputFolder = MvcBindPathUtility.ToOutputFolder(settings);
                if (!MvcBindComponentWindowBridge.GenerateAndBind(root, settings, nodes, false, out _, out string message))
                    throw new InvalidOperationException(message);
                PrefabUtility.SaveAsPrefabAsset(root, HubPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static RectTransform Panel(string name, Transform parent, Vector2 position, Vector2 size,
            Vector2 anchor, Vector2 pivot, Color color)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = root.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = root.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        private static TextMeshProUGUI Label(string name, Transform parent, TMP_FontAsset font, string text,
            Vector2 position, Vector2 size, float fontSize)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            var rect = root.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var label = root.GetComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSize = fontSize;
            label.text = text;
            label.color = new Color(0.94f, 0.95f, 1);
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.Normal;
            return label;
        }

        private static Button Button(string name, Transform parent, TMP_FontAsset font, string text, Vector2 position, Vector2 size)
        {
            var rect = Panel(name, parent, position, size, new Vector2(0, 1), new Vector2(0, 1), new Color(0.17f, 0.29f, 0.4f, 0.96f));
            var image = rect.GetComponent<Image>();
            image.raycastTarget = true;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var label = Label("Label", rect, font, text, Vector2.zero, size, 28);
            Stretch(label.rectTransform);
            label.alignment = TextAlignmentOptions.Center;
            return button;
        }

        private static TMP_InputField Input(string name, Transform parent, TMP_FontAsset font, string text,
            string hint, Vector2 position, Vector2 size, bool integer = false)
        {
            var rect = Panel(name, parent, position, size, new Vector2(0, 1), new Vector2(0, 1), new Color(0.13f, 0.16f, 0.21f));
            var image = rect.GetComponent<Image>();
            image.raycastTarget = true;
            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
            viewport.SetParent(rect, false);
            Stretch(viewport);
            viewport.offsetMin = new Vector2(12, 4);
            viewport.offsetMax = new Vector2(-12, -4);
            var label = Label("Text", viewport, font, text, Vector2.zero, size, 28);
            Stretch(label.rectTransform);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            var placeholder = Label("Placeholder", viewport, font, hint, Vector2.zero, size, 28);
            Stretch(placeholder.rectTransform);
            placeholder.alignment = TextAlignmentOptions.MidlineLeft;
            placeholder.color = new Color(0.65f, 0.68f, 0.73f);
            var input = rect.gameObject.AddComponent<TMP_InputField>();
            input.targetGraphic = image;
            input.textViewport = viewport;
            input.textComponent = label;
            input.placeholder = placeholder;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.contentType = integer ? TMP_InputField.ContentType.IntegerNumber : TMP_InputField.ContentType.Standard;
            input.text = text;
            input.characterLimit = integer ? 9 : 64;
            return input;
        }

        private static JinxCasinoTouchPad TouchPad(string name, Transform parent, TMP_FontAsset font,
            string text, Vector2 position, Vector2 anchor, Vector2 pivot)
        {
            var rect = Panel(name, parent, position, new Vector2(200, 200), anchor, pivot, new Color(0.12f, 0.22f, 0.3f, 0.65f));
            rect.GetComponent<Image>().raycastTarget = true;
            var label = Label("Label", rect, font, text, Vector2.zero, rect.sizeDelta, 30);
            Stretch(label.rectTransform);
            label.alignment = TextAlignmentOptions.Center;
            var pad = rect.gameObject.AddComponent<JinxCasinoTouchPad>();
            pad.Configure(name == "LookPad");
            return pad;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        private static void SetReference(SerializedObject serialized, string fieldName, Object value)
        {
            var property = serialized.FindProperty(fieldName);
            if (property == null) throw new InvalidOperationException("HUD Presenter 缺少序列化字段：" + fieldName);
            property.objectReferenceValue = value;
        }

        private static Material Material(string name, Color color)
        {
            string path = Root + "/Art/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) throw new InvalidOperationException("缺少 URP Lit Shader。");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject Primitive(string name, PrimitiveType type, Vector3 position, Vector3 scale,
            Material material, Transform parent = null)
        {
            var item = GameObject.CreatePrimitive(type);
            item.name = name;
            if (parent != null) item.transform.SetParent(parent, false);
            item.transform.position = position;
            item.transform.localScale = scale;
            item.GetComponent<Renderer>().sharedMaterial = material;
            return item;
        }

        private static void WorldLabel(string name, TMP_FontAsset font, string text, Vector3 position,
            float width, Transform parent = null)
        {
            var item = new GameObject(name);
            if (parent != null) item.transform.SetParent(parent, false);
            item.transform.position = position;
            var label = item.AddComponent<TextMeshPro>();
            label.font = font;
            label.text = text;
            label.fontSize = 4;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(0.98f, 0.79f, 0.36f);
            label.rectTransform.sizeDelta = new Vector2(width, 0.75f);
        }

        private static void Select(MvcBindNode node, Type type, string callback = null)
        {
            node.selectedComponentType = type;
            node.selectedComponentTypeName = type.FullName;
            node.selectedComponentTypes.Clear();
            node.selectedMethodNames.Clear();
            node.selectedMethodNamesByComponentTypeName.Clear();
            if (callback != null) node.selectedMethodNames.Add(callback);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(parent)) throw new InvalidOperationException("资源目录必须位于 Assets 内。");
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
