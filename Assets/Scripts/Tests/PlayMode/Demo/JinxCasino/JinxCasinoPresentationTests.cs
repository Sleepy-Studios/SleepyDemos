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
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tests.Demo
{
    /// 保存的P4角色与音源行为回归；专有存档不修改用户档案、场景资源或GameView设置。
    public sealed class JinxCasinoPresentationTests
    {
        private JinxCasinoController controller;
        private JinxCasinoGameSettings settings;
        private CasinoProfileStore profileStore;
        private string saveDirectory;

        [UnityTest]
        public IEnumerator SavedAvatarRejectsLockedOutfitsDefersColorUntilOverrideEndsAndRestoresEmotePose()
        {
            yield return EnterSavedDemo();
            controller.StartAdventure(CasinoAdventureMode.Practice, 1); yield return null;
            var camera = Camera.main; var capsule = camera.GetComponentInParent<CharacterController>();
            var avatar = Object.FindObjectsByType<JinxCasinoAvatarPresentation>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Single(value => value.ActorRoot == capsule.transform);
            var effects = controller.GetComponent<JinxCasinoSceneEffects>();
            yield return Wait(() => avatar.CurrentColor == "color_blue", "专有空档案的默认配色", 5);
            Assert.That(controller.Game.EquipProfile("color_pink", "hat_party"), Is.False);
            Assert.That(avatar.PlayEmote("emote_fireworks"), Is.False);
            Assert.That(controller.Game.ProfileData.EquippedColor, Is.EqualTo("color_blue"));
            Assert.That(avatar.CurrentHat, Is.EqualTo("hat_none"));

            // 真正结束一次正式旅程解锁派对粉/纸帽，不直接注入伪造的UnlockedIds。
            controller.StartAdventure(CasinoAdventureMode.Standard, 1); yield return null;
            Assert.That(controller.Game.ChooseEnding(CasinoAdventureEnding.Withdraw, Time.frameCount).Success, Is.True); yield return null;
            Assert.That(controller.Game.ProfileData.UnlockedIds, Does.Contain("color_pink"));
            controller.StartAdventure(CasinoAdventureMode.Practice, 1); yield return null;
            var clothing = avatar.GetComponentsInChildren<Renderer>(true).First(renderer => renderer.sharedMaterials.Any(material => material != null && material.name == "color_blue"));
            var original = clothing.sharedMaterials;
            int[] slots = Enumerable.Range(0, original.Length).Where(index => original[index] != null && original[index].name.StartsWith("color_", StringComparison.Ordinal)).ToArray();
            effects.ApplyEffects(new[] { MaterialEffect("presentation-ink", "Ink") });
            Assert.That(effects.IsCostumeOverridden, Is.True);
            Assert.That(controller.Game.EquipProfile("color_pink", "hat_party"), Is.True);
            yield return null;
            Assert.That(avatar.CurrentColor, Is.EqualTo("color_blue"), "临时覆盖拥有材质时不能抢写永久色。");
            Assert.That(slots.All(index => clothing.sharedMaterials[index].name == "InkPaint"), Is.True);
            yield return Wait(() => !effects.IsCostumeOverridden && avatar.CurrentColor == "color_pink", "墨迹结束后应用新保存的配色", 5);
            AssertPermanentSlots(clothing, original, slots, "color_pink");
            Assert.That(HasVisibleHat(avatar, "hat_party"), Is.True);

            effects.ApplyEffects(new[] { MaterialEffect("presentation-disguise", "Disguise") });
            Assert.That(controller.Game.EquipProfile("color_blue", "hat_none"), Is.True);
            yield return Wait(() => !effects.IsCostumeOverridden && avatar.CurrentColor == "color_blue" && avatar.CurrentHat == "hat_none", "临时换装后恢复最新轻装配置", 5);
            AssertPermanentSlots(clothing, original, slots, "color_blue");
            Assert.That(HasVisibleHat(avatar, null), Is.False);
            Assert.That(profileStore.LoadOrCreate().Data.EquippedHat, Is.EqualTo("hat_none"));

            // 保存机台面板的输入阻挡隔离鼠标/触控，检查的是纯模型动作对相机和物理的影响。
            var hud = UIManager.Instance.Get<JinxCasinoHudView>();
            hud.gameObject.GetComponentInChildren<JinxCasinoAdventurePresenter>(true).ShowStation(CasinoGameKind.CoinFlip);
            yield return null;
            var left = Node(avatar, "Avatar.ArmLeftPivot"); var right = Node(avatar, "Avatar.ArmRightPivot");
            Quaternion leftNeutral = left.localRotation, rightNeutral = right.localRotation;
            Vector3 cameraPosition = camera.transform.localPosition, capsuleCenter = capsule.center;
            Quaternion cameraRotation = camera.transform.localRotation;
            Transform cameraParent = camera.transform.parent;
            float height = capsule.height, radius = capsule.radius;
            Assert.That(avatar.PlayEmote("emote_wave"), Is.True);
            float started = Time.unscaledTime;
            yield return Wait(() => Time.unscaledTime - started >= 0.35f, "挥手抬臂进入动作", 3);
            Assert.That(Quaternion.Angle(right.localRotation, rightNeutral), Is.GreaterThan(30));
            yield return Wait(() => !avatar.IsPlayingEmote, "挥手结束", 5);
            Assert.That(Quaternion.Angle(left.localRotation, leftNeutral), Is.LessThan(0.01f));
            Assert.That(Quaternion.Angle(right.localRotation, rightNeutral), Is.LessThan(0.01f));
            Assert.That(camera.transform.parent, Is.SameAs(cameraParent));
            Assert.That(Vector3.Distance(camera.transform.localPosition, cameraPosition), Is.LessThan(0.0001f));
            Assert.That(Quaternion.Angle(camera.transform.localRotation, cameraRotation), Is.LessThan(0.001f));
            Assert.That(capsule.height, Is.EqualTo(height)); Assert.That(capsule.radius, Is.EqualTo(radius)); Assert.That(capsule.center, Is.EqualTo(capsuleCenter));
            AssertSingleListener();
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            try
            {
                if (controller != null)
                {
                    yield return Wait(() => controller == null || !controller.IsBusy, "等待本用例在途操作", 15);
                    if (controller != null) controller.RequestExit();
                    yield return Wait(() => controller == null && IsStableHub(), "释放本用例场景并返回Hub", 30);
                }
            }
            finally
            {
                if (settings != null) Object.Destroy(settings);
                if (controller == null && saveDirectory != null)
                {
                    string allowed = Path.GetFullPath("Library/JinxCasino/TestSaves") + Path.DirectorySeparatorChar;
                    string absolute = Path.GetFullPath(saveDirectory);
                    Assert.That(absolute.StartsWith(allowed, StringComparison.OrdinalIgnoreCase), Is.True);
                    if (Directory.Exists(absolute)) Directory.Delete(absolute, true);
                }
            }
        }

        private IEnumerator EnterSavedDemo()
        {
            if (GameSceneNavigator.Instance == null)
            {
                var boot = SceneManager.LoadSceneAsync("AppEntrance", LoadSceneMode.Single); Assert.That(boot, Is.Not.Null);
                yield return Wait(() => boot.isDone, "正式启动入口", 90);
            }
            yield return Wait(IsStableHub, "正式Hub", 90);
            Assert.That(GameSceneNavigator.Instance.IsEditorDirect, Is.False);
            var travel = GameSceneNavigator.Instance.SwitchAsync(GameSceneId.JinxCasino).AsTask();
            yield return Wait(() => travel.IsCompleted, "正式导航保存的P4场景", 30);
            Assert.That(travel.GetAwaiter().GetResult().Status, Is.EqualTo(GameSceneSwitchStatus.Succeeded));
            yield return Wait(() => UIManager.Instance.Get<JinxCasinoHudView>()?.State == ViewState.Visible, "保存HUD绑定", 30);
            controller = Object.FindFirstObjectByType<JinxCasinoController>(); Assert.That(controller, Is.Not.Null);
            saveDirectory = Path.GetFullPath(Path.Combine("Library/JinxCasino/TestSaves", "P4Presentation-" + Guid.NewGuid().ToString("N")));
            controller.Game.SetLocalSaveStore(new CasinoLocalSaveStore(saveDirectory));
            profileStore = new CasinoProfileStore(Path.Combine(saveDirectory, "Profile")); controller.Game.SetLocalProfileStore(profileStore);
            settings = Object.Instantiate(Field<JinxCasinoGameSettings>(controller, "gameSettings"));
            var config = settings.CreateConfig(); config.StageCount = 1; config.Targets = new long[] { 100000 }; config.EventIntervalMilliseconds = 0;
            typeof(JinxCasinoGameSettings).GetField("adventure", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(settings, config);
            controller.ConfigureAdventure(settings, Field<JinxCasinoWorldArea[]>(controller, "areas"), controller.GetComponent<JinxCasinoSceneEffects>());
            AssertSingleListener(); yield return null;
        }
        private static CasinoSceneEffect MaterialEffect(string id, string kind) => new CasinoSceneEffect { Id = id, EffectKind = kind, TargetId = "local", DurationMilliseconds = 650, Description = "本用例临时材质覆盖" };
        private static bool HasVisibleHat(JinxCasinoAvatarPresentation avatar, string id)
            => avatar.GetComponentsInChildren<Renderer>(true).Any(renderer => renderer.gameObject.activeInHierarchy && renderer.GetComponentsInParent<Transform>(true)
                .Any(parent => id == null ? parent.name.StartsWith("hat_", StringComparison.Ordinal) : parent.name == id));
        private static Transform Node(JinxCasinoAvatarPresentation avatar, string name) => avatar.GetComponentsInChildren<Transform>(true).Single(node => node.name == name);
        private static void AssertPermanentSlots(Renderer clothing, Material[] original, int[] slots, string color)
        {
            var current = clothing.sharedMaterials;
            for (int index = 0; index < original.Length; index++)
                if (slots.Contains(index)) Assert.That(current[index].name, Is.EqualTo(color));
                else Assert.That(current[index], Is.SameAs(original[index]), "永久配色不能覆盖眼睛、纸面或铜金槽。");
        }
        private static T Field<T>(object target, string name)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic); Assert.That(field, Is.Not.Null); return (T)field.GetValue(target);
        }
        private static bool IsStableHub() => GameSceneNavigator.Instance != null && GameSceneNavigator.Instance.CurrentScene == GameSceneId.Hub && !GameSceneNavigator.Instance.IsTransitioning && UIManager.Instance.Get<MainMenuView>()?.State == ViewState.Visible;
        private static void AssertSingleListener() => Assert.That(Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(listener => listener.isActiveAndEnabled), Is.EqualTo(1));
        private static IEnumerator Wait(Func<bool> predicate, string reason, float timeout)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!predicate()) { if (Time.realtimeSinceStartup > deadline) Assert.Fail("等待超时：" + reason); yield return null; }
        }
    }
}
#endif
