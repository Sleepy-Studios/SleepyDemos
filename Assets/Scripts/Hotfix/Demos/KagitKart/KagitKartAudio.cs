using System;
using System.Collections.Generic;
using UnityEngine;
using Core.Runtime.Settings;

namespace Hotfix.KagitKart
{
    /// 原作 audio.js 的事件桥接；所有声音随场景释放，不建立全局音频框架。
    public sealed class KagitKartAudio : MonoBehaviour
    {
        private readonly Dictionary<string, double> lastPlayed = new();
        private readonly double[] wallAt = new double[8];
        private readonly double[] bumpSeen = new double[64];
        private readonly double[] bumpPlayed = new double[64];
        private readonly int[] previousPlace = new int[8];
        private readonly KagitKartAudioDsp.Voice[] effects = new KagitKartAudioDsp.Voice[28];
        private readonly double[] ends = new double[28];
        private readonly float[] effectGains = new float[28];
        private readonly bool[] effectMusic = new bool[28];
        private readonly bool[] effectUi = new bool[28];
        private float globalMusic = 1;
        private float globalEffects = 1;
        private float globalUi = 1;
        private KagitKartEngineSynth engine;
        private readonly bool[] cpuFresh = new bool[3];
        private readonly KagitKartVehicle[] cpuKart = new KagitKartVehicle[3];
        private readonly float[] cpuDistance = new float[3];
        private readonly KagitKartVehicle[] nearest = new KagitKartVehicle[3];
        private readonly KagitKartAudioDsp.Voice[] music = new KagitKartAudioDsp.Voice[4];
        private readonly double[] pairStart = new double[2];
        private readonly double[] pairLoopStart = new double[2];
        private readonly double[] pairEnd = { double.PositiveInfinity, double.PositiveInfinity };
        private readonly float[] pairFade = new float[2];
        private readonly float[] pairOutFade = new float[2];
        private readonly float[] pairStingDuck = { 1, 1 };
        private readonly double[] pairDuckStart = new double[2];
        private int musicPair;
        private KagitKartAudioBank bank;
        private KagitKartAudioDsp dsp;
        private float[] white;
        private float[] crackle;
        private readonly System.Random audioRandom = new();
        private KagitKartSession session;
        private Camera listener;
        private string chapter;
        private string musicKey;
        private double musicStart;
        private float musicVolume = 0.7f;
        private float sfxVolume = 0.9f;
        private float duck = 1;
        private float rpm = 0.25f;
        private float rouletteTime;
        private bool rouletteFlip;
        private bool muted;
        private bool paused;
        private bool playerFinished;
        private bool initialized;
        private bool finalLap;
        private bool intro;
        private Vector3 popupSum;
        private int popupCount;
        private float popupSize;
        private double nextPopup;

        private void OnEnable()
        {
            AudioSettingsRuntime.Changed += RefreshGlobalVolumes;
            RefreshGlobalVolumes();
        }

        private void OnDisable()
        {
            AudioSettingsRuntime.Changed -= RefreshGlobalVolumes;
        }

        private void RefreshGlobalVolumes()
        {
            globalMusic = AudioSettingsRuntime.GetGain(AudioBus.Music);
            globalEffects = AudioSettingsRuntime.GetGain(AudioBus.Effects);
            globalUi = AudioSettingsRuntime.GetGain(AudioBus.Ui);
        }

