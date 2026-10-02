using Core.Runtime.Inputs;
#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Core.Runtime;
using Cysharp.Threading.Tasks;
using Hotfix;
using Hotfix.JinxCasino.Adapters;
using Hotfix.JinxCasino.Persistence;
using Hotfix.JinxCasino.Adapters.UI;
using Hotfix.JinxCasino.Rules;
using Hotfix.SceneManagement;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Tests.Demo
{
    /// 正式保存设置/表情面板回归；PlayerPrefs及存档目录都使用独立UUID，不接管View生命周期。
    public sealed class JinxCasinoLocalSettingsUiTests
    {
        private readonly List<JinxCasinoGameSettings> clones = new List<JinxCasinoGameSettings>();
        private JinxCasinoController controller;
        private JinxCasinoAdventurePresenter adventure;
        private JinxCasinoLocalSettingsPresenter local;
        private JinxCasinoHudView hud;
        private GameViewResolution resolution;
        private CasinoLocalPreferencesStore preferences;
        private string preferenceKey;
        private string saveDirectory;

        [UnityTest, Timeout(240000)]
        public IEnumerator SavedSettingsPersistReenterAndCancelPreviewWhenEventHidesModal()
        {
            yield return Enter(); Click(Field<Button>(adventure, "practiceButton")); yield return null;
            Click(Field<Button>(adventure, "settingsButton")); yield return null;
            Assert.That(Field<GameObject>(local, "settingsPanel").activeInHierarchy, Is.True);
            var move = Field<TouchInputPad>(local, "movePad"); var look = Field<TouchInputPad>(local, "lookPad");
            Assert.That(ScreenCenter(move).x, Is.LessThan(ScreenCenter(look).x));
            var safeRoot = (RectTransform)hud.gameObject.GetComponentInChildren<JinxCasinoHudPresenter>(true).transform;
            Vector2 safeMinimum = safeRoot.anchorMin, safeMaximum = safeRoot.anchorMax;
            var oldMove = Pointer(51, new Vector2(100, 100), Vector2.zero); var oldLook = Pointer(52, new Vector2(1000, 100), new Vector2(24, 12));
            move.OnPointerDown(oldMove); look.OnPointerDown(oldLook);
            oldMove.position += new Vector2(60, 0); move.OnDrag(oldMove); look.OnDrag(oldLook);
            Assert.That(move.Move.sqrMagnitude, Is.GreaterThan(0));
            Field<Toggle>(local, "leftHanded").isOn = true;
            move.OnDrag(oldMove); look.OnDrag(oldLook);
            Assert.That(move.Move, Is.EqualTo(Vector2.zero)); Assert.That(look.ConsumeLook(), Is.EqualTo(Vector2.zero), "布局改变必须释放旧手指，不反射pointerId");
            Assert.That(ScreenCenter(move).x, Is.GreaterThan(ScreenCenter(look).x));
            Assert.That(safeRoot.anchorMin, Is.EqualTo(safeMinimum)); Assert.That(safeRoot.anchorMax, Is.EqualTo(safeMaximum));
            Field<Slider>(local, "pcSensitivity").value = 2; Field<Slider>(local, "touchSensitivity").value = 0.5f;
            Field<Slider>(local, "volume").value = 0.2f; Field<Toggle>(local, "muted").isOn = true;
            Assert.That(PlayerPrefs.HasKey(preferenceKey), Is.False, "预览不写本机偏好键");
            AssertAudio(0.2f, true);
            Click(Field<Button>(local, "saveButton")); yield return null;
            AssertSaved(); yield return Screenshot("P4SavedSettings720p");
            yield return ExitBySavedBack(); yield return Enter();
            AssertSaved(); Assert.That(controller.LocalPreferences.PcLookMultiplier, Is.EqualTo(2)); AssertAudio(0.2f, true);
            move = Field<TouchInputPad>(local, "movePad"); look = Field<TouchInputPad>(local, "lookPad");
            Assert.That(ScreenCenter(move).x, Is.GreaterThan(ScreenCenter(look).x));
            Click(Field<Button>(adventure, "standardButton")); yield return null;
            Click(Field<Button>(adventure, "settingsButton")); yield return null;
            Field<Slider>(local, "pcSensitivity").value = 1.25f; Field<Toggle>(local, "leftHanded").isOn = false;
            Click(Field<Button>(local, "settingsCloseButton")); yield return null;
            Assert.That(controller.LocalPreferences.PcLookMultiplier, Is.EqualTo(2)); Assert.That(controller.LocalPreferences.LeftHanded, Is.True);
            Click(Field<Button>(adventure, "settingsButton")); yield return null;
            Field<Slider>(local, "pcSensitivity").value = 2.75f; Field<Slider>(local, "volume").value = 0.8f;
            Field<Toggle>(local, "muted").isOn = false; Field<Toggle>(local, "leftHanded").isOn = false;
            Assert.That(controller.LocalPreferences.PcLookMultiplier, Is.EqualTo(2.75f));
            yield return Wait(() => Field<GameObject>(adventure, "eventPanel").activeInHierarchy, "真实神秘商人事件打开模态", 8);
            Assert.That(local.gameObject.activeInHierarchy, Is.False);
            Assert.That(controller.LocalPreferences.PcLookMultiplier, Is.EqualTo(2)); Assert.That(controller.LocalPreferences.LeftHanded, Is.True);
            Assert.That(ScreenCenter(move).x, Is.GreaterThan(ScreenCenter(look).x)); AssertSaved(); AssertAudio(0.2f, true);
            // 事件在宿主Update中激活控件；先让新画布完成首帧渲染/裁剪，再按玩家可见的按钮中心点击。
            yield return null;
            Click(Field<Button[]>(adventure, "eventButtons")[0]); yield return null;
            Click(Field<Button>(adventure, "settingsButton")); yield return null;
            resolution?.Dispose(); resolution = new GameViewResolution(1600, 720);
            yield return Wait(() => Screen.width == 1600 && Screen.height == 720, "真实20:9 GameView", 15);
            yield return Screenshot("P4LocalSettings20x9"); yield return ExitBySavedBack();
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator SavedEmotePickerOnlyPlaysUnlockedLocalAvatarAndRestoresJoints()
        {
            yield return Enter(); Click(Field<Button>(adventure, "practiceButton")); yield return null;
            var body = Field<CharacterController>(controller, "body");
            var avatar = body.GetComponentsInChildren<JinxCasinoAvatarPresentation>(true).Single(value => value.ActorRoot == body.transform);
            var rightArm = avatar.GetComponentsInChildren<Transform>(true).Single(value => value.name == "Avatar.ArmRightPivot");
            string equipped = controller.Game.ProfileData.EquippedEmote; long coins = controller.Game.State.Coins;
            Click(Field<Button>(adventure, "emoteButton")); yield return null;
            Assert.That(Field<GameObject>(local, "emotePanel").activeInHierarchy, Is.True);
            var dropdown = Field<object>(local, "emoteDropdown");
            var expected = CasinoProfileCatalog.Definitions.Where(value => value.Kind == CasinoCosmeticKind.Emote && controller.Game.ProfileData.UnlockedIds.Contains(value.Id)).Select(value => value.Name).ToArray();
            CollectionAssert.AreEqual(expected, Labels(dropdown)); Assert.That(Labels(dropdown), Does.Contain("挥手"));
            Assert.That(avatar.IsPlayingEmote, Is.False); Quaternion originalArm = rightArm.localRotation; Vector3 actorPosition = body.transform.position;
            SetValue(dropdown, Array.IndexOf(expected, "挥手")); Click(Field<Button>(local, "playEmoteButton")); yield return null;
            Assert.That(avatar.IsPlayingEmote, Is.True);
            yield return Wait(() => Quaternion.Angle(originalArm, rightArm.localRotation) > 15, "真实挥手关节动作", 1);
            Assert.That(body.transform.position, Is.EqualTo(actorPosition), "表情不改变玩家角色根或相机碰撞体");
            Assert.That(controller.TryPlayLocalEmote("emote_fireworks"), Is.False, "未解锁纸片烟花不能绕过保存列表播放");
            yield return Screenshot("P4SavedEmote720p");
            yield return Wait(() => !avatar.IsPlayingEmote, "两秒表情结束", 4);
            Assert.That(Quaternion.Angle(originalArm, rightArm.localRotation), Is.LessThan(1));
            Assert.That(controller.Game.ProfileData.EquippedEmote, Is.EqualTo(equipped)); Assert.That(controller.Game.State.Coins, Is.EqualTo(coins));
            Click(Field<Button>(local, "emoteCloseButton")); yield return null;
            Click(Field<Button>(adventure, "settingsButton")); yield return null; yield return ExitBySavedBack();
        }

        private void AssertSaved()
        {
            var data = preferences.Load(); Assert.That(data.PcLookMultiplier, Is.EqualTo(2)); Assert.That(data.TouchLookMultiplier, Is.EqualTo(0.5f));
            Assert.That(data.Volume, Is.EqualTo(0.2f)); Assert.That(data.Muted && data.LeftHanded, Is.True);
        }
        private void AssertAudio(float volume, bool mute)
        {
            var audio = controller.GetComponent<JinxCasinoAudioDirector>(); Assert.That(audio, Is.Not.Null);
            Assert.That(audio.Volume, Is.EqualTo(volume)); Assert.That(audio.Muted, Is.EqualTo(mute));
            var music = Field<AudioSource>(audio, "music"); var sfx = Field<AudioSource>(audio, "sfx");
            Assert.That(music.volume, Is.EqualTo(volume * 0.16f).Within(0.0001)); Assert.That(sfx.volume, Is.EqualTo(volume).Within(0.0001));
            Assert.That(music.mute && sfx.mute, Is.EqualTo(mute));
        }
        private IEnumerator Enter()
        {
            if (GameSceneNavigator.Instance == null)
            { var startup = SceneManager.LoadSceneAsync("AppEntrance", LoadSceneMode.Single); yield return Wait(() => startup.isDone, "正式启动", 90); }
            yield return Wait(IsStableHub, "正式Hub", 90);
            resolution?.Dispose(); resolution = new GameViewResolution(1280, 720);
            yield return Wait(() => Screen.width == 1280 && Screen.height == 720, "720p GameView", 15);
            var travel = GameSceneNavigator.Instance.SwitchAsync(GameSceneId.JinxCasino).AsTask(); yield return Wait(() => travel.IsCompleted, "正式赌场", 45);
            Assert.That(travel.GetAwaiter().GetResult().Status, Is.EqualTo(GameSceneSwitchStatus.Succeeded));
            yield return Wait(() => UIManager.Instance.Get<JinxCasinoHudView>()?.State == ViewState.Visible && !GameSceneNavigator.Instance.IsTransitioning, "正式HUD", 30);
            controller = Object.FindFirstObjectByType<JinxCasinoController>(); hud = UIManager.Instance.Get<JinxCasinoHudView>();
            adventure = hud.gameObject.GetComponentInChildren<JinxCasinoAdventurePresenter>(true); local = Field<JinxCasinoLocalSettingsPresenter>(adventure, "localSettingsPresenter"); Assert.That(local, Is.Not.Null);
            var groups = hud.gameObject.GetComponentsInParent<CanvasGroup>(true); Assert.That(groups.Length, Is.GreaterThan(0));
            yield return Wait(() => groups.All(group => group.alpha >= 0.99f), "Core入场淡出真正结束", 2);
            var settings = Object.Instantiate(Field<JinxCasinoGameSettings>(controller, "gameSettings")); clones.Add(settings);
            var config = settings.CreateConfig(); config.EventIntervalMilliseconds = 4000;
            config.EventWeights = CasinoContentCatalog.Events.Select(value => new CasinoEventWeight { EventId = value.Id, Weight = value.Id == "mystery_merchant" ? 1 : 0 }).ToArray();
            SetField(settings, "adventure", config);
            controller.ConfigureAdventure(settings, Field<JinxCasinoWorldArea[]>(controller, "areas"), Field<JinxCasinoSceneEffects>(controller, "sceneEffects"));
            if (preferences == null)
            {
                preferenceKey = "JinxCasino.Tests.LocalSettingsUi." + Guid.NewGuid().ToString("N"); preferences = new CasinoLocalPreferencesStore(preferenceKey);
                saveDirectory = Path.GetFullPath(Path.Combine("Library/JinxCasino/TestSaves", "LocalSettingsUi-" + Guid.NewGuid().ToString("N")));
            }
            controller.Game.SetLocalSaveStore(new CasinoLocalSaveStore(saveDirectory)); controller.Game.SetLocalProfileStore(new CasinoProfileStore(Path.Combine(saveDirectory, "Profile")));
            controller.LoadLocalPreferences(preferences); yield return null;
        }
        private IEnumerator ExitBySavedBack()
        {
            var previous = controller; var previousHud = hud;
            Click(Field<Button[]>(adventure, "panelBackButtons").Single(button => button.gameObject.activeInHierarchy));
            yield return Wait(() => previous == null && IsStableHub(), "正式退出与View销毁", 45);
            Assert.That(previousHud.State, Is.EqualTo(ViewState.Destroyed)); controller = null;
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            try
            {
                var live = Object.FindFirstObjectByType<JinxCasinoController>();
                if (live != null) { live.RequestExit(); yield return Wait(() => live == null && IsStableHub(), "设置回归返回Hub", 45); }
            }
            finally
            {
                resolution?.Dispose(); foreach (var clone in clones) if (clone != null) Object.Destroy(clone); clones.Clear();
                if (!string.IsNullOrEmpty(preferenceKey)) { PlayerPrefs.DeleteKey(preferenceKey); PlayerPrefs.Save(); }
                if (!string.IsNullOrEmpty(saveDirectory) && Object.FindFirstObjectByType<JinxCasinoController>() == null)
                {
                    string allowed = Path.GetFullPath("Library/JinxCasino/TestSaves") + Path.DirectorySeparatorChar;
                    Assert.That(saveDirectory.StartsWith(allowed, StringComparison.OrdinalIgnoreCase) && Path.GetFileName(saveDirectory).StartsWith("LocalSettingsUi-", StringComparison.Ordinal), Is.True);
                    if (Directory.Exists(saveDirectory)) Directory.Delete(saveDirectory, true);
                }
                preferences = null; preferenceKey = saveDirectory = null;
            }
        }
        private static PointerEventData Pointer(int id, Vector2 position, Vector2 delta) => new PointerEventData(EventSystem.current) { pointerId = id, position = position, delta = delta };
        private static Vector2 ScreenCenter(Component component)
        { var rect = (RectTransform)component.transform; var canvas = component.GetComponentInParent<Canvas>(); return RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, rect.TransformPoint(rect.rect.center)); }
        private static void Click(Button button)
        {
            Assert.That(button != null && button.isActiveAndEnabled && button.interactable, Is.True); Canvas.ForceUpdateCanvases();
            var pointer = new PointerEventData(EventSystem.current) { position = ScreenCenter(button), button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == button, Is.True,
                "保存按钮中心射线不能被遮挡：目标=" + button.name + "，首命中=" + (hits.Count > 0 ? hits[0].gameObject.name : "无") + "，中心=" + pointer.position);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }
        private static void SetValue(object component, int value) => component.GetType().GetProperty("value").SetValue(component, value);
        private static string[] Labels(object dropdown) => ((IEnumerable)dropdown.GetType().GetProperty("options").GetValue(dropdown)).Cast<object>().Select(option => (string)option.GetType().GetProperty("text").GetValue(option)).ToArray();
        private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(owner);
        private static void SetField(object owner, string name, object value) => owner.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(owner, value);
        private static bool IsStableHub() => GameSceneNavigator.Instance != null && GameSceneNavigator.Instance.CurrentScene == GameSceneId.Hub && !GameSceneNavigator.Instance.IsTransitioning && UIManager.Instance.Get<MainMenuView>()?.State == ViewState.Visible;
        private static IEnumerator Wait(Func<bool> predicate, string reason, float seconds)
        { double deadline = Time.realtimeSinceStartupAsDouble + seconds; while (!predicate() && Time.realtimeSinceStartupAsDouble < deadline) yield return null; Assert.That(predicate(), Is.True, reason + "超时"); }
        private IEnumerator Screenshot(string name)
        {
            var groups = hud.gameObject.GetComponentsInParent<CanvasGroup>(true);
            yield return Wait(() => groups.All(group => group.alpha >= 0.99f), "截图前Core入场淡出结束", 2); yield return null; yield return null;
            string directory = Path.GetFullPath("Library/JinxCasino/Verification"); Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, name + "-" + Guid.NewGuid().ToString("N") + ".png"); int width = Screen.width, height = Screen.height;
            ScreenCapture.CaptureScreenshot(path); yield return null; yield return null;
            yield return Wait(() => File.Exists(path) && new FileInfo(path).Length > 24, "实际设置/表情截图", 15);
            byte[] bytes = File.ReadAllBytes(path);
            Assert.That(bytes[16] << 24 | bytes[17] << 16 | bytes[18] << 8 | bytes[19], Is.EqualTo(width));
            Assert.That(bytes[20] << 24 | bytes[21] << 16 | bytes[22] << 8 | bytes[23], Is.EqualTo(height));
            Debug.Log("[JinxCasinoLocalSettingsUiTests] " + width + "x" + height + " " + path);
        }
    }
}
#endif
