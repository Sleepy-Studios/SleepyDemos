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
            // 等待 Input System、UI 指针和固定步命令均实际处理，不用墙钟时间代替输入帧。
            yield return null;
            yield return null;
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

        [UnityTest, Timeout(240000)]
        public IEnumerator FourChapterGrandPrixReachesPodiumAndReturnsToCover()
        {
            yield return GameSceneNavigator.Instance.SwitchAsync(GameSceneId.KagitKart).ToCoroutine();
            KagitKartWorld world = null;
            yield return Wait(() => (world = Object.FindFirstObjectByType<KagitKartWorld>()) != null
                && !world.Busy && UIManager.Instance.Get<KagitKartTitleView>()?.State == ViewState.Visible, "封面就绪");
            yield return world.StartRaceAsync(KagitKartRaceMode.GrandPrix, "meadow", "tilki", 120).ToCoroutine();
            for (int round = 0; round < 4; round++)
            {
                yield return Wait(() => world.Session != null && !world.Busy && world.IntroPlaying, "第 " + (round + 1) + " 章开场");
                world.Dispatch(KagitKartCommand.SkipIntro);
                var session = world.Session;
                typeof(KagitKartVehicle).GetProperty(nameof(KagitKartVehicle.Autopilot)).SetValue(session.Player, true);
                // 使用正式固定步模拟与原作 AI 完赛，避免测试耗费四场真实等待时间。
                for (int step = 0; step < 120 * 480 && session.Race.State != KagitKartRaceState.Done; step++)
                {
                    session.AdvanceFrame(KagitKartSimulationClock.Step, default);
                    if (step % 240 == 0) yield return null;
                }
                Assert.That(session.Race.State, Is.EqualTo(KagitKartRaceState.Done), world.TrackId);
                Assert.That(world.PlayerEntry.Progress.LapTimes.Count, Is.EqualTo(3), world.TrackId + " 三圈");
                Assert.That(world.GrandPrix.SortedStandings().Sum(row => row.Points), Is.EqualTo(58 * (round + 1)));
                yield return Wait(() => UIManager.Instance.Get<KagitKartResultsView>()?.State == ViewState.Visible, "本场结算");
                var next = Button(UIManager.Instance.Get<KagitKartResultsView>(), "Next");
                Assert.That(next.gameObject.activeInHierarchy, Is.True, "最终场也必须提供颁奖入口。");
                EventSystem.current.SetSelectedGameObject(next.gameObject);
                yield return KeyPress(Key.Enter);
            }
            yield return Wait(() => world.PodiumPlaying && world.Session == null, "退出比赛并进入立体书领奖台");
            Assert.That(world.GrandPrix.Completed, Is.True);
            Assert.That(Object.FindObjectsByType<KagitKartSceneryWorld>(FindObjectsSortMode.None), Is.Empty);
            var book = Object.FindFirstObjectByType<KagitKartBookStage>();
            Assert.That(book.ChapterId, Is.EqualTo("desk"));
            Assert.That(book.ClosedFraction, Is.Zero);
            Assert.That(book.CurrentCharacterId, Is.Null, "菜单车手应让出领奖台。");
            yield return Wait(() => UIManager.Instance.Get<KagitKartPodiumView>()?.State == ViewState.Visible && !world.Busy, "中文领奖纸卡");
            yield return Wait(() => UIManager.Instance.Get<KagitKartPodiumView>().gameObject.GetComponentsInChildren<CanvasGroup>()
                .All(group => group.alpha > 0.99f), "前三名及返回纸卡全部显示");
            yield return Capture("GrandPrixPodium");
            var podium = Object.FindFirstObjectByType<KagitKartPodiumStage>();
            Assert.That(podium.IsComplete, Is.True);
            EventSystem.current.SetSelectedGameObject(Button(UIManager.Instance.Get<KagitKartPodiumView>(), "Menu").gameObject);
            yield return KeyPress(Key.Enter);
            yield return Wait(() => UIManager.Instance.Get<KagitKartTitleView>()?.State == ViewState.Visible && !world.Busy, "领奖后返回封面");
            Assert.That(world.PodiumPlaying, Is.False);
            Assert.That(podium.IsComplete, Is.False);
            Assert.That(UIManager.Instance.Get<KagitKartPodiumView>(), Is.Null);
        }

        [Serializable]
        private sealed class EditorTrackTiming
        {
            public string track, gpu;
            public int width, height, frames;
            public float averageMs, maximumMs;
            public long allocatedBytes, managedBytes;
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator NormalDrivingRecordsEditorTimingsForAllChapters()
        {
            yield return GameSceneNavigator.Instance.SwitchAsync(GameSceneId.KagitKart).ToCoroutine();
            KagitKartWorld world = null;
            yield return Wait(() => (world = Object.FindFirstObjectByType<KagitKartWorld>()) != null
                && !world.Busy && UIManager.Instance.Get<KagitKartTitleView>()?.State == ViewState.Visible, "性能场景启动");
            string folder = "D:/Unity/Unity_Project/SleepyDemos-ArtSource/ArtSource/kagit_kart/Previews";
            Directory.CreateDirectory(folder);
            foreach (string id in KagitKartCatalog.Tracks)
            {
                yield return world.StartRaceAsync(KagitKartRaceMode.SingleRace, id, "tilki", 120).ToCoroutine();
                world.Dispatch(KagitKartCommand.SkipIntro);
                yield return Wait(() => world.Session.Race.State == KagitKartRaceState.Running, "正常倒计时结束");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                yield return new WaitForSecondsRealtime(2);
                var timing = new EditorTrackTiming { track = id, gpu = SystemInfo.graphicsDeviceName, width = Screen.width, height = Screen.height };
                float end = Time.realtimeSinceStartup + 3;
                while (Time.realtimeSinceStartup < end)
                {
                    yield return null;
                    float ms = Time.unscaledDeltaTime * 1000;
                    timing.averageMs += ms;
                    timing.maximumMs = Mathf.Max(timing.maximumMs, ms);
                    timing.frames++;
                }
                Assert.That(timing.frames, Is.GreaterThan(0));
                Assert.That(world.Session.Pause.IsPaused, Is.False, "采样必须在实际行驶时进行。");
                Assert.That(world.Session.Player.Controls.Throttle, Is.GreaterThan(0.5f));
                timing.averageMs /= timing.frames;
                timing.allocatedBytes = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
                timing.managedBytes = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();
                File.WriteAllText(folder + "/EditorTiming_" + id + ".json", JsonUtility.ToJson(timing, true));
                ScreenCapture.CaptureScreenshot(folder + "/Race_" + id + ".png");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
            }
            world.Dispatch(KagitKartCommand.Hub);
            yield return Wait(() => Object.FindFirstObjectByType<KagitKartWorld>() == null, "四章采样后释放场景");
            Assert.That(Object.FindObjectsByType<KagitKartSceneryWorld>(FindObjectsSortMode.None), Is.Empty);
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator FirstTutorialSurvivesCountdownAndPauseThenPersistsAfterSevenRunningSeconds()
        {
            string key = LocalDataKeys.KagitKartPreferences;
            bool existed = PlayerPrefs.HasKey(key);
            string previous = PlayerPrefs.GetString(key);
            string hintKey = LocalDataKeys.KagitKartHintsPreferences;
            bool hadHints = PlayerPrefs.HasKey(hintKey);
            string previousHints = PlayerPrefs.GetString(hintKey);
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();

            try
            {
                yield return GameSceneNavigator.Instance.SwitchAsync(GameSceneId.KagitKart).ToCoroutine();
                KagitKartWorld world = null;
                yield return Wait(() => (world = Object.FindFirstObjectByType<KagitKartWorld>()) != null
                    && !world.Busy && UIManager.Instance.Get<KagitKartTitleView>()?.State == ViewState.Visible, "首次教程入口");
                yield return world.StartRaceAsync(KagitKartRaceMode.SingleRace, "meadow", "tilki", 120).ToCoroutine();
                world.SendMessage("OnApplicationFocus", true);
                world.Dispatch(KagitKartCommand.SkipIntro);
                Assert.That(world.Hints.TutorialVisible, Is.True, "跳过赛前电影仍打开倒计时3教程。");
                Assert.That(world.Settings.SeenTutorial, Is.False);
                var view = UIManager.Instance.Get<KagitKartHintsView>();
                Assert.That(view, Is.Not.Null);
                Assert.That(view.ViewMode, Is.EqualTo(UIViewMode.Widget));
                Assert.That(view.gameObject.GetComponentsInChildren<Button>(true), Is.Empty);
                Assert.That(view.gameObject.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
                Assert.That(view.gameObject.GetComponentsInChildren<TMPro.TextMeshProUGUI>()
                    .Any(text => text.text == "怎么驾驶？" && text.gameObject.activeInHierarchy), Is.True,
                    "教程状态为真时，实际纸卡与文字必须显示。");
                world.Dispatch(KagitKartCommand.Pause);
                yield return Wait(() => world.Session.Pause.IsPaused, "倒计时暂停");
                yield return new WaitForSecondsRealtime(0.5f);
                Assert.That(world.Hints.TutorialVisible, Is.True);
                Assert.That(world.Settings.SeenTutorial, Is.False);
                yield return Capture("TutorialKeyboard");
                world.Dispatch(KagitKartCommand.Resume);
                yield return Wait(() => world.Session.Race.State == KagitKartRaceState.Running, "起跑");
                Assert.That(world.Hints.TutorialVisible, Is.True, "倒计时三秒不占教程七秒。");
                yield return new WaitForSecondsRealtime(3);
                Assert.That(world.Hints.TutorialVisible, Is.True);
                yield return Capture("TutorialKeyboard");
                world.Dispatch(KagitKartCommand.Pause);
                yield return Wait(() => world.Session.Pause.IsPaused, "教程中暂停");
                yield return new WaitForSecondsRealtime(0.5f);
                Assert.That(world.Hints.TutorialVisible, Is.True);
                world.Dispatch(KagitKartCommand.Resume);
                yield return Wait(() => !world.Hints.TutorialVisible, "累计比赛七秒自动收起");
                Assert.That(world.Settings.SeenTutorial, Is.True);
                Assert.That(LocalDataManager.LoadData(key, new KagitKartSettings()).SeenTutorial, Is.True);
                yield return world.StartRaceAsync(KagitKartRaceMode.SingleRace, "meadow", "tilki", 120).ToCoroutine();
                world.Dispatch(KagitKartCommand.SkipIntro);
                Assert.That(world.Hints.TutorialVisible, Is.False, "已看教程下场不重复。");
            }
            finally
            {
                if (existed)
                {
                    PlayerPrefs.SetString(key, previous);
                }
                else
                {
                    PlayerPrefs.DeleteKey(key);
                }
                if (hadHints) PlayerPrefs.SetString(hintKey, previousHints);
                else PlayerPrefs.DeleteKey(hintKey);
                PlayerPrefs.Save();
            }
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
        [UnityTest, Timeout(240000)]
        public IEnumerator SettingsAndGraphicsRestorePauseAndItsActualButtons()
        {
            yield return GameSceneNavigator.Instance.SwitchAsync(GameSceneId.KagitKart).ToCoroutine();
            KagitKartWorld world = null;
            yield return Wait(() => (world = Object.FindFirstObjectByType<KagitKartWorld>()) != null
                && !world.Busy && UIManager.Instance.Get<KagitKartTitleView>()?.State == ViewState.Visible, "设置验收入口");
            world.Dispatch(KagitKartCommand.Modes);
            yield return Wait(() => !world.Busy && UIManager.Instance.Get<KagitKartModeView>()?.State == ViewState.Visible, "模式页");
            var mode = UIManager.Instance.Get<KagitKartModeView>();
            mode.gameObject.GetComponentsInChildren<Button>().Single(button => button.name == "Settings").onClick.Invoke();
            yield return Wait(() => !world.Busy && UIManager.Instance.Get<KagitKartSettingsView>()?.State == ViewState.Visible, "菜单设置");
            yield return Capture("Settings");
            yield return KeyPress(Key.Escape);
            yield return Wait(() => !world.Busy && UIManager.Instance.Get<KagitKartSettingsView>() == null, "返回模式");
            Assert.That(UIManager.Instance.Get<KagitKartModeView>(), Is.SameAs(mode));
            Assert.That(mode.State, Is.EqualTo(ViewState.Visible));
            world.Dispatch(KagitKartCommand.Controls);
            yield return Wait(() => !world.Busy && UIManager.Instance.Get<KagitKartControlsView>()?.State == ViewState.Visible, "菜单操作页");
            yield return Capture("Controls");
            touchscreen = InputSystem.AddDevice<Touchscreen>();
            yield return Tap(UIManager.Instance.Get<KagitKartControlsView>(), "Back");
            yield return Wait(() => !world.Busy && UIManager.Instance.Get<KagitKartControlsView>() == null, "返回模式");
            yield return world.StartRaceAsync(KagitKartRaceMode.SingleRace, "meadow", "tilki", 120).ToCoroutine();
            world.SendMessage("OnApplicationFocus", true);
            world.Dispatch(KagitKartCommand.Pause);
            yield return Wait(() => UIManager.Instance.Get<KagitKartPauseView>()?.State == ViewState.Visible, "暂停");
            var pause = UIManager.Instance.Get<KagitKartPauseView>();
            var session = world.Session;
            double clock = session.RaceTime;
            pause.gameObject.GetComponentsInChildren<Button>().Single(button => button.name == "Settings").onClick.Invoke();
            yield return Wait(() => !world.Busy && UIManager.Instance.Get<KagitKartSettingsView>()?.State == ViewState.Visible, "暂停设置");
            var settings = UIManager.Instance.Get<KagitKartSettingsView>();
            Assert.That(pause.State, Is.EqualTo(ViewState.LoadedHidden));
            settings.gameObject.GetComponentsInChildren<Button>().Single(button => button.name == "Graphics").onClick.Invoke();
            yield return Wait(() => !world.Busy && UIManager.Instance.Get<DlssSettingsView>()?.State == ViewState.Visible, "公共画面设置");
            Assert.That(settings.State, Is.EqualTo(ViewState.LoadedHidden));
            yield return UIManager.Instance.CloseAsync<DlssSettingsView>().ToCoroutine();
            Assert.That(UIManager.Instance.Get<KagitKartSettingsView>(), Is.SameAs(settings));
            Assert.That(settings.State, Is.EqualTo(ViewState.Visible));
            yield return KeyPress(Key.Escape);
            yield return Wait(() => !world.Busy && UIManager.Instance.Get<KagitKartSettingsView>() == null, "恢复暂停");
            Assert.That(UIManager.Instance.Get<KagitKartPauseView>(), Is.SameAs(pause));
            Assert.That(pause.State, Is.EqualTo(ViewState.Visible));
            Assert.That(world.Session, Is.SameAs(session));
            Assert.That(session.Pause.IsPaused, Is.True);
            Assert.That(session.RaceTime, Is.EqualTo(clock));
            pause.gameObject.GetComponentsInChildren<Button>().Single(button => button.name == "Controls").onClick.Invoke();
            yield return Wait(() => !world.Busy && UIManager.Instance.Get<KagitKartControlsView>()?.State == ViewState.Visible, "暂停操作说明");
            gamepad = InputSystem.AddDevice<Gamepad>();
            yield return PadPress(GamepadButton.East);
            yield return Wait(() => !world.Busy && UIManager.Instance.Get<KagitKartControlsView>() == null, "手柄返回暂停");
            Assert.That(session.Pause.IsPaused, Is.True);
            pause.gameObject.GetComponentsInChildren<Button>().Single(button => button.name == "Resume").onClick.Invoke();
            yield return Wait(() => !session.Pause.IsPaused && UIManager.Instance.Get<KagitKartPauseView>() == null, "真实按钮继续");
        }


    }
}
#endif