        /// <summary>借用 Loader 持有的原作音频 Bank 和摄像机，初始化一次。</summary>
        /// <param name="asset">固定原作提交导出的音频资产；由宿主保持加载句柄。</param>
        /// <param name="camera">计算源坐标事件的距离与声像，不修改其 AudioListener。</param>
        public void Initialize(KagitKartAudioBank asset, Camera camera)
        {
            if (bank != null)
            {
                throw new InvalidOperationException("音频已经初始化。");
            }
            bank = asset != null ? asset : throw new ArgumentNullException(nameof(asset));
            listener = camera != null ? camera : throw new ArgumentNullException(nameof(camera));
            dsp = gameObject.AddComponent<KagitKartAudioDsp>();
            dsp.Initialize();
            white = ReadNoise("bank_white");
            crackle = ReadNoise("bank_crackle");
            for (int i = 0; i < effects.Length; i++)
            {
                effects[i] = dsp.CreateVoice();
            }
            for (int i = 0; i < music.Length; i++)
            {
                music[i] = dsp.CreateVoice();
                var filter = music[i];
                filter.FilterEnabled = true;
                filter.Cutoff = 20000;
            }
            engine = new KagitKartEngineSynth(AudioSettings.outputSampleRate, white, ReadNoise("bank_pink"), bank.GetClip("bank_white").frequency);
            dsp.Engine = engine;
            PlayMenu();
            initialized = true;
        }

        /// <summary>订阅比赛、道具、赛车和暂停事件；绑定前取消上一场订阅。</summary>
        /// <param name="value">已组装且尚未开始倒计时的比赛；不得为 null。</param>
        public void Bind(KagitKartSession value)
        {
            Unbind();
            session = value ?? throw new ArgumentNullException(nameof(value));
            chapter = session.Track.Track.definition.id;
            session.Race.Signaled += OnRace;
            session.Items.Signaled += OnItem;
            session.Collisions.Bumped += OnBump;
            session.Pause.Changed += OnPause;
            for (int i = 0; i < session.Karts.Count; i++)
            {
                session.Karts[i].Signaled += OnVehicle;
                previousPlace[i] = session.Karts[i].Place;
            }
            Array.Fill(wallAt, -9);
            Array.Fill(bumpSeen, -9);
            Array.Fill(bumpPlayed, -9);
            playerFinished = finalLap = false;
            rpm = 0.25f;
            StopMusic(0.9f);
            OnPause();
        }

        /// 取消本场事件并停止比赛持续声音；宿主随后可打开菜单音乐。
        public void Unbind()
        {
            if (session != null)
            {
                session.Race.Signaled -= OnRace;
                session.Items.Signaled -= OnItem;
                session.Collisions.Bumped -= OnBump;
                session.Pause.Changed -= OnPause;
                foreach (var kart in session.Karts)
                {
                    kart.Signaled -= OnVehicle;
                }
            }
            session = null;
            intro = false;
            paused = false;
            rouletteTime = 0;
            popupCount = 0;
            popupSize = 0;
            popupSum = Vector3.zero;
            lastPlayed.Clear();
            engine?.Silence();
            foreach (var source in effects)
            {
                if (source != null)
                {
                    source.Stop();
                }
            }
            Array.Clear(ends, 0, ends.Length);
            Array.Clear(cpuKart, 0, cpuKart.Length);
        }

        /// <summary>播放原作标题八音盒或大奖赛领奖音乐，重复调用不会重新起曲。</summary>
        /// <param name="podium">true 为大奖赛领奖；默认 false 为标题及选择页面。</param>
        public void PlayMenu(bool podium = false) => PlayMusic(podium ? "podium" : "title", podium ? 0 : 0.4f, podium ? AudioSettings.dspTime + 0.1 : 0);

        /// <summary>播放对应章节的原作开场段落。</summary>
        /// <param name="chapterId">meadow、bosphorus、glacier 或 desk。</param>
        public void PlayIntro(string chapterId)
        {
            intro = true;
            chapter = chapterId;
            PlayMusic("intro_" + chapterId, 0.8f);
        }

        /// 赛前镜头结束后启用引擎，并播放原作倒计时的第一声。
        public void BeginCountdown()
        {
            intro = false;
            StopMusic(0.9f);
            Play("count", important: true);
        }

