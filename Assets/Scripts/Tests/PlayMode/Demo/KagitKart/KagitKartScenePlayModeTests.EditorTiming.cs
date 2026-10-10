#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Core.Runtime;
using Cysharp.Threading.Tasks;
using Hotfix;
using Hotfix.KagitKart;
using Hotfix.SceneManagement;
using NUnit.Framework;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tests.Demo
{
    public sealed partial class KagitKartScenePlayModeTests
    {
        // 保存可比较的长圈采样；保留原 JSON 字段含义。
        [Serializable]
        private sealed class EditorTrackTiming
        {
            public string track, gpu;
            public int width, height, frames;
            public float averageMs, maximumMs;
            public long allocatedBytes, managedBytes;
            public string utc, unity, cpu, os, graphicsApi, quality, renderPipeline, focusedWindow;
            public int cpuCores, cpuMhz, systemMemoryMb, gpuMemoryMb, vSync, targetFrameRate;
            public Rect gameViewWindow, cameraViewport;
            public bool gameViewFocused, gameViewVSync, applicationFocused, profilerEnabled, reducedMotion;
            public bool gcRecorderAvailable, mainThreadRecorderAvailable, renderThreadRecorderAvailable;
            public bool managedRecorderAvailable;
            public double managedProbeMs;
            public float timeScale, p50Ms, p95Ms, p99Ms;
            public int over16Ms, over33Ms, over50Ms, over100Ms;
            public double wallSeconds, raceSecondsStart, raceSecondsEnd, progressStart, progressEnd;
            public int lapsCompleted, naturalBoosts, naturalHits, naturalInks, naturalRescues;
            public string introMusicKey, countdownMusicKey, raceMusicKey, musicKeyAtEnd;
            public int naturalPopups, rustleStarts, audioSampleRate;
            public float maximumPopupSize, musicVolume, sfxVolume, listenerVolume;
            public bool muted, editorAudioMuted, listenerPaused;
            public double rustleBefore, rustleAfter;
            public long managedStartBytes, managedPeakBytes, managedDeltaBytes, gcAllocatedBytes, gcMaximumFrameBytes;
            public int gcGen0, gcGen1, gcGen2, gcMaxGeneration, gcSampleFrames;
            public EditorTimingFrame slowest;
            public EditorTimingFrame[] samples;
        }

        [Serializable]
        private struct EditorTimingFrame
        {
            public int frame, lap, checkpoint, rescueStage;
            public float ms, trackT, speed, inkSeconds, boostSeconds, spinSeconds;
            public double wallSeconds, raceSeconds, progress;
            public long managedBytes, gcBytes, mainThreadNs, renderThreadNs;
            public int popups, rustles;
            public double rustleTime;
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator NormalDrivingRecordsLongEditorTimingsForChaptersOneAndTwo()
        {
            yield return RecordNormalEditorRoads(KagitKartCatalog.Tracks.Take(2).ToArray());
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator NormalDrivingRecordsLongEditorTimingsForChaptersThreeAndFour()
        {
            yield return RecordNormalEditorRoads(KagitKartCatalog.Tracks.Skip(2).ToArray());
        }

        private IEnumerator RecordNormalEditorRoads(string[] tracks)
        {
            Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(UnityEngine.Rendering.GraphicsDeviceType.Null), "使用正常 Editor 画面窗口");
            Assert.That(Time.timeScale, Is.EqualTo(1), "采样不修改项目时间设置，进入测试前需为正常速度");
            var gameView = Resources.FindObjectsOfTypeAll<EditorWindow>().FirstOrDefault(window => window.GetType().Name == "GameView");
            Assert.That(gameView, Is.Not.Null, "先打开正常 Game View，再运行精确方法");
            yield return GameSceneNavigator.Instance.SwitchAsync(GameSceneId.KagitKart).ToCoroutine();
            KagitKartWorld world = null;
            yield return Wait(() => (world = Object.FindFirstObjectByType<KagitKartWorld>()) != null
                && !world.Busy && UIManager.Instance.Get<KagitKartTitleView>()?.State == ViewState.Visible, "正常道路性能入口");
            const string folder = "D:/Unity/Unity_Project/SleepyDemos-ArtSource/ArtSource/kagit_kart/Previews";
            Directory.CreateDirectory(folder);
            string run = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var syncProperty = gameView.GetType().GetProperty("vSyncEnabled");
            bool sync = (bool)syncProperty.GetValue(gameView);
            // 本机 DX12 的 Game View VSync 造成呈现等待；性能窗口解除显示限速，退出恢复原窗口设置。
            try
            {
                syncProperty.SetValue(gameView, false);
                foreach (string id in tracks)
                {
                    yield return world.StartRaceAsync(KagitKartRaceMode.SingleRace, id, "tilki", 120).ToCoroutine();
                    world.SendMessage("OnApplicationFocus", true);
                    world.Dispatch(KagitKartCommand.Resume);
                    var session = world.Session;
                    var audio = world.GetComponent<KagitKartAudio>();
                    string introKey = NativeField<string>(audio, "musicKey");
                    Assert.That(introKey, Is.EqualTo("intro_" + id), "自然电影播放本章开场曲");
                    // 沿已有 GP 场景测试的正式 AI 开关；只让宿主真实 Update 驱动，不主动调用 AdvanceFrame。
                    typeof(KagitKartVehicle).GetProperty(nameof(KagitKartVehicle.Autopilot)).SetValue(session.Player, true);
                    yield return Wait(() => !world.IntroPlaying && session.Race.State == KagitKartRaceState.Countdown, "自然电影结束进入倒计时");
                    string countdownKey = NativeField<string>(audio, "musicKey");
                    Assert.That(countdownKey, Is.Null, "倒计时退开场曲，仅保留淡出尾音");
                    Assert.That(NativeRouteTime(audio, "count"), Is.GreaterThan(0), "自然 BeginCountdown 已播放第一声");
                    yield return Wait(() => session.Race.State == KagitKartRaceState.Running, "正常倒计时起跑");
                    string raceKey = NativeField<string>(audio, "musicKey");
                    Assert.That(raceKey, Is.EqualTo(id), "自然起跑切换本章比赛曲");
                    yield return RecordNormalEditorWindow(world, gameView, folder, run, new[] { introKey, countdownKey, raceKey });
                    Assert.That(session.Pause.IsPaused, Is.False);
                    Assert.That(session.Player.Autopilot, Is.True);
                    Assert.That(world.PlayerEntry.Progress.LapTimes.Count, Is.GreaterThanOrEqualTo(1), "至少实际完成一圈，不仅起点短采样");
                }
                world.Dispatch(KagitKartCommand.Hub);
                yield return Wait(() => Object.FindFirstObjectByType<KagitKartWorld>() == null, "两章正常道路采样后释放场景");
                Assert.That(Object.FindObjectsByType<KagitKartSceneryWorld>(FindObjectsSortMode.None), Is.Empty);
            }
            finally
            {
                syncProperty.SetValue(gameView, sync);
            }
        }

        private IEnumerator RecordNormalEditorWindow(KagitKartWorld world, EditorWindow gameView, string folder, string run, string[] musicKeys)
        {
            var session = world.Session;
            var camera = world.GetComponentsInChildren<Camera>(true).First(value => value.enabled);
            var audio = world.GetComponent<KagitKartAudio>();
            var scenery = NativeField<KagitKartSceneryWorld>(world, "scenery");
            var played = NativeField<Dictionary<string, double>>(audio, "lastPlayed");
            var timing = new EditorTrackTiming
            {
                track = world.TrackId, gpu = SystemInfo.graphicsDeviceName, width = Screen.width, height = Screen.height,
                utc = DateTime.UtcNow.ToString("O"), unity = Application.unityVersion,
                cpu = SystemInfo.processorType, cpuCores = SystemInfo.processorCount, cpuMhz = SystemInfo.processorFrequency,
                systemMemoryMb = SystemInfo.systemMemorySize, gpuMemoryMb = SystemInfo.graphicsMemorySize,
                os = SystemInfo.operatingSystem, graphicsApi = SystemInfo.graphicsDeviceType.ToString(),
                quality = QualitySettings.names[QualitySettings.GetQualityLevel()],
                renderPipeline = (QualitySettings.renderPipeline ?? UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline)?.name,
                gameViewWindow = gameView.position, cameraViewport = camera.pixelRect,
                gameViewFocused = EditorWindow.focusedWindow == gameView,
                gameViewVSync = (bool)gameView.GetType().GetProperty("vSyncEnabled").GetValue(gameView),
                focusedWindow = EditorWindow.focusedWindow != null ? EditorWindow.focusedWindow.GetType().Name : "",
                applicationFocused = Application.isFocused, profilerEnabled = UnityEngine.Profiling.Profiler.enabled,
                reducedMotion = world.ReducedMotion, vSync = QualitySettings.vSyncCount, targetFrameRate = Application.targetFrameRate,
                timeScale = Time.timeScale, gcAllocatedBytes = -1, gcMaximumFrameBytes = -1,
                introMusicKey = musicKeys[0], countdownMusicKey = musicKeys[1], raceMusicKey = musicKeys[2],
                musicVolume = world.Settings.MusicVolume, sfxVolume = world.Settings.SfxVolume, muted = world.Settings.Muted,
                editorAudioMuted = EditorUtility.audioMasterMute, listenerVolume = AudioListener.volume,
                listenerPaused = AudioListener.pause, audioSampleRate = AudioSettings.outputSampleRate,
            };
            // 预分配与记录器初始化在窗口外；窗口内没有 List 扩容、JSON、截图或逐帧日志。
            var samples = new EditorTimingFrame[65536];
            var gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1);
            var managed = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Used Memory", 1);
            var main = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1);
            var render = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Render Thread", 1);
            Action<KagitKartVehicle, KagitKartVehicleSignal> notice = (kart, signal) =>
            {
                switch (signal)
                {
                    case KagitKartVehicleSignal.Boost:
                        timing.naturalBoosts++;
                        break;
                    case KagitKartVehicleSignal.Hit:
                        timing.naturalHits++;
                        break;
                    case KagitKartVehicleSignal.Inked:
                        timing.naturalInks++;
                        break;
                    case KagitKartVehicleSignal.RescueLift:
                        timing.naturalRescues++;
                        break;
                }
            };
            Action<Vector3, float> popup = (position, size) =>
            {
                timing.naturalPopups++;
                timing.maximumPopupSize = Mathf.Max(timing.maximumPopupSize, size);
            };
            foreach (var kart in session.Karts)
            {
                kart.Signaled += notice;
            }
            scenery.Popup += popup;
            try
            {
                yield return null;
                timing.naturalBoosts = timing.naturalHits = timing.naturalInks = timing.naturalRescues = 0;
                timing.naturalPopups = 0;
                timing.maximumPopupSize = 0;
                double lastRustle = timing.rustleBefore = NativeRouteTime(audio, "rustle:world");
                timing.gcRecorderAvailable = gc.Valid;
                timing.mainThreadRecorderAvailable = main.Valid;
                timing.renderThreadRecorderAvailable = render.Valid;
                timing.managedRecorderAvailable = managed.Valid;
                timing.gcMaxGeneration = GC.MaxGeneration;
                int gen0 = GC.CollectionCount(0);
                int gen1 = timing.gcMaxGeneration >= 1 ? GC.CollectionCount(1) : -1;
                int gen2 = timing.gcMaxGeneration >= 2 ? GC.CollectionCount(2) : -1;
                // 直接堆查询只在窗口外测一次开销，逐帧读取原生计数器。
                var probe = System.Diagnostics.Stopwatch.StartNew();
                timing.managedStartBytes = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();
                probe.Stop();
                timing.managedProbeMs = probe.Elapsed.TotalMilliseconds;
                yield return null;
                timing.managedPeakBytes = timing.managedStartBytes;
                timing.raceSecondsStart = session.RaceTime;
                timing.progressStart = world.PlayerEntry.Progress.Progress;
                double start = Time.realtimeSinceStartupAsDouble;
                if (gc.Valid)
                {
                    timing.gcAllocatedBytes = timing.gcMaximumFrameBytes = 0;
                }
                while (Time.realtimeSinceStartupAsDouble - start < 180
                    && (Time.realtimeSinceStartupAsDouble - start < 45 || world.PlayerEntry.Progress.LapTimes.Count < 1)
                    && session.Race.State == KagitKartRaceState.Running)
                {
                    yield return null;
                    if (timing.frames == samples.Length)
                    {
                        break;
                    }
                    var player = session.Player;
                    if (played.TryGetValue("rustle:world", out double rustle) && rustle > lastRustle)
                    {
                        timing.rustleStarts++;
                        lastRustle = rustle;
                    }
                    var frame = new EditorTimingFrame
                    {
                        frame = Time.frameCount - 1, ms = Time.unscaledDeltaTime * 1000,
                        wallSeconds = Time.realtimeSinceStartupAsDouble - start, raceSeconds = session.RaceTime,
                        progress = world.PlayerEntry.Progress.Progress, lap = world.PlayerEntry.Progress.Lap,
                        checkpoint = world.PlayerEntry.Progress.PassedCheckpoints, trackT = player.TrackInfo.T, speed = player.Speed,
                        rescueStage = player.RescueStage, inkSeconds = player.InkTime, boostSeconds = player.BoostTime, spinSeconds = player.SpinTime,
                        managedBytes = managed.Valid && managed.Count > 0 ? managed.LastValue : -1,
                        gcBytes = gc.Valid && gc.Count > 0 ? gc.LastValue : -1,
                        mainThreadNs = main.Valid && main.Count > 0 ? main.LastValue : -1,
                        renderThreadNs = render.Valid && render.Count > 0 ? render.LastValue : -1,
                        popups = timing.naturalPopups, rustles = timing.rustleStarts, rustleTime = lastRustle,
                    };
                    samples[timing.frames++] = frame;
                    timing.averageMs += frame.ms;
                    if (frame.ms >= timing.maximumMs) { timing.maximumMs = frame.ms; timing.slowest = frame; }
                    if (frame.ms > 1000f / 60) timing.over16Ms++;
                    if (frame.ms > 1000f / 30) timing.over33Ms++;
                    if (frame.ms > 50) timing.over50Ms++;
                    if (frame.ms > 100) timing.over100Ms++;
                    timing.managedPeakBytes = Math.Max(timing.managedPeakBytes, frame.managedBytes);
                    if (frame.gcBytes >= 0)
                    {
                        timing.gcSampleFrames++;
                        timing.gcAllocatedBytes += frame.gcBytes;
                        timing.gcMaximumFrameBytes = Math.Max(timing.gcMaximumFrameBytes, frame.gcBytes);
                    }
                }
                timing.wallSeconds = Time.realtimeSinceStartupAsDouble - start;
                timing.raceSecondsEnd = session.RaceTime;
                timing.progressEnd = world.PlayerEntry.Progress.Progress;
                timing.lapsCompleted = world.PlayerEntry.Progress.LapTimes.Count;
                timing.allocatedBytes = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
                timing.managedBytes = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();
                timing.managedDeltaBytes = timing.managedBytes - timing.managedStartBytes;
                timing.gcGen0 = GC.CollectionCount(0) - gen0;
                timing.gcGen1 = gen1 >= 0 ? GC.CollectionCount(1) - gen1 : -1;
                timing.gcGen2 = gen2 >= 0 ? GC.CollectionCount(2) - gen2 : -1;
                if (timing.gcSampleFrames == 0) timing.gcAllocatedBytes = timing.gcMaximumFrameBytes = -1;
                timing.rustleAfter = NativeRouteTime(audio, "rustle:world");
                timing.musicKeyAtEnd = NativeField<string>(audio, "musicKey");
            }
            finally
            {
                foreach (var kart in session.Karts)
                {
                    kart.Signaled -= notice;
                }
                scenery.Popup -= popup;
                gc.Dispose();
                managed.Dispose();
                main.Dispose();
                render.Dispose();
            }
            Array.Resize(ref samples, timing.frames);
            timing.samples = samples;
            if (timing.frames > 0)
            {
                timing.averageMs /= timing.frames;
                var distribution = samples.Select(frame => frame.ms).OrderBy(value => value).ToArray();
                timing.p50Ms = distribution[Math.Max(0, (int)Math.Ceiling(timing.frames * 0.50) - 1)];
                timing.p95Ms = distribution[Math.Max(0, (int)Math.Ceiling(timing.frames * 0.95) - 1)];
                timing.p99Ms = distribution[Math.Max(0, (int)Math.Ceiling(timing.frames * 0.99) - 1)];
            }
            // 先保存失败窗口证据，再断言覆盖。截图与JSON不进入本采样窗口。
            string path = folder + "/EditorTiming_" + world.TrackId + "_LongRoad_" + run;
            File.WriteAllText(path + ".json", JsonUtility.ToJson(timing, true));
            ScreenCapture.CaptureScreenshot(path + ".png");
            yield return null;
            Assert.That(timing.frames, Is.InRange(1, 65535), "有真实帧，且预分配容量未被截断");
            Assert.That(timing.wallSeconds, Is.GreaterThanOrEqualTo(45), "正常道路采样至少45秒");
            Assert.That(timing.lapsCompleted, Is.GreaterThanOrEqualTo(1), "正式AI实际通过完整圈线与检查点");
            Assert.That(timing.progressEnd - timing.progressStart, Is.GreaterThanOrEqualTo(session.Track.Track.data.length - 120), "不是停在起点仅消耗墙钟");
            Assert.That(Time.timeScale, Is.EqualTo(timing.timeScale), "采样不改变时间设置");
            Assert.That(Screen.width, Is.EqualTo(timing.width), "采样期间保持同一画面分辨率");
            Assert.That(Screen.height, Is.EqualTo(timing.height));
        }
    }
}
#endif
