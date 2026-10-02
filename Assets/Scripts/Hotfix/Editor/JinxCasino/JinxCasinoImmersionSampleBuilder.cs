using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Core.Editor.MvcBind;
using Core.Runtime;
using Hotfix.JinxCasino.Adapters;
using Hotfix.JinxCasino.Adapters.UI;
using Hotfix.JinxCasino.Rules;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Hotfix.Editor.JinxCasino
{
    public static partial class JinxCasinoPrototypeBuilder
    {
        private const string ImmersionScenePath = Root + "/Scenes/Immersion.unity";
        private const string ImmersionHudPath = Root + "/Prefabs/UI/JinxCasinoImmersionHudView.prefab";

        /// 分步装配桌面赔率夹板，保留已保存场景与机台的人工调整。
        [MenuItem("Tools/SleepyDemos/整蛊赌场/沉浸样板/更新桌面规则铭牌")]
        public static void UpdateImmersionRulesPlacards()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请在编辑模式更新样板。");
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = SceneManager.GetSceneByPath(ImmersionScenePath);
            bool opened = !scene.isLoaded;
            if (!opened && scene.isDirty) throw new InvalidOperationException("请先保存样板的人工修改。");
            if (opened) scene = EditorSceneManager.OpenScene(ImmersionScenePath, OpenSceneMode.Additive);
            try
            {
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
                var palette = Directory.GetFiles(JinxCasinoImmersionArtBuilder.Root + "/Materials", "*.mat")
                    .ToDictionary(Path.GetFileNameWithoutExtension, path => AssetDatabase.LoadAssetAtPath<Material>(path));
                var blueprint = JsonUtility.FromJson<RulesFocusBlueprint>(File.ReadAllText(JinxCasinoImmersionArtBuilder.Root + "/S1Layout.json"));
                foreach (var station in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<JinxCasinoStation>(true)))
                {
                    var pose = blueprint.stations.Single(item => item.id == station.StationId);
                    station.FocusPose.localPosition = new Vector3(pose.focus[0], pose.focus[1], pose.focus[2]);
                    Vector3 lookAt = station.transform.TransformPoint(new Vector3(pose.lookAt[0], pose.lookAt[1], pose.lookAt[2]));
                    station.FocusPose.rotation = Quaternion.LookRotation(lookAt - station.FocusPose.position, Vector3.up);
                    var label = JinxCasinoS1PresentationBuilder.EnsureRulesPlacard(station, font, palette, true);
                    var saved = new SerializedObject(station.GetComponent<JinxCasinoS1Presentation>());
                    saved.FindProperty("rulesText").objectReferenceValue = label;
                    saved.ApplyModifiedPropertiesWithoutUndo();
                }
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("保存规则铭牌失败。");
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                if (opened && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Serializable] private sealed class RulesFocusBlueprint { public RulesFocusPose[] stations; }
        [Serializable] private sealed class RulesFocusPose { public string id; public float[] focus; public float[] lookAt; }

        /// 分步修正本地角色射线层与动态铭牌行高，不改变场景布局和人工美术。
        [MenuItem("Tools/SleepyDemos/整蛊赌场/沉浸样板/更新交互绑定")]
        public static void UpdateImmersionInteractionBindings()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请在编辑模式更新样板。");
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = SceneManager.GetSceneByPath(ImmersionScenePath);
            bool opened = !scene.isLoaded;
            if (!opened && scene.isDirty) throw new InvalidOperationException("请先保存样板的人工修改。");
            if (opened) scene = EditorSceneManager.OpenScene(ImmersionScenePath, OpenSceneMode.Additive);
            try
            {
                var owner = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<JinxCasinoController>(true)).Single();
                var player = owner.GetComponentInChildren<CharacterController>(true);
                if (player == null) throw new InvalidOperationException("样板缺少本地角色。");
                // 第一人称自身的不可见胶囊不参与指向机台的射线，物理碰撞仍然保留。
                player.gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
                foreach (var visual in owner.GetComponentsInChildren<JinxCasinoS1Presentation>(true))
                {
                    var saved = new SerializedObject(visual);
                    foreach (string field in new[] { "amountText", "resultText" })
                    {
                        var label = saved.FindProperty(field).objectReferenceValue as TMP_Text;
                        if (label == null) throw new InvalidOperationException("样板缺少动态铭牌：" + visual.name);
                        var size = label.rectTransform.sizeDelta; size.y = .046f;
                        label.rectTransform.sizeDelta = size;
                        EditorUtility.SetDirty(label.rectTransform);
                    }
                }
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("保存交互绑定失败。");
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                if (opened && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }

        /// 分步更新样板玩法范围，不重建场景或覆盖人工美术调整。
        [MenuItem("Tools/SleepyDemos/整蛊赌场/沉浸样板/更新样板玩法范围")]
        public static void UpdateImmersionSampleSettings()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请在编辑模式更新样板配置。");
            var settings = AssetDatabase.LoadAssetAtPath<JinxCasinoGameSettings>(Root + "/Data/ImmersionSettings.asset");
            if (settings == null) throw new InvalidOperationException("请先创建独立样板场景。");
            ApplyImmersionSampleSettings(settings);
            AssetDatabase.SaveAssets();
        }

        private static void ApplyImmersionSampleSettings(JinxCasinoGameSettings settings)
        {
            var serialized = new SerializedObject(settings);
            var config = serialized.FindProperty("adventure");
            config.FindPropertyRelative("StageCount").intValue = 1;
            config.FindPropertyRelative("EventIntervalMilliseconds").intValue = 0;
            var targets = config.FindPropertyRelative("Targets");
            targets.arraySize = 1; targets.GetArrayElementAtIndex(0).longValue = 1200;
            var games = new[] { CasinoGameKind.Slots, CasinoGameKind.Blackjack, CasinoGameKind.CooperativeLevers };
            foreach (string field in new[] { "AllowedGames", "InitiallyAvailableGames" })
            {
                var list = config.FindPropertyRelative(field); list.arraySize = games.Length;
                for (int index = 0; index < games.Length; index++) list.GetArrayElementAtIndex(index).intValue = (int)games[index];
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
        }

        /// 首次创建独立样板场景和薄HUD；已保存文件不整包覆盖，后续按组件分步更新。
        [MenuItem("Tools/SleepyDemos/整蛊赌场/沉浸样板/创建独立场景与薄HUD")]
        public static void CreateImmersionSample()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请在编辑模式装配样板。");
            Scene previous = SceneManager.GetActiveScene();
            Scene staging = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(staging);
                var font = EnsurePrototypeFont();
                if (!File.Exists(ImmersionHudPath)) BuildImmersionHud(font);
                if (!File.Exists(ImmersionScenePath)) BuildImmersionScene(staging);
                AssetDatabase.SaveAssets();
                Debug.Log("[JinxCasino] 独立样板骨架已保存，后续分步绑定演出/教学并进行实玩验收。");
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                if (staging.IsValid() && staging.isLoaded) EditorSceneManager.CloseScene(staging, true);
            }
        }

        private static void BuildImmersionHud(TMP_FontAsset font)
        {
            var root = new GameObject("JinxCasinoImmersionHudView", typeof(RectTransform), typeof(CanvasGroup));
            try
            {
                Stretch(root.GetComponent<RectTransform>());
                var presenter = root.AddComponent<JinxCasinoImmersionHudPresenter>();
                var saved = new SerializedObject(presenter);
                var menu = Panel("MainMenu", root.transform, new Vector2(100, 0), new Vector2(650, 790), new Vector2(0, .5f), new Vector2(0, .5f), new Color(.055f, .09f, .15f, .96f));
                SetReference(saved, "mainMenu", menu.gameObject);
                Label("Eyebrow", menu, font, "欢迎来到好运维修站", new Vector2(48, -40), new Vector2(555, 42), 26);
                Label("Title", menu, font, "倒霉蛋\n俱乐部", new Vector2(44, -112), new Vector2(570, 200), 68);
                Label("Subtitle", menu, font, "投一点勇气，修一修运气。", new Vector2(48, -340), new Vector2(555, 60), 28);
                SetReference(saved, "start", Button("Start", menu, font, "开始冒险", new Vector2(48, -456), new Vector2(550, 88)));
                SetReference(saved, "practice", Button("Practice", menu, font, "自由练习", new Vector2(48, -568), new Vector2(550, 88)));
                Label("Footer", menu, font, "水果维修  /  发条牌桌  /  合拍拉杆", new Vector2(48, -710), new Vector2(560, 50), 23);
                var paused = Panel("PauseMenu", root.transform, Vector2.zero, new Vector2(650, 510), new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Color(.055f, .09f, .15f, .97f));
                SetReference(saved, "pauseMenu", paused.gameObject);
                Label("Title", paused, font, "歇一会儿", new Vector2(48, -42), new Vector2(554, 80), 48);
                Label("Hint", paused, font, "旅程已暂停，准备好再继续。", new Vector2(48, -142), new Vector2(554, 52), 26);
                SetReference(saved, "resume", Button("Resume", paused, font, "继续游玩", new Vector2(48, -244), new Vector2(554, 86)));
                SetReference(saved, "leave", Button("Leave", paused, font, "返回大厅", new Vector2(48, -356), new Vector2(554, 86)));
                var hud = new GameObject("FieldHud", typeof(RectTransform)).GetComponent<RectTransform>(); hud.SetParent(root.transform, false); Stretch(hud);
                SetReference(saved, "fieldHud", hud.gameObject);
                SetReference(saved, "wallet", Label("Wallet", hud, font, "筹码  1000", new Vector2(36, -26), new Vector2(380, 56), 32));
                SetReference(saved, "objective", Label("Objective", hud, font, "目标", new Vector2(450, -26), new Vector2(1020, 56), 30));
                var pauseButton = Button("Pause", hud, font, "暂停", new Vector2(-36, -26), new Vector2(180, 64));
                var pauseRect = (RectTransform)pauseButton.transform; pauseRect.anchorMin = pauseRect.anchorMax = new Vector2(1, 1); pauseRect.pivot = new Vector2(1, 1);
                SetReference(saved, "pause", pauseButton);
                var prompt = Label("Prompt", hud, font, "走近机台", new Vector2(0, 46), new Vector2(1200, 60), 28);
                prompt.rectTransform.anchorMin = prompt.rectTransform.anchorMax = new Vector2(.5f, 0); prompt.rectTransform.pivot = new Vector2(.5f, 0); prompt.alignment = TextAlignmentOptions.Center;
                SetReference(saved, "prompt", prompt);
                var feedback = Label("Feedback", hud, font, "", new Vector2(0, 122), new Vector2(1000, 74), 25);
                feedback.rectTransform.anchorMin = feedback.rectTransform.anchorMax = new Vector2(.5f, 0); feedback.rectTransform.pivot = new Vector2(.5f, 0); feedback.alignment = TextAlignmentOptions.Center;
                SetReference(saved, "feedback", feedback);
                SetReference(saved, "movePad", TouchPad("MovePad", hud, font, "移动", new Vector2(32, 32), Vector2.zero, Vector2.zero));
                SetReference(saved, "lookPad", TouchPad("LookPad", hud, font, "视角", new Vector2(-32, 32), new Vector2(1, 0), new Vector2(1, 0)));
                var interact = Button("Interact", hud, font, "交互", new Vector2(-50, 275), new Vector2(190, 92));
                var interactRect = (RectTransform)interact.transform; interactRect.anchorMin = interactRect.anchorMax = new Vector2(1, 0); interactRect.pivot = new Vector2(1, 0);
                SetReference(saved, "interact", interact);
                var back = Button("ExitTable", hud, font, "离开桌面", new Vector2(36, 35), new Vector2(230, 72));
                var backRect = (RectTransform)back.transform; backRect.anchorMin = backRect.anchorMax = Vector2.zero; backRect.pivot = Vector2.zero;
                SetReference(saved, "exitTable", back);
                saved.ApplyModifiedPropertiesWithoutUndo(); paused.gameObject.SetActive(false); hud.gameObject.SetActive(false);
                var nodes = MvcPrefabScanner.Scan(root);
                foreach (var node in nodes) if (node.gameObject == root) Select(node, typeof(JinxCasinoImmersionHudPresenter));
                var config = new MvcBindSettings { prefabPath = ImmersionHudPath, moduleName = "JinxCasino", viewName = "JinxCasinoImmersionHudView", namespaceName = "Hotfix",
                    useCustomModuleOutputDirectory = true, customModuleOutputDirectory = ModuleOutput, address = MvcBindPathUtility.ToRuntimeAddress(ImmersionHudPath),
                    viewType = ViewType.View, layer = UILayer.Base, viewMode = UIViewMode.Page, mask = MaskType.None, isHotfix = true, isAsync = true, enableOnInit = true, destroyOnHide = true };
                config.outputFolder = MvcBindPathUtility.ToOutputFolder(config);
                if (!MvcBindComponentWindowBridge.GenerateAndBind(root, config, nodes, false, out _, out string message)) throw new InvalidOperationException(message);
                PrefabUtility.SaveAsPrefabAsset(root, ImmersionHudPath);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void BuildImmersionScene(Scene scene)
        {
            const string art = JinxCasinoImmersionArtBuilder.Root;
            var root = new GameObject("JinxCasinoImmersion");
            var models = new Dictionary<string, GameObject>();
            foreach (string name in new[] { "S1Slots", "S1Blackjack", "S1Levers" }) models.Add(name, AssetDatabase.LoadAssetAtPath<GameObject>(art + "/Models/" + name + ".fbx"));
            var layout = JinxCasinoImmersionLayoutBuilder.Build(root.transform, art + "/S1Layout.json", AssetDatabase.LoadAssetAtPath<GameObject>(art + "/Models/S1Hall.fbx"), models);
            var player = new GameObject("LocalPlayer", typeof(CharacterController));
            player.layer = LayerMask.NameToLayer("Ignore Raycast");
            player.transform.SetParent(root.transform); player.transform.position = layout.Spawn.position + Vector3.up * .03f;
            var body = player.GetComponent<CharacterController>(); body.height = 1.8f; body.center = new Vector3(0, .9f, 0); body.radius = .3f; body.stepOffset = .25f;
            var camera = new GameObject("MainCamera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            camera.transform.SetParent(player.transform, false); camera.transform.localPosition = new Vector3(0, 1.6f, 0); camera.tag = "MainCamera";
            camera.nearClipPlane = .06f; camera.farClipPlane = 70; camera.fieldOfView = 65; camera.backgroundColor = new Color(.03f, .045f, .07f); camera.clearFlags = CameraClearFlags.SolidColor;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            var light = new GameObject("KeyLight").AddComponent<Light>(); light.transform.SetParent(root.transform); light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(48, -28, 0); light.intensity = 1.2f; light.shadows = LightShadows.Soft;
            RenderSettings.ambientMode = AmbientMode.Trilight; RenderSettings.ambientSkyColor = new Color(.4f, .47f, .6f); RenderSettings.ambientEquatorColor = new Color(.25f, .29f, .36f); RenderSettings.ambientGroundColor = new Color(.15f, .12f, .1f); RenderSettings.skybox = null;
            var area = root.AddComponent<JinxCasinoWorldArea>(); area.Configure(0, layout.Spawn, layout.Root.gameObject, null);
            string configPath = Root + "/Data/ImmersionSettings.asset";
            var settings = AssetDatabase.LoadAssetAtPath<JinxCasinoGameSettings>(configPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<JinxCasinoGameSettings>();
                AssetDatabase.CreateAsset(settings, configPath);
                ApplyImmersionSampleSettings(settings);
            }
            var owner = root.AddComponent<JinxCasinoController>(); owner.Configure(camera, EnsureSessionSettings(), layout.Stations.Select(value => value.transform).ToArray());
            owner.ConfigureAdventure(settings, new[] { area }, null);
            owner.ConfigureImmersion(AssetDatabase.LoadAssetAtPath<InputActionAsset>(JinxCasinoImmersionInputBuilder.AssetPath));
            var palette = new Dictionary<string, Material>();
            foreach (string path in Directory.GetFiles(art + "/Materials", "*.mat"))
                palette.Add(Path.GetFileNameWithoutExtension(path), AssetDatabase.LoadAssetAtPath<Material>(path));
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            var presentations = new List<JinxCasinoS1Presentation>();
            foreach (var station in layout.Stations)
            {
                string model = station.Game == CasinoGameKind.Slots ? "S1Slots" : station.Game == CasinoGameKind.Blackjack ? "S1Blackjack" : "S1Levers";
                presentations.Add(JinxCasinoS1PresentationBuilder.ConfigureS1Presentation(station, station.transform.Find(model), font, palette));
            }
            root.AddComponent<JinxCasinoS1PresentationCoordinator>().Configure(owner, layout.Stations, presentations.ToArray());
            if (!EditorSceneManager.SaveScene(scene, ImmersionScenePath)) throw new InvalidOperationException("保存样板场景失败。");
        }
    }
}
