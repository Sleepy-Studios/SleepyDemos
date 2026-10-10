#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Core.Runtime;
using Cysharp.Threading.Tasks;
using Hotfix;
using Hotfix.KagitKart;
using Hotfix.SceneManagement;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tests.Demo
{
    public sealed class KagitKartScenePlayModeTests
    {
        private InputSettings original;
        private InputSettings temporary;
        private Keyboard keyboard;
        private Gamepad gamepad;
        private Touchscreen touchscreen;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            original = InputSystem.settings;
            temporary = Object.Instantiate(original);
            temporary.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            temporary.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = temporary;
            keyboard = InputSystem.AddDevice<Keyboard>();
            if (GameSceneNavigator.Instance == null)
            {
                yield return SceneManager.LoadSceneAsync("Assets/Scenes/AppEntrance.unity", LoadSceneMode.Single);
            }
            yield return Wait(() => GameSceneNavigator.Instance != null && !GameSceneNavigator.Instance.IsTransitioning
                && UIManager.Instance.Get<MainMenuView>()?.State == ViewState.Visible, "大厅启动");
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator HubRaceKeyboardPauseRestartAndReturn()
        {
            yield return GameSceneNavigator.Instance.SwitchAsync(GameSceneId.KagitKart).ToCoroutine();
            KagitKartWorld world = null;
            yield return Wait(() => (world = Object.FindFirstObjectByType<KagitKartWorld>()) != null
                && !world.Busy && UIManager.Instance.Get<KagitKartTitleView>()?.State == ViewState.Visible, "立体书封面");
            var book = Object.FindFirstObjectByType<KagitKartBookStage>();
            Assert.That(book.ClosedFraction, Is.EqualTo(1));
            yield return Capture("MenuCover");
            yield return KeyPress(Key.Enter);
            yield return Wait(() => UIManager.Instance.Get<KagitKartModeView>()?.State == ViewState.Visible && !world.Busy, "模式选择");
            Assert.That(world.Session, Is.Null, "进入 Demo 应先选赛。");
            yield return Capture("MenuMode");
            Assert.That(book.ClosedFraction, Is.Zero, "开书应实际转动封面网格。");
            yield return KeyPress(Key.RightArrow);
            yield return KeyPress(Key.Enter);
            yield return Wait(() => UIManager.Instance.Get<KagitKartClassView>()?.State == ViewState.Visible && !world.Busy, "单场速度选择");
            Assert.That(world.Mode, Is.EqualTo(KagitKartRaceMode.SingleRace));
            yield return Capture("MenuClass");
            yield return KeyPress(Key.Enter);
            yield return Wait(() => UIManager.Instance.Get<KagitKartCharacterView>()?.State == ViewState.Visible && !world.Busy, "车手选择");
            yield return Capture("MenuCharacter");
            Assert.That(book.CurrentCharacterId, Is.EqualTo("tilki"));
            yield return KeyPress(Key.Enter);
            yield return Wait(() => UIManager.Instance.Get<KagitKartTrackView>()?.State == ViewState.Visible && !world.Busy, "章节选择");
            Assert.That(world.CharacterId, Is.EqualTo("tilki"), "静止鼠标不应抢走键盘车手焦点。");
            yield return Capture("MenuTrack");
            yield return KeyPress(Key.Enter);
            yield return Wait(() => (world = Object.FindFirstObjectByType<KagitKartWorld>()) != null
                && world.Session != null && !world.Busy && UIManager.Instance.Get<KagitKartHudView>()?.State == ViewState.Visible,
                "八车比赛与 HUD");
            Assert.That(world.Session.Karts.Count, Is.EqualTo(8));
            Assert.That(GlobalData.Get<KagitKartData>().Session, Is.SameAs(world.Session));
            Assert.That(Array.FindAll(Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None), a => a.enabled).Length, Is.EqualTo(1));
            world.SendMessage("OnApplicationFocus", true);
            world.Dispatch(KagitKartCommand.Resume);
            Assert.That(world.IntroPlaying, Is.True);
            world.Dispatch(KagitKartCommand.Pause);
            yield return Wait(() => world.Session.Pause.IsPaused, "赛前暂停");
            Vector3 introCamera = Camera.main.transform.position;
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.That(Vector3.Distance(Camera.main.transform.position, introCamera), Is.LessThan(0.001f));
            Assert.That(world.Session.RaceTime, Is.Zero, "赛前演出不推进比赛时钟。");
            world.Dispatch(KagitKartCommand.Resume);
            yield return KeyPress(Key.Enter);
            Assert.That(world.IntroPlaying, Is.False, "键盘跳过演出进入倒计时。");
            yield return Wait(() => world.Session.Race.State == KagitKartRaceState.Running, "正常倒计时起跑");
            Vector3 before = world.Session.Player.Position;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            yield return new WaitForSecondsRealtime(1.8f);
            Assert.That(world.Session.Player.Speed, Is.GreaterThan(1));
            Assert.That(Vector3.Distance(before, world.Session.Player.Position), Is.GreaterThan(1));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.D, Key.Space));
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.That(world.Session.Player.Controls.Throttle, Is.EqualTo(1));
            Assert.That(world.Session.Player.Controls.Steer, Is.GreaterThan(0));
            Assert.That(world.Session.Player.Controls.Drift, Is.True, "漂移键不应触发 HUD 暂停按钮的 Submit。");
            Assert.That(world.Session.Pause.IsPaused, Is.False);
            var map = Object.FindFirstObjectByType<KagitKartMinimap>();
            var mesh = map.canvasRenderer.GetMesh();
            Assert.That(mesh, Is.Not.Null, "小地图应提交实际 UI 网格。");
            Assert.That(mesh.vertexCount, Is.GreaterThan(0), "小地图应绘制实际路线与车辆。");
            string folder = "D:/Unity/Unity_Project/SleepyDemos-ArtSource/ArtSource/kagit_kart/Previews";
            Directory.CreateDirectory(folder);
            ScreenCapture.CaptureScreenshot(folder + "/RaceHud.png");
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return Wait(() => UIManager.Instance.Get<KagitKartPauseView>()?.State == ViewState.Visible, "暂停页面");
            double pausedTime = world.Session.RaceTime;
            Vector3 pausedPosition = world.Session.Player.Position;
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.That(world.Session.RaceTime, Is.EqualTo(pausedTime));
            Assert.That(world.Session.Player.Position, Is.EqualTo(pausedPosition));
            ScreenCapture.CaptureScreenshot(folder + "/RacePause.png");
            yield return null;
            world.Dispatch(KagitKartCommand.Resume);
            yield return Wait(() => !world.Session.Pause.IsPaused && UIManager.Instance.Get<KagitKartPauseView>() == null, "继续比赛");
            var old = world.Session;
            world.Dispatch(KagitKartCommand.Restart);
            yield return Wait(() => world.Session != null && world.Session != old && !world.Busy, "本场重赛");
            Assert.That(world.Session.Race.State, Is.EqualTo(KagitKartRaceState.Countdown));
            Assert.That(world.Session.RaceTime, Is.Zero);
            world.Dispatch(KagitKartCommand.Hub);
            yield return Wait(() => GlobalData.Get<KagitKartData>() == null && Object.FindFirstObjectByType<KagitKartWorld>() == null
                && UIManager.Instance.Get<MainMenuView>()?.State == ViewState.Visible, "返回大厅并释放本场");
            Assert.That(UIManager.Instance.Get<KagitKartHudView>(), Is.Null);
            Assert.That(UIManager.Instance.Get<KagitKartPauseView>(), Is.Null);
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator GamepadTimeTrialAndTouchGrandPrixUseSavedMenus()
        {
            gamepad = InputSystem.AddDevice<Gamepad>();
            touchscreen = InputSystem.AddDevice<Touchscreen>();
            yield return GameSceneNavigator.Instance.SwitchAsync(GameSceneId.KagitKart).ToCoroutine();
            KagitKartWorld world = null;
            yield return Wait(() => (world = Object.FindFirstObjectByType<KagitKartWorld>()) != null
                && !world.Busy && UIManager.Instance.Get<KagitKartTitleView>()?.State == ViewState.Visible, "手柄开书入口");
            yield return PadPress(GamepadButton.South);
            yield return Wait(() => UIManager.Instance.Get<KagitKartModeView>()?.State == ViewState.Visible && !world.Busy, "手柄模式选择");
            yield return PadPress(GamepadButton.DpadRight);
            yield return PadPress(GamepadButton.DpadRight);
            yield return PadPress(GamepadButton.South);
            yield return Wait(() => UIManager.Instance.Get<KagitKartClassView>()?.State == ViewState.Visible && !world.Busy, "计时速度选择");
            Assert.That(world.Mode, Is.EqualTo(KagitKartRaceMode.TimeTrial));
            yield return PadPress(GamepadButton.DpadLeft);
            yield return PadPress(GamepadButton.South);
            yield return Wait(() => UIManager.Instance.Get<KagitKartCharacterView>()?.State == ViewState.Visible && !world.Busy, "手柄车手选择");
            Assert.That(world.SpeedClass, Is.EqualTo(80));
            for (int i = 0; i < 3; i++)
            {
                yield return PadPress(GamepadButton.DpadRight);
            }
            Assert.That(world.CharacterId, Is.EqualTo("ayi"));
            Assert.That(world.Session, Is.Null, "焦点更新车手模型，但不自动开赛。");
            yield return PadPress(GamepadButton.South);
            yield return Wait(() => UIManager.Instance.Get<KagitKartTrackView>()?.State == ViewState.Visible && !world.Busy, "手柄章节选择");
            Assert.That(world.CharacterId, Is.EqualTo("ayi"));
            for (int i = 0; i < 3; i++)
            {
                yield return PadPress(GamepadButton.DpadDown);
            }
            Assert.That(world.TrackId, Is.EqualTo("desk"));
            Assert.That(world.Session, Is.Null, "章节焦点应翻页预览，不自动开赛。");
            yield return PadPress(GamepadButton.East);
            yield return Wait(() => UIManager.Instance.Get<KagitKartCharacterView>()?.State == ViewState.Visible && !world.Busy, "手柄返回车手页");
            yield return PadPress(GamepadButton.South);
            yield return Wait(() => UIManager.Instance.Get<KagitKartTrackView>()?.State == ViewState.Visible && !world.Busy, "重新进入章节页");
            yield return PadPress(GamepadButton.South);
            yield return Wait(() => world.Session != null && !world.Busy && UIManager.Instance.Get<KagitKartHudView>()?.State == ViewState.Visible, "书桌计时赛");
            Assert.That(world.TrackId, Is.EqualTo("desk"));
            Assert.That(world.Session.Karts.Count, Is.EqualTo(1));
            world.SendMessage("OnApplicationFocus", true);
            world.Dispatch(KagitKartCommand.Resume);
            yield return PadPress(GamepadButton.South);
            Assert.That(world.IntroPlaying, Is.False, "手柄跳过赛前演出。");
            yield return Wait(() => world.Session.Race.State == KagitKartRaceState.Running, "手柄计时起跑");
            InputSystem.QueueStateEvent(gamepad, new GamepadState { rightTrigger = 1 });
            yield return new WaitForSecondsRealtime(1.5f);
            Assert.That(world.Session.Player.Speed, Is.GreaterThan(1));
            InputSystem.QueueStateEvent(gamepad, new GamepadState());
            yield return null;
            world.Dispatch(KagitKartCommand.Modes);
            yield return Wait(() => UIManager.Instance.Get<KagitKartModeView>()?.State == ViewState.Visible && !world.Busy, "重新选赛");
            Assert.That(world.Session, Is.Null);
            yield return Tap(UIManager.Instance.Get<KagitKartModeView>(), "GrandPrix");
            yield return Wait(() => UIManager.Instance.Get<KagitKartClassView>()?.State == ViewState.Visible && !world.Busy, "触屏速度选择");
            yield return Tap(UIManager.Instance.Get<KagitKartClassView>(), "Class200");
            yield return Wait(() => UIManager.Instance.Get<KagitKartCharacterView>()?.State == ViewState.Visible && !world.Busy, "触屏车手选择");
            yield return Tap(UIManager.Instance.Get<KagitKartCharacterView>(), "Character7");
            yield return Wait(() => world.Session != null && !world.Busy && UIManager.Instance.Get<KagitKartHudView>()?.State == ViewState.Visible, "触屏大奖赛");
            Assert.That(world.Mode, Is.EqualTo(KagitKartRaceMode.GrandPrix));
            Assert.That(world.SpeedClass, Is.EqualTo(200));
            Assert.That(world.CharacterId, Is.EqualTo("ahtapot"));
            Assert.That(world.TrackId, Is.EqualTo("meadow"));
            Assert.That(world.Session.Karts.Count, Is.EqualTo(8));
            Assert.That(UIManager.Instance.Get<KagitKartTrackView>(), Is.Null, "大奖赛从首章开始，不弹出章节选择。");
            world.SendMessage("OnApplicationFocus", true);
            world.Dispatch(KagitKartCommand.Resume);
            yield return Wait(() => world.Session.Race.State == KagitKartRaceState.Running, "触屏大奖赛起跑");
            var hud = UIManager.Instance.Get<KagitKartHudView>();
            Vector2 steer = ScreenPoint(hud.gameObject.GetComponentsInChildren<Core.Runtime.Inputs.TouchInputPad>().Single().transform);
            Vector2 gas = ScreenPoint(Button(hud, "Throttle").transform);
            Vector2 drift = ScreenPoint(Button(hud, "Drift").transform);
            Touch(1, UnityEngine.InputSystem.TouchPhase.Began, steer);
            Touch(2, UnityEngine.InputSystem.TouchPhase.Began, gas);
            Touch(3, UnityEngine.InputSystem.TouchPhase.Began, drift);
            yield return new WaitForSecondsRealtime(0.1f);
            Touch(1, UnityEngine.InputSystem.TouchPhase.Moved, steer + Vector2.right * (Screen.width * 0.04f));
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.That(world.Session.Player.Controls.Throttle, Is.EqualTo(1));
            Assert.That(world.Session.Player.Controls.Steer, Is.GreaterThan(0));
            Assert.That(world.Session.Player.Controls.Drift, Is.True);
            yield return Capture("RaceTouch");
            Touch(1, UnityEngine.InputSystem.TouchPhase.Ended, steer);
            Touch(2, UnityEngine.InputSystem.TouchPhase.Ended, gas);
            Touch(3, UnityEngine.InputSystem.TouchPhase.Ended, drift);
            yield return new WaitForSecondsRealtime(0.2f);
            yield return Tap(hud, "Pause");
            yield return Wait(() => UIManager.Instance.Get<KagitKartPauseView>()?.State == ViewState.Visible, "触屏暂停");
            Assert.That(world.Session.Pause.CanResume, Is.True, "触屏暂停阻塞：" + world.Session.Pause.BlockingReasons);
            Assert.That(Button(UIManager.Instance.Get<KagitKartPauseView>(), "Resume").interactable, Is.True, "继续按钮应可操作。");
            yield return Tap(UIManager.Instance.Get<KagitKartPauseView>(), "Resume");
            yield return Wait(() => !world.Session.Pause.IsPaused && UIManager.Instance.Get<KagitKartPauseView>() == null, "触屏继续");
        }

        private IEnumerator KeyPress(Key key)
        {
            yield return new WaitForSecondsRealtime(0.1f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            yield return new WaitForSecondsRealtime(0.1f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return new WaitForSecondsRealtime(0.1f);
        }

        private IEnumerator PadPress(GamepadButton key)
        {
            yield return new WaitForSecondsRealtime(0.1f);
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(key));
            yield return new WaitForSecondsRealtime(0.1f);
            InputSystem.QueueStateEvent(gamepad, new GamepadState());
            yield return new WaitForSecondsRealtime(0.1f);
        }

        private IEnumerator Tap(View view, string name)
        {
            yield return new WaitForSecondsRealtime(1.8f);
            Vector2 position = ScreenPoint(Button(view, name).transform);
            Touch(8, UnityEngine.InputSystem.TouchPhase.Began, position);
            yield return new WaitForSecondsRealtime(0.1f);
            Touch(8, UnityEngine.InputSystem.TouchPhase.Ended, position);
            yield return new WaitForSecondsRealtime(0.2f);
        }

        private void Touch(int id, UnityEngine.InputSystem.TouchPhase phase, Vector2 position) =>
            InputSystem.QueueStateEvent(touchscreen, new TouchState { touchId = id, phase = phase, position = position });

        private static Button Button(View view, string name) => view.gameObject.GetComponentsInChildren<Button>().Single(value => value.name == name);

        private static Vector2 ScreenPoint(Transform target)
        {
            var canvas = target.GetComponentInParent<Canvas>();
            return RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, target.position);
        }

        private static IEnumerator Capture(string name)
        {
            yield return new WaitForSecondsRealtime(2);
            string folder = "D:/Unity/Unity_Project/SleepyDemos-ArtSource/ArtSource/kagit_kart/Previews";
            Directory.CreateDirectory(folder);
            ScreenCapture.CaptureScreenshot(folder + "/" + name + ".png");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            try
            {
                if (gamepad != null && gamepad.added)
                {
                    InputSystem.RemoveDevice(gamepad);
                }
                if (touchscreen != null && touchscreen.added)
                {
                    InputSystem.RemoveDevice(touchscreen);
                }
                if (keyboard != null && keyboard.added)
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    yield return null;
                    InputSystem.RemoveDevice(keyboard);
                }
                var world = Object.FindFirstObjectByType<KagitKartWorld>();
                if (world != null && !world.Busy)
                {
                    world.Dispatch(KagitKartCommand.Hub);
                    yield return Wait(() => Object.FindFirstObjectByType<KagitKartWorld>() == null, "收口测试场景");
                }
            }
            finally
            {
                if (original != null)
                {
                    InputSystem.settings = original;
                }
                if (temporary != null)
                {
                    Object.Destroy(temporary);
                }
            }
        }

        private static IEnumerator Wait(Func<bool> condition, string step)
        {
            float timeout = Time.realtimeSinceStartup + 50;
            while (!condition() && Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }
            Assert.That(condition(), Is.True, step);
        }
    }
}
#endif