        /// <summary>沿用原作音量平方曲线，静音只作用于本 Demo。</summary>
        /// <param name="musicLevel">音乐滑块 0 到 1。</param>
        /// <param name="effectsLevel">音效与引擎滑块 0 到 1。</param>
        /// <param name="isMuted">是否整体静音；不修改其它 Demo 或系统音量。</param>
        public void SetVolumes(float musicLevel, float effectsLevel, bool isMuted)
        {
            musicVolume = Mathf.Clamp01(musicLevel);
            sfxVolume = Mathf.Clamp01(effectsLevel);
            muted = isMuted;
        }

        /// 原作笔帽点击反馈。
        public void Click() => Play("click", gap: 0.04f, important: true);

        /// 原作菜单翻页反馈。
        public void PageTurn() => Play("pageTurn", gap: 0.15f, important: true);

        /// <summary>汇聚原作弹起纸景，按不规则波次发出纸页窸窣声。</summary>
        /// <param name="sourcePosition">原作坐标；内部统一转换 Unity 坐标。</param>
        /// <param name="size">弹起景物尺寸，最大尺寸与数量共同影响音量。</param>
        public void Popup(Vector3 sourcePosition, float size)
        {
            popupCount++;
            popupSum += sourcePosition;
            popupSize = Mathf.Max(popupSize, size);
        }

        /// <summary>原作四挡连续转速曲线；倒计时在原地按油门也能拉高转速。</summary>
        /// <param name="speedRatio">绝对车速除以角色基础极速。</param>
        /// <param name="throttle">归一化油门。</param>
        /// <returns>原作归一化转速，最小 0.2。</returns>
        public static float Rpm(float speedRatio, float throttle)
        {
            int gear = 0;
            while (gear < 3 && speedRatio >= 0.24f * (gear + 1))
            {
                gear++;
            }
            float local = Mathf.Clamp((speedRatio - gear * 0.24f) / (gear < 3 ? 0.24f : 0.45f), 0, 1.35f);
            float result = 0.32f + 0.09f * gear + (0.68f - 0.09f * gear) * local;
            if (speedRatio < 0.2f)
            {
                float weight = speedRatio / 0.2f;
                result = result * weight + (0.2f + 0.5f * throttle) * (1 - weight);
            }
            return Mathf.Max(0.2f, result);
        }

        private void Update()
        {
            if (!initialized)
            {
                return;
            }
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            double now = AudioSettings.dspTime;
            duck = Mathf.Lerp(duck, paused ? 0.28f : 1, 1 - Mathf.Exp(-dt / 0.12f));
            float musicGain = muted ? 0 : musicVolume * musicVolume * duck * globalMusic;
            engine.SetVolume(sfxVolume * sfxVolume * globalEffects, muted);
            for (int i = 0; i < music.Length; i++)
            {
                var source = music[i];
                int pair = i / 2;
                float fade = now >= pairEnd[pair] ? 0 : Mathf.Clamp01((float)(now - pairStart[pair]) / Mathf.Max(0.001f, pairFade[pair]));
                if (!double.IsPositiveInfinity(pairEnd[pair]) && pairOutFade[pair] > 0)
                {
                    fade *= Mathf.Clamp01((float)(pairEnd[pair] - now) / pairOutFade[pair]);
                }
                fade *= Mathf.Lerp(1, pairStingDuck[pair], Mathf.Clamp01((float)(now - pairDuckStart[pair]) / 0.05f));
                source.Volume = musicGain * fade;
                source.Cutoff = Mathf.Lerp(source.Cutoff, paused ? 650 : 20000, 1 - Mathf.Exp(-dt / (paused ? 0.1f : 0.25f)));
            }
            for (int i = 0; i < effects.Length; i++)
            {
                effects[i].Volume = effectGains[i] * (effectMusic[i] ? musicGain : muted ? 0
                    : sfxVolume * sfxVolume * (effectUi[i] ? globalUi : globalEffects));
                if (effectMusic[i])
                {
                    effects[i].Cutoff = music[0].Cutoff;
                }
            }
            if (session != null)
            {
                UpdateEngines(dt);
                UpdateRoulette(dt);
            }
            if (popupCount > 0 && now >= nextPopup)
            {
                float size = Mathf.Min(1, popupSize / 8 + 0.1f * (popupCount - 1));
                Play("rustle", position: popupSum / popupCount, gain: 0.9f, reference: 45, intensity: 0.5f + 0.5f * size);
                nextPopup = now + 0.45 + UnityEngine.Random.value * 0.2;
                popupCount = 0;
                popupSize = 0;
                popupSum = Vector3.zero;
            }
        }

