using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Hotfix.JinxCasino.Adapters;
using Hotfix.JinxCasino.Adapters.UI;
using Hotfix.JinxCasino.Rules;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Hotfix.Editor.JinxCasino
{
    public static partial class JinxCasinoPrototypeBuilder
    {
        private const string FormalModelFolder = Root + "/Art/Models";
        private const string FormalIconFolder = Root + "/Art/Icons";
        private static readonly string[] FormalHatIds = { "hat_party", "hat_banana", "hat_mechanic", "hat_crown", "hat_lucky" };
        private static readonly string[] FormalEmoteIds = { "emote_wave", "emote_clap", "emote_shrug", "emote_dance", "emote_salute", "emote_bow", "emote_crown", "emote_fireworks" };
        private static readonly string[] FormalClipIds = { "UiClick", "MachineBegin", "Win", "Lose", "Coin", "TaskComplete", "Event", "EndingDignity", "EndingTakeover", "EndingWithdraw", "Horn", "Boing", "Charge", "ClubLoop" };

        private static void PrepareFormalImports()
        {
            var palette = EnsureFormalPalette();
            foreach (string path in Directory.GetFiles(FormalModelFolder, "*.fbx").OrderBy(value => value, StringComparer.Ordinal))
            {
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) throw new InvalidOperationException("模型尚未完成首次导入：" + path);
                importer.importCameras = false; importer.importLights = false; importer.addCollider = false;
                foreach (var pair in palette)
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), pair.Key), pair.Value);
                importer.SaveAndReimport();
            }
            foreach (string path in Directory.GetFiles(FormalIconFolder, "*.png"))
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true; importer.sRGBTexture = true; importer.mipmapEnabled = false;
                importer.maxTextureSize = 512; importer.textureCompression = TextureImporterCompression.Compressed;
                importer.SaveAndReimport();
            }
            string endings = Root + "/Art/Endings";
            if (Directory.Exists(endings))
                foreach (string path in Directory.GetFiles(endings, "*.png"))
                {
                    var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                    importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
                    importer.sRGBTexture = true; importer.mipmapEnabled = false; importer.maxTextureSize = 2048;
                    importer.SaveAndReimport();
                }
            foreach (string path in Directory.GetFiles(Root + "/Audio", "*.wav"))
            {
                var importer = (AudioImporter)AssetImporter.GetAtPath(path);
                var sample = importer.defaultSampleSettings;
                sample.loadType = path.EndsWith("ClubLoop.wav", StringComparison.Ordinal) ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
                sample.compressionFormat = AudioCompressionFormat.Vorbis; sample.quality = 0.7f;
                importer.defaultSampleSettings = sample; importer.forceToMono = true; importer.SaveAndReimport();
            }
            AssetDatabase.SaveAssets();
        }

        /// <summary>在Builder已有临时装配场景中安装原创正式视觉；旧碰撞体、锚点和相机保持原位。</summary>
        /// <param name="controller">已配置冒险的场景宿主。</param>
        /// <param name="player">原CharacterController节点。</param>
        /// <param name="areas">四个区域及其原安全点。</param>
        /// <param name="buddies">四个原助手物理根。</param>
        /// <param name="font">赌场专用中文字体。</param>
        public static void ConfigureFormalArt(JinxCasinoController controller, Transform player,
            JinxCasinoWorldArea[] areas, Transform[] buddies, TMP_FontAsset font)
        {
            if (controller == null || player == null || font == null) throw new ArgumentException("正式装配需要宿主、原玩家和中文字体。");
            var palette = EnsureFormalPalette();
            ValidateFormalModels();
            var effects = controller.GetComponent<JinxCasinoSceneEffects>();
            var actors = new List<CasinoActorEffectBinding>();
            var localRoot = player.Find("LocalVisual");
            if (localRoot == null) { localRoot = new GameObject("LocalVisual").transform; localRoot.SetParent(player, false); }
            InstallFormalAvatar(controller, player, localRoot, effects, true, Quaternion.identity, palette, actors);
            for (int index = 0; index < buddies.Length; index++)
            {
                var buddy = buddies[index]; var visual = buddy.Find("Visual");
                if (visual == null) throw new InvalidOperationException("助手缺少原Visual根，禁止替换其物理根。");
                var area = areas.FirstOrDefault(value => value.Index == index);
                var direction = (area != null ? area.SafePosition : player.position) - buddy.position; direction.y = 0;
                InstallFormalAvatar(controller, buddy, visual, effects, false,
                    direction.sqrMagnitude > 0.01f ? Quaternion.LookRotation(direction, Vector3.up) : Quaternion.identity, palette, actors);
            }
            if (effects != null)
            {
                effects.ConfigureActors(actors.ToArray());
                effects.ConfigureMaterials(Material("DisguisePink", new Color(1, 0.3f, 0.65f)), Material("InkPaint", new Color(0.055f, 0.015f, 0.095f)));
            }
            foreach (var area in areas)
            {
                var contents = area.transform.Find(area.Index == 0 ? "StreetContent" : "Contents");
                foreach (var station in contents.GetComponentsInChildren<JinxCasinoStation>(true)) InstallFormalStation(controller, station, palette, font);
                InstallFormalArchitecture(area, contents, palette);
            }
            UpgradeFormalMissionPrefabs(palette);
            UpgradeFormalEffectPrefabs(palette);
            ConfigureFormalAudio(controller);
            ConfigureLocalSocialFeedback(controller);
        }

        /// <summary>为保存HUD模板绑定24张图标；调用方负责保存Prefab。</summary>
        /// <param name="prefabContents">LoadPrefabContents加载的现有HUD根，不创建Canvas。</param>
        public static void ApplyFormalHudAssets(GameObject prefabContents)
        {
            ApplyFormalHudStyle(prefabContents);
            ApplyFormalEndingArtwork(prefabContents);
            var bindings = CasinoContentCatalog.Items.Select(item => new CasinoItemIconBinding { Id = item.Id, Sprite = LoadFormalSprite("Item_" + item.Id) }).ToArray();
            foreach (var card in prefabContents.GetComponentsInChildren<JinxCasinoItemCard>(true))
            {
                var rect = (RectTransform)card.transform;
                var icon = rect.Find("FormalIcon")?.GetComponent<Image>();
                if (icon == null)
                {
                    var root = new GameObject("FormalIcon", typeof(RectTransform), typeof(Image)); icon = root.GetComponent<Image>();
                    var iconRect = (RectTransform)root.transform; iconRect.SetParent(rect, false);
                    iconRect.anchorMin = iconRect.anchorMax = new Vector2(1, 1); iconRect.pivot = new Vector2(1, 1);
                    iconRect.anchoredPosition = new Vector2(-14, -10); iconRect.sizeDelta = new Vector2(48, 48);
                    var title = rect.Find("Title")?.GetComponent<RectTransform>(); if (title != null) title.sizeDelta = new Vector2(310, title.sizeDelta.y);
                    var description = rect.Find("Description")?.GetComponent<RectTransform>();
                    if (description != null) { description.anchoredPosition = new Vector2(14, -64); description.sizeDelta = new Vector2(365, 70); description.GetComponent<TMP_Text>().fontSize = 18; }
                }
                icon.raycastTarget = false; card.ConfigureIcons(icon, bindings); EditorUtility.SetDirty(card);
            }
        }

        private static void ApplyFormalEndingArtwork(GameObject root)
        {
            var presenter = root.GetComponentInChildren<JinxCasinoAdventurePresenter>(true);
            if (presenter == null) throw new InvalidOperationException("正式结局缺少冒险Presenter。");
            var panel = presenter.transform.Find("Ending");
            string[] names = { "EndingDignity", "EndingTakeover", "EndingWithdraw" };
            CasinoAdventureEnding[] kinds = { CasinoAdventureEnding.LeaveWithDignity, CasinoAdventureEnding.TakeOver, CasinoAdventureEnding.Withdraw };
            // 迭代装配允许尚未完成画幅时保留文本结局，最终资源测试与Player交付必须核对三张绑定。
            if (!names.All(name => File.Exists(Root + "/Art/Endings/" + name + ".png"))) return;
            var image = new GameObject("EndingArtwork", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            var rect = (RectTransform)image.transform; rect.SetParent(panel, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(600, -132); rect.sizeDelta = new Vector2(450, 280);
            image.preserveAspect = true; image.raycastTarget = false; image.gameObject.SetActive(false);
            var story = panel.Find("EndingStory") as RectTransform; story.sizeDelta = new Vector2(550, 300);
            var serialized = new SerializedObject(presenter); SetReference(serialized, "endingArtwork", image);
            var bindings = serialized.FindProperty("endingArtworks"); bindings.arraySize = 3;
            for (int index = 0; index < 3; index++)
            {
                var entry = bindings.GetArrayElementAtIndex(index); entry.FindPropertyRelative("Ending").intValue = (int)kinds[index];
                entry.FindPropertyRelative("Sprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/Art/Endings/" + names[index] + ".png");
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ApplyFormalHudStyle(GameObject root)
        {
            var adventure = root.transform.Find("Adventure");
            if (adventure == null) throw new InvalidOperationException("正式HUD缺少保存的Adventure根。");
            foreach (string name in new[] { "Menu", "Machine", "Shop", "Slots", "Ending", "EventChoice", "Profile", "Social" })
            {
                var image = adventure.Find(name)?.GetComponent<Image>();
                if (image != null) image.color = new Color(0.035f, 0.065f, 0.105f, 1);
            }
            foreach (string name in new[] { "Settings", "Emotes" })
            {
                var image = adventure.Find("LocalPreferences/" + name)?.GetComponent<Image>();
                if (image != null) image.color = new Color(0.035f, 0.065f, 0.105f, 1);
            }
            foreach (var button in adventure.GetComponentsInChildren<Button>(true))
            {
                var image = button.targetGraphic as Image;
                if (image != null) image.color = new Color(0.24f, 0.1f, 0.19f, 1);
                var colors = button.colors;
                colors.highlightedColor = new Color(1, 0.88f, 0.74f);
                colors.pressedColor = new Color(0.65f, 0.85f, 0.8f);
                button.colors = colors;
            }
            var header = adventure.Find("AdventureHeader") as RectTransform;
            if (header != null)
            {
                header.sizeDelta = new Vector2(720, 160);
                foreach (var text in header.GetComponentsInChildren<TMP_Text>(true)) text.rectTransform.sizeDelta = new Vector2(680, text.rectTransform.sizeDelta.y);
                var fit = header.GetComponent<JinxCasinoPanelFit>(); fit.Configure(header.sizeDelta, new Vector2(24, 24));
            }
            var mission = adventure.Find("MissionProgress") as RectTransform;
            if (mission != null)
            {
                mission.sizeDelta = new Vector2(720, 130);
                foreach (var text in mission.GetComponentsInChildren<TMP_Text>(true)) text.rectTransform.sizeDelta = new Vector2(680, text.rectTransform.sizeDelta.y);
                mission.GetComponent<JinxCasinoPanelFit>().Configure(mission.sizeDelta, new Vector2(24, 24));
            }
            var toolbar = adventure.Find("Toolbar") as RectTransform;
            if (toolbar != null)
            {
                // 隐藏的上下阶段按钮不再留下空格；沿用保存按钮，给触屏留出稳定高度。
                var fit = toolbar.GetComponent<JinxCasinoPanelFit>(); if (fit != null) Object.DestroyImmediate(fit);
                var layout = toolbar.gameObject.AddComponent<VerticalLayoutGroup>();
                layout.padding = new RectOffset(12, 12, 12, 12); layout.spacing = 8;
                layout.childControlWidth = true; layout.childControlHeight = true; layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
                var fitter = toolbar.gameObject.AddComponent<ContentSizeFitter>(); fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                foreach (var button in toolbar.GetComponentsInChildren<Button>(true))
                {
                    var element = button.gameObject.AddComponent<LayoutElement>(); element.minHeight = 68; element.preferredHeight = 76;
                }
            }
        }

        private static void InstallFormalAvatar(JinxCasinoController controller, Transform actor, Transform visual,
            JinxCasinoSceneEffects effects, bool local, Quaternion facing, Dictionary<string, Material> palette, List<CasinoActorEffectBinding> actors)
        {
            HideFormalLegacyRenderers(visual);
            var model = ReplaceFormalMount(visual, "FormalAvatar", "Avatar", actor.position, facing, palette);
            var head = FindFormalNode(model, "Avatar.Head"); var hatMount = FindFormalNode(model, "Avatar.HatMount");
            if (head == null || hatMount == null) throw new InvalidOperationException("Avatar缺少Head或HatMount原关节。");
            var hats = FormalHatIds.Select(id => new CasinoAvatarModelBinding { Id = id, Model = InstantiateFormalModel(id, hatMount, palette).gameObject }).ToArray();
            var faces = FormalEmoteIds.Select(id => new CasinoAvatarModelBinding { Id = id, Model = InstantiateFormalModel("Face_" + id, head, palette).gameObject }).ToArray();
            foreach (var binding in hats.Concat(faces)) binding.Model.SetActive(false);
            var colors = palette.Where(pair => pair.Key.StartsWith("color_", StringComparison.Ordinal))
                .Select(pair => new CasinoAvatarColorBinding { Id = pair.Key, Material = pair.Value }).ToArray();
            var presenter = model.gameObject.AddComponent<JinxCasinoAvatarPresentation>(); presenter.Setup(controller, actor, model, effects, local, colors, hats, faces);
            if (local)
                foreach (var renderer in head.GetComponentsInChildren<Renderer>(true)) renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            actors.Add(new CasinoActorEffectBinding { Target = actor, VisualRoot = visual, CostumeRenderers = model.GetComponentsInChildren<Renderer>(true) });
        }

        private static void InstallFormalStation(JinxCasinoController controller, JinxCasinoStation station,
            Dictionary<string, Material> palette, TMP_FontAsset font)
        {
            HideFormalLegacyRenderers(station.transform);
            var rotation = station.GetComponent<JinxCasinoRotationStand>();
            // 原P0三个Station根在世界零点，真实几何和Anchor使用世界位置；新十四个根已在机台中心。
            bool firstThree = rotation == null && (int)station.Game <= 2 && station.transform.Find("Interaction") == null;
            Vector3 center = firstThree ? station.InteractionPosition + Vector3.forward * 1.7f : station.transform.position;
            Vector3 direction = station.InteractionPosition - center; direction.y = 0;
            if (direction.sqrMagnitude < 0.01f) throw new InvalidOperationException("机台缺少可判定的展示朝向：" + station.name);
            Quaternion facing = Quaternion.LookRotation(direction, Vector3.up);
            Transform model;
            if (rotation != null)
            {
                var old = station.transform.Find("FormalVisual"); if (old != null) Object.DestroyImmediate(old.gameObject);
                var mount = new GameObject("FormalVisual").transform; mount.SetParent(station.transform, false); mount.SetPositionAndRotation(center, facing);
                var displays = new GameObject[17];
                foreach (var game in CasinoContentCatalog.Games) { displays[(int)game.Kind] = InstantiateFormalModel(game.Kind.ToString(), mount, palette).gameObject; displays[(int)game.Kind].SetActive(false); }
                rotation.Configure(controller, station, station.GetComponentInChildren<TMP_Text>(true), displays); model = mount;
            }
            else model = ReplaceFormalMount(station.transform, "FormalVisual", station.Game.ToString(), center, facing, palette);
            // 展示朝向是明确的场景朝向；FBX内部仍保留源门禁通过的identity，不以包装层修补源轴。
            if (Vector3.Dot(model.parent.forward, direction.normalized) < 0.99f && rotation == null) throw new InvalidOperationException("正式机台正面没有朝交互Anchor。");
            var resultRoot = station.transform.Find("FormalResult");
            if (resultRoot != null) Object.DestroyImmediate(resultRoot.gameObject);
            WorldLabel("FormalResult", font, "", center + Vector3.up * 2.95f, 3.4f, station.transform);
            var result = station.transform.Find("FormalResult").GetComponent<TMP_Text>();
            // TMP平面的可见面朝局部-Z；模型正面约定为+Z，因此二者展示朝向相反。
            result.transform.rotation = facing * Quaternion.Euler(0, 180, 0); result.fontSize = 2.8f;
            var presentation = station.GetComponent<JinxCasinoStationPresentation>();
            if (presentation == null) presentation = station.gameObject.AddComponent<JinxCasinoStationPresentation>();
            var lamps = new List<CasinoLeverLampBinding>();
            for (int playerIndex = 0; playerIndex < 6; playerIndex++)
            {
                var face = FindFormalNode(model, "CooperativeLevers.TimingFace" + playerIndex);
                if (face == null) continue;
                var renderer = face.GetComponentInChildren<Renderer>(true);
                if (renderer == null || renderer.sharedMaterials.Length != 2)
                    throw new InvalidOperationException("拉杆表盘缺少正式盘面与指针槽：" + face.name);
                for (int slot = 0; slot < 2; slot++)
                {
                    bool pointer = renderer.sharedMaterials[slot].name == "PlumRed";
                    lamps.Add(new CasinoLeverLampBinding
                    {
                        Renderer = renderer, MaterialSlot = slot, PlayerIndex = playerIndex,
                        Dark = palette[pointer ? "FaceInk" : "InkBlue"],
                        Open = palette[pointer ? "PaperLight" : "Mint"], Pulled = palette["CopperGold"]
                    });
                }
            }
            presentation.Setup(controller, station, model, result, lamps.ToArray());
        }

        private static Transform ReplaceFormalMount(Transform parent, string name, string modelName, Vector3 position, Quaternion rotation, Dictionary<string, Material> palette)
        {
            var old = parent.Find(name); if (old != null) Object.DestroyImmediate(old.gameObject);
            var mount = new GameObject(name).transform; mount.SetParent(parent, false); mount.SetPositionAndRotation(position, rotation);
            return InstantiateFormalModel(modelName, mount, palette);
        }

        private static Transform InstantiateFormalModel(string name, Transform parent, Dictionary<string, Material> palette)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FormalModelFolder + "/" + name + ".fbx");
            if (prefab == null) throw new InvalidOperationException("缺少已导入正式FBX：" + name);
            if (Quaternion.Angle(prefab.transform.localRotation, Quaternion.identity) > 0.01f || (prefab.transform.localScale - Vector3.one).sqrMagnitude > 0.00001f)
                throw new InvalidOperationException("FBX根未通过identity轴门禁：" + name);
            var model = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, parent)).transform;
            model.localPosition = Vector3.zero; model.localRotation = Quaternion.identity; model.localScale = Vector3.one;
            if (model.GetComponentsInChildren<Camera>(true).Length != 0 || model.GetComponentsInChildren<AudioListener>(true).Length != 0 || model.GetComponentsInChildren<Light>(true).Length != 0 || model.GetComponentsInChildren<Collider>(true).Length != 0)
                throw new InvalidOperationException("正式视觉FBX夹带相机、监听器、灯光或物理体：" + name);
            foreach (var node in model.GetComponentsInChildren<Transform>(true))
                if (Quaternion.Angle(node.localRotation, Quaternion.identity) > 0.01f || node.localScale.x <= 0 || node.localScale.y <= 0 || node.localScale.z <= 0)
                    throw new InvalidOperationException("正式FBX内部轴/缩放未通过门禁：" + name + "/" + node.name);
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                var slots = renderer.sharedMaterials;
                for (int index = 0; index < slots.Length; index++)
                {
                    string key = slots[index] != null ? slots[index].name : string.Empty;
                    if (!palette.TryGetValue(key, out var material)) throw new InvalidOperationException("manifest没有对应材质：" + name + "/" + key);
                    slots[index] = material;
                }
                renderer.sharedMaterials = slots;
            }
            return model;
        }

        private static Transform FindFormalNode(Transform root, string name)
        { return root.GetComponentsInChildren<Transform>(true).FirstOrDefault(value => value.name == name); }
        private static void HideFormalLegacyRenderers(Transform root)
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                if (renderer.GetComponentInParent<TMP_Text>() == null) renderer.enabled = false;
        }

        private static Dictionary<string, Material> EnsureFormalPalette()
        {
            string manifestPath = FormalModelFolder + "/manifest.json";
            if (!File.Exists(manifestPath)) throw new InvalidOperationException("缺少正式模型manifest。");
            string json = File.ReadAllText(manifestPath);
            int begin = json.IndexOf("\"palette\"", StringComparison.Ordinal); int start = begin >= 0 ? json.IndexOf('{', begin) : -1;
            int end = start >= 0 ? json.IndexOf('}', start) : -1;
            if (start < 0 || end < 0) throw new InvalidOperationException("manifest缺少palette。");
            var materials = new Dictionary<string, Material>(StringComparer.Ordinal);
            var shader = Shader.Find("Universal Render Pipeline/Lit"); if (shader == null) throw new InvalidOperationException("缺少URP Lit。");
            foreach (Match match in Regex.Matches(json.Substring(start, end - start), "\"([A-Za-z_]+)\"\\s*:\\s*\"([0-9A-Fa-f]{6})\""))
            {
                string id = match.Groups[1].Value;
                if (!ColorUtility.TryParseHtmlString("#" + match.Groups[2].Value, out var color)) throw new InvalidOperationException("palette颜色无效。");
                var material = Material(id, color); material.shader = shader; material.enableInstancing = true;
                material.SetFloat("_Metallic", id == "CopperGold" ? 0.65f : 0.08f); material.SetFloat("_Smoothness", id == "CopperGold" ? 0.5f : 0.3f);
                EditorUtility.SetDirty(material); materials.Add(id, material);
            }
            if (materials.Count != 12) throw new InvalidOperationException("正式palette需要7风格色及5角色配色。");
            return materials;
        }

        private static void ValidateFormalModels()
        {
            var names = CasinoContentCatalog.Games.Select(game => game.Kind.ToString()).Concat(CasinoContentCatalog.Items.Select(item => "Item_" + item.Id))
                .Concat(FormalHatIds).Concat(FormalEmoteIds.Select(id => "Face_" + id)).Concat(new[] { "Avatar", "Mission_chip_rain", "Mission_mascot_chase", "Mission_gold_delivery", "Mission_power_relay", "Mission_sync_buttons", "Mission_power_repair", "Region_lobby", "Region_arcade", "Region_backroom", "Region_penthouse" });
            foreach (var name in names) if (AssetDatabase.LoadAssetAtPath<GameObject>(FormalModelFolder + "/" + name + ".fbx") == null) throw new InvalidOperationException("正式资源不完整：" + name);
        }

        private static void UpgradeFormalMissionPrefabs(Dictionary<string, Material> palette)
        {
            // 先更新被GoldSource嵌套引用的持箱Prefab，随后隐藏其旧实例；避免后续子资产新增又继承一套可见模型。
            string[] prefabNames = { "CarriedGold", "TaskTarget", "Mascot", "GoldSource", "GoldDestination", "Relay", "Sync", "Repair" };
            string[] modelNames = { "Mission_gold_delivery", "Mission_chip_rain", "Mission_mascot_chase", "Mission_gold_delivery", "Mission_gold_delivery", "Mission_power_relay", "Mission_sync_buttons", "Mission_power_repair" };
            for (int index = 0; index < prefabNames.Length; index++)
            {
                string path = Root + "/Prefabs/Missions/" + prefabNames[index] + ".prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    HideFormalLegacyRenderers(root.transform);
                    // 目标根是原Trigger中心；正式源模型脚底仍为0，只调整显式视觉挂点。
                    ReplaceFormalMount(root.transform, "FormalMission", modelNames[index], root.transform.position + Vector3.down * (prefabNames[index] == "CarriedGold" ? 0.25f : 0.4f), Quaternion.identity, palette);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
        }

        private static void UpgradeFormalEffectPrefabs(Dictionary<string, Material> palette)
        {
            string[] kinds = { "Bubble", "Banana", "SpringPunch", "FakeJackpot", "Ink", "Magnet", "Horn", "Disguise" };
            string[] items = { "bubble_gun", "banana_peel", "spring_glove", "fake_jackpot", "ink_balloon", "magnet_hat", "remote_horn", "disguise_spray" };
            for (int index = 0; index < kinds.Length; index++)
            {
                string path = Root + "/Prefabs/Effects/" + kinds[index] + ".prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (kinds[index] != "Bubble") HideFormalLegacyRenderers(root.transform);
                    ReplaceFormalMount(root.transform, "FormalEffect", "Item_" + items[index], root.transform.position + new Vector3(0, -0.4f, -0.65f), Quaternion.identity, palette);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
        }

        private static void ConfigureFormalAudio(JinxCasinoController controller)
        {
            var bindings = FormalClipIds.Select(id => new CasinoAudioClipBinding { Id = id, Clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "/Audio/" + id + ".wav") }).ToArray();
            foreach (var binding in bindings) if (binding.Clip == null) throw new InvalidOperationException("缺少原创音频：" + binding.Id);
            var music = FormalAudioSource(controller.transform, "ClubMusic"); var sfx = FormalAudioSource(controller.transform, "ClubFeedback");
            var director = controller.GetComponent<JinxCasinoAudioDirector>();
            if (director == null) director = controller.gameObject.AddComponent<JinxCasinoAudioDirector>();
            director.Setup(controller, music, sfx, bindings);
        }
        private static AudioSource FormalAudioSource(Transform parent, string name)
        {
            var child = parent.Find(name); if (child == null) { child = new GameObject(name).transform; child.SetParent(parent, false); }
            // Unity编辑器GetComponent可能返回fake-null引用；??不能识别其缺失的原生组件。
            var source = child.GetComponent<AudioSource>();
            if (source == null) source = child.gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false; source.spatialBlend = 0;
            return source;
        }
        private static Sprite LoadFormalSprite(string name)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(FormalIconFolder + "/" + name + ".png");
            if (sprite == null) throw new InvalidOperationException("原创图标未按Sprite导入：" + name);
            return sprite;
        }
    }
}
