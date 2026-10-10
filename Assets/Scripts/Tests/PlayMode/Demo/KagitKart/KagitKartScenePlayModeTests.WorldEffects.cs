#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Core.Runtime;
using Cysharp.Threading.Tasks;
using Hotfix;
using Hotfix.KagitKart;
using Hotfix.SceneManagement;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tests.Demo
{
    // 合入现有场景测试类，复用 SetUp/TearDown、Wait 和 Capture；没有人工派发事件。
    public sealed partial class KagitKartScenePlayModeTests
    {
        [UnityTest, Timeout(180000)]
        public IEnumerator NativeAudioMixerHasRealOutput()
        {
            KagitKartWorld world = null;
            yield return EnterNativeEffectsWorld(value => world = value, KagitKartRaceMode.TimeTrial);
            var audio = world.GetComponent<KagitKartAudio>();
            var dsp = world.GetComponent<KagitKartAudioDsp>();
            var output = dsp.GetComponent<AudioSource>();
            var samples = new float[1024];
            float peak = 0;
            double start = AudioSettings.dspTime;
            try
            {
                audio.SetVolumes(1, 1, false);
                double beforeClick = NativeRouteTime(audio, "click");
                yield return KeyPress(Key.P);
                yield return Wait(() => world.Session.Pause.IsPaused && !world.Busy
                    && UIManager.Instance.Get<KagitKartPauseView>()?.State == ViewState.Visible, "真实P暂停");
                Assert.That(NativeRouteTime(audio, "click"), Is.EqualTo(beforeClick));
                Assert.That(NativeRouteTime(audio, "pause"), Is.GreaterThan(0));
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(Button(UIManager.Instance.Get<KagitKartPauseView>(), "Resume").gameObject);
                yield return KeyPress(Key.Enter);
                yield return Wait(() => !world.Session.Pause.IsPaused && !world.Busy, "实际继续按钮");
                Assert.That(NativeRouteTime(audio, "click"), Is.GreaterThan(beforeClick));
                Assert.That(NativeRouteTime(audio, "unpause"), Is.GreaterThan(0));
                for (int i = 0; i < 40; i++)
                {
                    AudioListener.GetOutputData(samples, 0);
                    peak = Mathf.Max(peak, samples.Max(value => Mathf.Abs(value)));
                    yield return new WaitForSecondsRealtime(0.05f);
                }
                var voices = NativeField<Array>(audio, "music");
                string cursors = string.Join(",", voices.Cast<object>().Select(voice => NativeField<double>(voice, "cursor") + "/" + NativeField<float>(voice, "Volume")));
                string environment = $"peak={peak}, editorMute={UnityEditor.EditorUtility.audioMasterMute}, listener={AudioListener.volume}/{AudioListener.pause}, "
                    + $"source={output.volume}/{output.mute}/{output.isVirtual}, dsp={start}->{AudioSettings.dspTime}, cursors={cursors}";
                Debug.Log("KagitKart 实际音频链路 " + environment);
                Assert.That(peak, Is.GreaterThan(0.000001f), environment);
            }
            finally
            {
                audio.SetVolumes(world.Settings.MusicVolume, world.Settings.SfxVolume, world.Settings.Muted);
            }
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator NativeFallSummonsPaperCraneAndReturnsToRoad()
        {
            KagitKartWorld world = null;
            yield return EnterNativeEffectsWorld(value => world = value, KagitKartRaceMode.TimeTrial, "glacier");
            var session = world.Session;
            var player = session.Player;
            var audio = world.GetComponent<KagitKartAudio>();
            var crane = Object.FindFirstObjectByType<KagitKartRescueCrane>();
            var effects = Object.FindFirstObjectByType<KagitKartRaceFx>();
            var items = Object.FindFirstObjectByType<KagitKartItemVisuals>();
            var dsp = world.GetComponent<KagitKartAudioDsp>();
            var carrier = dsp.GetComponent<AudioSource>().clip;
            Assert.That(crane, Is.Not.Null);
            Assert.That(NativeField<Mesh>(crane, "bodyShape"), Is.Not.Null);
            Assert.That(NativeField<Mesh>(crane, "wingShape"), Is.Not.Null);
            Assert.That(NativeField<Material[]>(crane, "paperMaterials").All(value => value != null), Is.True);

            var signals = new List<KagitKartVehicleSignal>();
            void Record(KagitKartVehicle kart, KagitKartVehicleSignal signal)
            {
                if (signal is KagitKartVehicleSignal.RescueLift or KagitKartVehicleSignal.RescueCarry
                    or KagitKartVehicleSignal.RescueDrop or KagitKartVehicleSignal.RescueLand)
                {
                    signals.Add(signal);
                }
            }
            player.Signaled += Record;
            try
            {
                audio.SetVolumes(0, 1, false); // 只改当前音频实例，不写玩家偏好。
                var track = session.Track;
                var road = track.PointAt((float)(track.Track.data.startDist + 40) / track.Length, 0);
                var roadInfo = QueryNative(track, road.Position);
                Assert.That(roadInfo.HasGround && roadInfo.OnRoad && roadInfo.RampId < 0, Is.True, "记录安全点需真实地面、道路且非跳台");
                player.PlaceAt(road.Position, road.Heading);
                double roadClock = session.RaceTime;
                float safeEnd = Time.realtimeSinceStartup + 8;
                while (!NativeField<bool>(player, "safeValid") && Time.realtimeSinceStartup < safeEnd) yield return null;
                Assert.That(NativeField<bool>(player, "safeValid"), Is.True,
                    $"正式运动步安全点：raceTime={roadClock}->{session.RaceTime}, paused={session.Pause.IsPaused}, busy={world.Busy}, "
                    + $"pinned={player.Pinned}, ground={player.Grounded}, road={player.TrackInfo.OnRoad}, ramp={player.TrackInfo.RampId}");

                Assert.That(FindNativeFallPosition(track, out var fall, out float heading), Is.True,
                    "只接受真实 Query 无地面且高于死亡/水面阈值的位置。");
                var initialY = fall.y;
                player.PlaceAt(fall, heading); // 仅位置前置；不调用 StartRescue/Summon/AdvanceRescue。
                Assert.That(player.Rescuing, Is.False);
                Assert.That(player.TrackInfo.HasGround, Is.False);
                yield return Wait(() => !player.Grounded && player.Position.y < initialY - 0.1f
                    && player.VerticalSpeed < 0 && !player.Rescuing, "正式重力落空");
                yield return Capture("NativeRescueFalling", 0);
                yield return Wait(() => player.RescueStage == 1 && NativeCraneHas(crane, player)
                    && NativeProperty<int>(crane, "BodyCount") > 0 && NativeProperty<int>(crane, "WingCount") > 0, "实际纸鹤提起");
                yield return Capture("NativeRescueLift", 0);
                yield return Wait(() => player.RescueStage == 2 && NativeCraneHas(crane, player), "实际纸鹤搬运");
                yield return Capture("NativeRescueCarry", 0);
                yield return Wait(() => player.RescueStage == 3 && NativeCraneHas(crane, player), "实际纸鹤放下");
                yield return Capture("NativeRescueDrop", 0);
                yield return Wait(() => !player.Rescuing && player.Grounded, "实际救援落地");
                Assert.That(QueryNative(track, player.Position).HasGround, Is.True);
                Assert.That(player.GraceTime, Is.GreaterThan(0));
                CollectionAssert.AreEqual(new[] { KagitKartVehicleSignal.RescueLift, KagitKartVehicleSignal.RescueCarry,
                    KagitKartVehicleSignal.RescueDrop, KagitKartVehicleSignal.RescueLand }, signals);
                Assert.That(NativeRouteTime(audio, "respawnLift"), Is.GreaterThan(0));
                Assert.That(NativeRouteTime(audio, "respawnDrop"), Is.GreaterThan(0));
                yield return Capture("NativeRescueLanded", 0);
                yield return Wait(() => NativeField<Array>(crane, "slots").Cast<object>()
                    .All(slot => NativeField<KagitKartVehicle>(slot, "kart") != player), "纸鹤离场释放车手引用");
            }
            finally
            {
                player.Signaled -= Record;
                if (audio != null)
                {
                    audio.SetVolumes(world.Settings.MusicVolume, world.Settings.SfxVolume, world.Settings.Muted);
                }
            }
            world.Dispatch(KagitKartCommand.Hub);
            yield return Wait(() => world == null && crane == null && effects == null && items == null && dsp == null
                && carrier == null, "救援场景、表现实例与混音载体释放");
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator NativeEightItemsRenderAndReachUnityAudioOutput()
        {
            KagitKartWorld world = null;
            yield return EnterNativeEffectsWorld(value => world = value, KagitKartRaceMode.SingleRace);
            var session = world.Session;
            var player = session.Player;
            var visuals = Object.FindFirstObjectByType<KagitKartItemVisuals>();
            var effects = Object.FindFirstObjectByType<KagitKartRaceFx>();
            var crane = Object.FindFirstObjectByType<KagitKartRescueCrane>();
            var audio = world.GetComponent<KagitKartAudio>();
            var dsp = world.GetComponent<KagitKartAudioDsp>();
            var output = dsp.GetComponent<AudioSource>();
            var carrier = output.clip;
            var samples = new float[1024];
            float peak = 0;
            var events = new List<KagitKartItemEvent>();
            var pinned = session.Karts.Select(kart => kart.Pinned).ToArray();
            session.Items.Signaled += events.Add;
            try
            {
                audio.SetVolumes(0, 1, false);
                Assert.That(output.isPlaying && output.loop && output.ignoreListenerPause, Is.True);
                Assert.That(Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None)
                    .Count(listener => listener.isActiveAndEnabled), Is.EqualTo(1));
                Assert.That(world.GetComponentsInChildren<SkinnedMeshRenderer>().Any(renderer => renderer.enabled
                    && renderer.sharedMesh != null && renderer.sharedMaterials.All(material => material != null)), Is.True,
                    "使用正式车手 Prefab 的原生 Renderer。");
                NativeArrangeItemPositions(session); // 固定库存/位置/运动，不绕过 Items 的使用与碰撞规则。
                for (int i = 0; i < session.Karts.Count; i++)
                {
                    session.Karts[i].Pinned = true;
                }
                yield return Wait(() => session.Karts.All(kart => kart.Progress >= 0) && player.Place > 1,
                    "全部车辆真实跨过起跑线并由道路进度产生前车");
                string[] names = { "rocket3", "rocket", "gum", "plane", "homing", "ink", "scissors", "foil" };
                foreach (string name in names)
                {
                    NativeSetInventory(player, name, name == "rocket3" ? 3 : 1);
                    yield return new WaitForSecondsRealtime(0.12f);
                    if (name == "rocket3")
                    {
                        Assert.That(NativeBatchCount(visuals, 3), Is.EqualTo(3));
                        yield return Capture("NativeItemRocket3Stowed", 0);
                    }
                    string route = "use_" + (name == "rocket3" ? "rocket" : name);
                    double routeBefore = NativeRouteTime(audio, route);
                    int firstEvent = events.Count;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
                    if (name == "gum")
                    {
                        yield return Wait(() => player.ItemHeld && session.Items.Hazards.Any(hazard => hazard.Held == player)
                            && NativeBatchCount(visuals, 1) > 0, "正式 E 按住口香糖");
                        AssertNativeBatchAssets(visuals, 1);
                        yield return Capture("NativeItemGumHeld", 0);
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                        yield return Wait(() => !player.ItemHeld && player.Item == null && session.Items.Hazards
                            .Any(hazard => hazard.Owner == player && hazard.Held == null && !hazard.Dead)
                            && NativeBatchCount(visuals, 1) > 0, "正式 E 松开掉落口香糖");
                        yield return Capture("NativeItemGumDropped", 0);
                    }
                    else
                    {
                        int batch = name is "rocket3" or "rocket" ? 3 : name is "plane" or "homing" ? 2
                            : name == "ink" ? 4 : name == "scissors" ? 6 : 10;
                        yield return Wait(() => events.Skip(firstEvent).Any(signal => signal.Kind == "itemUse"
                            && signal.Item == name && signal.Kart == player) && NativeBatchCount(visuals, batch) > 0,
                            "正式 E 使用并提交世界网格 " + name);
                        AssertNativeBatchAssets(visuals, batch);
                        if (name is "rocket3" or "rocket")
                        {
                            Assert.That(player.BoostTime > 0 && player.BoostSource == "item", Is.True);
                            Assert.That(player.ItemCount, Is.EqualTo(name == "rocket3" ? 2 : 0));
                        }
                        else if (name == "foil")
                        {
                            Assert.That(player.InvincibleTime, Is.GreaterThan(0));
                        }
                        else
                        {
                            var projectile = session.Items.Projectiles.Single(value => value.Owner == player && value.Kind == name);
                            if (name is "homing" or "scissors")
                            {
                                Assert.That(projectile.Target, Is.Not.Null,
                                    $"{name}: place={player.Place}, brake={player.Controls.Brake}, look={player.Controls.LookBack}, straight={projectile.Straight}, "
                                    + string.Join(",", session.Karts.Select(kart => $"{kart.Character.id}:{kart.Place}/{kart.Progress}/{kart.Rescuing}")));
                                Assert.That(projectile.Target, Is.Not.SameAs(player));
                            }
                        }
                        yield return Capture("NativeItem_" + name, 0);
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    }
                    yield return Wait(() => NativeRouteTime(audio, route) > routeBefore, "真实音频事件路由 " + name);
                    AudioListener.GetOutputData(samples, 0);
                    peak = Mathf.Max(peak, samples.Max(value => Mathf.Abs(value)));
                    if (name == "ink")
                    {
                        yield return Wait(() => events.Skip(firstEvent).Any(signal => signal.Kind == "ink")
                            && NativeBatchCount(visuals, 5) > 0, "真实瓶子爆墨提交墨滴");
                        Assert.That(session.Karts.Any(kart => kart != player && kart.InkTime > 0), Is.True);
                        AssertNativeBatchAssets(visuals, 5);
                        yield return Capture("NativeItemInkBurst", 0);
                    }
                    else if (name == "scissors")
                    {
                        yield return Wait(() => session.Items.Projectiles.Any(value => value.Kind == name
                            && value.Owner == player && value.Phase == 2 && value.PhaseTime > 0.3)
                            && NativeBatchCount(visuals, 6) > 0, "真实剪刀下降");
                        yield return Capture("NativeItemScissorsDive", 0);
                        yield return Wait(() => events.Skip(firstEvent).Any(signal => signal.Kind == "snip")
                            && NativeField<Array>(visuals, "snips").Cast<object>().Any(snip => NativeField<bool>(snip, "on"))
                            && NativeBatchCount(visuals, 8) > 0, "真实剪断事件与地面印记");
                        Assert.That(session.Karts.Any(kart => kart.SpinTime > 0 && kart.SpinCause == "scissors"), Is.True);
                        yield return Capture("NativeItemScissorsSnip", 0);
                    }
                    if (name is "plane" or "homing")
                    {
                        yield return Wait(() => !session.Items.Projectiles.Any(value => value.Kind == name && value.Owner == player),
                            "真实飞行道具命中/到期清理 " + name);
                    }
                    yield return new WaitForSecondsRealtime(0.15f); // 正式输入步看到松键，下一件才能产生 rising edge。
                }
                for (int i = 0; i < 12; i++)
                {
                    AudioListener.GetOutputData(samples, 0);
                    peak = Mathf.Max(peak, samples.Max(value => Mathf.Abs(value)));
                    yield return new WaitForSecondsRealtime(0.025f);
                }
                Debug.Log("KagitKart 原生混音输出 peak=" + peak + "，sampleRate=" + AudioSettings.outputSampleRate
                    + "，GPU=" + SystemInfo.graphicsDeviceName);
                Assert.That(peak, Is.GreaterThan(0.000001f), "必须采到 AudioListener 最终混音输出；全零需检查 Editor 静音/音频设备，不能用离线波形替代。");
            }
            finally
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                session.Items.Signaled -= events.Add;
                for (int i = 0; i < session.Karts.Count; i++)
                {
                    session.Karts[i].Pinned = pinned[i];
                }
                if (audio != null)
                {
                    audio.SetVolumes(world.Settings.MusicVolume, world.Settings.SfxVolume, world.Settings.Muted);
                }
            }
            world.Dispatch(KagitKartCommand.Hub);
            yield return Wait(() => world == null && visuals == null && effects == null && crane == null && dsp == null
                && output == null && carrier == null, "八道具场景与原生音频载体释放");
        }

        private IEnumerator EnterNativeEffectsWorld(Action<KagitKartWorld> receive, KagitKartRaceMode mode, string trackId = "meadow")
        {
            Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(GraphicsDeviceType.Null), "需实际 GPU 与 GameView。");
            yield return GameSceneNavigator.Instance.SwitchAsync(GameSceneId.KagitKart).ToCoroutine();
            KagitKartWorld world = null;
            yield return Wait(() => (world = Object.FindFirstObjectByType<KagitKartWorld>()) != null && !world.Busy
                && UIManager.Instance.Get<KagitKartTitleView>()?.State == ViewState.Visible, "原生世界表现入口");
            yield return world.StartRaceAsync(mode, trackId, "tilki", 120).ToCoroutine();
            world.SendMessage("OnApplicationFocus", true);
            world.Dispatch(KagitKartCommand.Resume);
            var bridge = world.GetComponent<KagitKartAudio>();
            Assert.That(NativeField<string>(bridge, "musicKey"), Is.EqualTo("intro_" + trackId));
            double beforeCountdown = AudioSettings.dspTime;
            world.Dispatch(KagitKartCommand.SkipIntro);
            Assert.That(world.Session.Race.State, Is.EqualTo(KagitKartRaceState.Countdown));
            Assert.That(NativeField<string>(bridge, "musicKey"), Is.Null, "倒计时退出赛前背景曲，仅保留淡出尾音");
            Assert.That(NativeField<double[]>(bridge, "pairEnd").All(end => end <= beforeCountdown + 0.93), Is.True);
            yield return Wait(() => world.Session.Race.State == KagitKartRaceState.Running, "正常倒计时起跑");
            Assert.That(NativeField<string>(bridge, "musicKey"), Is.EqualTo(trackId));
            receive(world);
        }

        private static KagitKartQueryInfo QueryNative(KagitKartTrackQuery track, Vector3 position)
        {
            var info = new KagitKartQueryInfo();
            track.Query(position, -1, ref info);
            return info;
        }

        private static bool FindNativeFallPosition(KagitKartTrackQuery track, out Vector3 position, out float heading)
        {
            for (int i = 0; i < 200; i++)
            {
                var point = track.SampleAt(i / 200f);
                foreach (int side in new[] { -1, 1 })
                {
                    var candidate = point.Position + point.Right * (side * (point.HalfWidth + point.Offroad + 4));
                    var info = QueryNative(track, candidate);
                    candidate.y = info.GroundY + 0.05f;
                    info = QueryNative(track, candidate);
                    if (!info.HasGround && (info.EdgeCode is 2 or 3) && candidate.y > track.KillY + 0.5f
                        && (float.IsNegativeInfinity(info.WaterY) || candidate.y > info.WaterY + 0.5f))
                    {
                        position = candidate;
                        heading = point.Heading;
                        return true;
                    }
                }
            }
            position = default;
            heading = 0;
            return false;
        }

        private static void NativeArrangeItemPositions(KagitKartSession session)
        {
            // 原圈线仅检测末60米→前60米；前置位置全部位于起跑线后60米内，仍由Race确认跨线。
            for (int offset = 12; offset <= 32; offset += 4)
            {
                var positions = new KagitKartTrackPoint[session.Karts.Count];
                bool valid = true;
                int ahead = 0;
                for (int i = 0; i < positions.Length; i++)
                {
                    bool mine = session.Karts[i] == session.Player;
                    float distance = (float)session.Track.Track.data.startDist + offset + (mine ? 0 : 12 + 2 * ahead++);
                    positions[i] = session.Track.PointAt(distance / session.Track.Length, mine ? 0 : (i % 2 == 0 ? -2 : 2));
                    var info = QueryNative(session.Track, positions[i].Position);
                    valid &= info.HasGround && info.OnRoad && info.RampId < 0 && session.Items.Boxes.All(box =>
                        (box.Position - positions[i].Position).sqrMagnitude > 9);
                }
                if (!valid)
                {
                    continue;
                }
                for (int i = 0; i < positions.Length; i++)
                {
                    session.Karts[i].PlaceAt(positions[i].Position, positions[i].Heading);
                    NativeSetInventory(session.Karts[i], null, 0);
                }
                return;
            }
            Assert.Fail("未找到避开道具箱的真实道路前置位置。");
        }

        private static void NativeSetInventory(KagitKartVehicle kart, string name, int count)
        {
            // 只设置库存，不修改计时器、弹体列表、事件或使用状态机。
            typeof(KagitKartVehicle).GetProperty("Item").SetValue(kart, name);
            typeof(KagitKartVehicle).GetProperty("ItemCount").SetValue(kart, count);
        }

        private static int NativeBatchCount(KagitKartItemVisuals visuals, int index) =>
            NativeField<int>(NativeField<Array>(visuals, "batches").GetValue(index), "count");

        private static void AssertNativeBatchAssets(KagitKartItemVisuals visuals, int index)
        {
            Assert.That(NativeField<Mesh[]>(visuals, "shapes")[index], Is.Not.Null);
            Assert.That(NativeField<Material[]>(visuals, "materials")[index], Is.Not.Null);
        }

        private static bool NativeCraneHas(KagitKartRescueCrane crane, KagitKartVehicle player) =>
            (bool)typeof(KagitKartRescueCrane).GetMethod("Has", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(crane, new object[] { player });

        private static double NativeRouteTime(KagitKartAudio audio, string name) =>
            NativeField<Dictionary<string, double>>(audio, "lastPlayed").TryGetValue(name, out double value) ? value : -1;

        private static T NativeField<T>(object target, string name) =>
            (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(target);

        private static T NativeProperty<T>(object target, string name) =>
            (T)target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(target);
    }
}
#endif