        private float[] ReadNoise(string name)
        {
            var clip = bank.GetClip(name);
            clip.LoadAudioData();
            var values = new float[clip.samples];
            if (!clip.GetData(values, 0))
            {
                throw new InvalidOperationException("原作噪声采样读取失败：" + name);
            }
            clip.UnloadAudioData();
            return values;
        }

        private void RetireMusicPair(int pair, double at, float fade)
        {
            if (pairEnd[pair] <= at)
            {
                return;
            }
            double end = Math.Min(pairEnd[pair], at + fade);
            pairEnd[pair] = end;
            pairOutFade[pair] = fade;
            for (int i = 0; i < 2; i++)
            {
                var voice = music[pair * 2 + i];
                double begins = i == 0 ? pairStart[pair] : pairLoopStart[pair];
                if (voice.Clip == null || begins >= end)
                {
                    voice.Stop();
                }
                else
                {
                    voice.SetScheduledEndTime(end);
                }
            }
        }

        private void StopMusic(float fade)
        {
            double now = AudioSettings.dspTime;
            RetireMusicPair(0, now, fade);
            RetireMusicPair(1, now, fade);
            musicKey = null;
        }

        private void PlayMusic(string key, float fade = 0, double at = 0)
        {
            if (musicKey == key)
            {
                return;
            }
            double now = AudioSettings.dspTime;
            double start = at > 0 ? at : now + 0.05;
            int previous = musicPair;
            if (musicKey != null)
            {
                if (key.StartsWith("final_", StringComparison.Ordinal))
                {
                    RetireMusicPair(previous, start, 0);
                    pairStingDuck[previous] = 0.3f;
                    pairDuckStart[previous] = now;
                }
                else
                {
                    RetireMusicPair(previous, now, key == chapter ? 0.15f : 0.9f);
                }
            }
            musicPair = 1 - musicPair;
            for (int i = 0; i < 2; i++)
            {
                music[musicPair * 2 + i].Stop();
            }
            musicKey = key;
            musicStart = pairStart[musicPair] = start;
            pairFade[musicPair] = fade;
            pairOutFade[musicPair] = 0;
            pairStingDuck[musicPair] = 1;
            pairEnd[musicPair] = double.PositiveInfinity;
            var first = music[musicPair * 2];
            first.Clip = bank.GetClip("music_" + key + "_start");
            first.Loop = false;
            first.PlayScheduled(start);
            double introEnds = start + (double)first.Clip.samples / first.Clip.frequency;
            pairLoopStart[musicPair] = introEnds;
            var repeat = music[musicPair * 2 + 1];
            repeat.Clip = bank.GetClip("music_" + key + "_loop");
            repeat.Loop = true;
            repeat.PlayScheduled(introEnds);
        }

