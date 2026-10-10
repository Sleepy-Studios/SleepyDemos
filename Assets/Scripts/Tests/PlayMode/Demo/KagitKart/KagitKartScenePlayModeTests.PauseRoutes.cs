#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using Core.Runtime;
using Core.Runtime.Inputs;
using Cysharp.Threading.Tasks;
using Hotfix;
using Hotfix.KagitKart;
using Hotfix.SceneManagement;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Tests.Demo
{
    // 合入现有场景测试类，复用SetUp/TearDown、Wait、KeyPress、PadPress和Capture。
    public sealed partial class KagitKartScenePlayModeTests
    {
        [UnityTest, Timeout(240000)]
        public IEnumerator PauseSubpagesAndGpExitKeepOriginalRoutes()
        {
            yield return GameSceneNavigator.Instance.SwitchAsync(GameSceneId.KagitKart).ToCoroutine();
            KagitKartWorld world = null;
            yield return Wait(() => (world = Object.FindFirstObjectByType<KagitKartWorld>()) != null
                && !world.Busy && UIManager.Instance.Get<KagitKartTitleView>()?.State == ViewState.Visible, "暂停路由入口");
            yield return world.StartRaceAsync(KagitKartRaceMode.GrandPrix, "meadow", "tilki", 120).ToCoroutine();
            world.SendMessage("OnApplicationFocus", true);
            world.Dispatch(KagitKartCommand.Pause);
            yield return Wait(() => UIManager.Instance.Get<KagitKartPauseView>()?.State == ViewState.Visible && !world.Busy, "GP暂停目录");
            var session = world.Session;
            var pause = UIManager.Instance.Get<KagitKartPauseView>();
            Assert.That(UIManager.Instance.Get<KagitKartHudView>().gameObject.GetComponentsInChildren<Button>(true)
                    .Single(button => button.name == "SkipIntro").transform.parent.gameObject.activeSelf,
                Is.False, "暂停赛前演出时跳过按钮及其阴影一起隐藏。");
            var quit = Button(pause, "Title");
            EventSystem.current.SetSelectedGameObject(quit.gameObject);
            quit.onClick.Invoke();
            StringAssert.Contains("杯赛进度将丢失", pause.gameObject.GetComponentsInChildren<TMP_Text>().Single(text => text.name == "Detail").text);
            Assert.That(world.Session, Is.SameAs(session), "GP第一次返回菜单只确认，不退出。");
            yield return Capture("PauseGpQuitConfirmation", 0);
            EventSystem.current.SetSelectedGameObject(Button(pause, "Resume").gameObject);
            Assert.That(pause.gameObject.GetComponentsInChildren<TMP_Text>().Single(text => text.name == "Detail").text.Contains("确定退出"), Is.False);
            EventSystem.current.SetSelectedGameObject(quit.gameObject);
            quit.onClick.Invoke();
            Assert.That(world.Session, Is.SameAs(session), "移开焦点后必须重新第一次确认。");
            EventSystem.current.SetSelectedGameObject(Button(pause, "Settings").gameObject);
            Button(pause, "Settings").onClick.Invoke();
            yield return Wait(() => UIManager.Instance.Get<KagitKartSettingsView>()?.State == ViewState.Visible && !world.Busy, "暂停设置页");
            yield return KeyPress(Key.Escape);
            yield return Wait(() => UIManager.Instance.Get<KagitKartSettingsView>() == null && pause.State == ViewState.Visible && !world.Busy, "Esc只退至暂停目录");
            Assert.That(session.Pause.IsPaused, Is.True);
            Button(pause, "Settings").onClick.Invoke();
            yield return Wait(() => UIManager.Instance.Get<KagitKartSettingsView>()?.State == ViewState.Visible && !world.Busy, "再开暂停设置");
            yield return KeyPress(Key.P);
            yield return Wait(() => !session.Pause.IsPaused && UIManager.Instance.Get<KagitKartSettingsView>() == null
                && UIManager.Instance.Get<KagitKartPauseView>() == null && !world.Busy, "P从设置继续");
            Assert.That(world.Session, Is.SameAs(session));

            gamepad = InputSystem.AddDevice<Gamepad>();
            InputDeviceState.Notify(gamepad);
            yield return null;
            world.Dispatch(KagitKartCommand.Pause);
            yield return Wait(() => UIManager.Instance.Get<KagitKartPauseView>()?.State == ViewState.Visible && !world.Busy, "手柄暂停目录");
            pause = UIManager.Instance.Get<KagitKartPauseView>();
            Button(pause, "Controls").onClick.Invoke();
            yield return Wait(() => UIManager.Instance.Get<KagitKartControlsView>()?.State == ViewState.Visible && !world.Busy, "暂停操作页");
            yield return PadPress(GamepadButton.East);
            yield return Wait(() => UIManager.Instance.Get<KagitKartControlsView>() == null && pause.State == ViewState.Visible && !world.Busy, "B只退至暂停目录");
            Assert.That(session.Pause.IsPaused, Is.True);
            Button(pause, "Controls").onClick.Invoke();
            yield return Wait(() => UIManager.Instance.Get<KagitKartControlsView>()?.State == ViewState.Visible && !world.Busy, "再开操作页");
            yield return PadPress(GamepadButton.Start);
            yield return Wait(() => !session.Pause.IsPaused && UIManager.Instance.Get<KagitKartControlsView>() == null
                && UIManager.Instance.Get<KagitKartPauseView>() == null && !world.Busy, "Start从操作页继续");
            Assert.That(world.Session, Is.SameAs(session));
            world.Dispatch(KagitKartCommand.Pause);
            yield return Wait(() => UIManager.Instance.Get<KagitKartPauseView>()?.State == ViewState.Visible && !world.Busy, "GP退出确认目录");
            pause = UIManager.Instance.Get<KagitKartPauseView>();
            quit = Button(pause, "Title");
            EventSystem.current.SetSelectedGameObject(quit.gameObject);
            quit.onClick.Invoke();
            Assert.That(world.Session, Is.SameAs(session));
            quit.onClick.Invoke();
            yield return Wait(() => UIManager.Instance.Get<KagitKartTitleView>()?.State == ViewState.Visible && !world.Busy, "第二次确认回Demo标题");
            Assert.That(world.Session, Is.Null);
            Assert.That(world.GrandPrix, Is.Null, "普通返回菜单清杯赛。");
            Assert.That(Object.FindFirstObjectByType<KagitKartWorld>(), Is.SameAs(world), "没有返回外部Hub。");

            InputDeviceState.Notify(keyboard);
            yield return world.StartRaceAsync(KagitKartRaceMode.TimeTrial, "meadow", "tilki", 120).ToCoroutine();
            world.SendMessage("OnApplicationFocus", true);
            world.Dispatch(KagitKartCommand.Pause);
            yield return Wait(() => UIManager.Instance.Get<KagitKartPauseView>()?.State == ViewState.Visible && !world.Busy, "TT赛前暂停");
            session = world.Session;
            Button(UIManager.Instance.Get<KagitKartPauseView>(), "Settings").onClick.Invoke();
            yield return Wait(() => UIManager.Instance.Get<KagitKartSettingsView>()?.State == ViewState.Visible && !world.Busy, "TT赛前设置");
            Assert.That(world.IntroPlaying, Is.True);
            yield return KeyPress(Key.R);
            Assert.That(world.Session, Is.SameAs(session), "TT R不能越过intro phase守卫。");
            Assert.That(UIManager.Instance.Get<KagitKartSettingsView>()?.State, Is.EqualTo(ViewState.Visible));
            yield return KeyPress(Key.P);
            yield return Wait(() => !session.Pause.IsPaused && UIManager.Instance.Get<KagitKartSettingsView>() == null && !world.Busy, "继续TT赛前");
            world.Dispatch(KagitKartCommand.SkipIntro);
            world.Dispatch(KagitKartCommand.Pause);
            yield return Wait(() => UIManager.Instance.Get<KagitKartPauseView>()?.State == ViewState.Visible && !world.Busy, "TT倒计时暂停");
            Button(UIManager.Instance.Get<KagitKartPauseView>(), "Controls").onClick.Invoke();
            yield return Wait(() => UIManager.Instance.Get<KagitKartControlsView>()?.State == ViewState.Visible && !world.Busy, "TT倒计时操作页");
            Assert.That(world.IntroPlaying, Is.False);
            Assert.That(session.Race.State, Is.EqualTo(KagitKartRaceState.Countdown));
            yield return KeyPress(Key.R);
            yield return Wait(() => world.Session != null && world.Session != session && !world.Busy
                && UIManager.Instance.Get<KagitKartControlsView>() == null && UIManager.Instance.Get<KagitKartPauseView>() == null, "R从TT倒计时子页真正重赛");
            Assert.That(world.Session.Race.State, Is.EqualTo(KagitKartRaceState.Countdown));
            world.Dispatch(KagitKartCommand.Pause);
            yield return Wait(() => UIManager.Instance.Get<KagitKartPauseView>()?.State == ViewState.Visible && !world.Busy, "普通返回菜单目录");
            Button(UIManager.Instance.Get<KagitKartPauseView>(), "Title").onClick.Invoke();
            yield return Wait(() => UIManager.Instance.Get<KagitKartTitleView>()?.State == ViewState.Visible && !world.Busy, "TT一次选择回Demo标题");
            Assert.That(world.Session, Is.Null);
            Button(UIManager.Instance.Get<KagitKartTitleView>(), "Hub").onClick.Invoke();
            yield return Wait(() => Object.FindFirstObjectByType<KagitKartWorld>() == null
                && UIManager.Instance.Get<MainMenuView>()?.State == ViewState.Visible, "显式Hub入口仍可退出Demo");
        }
    }
}
#endif