        private bool Play(string name, KagitKartVehicle kart = null, Vector3? position = null, float gain = 1,
            float gap = 0.03f, bool important = false, float reference = 12, double at = 0, float intensity = 1, string rateKey = null)
        {
            if (bank == null || muted)
            {
                return false;
            }
            float pan = 0;
            bool distant = kart != null && !kart.IsPlayer || position.HasValue;
            if (distant)
            {
                Vector3 point = KagitKartVehicle.ToUnity(position ?? kart.Position);
                Vector3 offset = point - listener.transform.position;
                float distance = Mathf.Max(1, offset.magnitude);
                gain *= distance > 150 ? 0 : Mathf.Min(1, reference / Mathf.Max(reference, distance));
                if (kart != null)
                {
                    gain *= 0.5f;
                }
                pan = Mathf.Clamp(Vector3.Dot(offset / distance, listener.transform.right), -0.9f, 0.9f);
            }
            if (gain < 0.06f)
            {
                return false;
            }
            string bucket = (rateKey ?? name) + (distant ? ":world" : "");
            double now = AudioSettings.dspTime;
            if (lastPlayed.TryGetValue(bucket, out double last) && now - last < gap)
            {
                return false;
            }
            int slot = -1;
            for (int i = 0; i < effects.Length; i++)
            {
                if (ends[i] <= now)
                {
                    slot = i;
                    break;
                }
            }
            if (slot < 0)
            {
                if (!important)
                {
                    return false;
                }
                slot = 0;
                for (int i = 1; i < ends.Length; i++)
                {
                    if (ends[i] < ends[slot])
                    {
                        slot = i;
                    }
                }
            }
            var source = effects[slot];
            bool isMusic = name.StartsWith("sting_", StringComparison.Ordinal);
            if (name == "boost")
            {
                source.SetWave(KagitKartBoostSynth.Render(kart.BoostSource, kart.LastBoostDuration, white, crackle, 44100, audioRandom.Next()), 44100);
            }
            else
            {
                source.Clip = bank.GetClip(isMusic ? name : "sfx_" + name);
            }
            source.Pan = pan;
            effectGains[slot] = gain * intensity;
            effectMusic[slot] = isMusic;
            effectUi[slot] = name == "click" || name == "pageTurn";
            source.FilterEnabled = effectMusic[slot];
            source.Cutoff = music[0].Cutoff;
            source.Volume = muted ? 0 : effectGains[slot] * (isMusic ? musicVolume * musicVolume * duck * globalMusic
                : sfxVolume * sfxVolume * (effectUi[slot] ? globalUi : globalEffects));
            if (at > now)
            {
                source.PlayScheduled(at);
            }
            else
            {
                source.Play();
            }
            ends[slot] = Math.Max(now, at) + source.Duration;
            lastPlayed[bucket] = now;
            return true;
        }

        private void OnPause()
        {
            bool next = session.Pause.IsPaused;
            if (paused == next)
            {
                return;
            }
            paused = next;
            // 原作暂停仅冻结比赛与引擎，音乐继续播放并降音量、低通。
            Play(paused ? "pause" : "unpause", important: true);
        }

        private void OnRace(KagitKartRaceEvent signal)
        {
            bool mine = signal.Kart != null && signal.Kart.IsPlayer;
            switch (signal.Kind)
            {
                case "countdown":
                    Play(signal.Number > 0 ? "count" : "go", important: true);
                    break;
                case "raceStart":
                    PlayMusic(chapter);
                    break;
                case "lap":
                    if (mine && signal.Number < session.Race.Laps)
                    {
                        Play("lap", important: true);
                    }
                    break;
                case "finalLap":
                    if (mine && !finalLap)
                    {
                        finalLap = true;
                        Play("sting_finalLap_" + chapter, important: true);
                        // 原作预排 0.3 秒，在尚未预排的下一拍升调切入高潮段，速度乘 1.12。
                        double beat = 60d / ChapterTempo();
                        double cut = musicStart + Math.Ceiling((AudioSettings.dspTime + 0.3 - musicStart) / beat) * beat;
                        PlayMusic("final_" + chapter, at: cut);
                    }
                    break;
                case "checkpoint":
                    if (mine && signal.Split.HasValue)
                    {
                        Play(signal.Split <= 0 ? "splitGood" : "splitBad");
                    }
                    break;
                case "place":
                    int index = IndexOf(signal.Kart);
                    int previous = previousPlace[index];
                    previousPlace[index] = signal.Number;
                    if (mine && session.Race.State == KagitKartRaceState.Running && previous > 0)
                    {
                        Play(signal.Number < previous ? "placeUp" : "placeDown", gap: 0.8f);
                    }
                    break;
                case "wrongWay":
                    if (mine && signal.Number != 0)
                    {
                        Play("wrongWay", gap: 1);
                    }
                    break;
                case "finish":
                    if (mine)
                    {
                        Finish(signal.Number);
                    }
                    break;
                case "raceEnd":
                    if (!playerFinished)
                    {
                        Finish(session.Player.Place);
                    }
                    break;
            }
        }

        private void Finish(int place)
        {
            if (playerFinished)
            {
                return;
            }
            playerFinished = true;
            StopMusic(0.35f);
            Play("finishLine", important: true);
            string sting = place == 1 ? "finishWin" : place <= 3 ? "finishGood" : "finishOk";
            Play("sting_" + sting, important: true, at: AudioSettings.dspTime + 0.3);
            // WAV 含乐器衰减尾音，结算入曲时间按原作乐句长度计算。
            double phrase = sting == "finishOk" ? 4.8 : (sting == "finishWin" ? 720d : 480d) / 124;
            PlayMusic("results", fade: 1.5f, at: AudioSettings.dspTime + 0.3 + phrase + 0.3);
        }

        private int ChapterTempo() => chapter == "meadow" ? 118 : chapter == "bosphorus" ? 96 : chapter == "glacier" ? 128 : 124;

        private void OnVehicle(KagitKartVehicle kart, KagitKartVehicleSignal signal)
        {
            float gain = kart.IsPlayer ? 1 : 0.6f;
            switch (signal)
            {
                case KagitKartVehicleSignal.Boost:
                    Play("boost", kart, gain: kart.IsPlayer ? 1 : 0.55f, gap: 0.02f);
                    break;
                case KagitKartVehicleSignal.Hit:
                    string cause = kart.SpinCause == "rocket" ? "homing" : kart.SpinCause;
                    if (cause != "gum" && cause != "plane" && cause != "homing" && cause != "scissors" && cause != "foil" && cause != "burnout")
                    {
                        cause = "default";
                    }
                    Play("hit_" + cause, kart, gain: kart.IsPlayer ? 1 : 0.7f, important: kart.IsPlayer);
                    break;
                case KagitKartVehicleSignal.Inked:
                    if (kart.IsPlayer)
                    {
                        Play("ink", important: true);
                    }
                    break;
                case KagitKartVehicleSignal.Hop:
                    Play("hop", kart, gain: kart.IsPlayer ? 1 : 0.4f, gap: 0.02f);
                    break;
                case KagitKartVehicleSignal.DriftTier:
                    Play("tier_" + Mathf.Clamp(kart.DriftTier - 1, 0, 2), kart, gain: kart.IsPlayer ? 1 : 0.35f, gap: 0.01f, rateKey: "tier");
                    break;
                case KagitKartVehicleSignal.DriftCancel:
                    if (kart.IsPlayer)
                    {
                        Play("driftCancel");
                    }
                    break;
                case KagitKartVehicleSignal.WallHit:
                    int wallIndex = IndexOf(kart);
                    double wallTime = AudioSettings.dspTime;
                    if (wallTime - wallAt[wallIndex] < 0.2)
                    {
                        return;
                    }
                    wallAt[wallIndex] = wallTime;
                    Play("wall", kart, gain: gain, gap: 0.02f, intensity: Mathf.Clamp(kart.LastImpactSpeed / 25, 0.25f, 1));
                    break;
                case KagitKartVehicleSignal.Trick:
                    Play("trick", kart, gain: gain);
                    break;
                case KagitKartVehicleSignal.Land:
                    if (kart.AirTime > 0.2f)
                    {
                        Play("land", kart, gain: gain, gap: 0.02f, intensity: Mathf.Clamp(kart.AirTime / 1.1f, 0.2f, 1));
                    }
                    break;
                case KagitKartVehicleSignal.RescueLift:
                    Play("respawnLift", kart, gain: gain);
                    break;
                case KagitKartVehicleSignal.RescueDrop:
                    Play("respawnDrop", kart, gain: gain);
                    break;
            }
        }

        private void OnItem(KagitKartItemEvent signal)
        {
            bool mine = signal.Kart != null && signal.Kart.IsPlayer;
            switch (signal.Kind)
            {
                case "itemBox":
                    Play("box", signal.Kart, gain: mine ? 1 : 0.5f, gap: 0.02f);
                    break;
                case "itemGet":
                    if (mine)
                    {
                        Play("itemGet");
                    }
                    break;
                case "itemUse":
                    Play("use_" + (signal.Item == "rocket3" ? "rocket" : signal.Item), signal.Kart, gain: mine ? 1 : 0.7f, gap: 0.02f);
                    break;
                case "projectile":
                    if (signal.State == "lock")
                    {
                        if (signal.Target != null && signal.Target.IsPlayer)
                        {
                            Play("lock", important: true);
                        }
                    }
                    else
                    {
                        string name = signal.State == "bounce" ? "planeBounce" : signal.State == "hit" ? "impact" : signal.State == "expire" ? "poof" : null;
                        if (name != null)
                        {
                            Play(name, position: signal.Position, gap: 0.05f);
                        }
                    }
                    break;
                case "threat":
                    if (mine)
                    {
                        Play(signal.Eta < 1 ? "threat_1" : "threat_0", important: true, gap: 0.05f, rateKey: "threat");
                    }
                    break;
            }
        }

        private void OnBump(KagitKartVehicle a, KagitKartVehicle b)
        {
            int ia = IndexOf(a), ib = IndexOf(b);
            int pair = Mathf.Min(ia, ib) * 8 + Mathf.Max(ia, ib);
            double now = AudioSettings.dspTime;
            bool mine = a.IsPlayer || b.IsPlayer;
            bool fresh = now - bumpSeen[pair] > 0.45;
            bumpSeen[pair] = now;
            if (!fresh && !(mine && now - bumpPlayed[pair] >= 0.6))
            {
                return;
            }
            if (Play("bump", mine ? null : a, gain: mine ? 1 : 0.4f, gap: mine ? 0.08f : 0.25f, intensity: Mathf.Clamp(session.BumpImpulse / 4, 0.3f, 1)))
            {
                bumpPlayed[pair] = now;
            }
        }

        private int IndexOf(KagitKartVehicle kart)
        {
            for (int i = 0; i < session.Karts.Count; i++)
            {
                if (session.Karts[i] == kart)
                {
                    return i;
                }
            }
            return 0;
        }

        private void UpdateRoulette(float dt)
        {
            var kart = session.Player;
            if (paused || kart.Roulette <= 0)
            {
                rouletteTime = 0;
                return;
            }
            rouletteTime -= dt;
            if (rouletteTime <= 0)
            {
                float left = Mathf.Clamp01((float)(kart.Roulette / kart.RouletteDuration));
                rouletteTime = 0.055f + 0.13f * (1 - left) * (1 - left);
                rouletteFlip = !rouletteFlip;
                Play(rouletteFlip ? "rouletteTick_1" : "rouletteTick_0", gap: 0.02f, rateKey: "rouletteTick");
            }
        }

        private void UpdateEngines(float dt)
        {
            engine.SetActive(!paused && !intro);
            if (paused || intro)
            {
                return;
            }
            var kart = session.Player;
            float level = session.Race.State == KagitKartRaceState.Done ? 0.5f : 1;
            float speed = Mathf.Abs(kart.Speed), ratio = speed / kart.BaseTop, throttle = Mathf.Clamp01(kart.Controls.Throttle);
            float nextRpm = Rpm(ratio, throttle) + (!kart.Grounded ? 0.12f * throttle : 0) + (kart.BoostTime > 0 ? 0.1f : 0);
            if (kart.SpinTime > 0)
            {
                nextRpm *= 0.8f + 0.1f * Mathf.Sin((float)AudioSettings.dspTime * 40);
            }
            rpm = Mathf.Lerp(rpm, nextRpm, 1 - Mathf.Exp(-dt / 0.05f));
            float pan = kart.SteerInput * 0.12f + (kart.Drifting ? kart.DriftDirection * 0.08f : 0);
            engine.SetPlayer(rpm, speed, ratio, throttle, level, !kart.Grounded, kart.Rescuing, kart.Surface,
                kart.Drifting, kart.DriftTier, kart.BoostTime, kart.BoostStrength,
                session.Collisions.GetDraftCharge(IndexOf(kart)), kart.InvincibleTime > 0, pan);
            UpdateCpus(level);
        }

        private void UpdateCpus(float level)
        {
            Array.Clear(nearest, 0, nearest.Length);
            Array.Fill(cpuDistance, float.PositiveInfinity);
            foreach (var kart in session.Karts)
            {
                if (kart.IsPlayer)
                {
                    continue;
                }
                float squared = (KagitKartVehicle.ToUnity(kart.Position) - listener.transform.position).sqrMagnitude;
                for (int i = 0; i < cpuKart.Length; i++)
                {
                    if (cpuKart[i] == kart)
                    {
                        squared *= 0.8f;
                    }
                }
                for (int i = 0; i < nearest.Length; i++)
                {
                    if (squared < cpuDistance[i])
                    {
                        for (int j = nearest.Length - 1; j > i; j--)
                        {
                            nearest[j] = nearest[j - 1];
                            cpuDistance[j] = cpuDistance[j - 1];
                        }
                        nearest[i] = kart;
                        cpuDistance[i] = squared;
                        break;
                    }
                }
            }
            for (int i = 0; i < cpuKart.Length; i++)
            {
                if (Array.IndexOf(nearest, cpuKart[i]) < 0)
                {
                    cpuKart[i] = null;
                }
            }
            foreach (var kart in nearest)
            {
                if (kart != null && Array.IndexOf(cpuKart, kart) < 0)
                {
                    int slot = Array.IndexOf(cpuKart, null);
                    cpuKart[slot] = kart;
                    cpuFresh[slot] = true;
                }
            }
            for (int i = 0; i < cpuKart.Length; i++)
            {
                var kart = cpuKart[i];
                if (kart == null)
                {
                    engine.SetCpu(i, 70, 900, 0, 0, false, present: false);
                    continue;
                }
                Vector3 offset = KagitKartVehicle.ToUnity(kart.Position) - listener.transform.position;
                float distance = Mathf.Max(1, offset.magnitude);
                float throttle = kart.Controls.Throttle;
                float nextRpm = Rpm(Mathf.Abs(kart.Speed) / kart.BaseTop, throttle) + (kart.BoostTime > 0 ? 0.1f : 0);
                Vector3 velocity = KagitKartVehicle.ToUnity(kart.Velocity - session.Player.Velocity);
                float relative = (velocity.x * offset.x + velocity.z * offset.z) / distance;
                float doppler = Mathf.Clamp(1 / (1 + relative / 140), 0.86f, 1.14f);
                float persona = 0.9f + 0.05f * (IndexOf(kart) % 5);
                float frequency = 62 * Mathf.Pow(2, nextRpm * 1.9f) * doppler * persona;
                float cutoff = 500 + 1800 * Mathf.Min(1.2f, nextRpm) * (0.5f + 0.5f * throttle);
                float volume = distance > 160 ? 0 : level * 0.065f * Mathf.Pow(Mathf.Min(1, 10 / Mathf.Max(distance, 10)), 1.15f) * (0.6f + 0.4f * throttle);
                float pan = Mathf.Clamp(Vector3.Dot(offset / distance, listener.transform.right) * 0.85f, -0.85f, 0.85f);
                engine.SetCpu(i, frequency, cutoff, volume, pan, cpuFresh[i]);
                cpuFresh[i] = false;
            }
        }

        private void OnDestroy() => Unbind();
    }
}
