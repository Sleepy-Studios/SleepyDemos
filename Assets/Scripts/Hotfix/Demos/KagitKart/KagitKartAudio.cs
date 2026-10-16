using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Core.Runtime;
using Core.Runtime.Settings;

namespace Hotfix.KagitKart
{
    /// 比赛事件与原生音源桥接；所有平台共用采样、变调和交叉淡化。
    public sealed class KagitKartAudio : MonoBehaviour
    {
        private static readonly Dictionary<AudioClip, int> musicOwners = new();
        private readonly Dictionary<string, double> lastPlayed = new();
        private readonly Dictionary<string, AudioClip> effectClips = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> worldBuckets = new(StringComparer.Ordinal);
        private readonly Dictionary<AudioClip, AsyncLazy> loading = new();
        private readonly Dictionary<AudioClip, string> ownedMusic = new();
        private readonly Dictionary<string, int> pendingMusic = new(StringComparer.Ordinal);
        private readonly Dictionary<string, AudioClip> musicClips = new(StringComparer.Ordinal);
        private readonly Dictionary<string, AsyncLazy<AudioClip>> musicLoads = new(StringComparer.Ordinal);
        private readonly List<AudioClip> unusedMusic = new();
        private readonly CancellationTokenSource lifetime = new();
        private readonly double[] wallAt = new double[8];
        private readonly double[] bumpSeen = new double[64];
        private readonly double[] bumpPlayed = new double[64];
        private readonly int[] previousPlace = new int[8];
        private readonly AudioSource[] effects = new AudioSource[28];
        private readonly double[] ends = new double[28];
        private readonly float[] effectGains = new float[28];
        private readonly bool[] effectMusic = new bool[28];
        private readonly bool[] effectUi = new bool[28];
        private readonly AudioSource[] music = new AudioSource[4];
        private readonly AudioSource[] engines = new AudioSource[6];
        private readonly KagitKartVehicle[] cpuKart = new KagitKartVehicle[3];
        private readonly float[] cpuDistance = new float[3];
        private readonly KagitKartVehicle[] nearest = new KagitKartVehicle[3];
        private readonly double[] pairStart = new double[2];
        private readonly double[] pairEnd = { double.PositiveInfinity, double.PositiveInfinity };
        private readonly float[] pairFade = new float[2];
        private readonly float[] pairOutFade = new float[2];
        private int musicPair;
        private int musicGeneration;
        private KagitKartAudioBank bank;
        private IResourceLoader musicLoader;
        private KagitKartSession session;
        private Camera listener;
        private string chapter;
        private string musicKey;
        private string warmMusicKey;
        private AudioClip warmStart;
        private AudioClip warmLoop;
        private double musicStart;
        private float globalMusic = 1;
        private float globalEffects = 1;
        private float globalUi = 1;
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
            ++musicGeneration;
            StopSources();
        }

        private void RefreshGlobalVolumes()
        {
            globalMusic = AudioSettingsRuntime.GetGain(AudioBus.Music);
            globalEffects = AudioSettingsRuntime.GetGain(AudioBus.Effects);
            globalUi = AudioSettingsRuntime.GetGain(AudioBus.Ui);
        }

        /// <summary>准备原作短音效、引擎采样和标题曲；由宿主 Loader 保持 Bank 句柄。</summary>
        /// <param name="asset">正式采样 Bank，不在运行时复制 PCM。</param>
        /// <param name="camera">距离与声像参考相机，不修改 AudioListener。</param>
        /// <param name="cancellationToken">宿主准备等待；销毁时结束所有音频请求。</param>
        public async UniTask InitializeAsync(KagitKartAudioBank asset, Camera camera, CancellationToken cancellationToken)
        {
            if (bank != null)
            {
                throw new InvalidOperationException("音频已经初始化。");
            }
            bank = asset != null ? asset : throw new ArgumentNullException(nameof(asset));
            listener = camera != null ? camera : throw new ArgumentNullException(nameof(camera));
            musicLoader = ResourceServices.CreateLoader();
            bank.CopyEffectsTo(effectClips);
            foreach (string name in effectClips.Keys)
            {
                worldBuckets.Add(name, name + ":world");
            }
            worldBuckets.Add("boost", "boost:world");
            worldBuckets.Add("tier", "tier:world");
            for (int i = 0; i < effects.Length; i++)
            {
                effects[i] = CreateSource("Effect" + i, false, 80);
            }
            for (int i = 0; i < music.Length; i++)
            {
                music[i] = CreateSource("Music" + i, false, 0);
            }
            for (int i = 0; i < engines.Length; i++)
            {
                engines[i] = CreateSource("Engine" + i, true, i < 3 ? 32 : 120);
                engines[i].clip = bank.GetClip(i == 0 ? "engineIdle" : i == 2 ? "engineDrift" : "engineDrive");
            }
            foreach (var clip in bank.Clips)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (clip.name.StartsWith("Sfx_", StringComparison.Ordinal) || clip.name.StartsWith("Sting_", StringComparison.Ordinal)
                    || clip.name.StartsWith("Engine", StringComparison.Ordinal))
                {
                    await LoadClipAsync(clip).AttachExternalCancellation(cancellationToken);
                }
            }
            await PrepareMusicAsync("title").AttachExternalCancellation(cancellationToken);
            initialized = true;
            PlayMenu();
        }

        private AudioSource CreateSource(string name, bool loop, int priority)
        {
            var child = new GameObject(name);
            child.transform.SetParent(transform, false);
            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0;
            source.dopplerLevel = 0;
            source.ignoreListenerPause = true;
            source.priority = priority;
            source.volume = 0;
            return source;
        }

        private UniTask LoadClipAsync(AudioClip clip)
        {
            if (clip.loadState == AudioDataLoadState.Loaded)
            {
                return UniTask.CompletedTask;
            }
            if (!loading.TryGetValue(clip, out var pending))
            {
                pending = new AsyncLazy(() => LoadClipDataAsync(clip));
                loading.Add(clip, pending);
            }
            return pending.Task;
        }

        private async UniTask LoadClipDataAsync(AudioClip clip)
        {
            var token = lifetime.Token;
            try
            {
#if !UNITY_WEBGL || UNITY_EDITOR
                if (!clip.LoadAudioData() && clip.loadState != AudioDataLoadState.Loading)
                {
                    throw new InvalidOperationException("音频加载失败：" + clip.name);
                }
#endif
                // Web 导入开启预加载，资源加载触发浏览器解码；未发起加载直接失败，避免永久等待。
                while (clip != null && clip.loadState == AudioDataLoadState.Loading)
                {
                    await UniTask.NextFrame(PlayerLoopTiming.Update, token);
                }
                token.ThrowIfCancellationRequested();
                if (clip == null || clip.loadState != AudioDataLoadState.Loaded)
                {
                    throw new InvalidOperationException("音频未完成加载；Web 音频须启用 WebGL 预加载覆盖。");
                }
            }
            finally
            {
                loading.Remove(clip);
            }
        }

        /// <summary>只准备所选章节的开场曲；后续音乐随比赛阶段预热，不整章常驻。</summary>
        /// <param name="chapterId">有效章节标识。</param>
        /// <param name="cancellationToken">取消当前等待，不取消仍被其他请求使用的加载。</param>
        public UniTask PrepareRaceAudioAsync(string chapterId, CancellationToken cancellationToken)
        {
            KagitKartCatalog.TrackName(chapterId);
            SetWarmMusic("intro_" + chapterId);
            return PrepareMusicAsync(warmMusicKey).AttachExternalCancellation(cancellationToken);
        }

        private async UniTask PrepareMusicAsync(string key)
        {
            lifetime.Token.ThrowIfCancellationRequested();
            pendingMusic.TryGetValue(key, out int pending);
            pendingMusic[key] = pending + 1;
            try
            {
                await LoadMusicClipAsync("music_" + key + "_start");
                await LoadMusicClipAsync("music_" + key + "_loop");
                if (warmMusicKey == key)
                {
                    warmStart = musicClips["music_" + key + "_start"];
                    warmLoop = musicClips["music_" + key + "_loop"];
                }
            }
            finally
            {
                int remaining = pendingMusic[key] - 1;
                if (remaining == 0)
                {
                    pendingMusic.Remove(key);
                }
                else
                {
                    pendingMusic[key] = remaining;
                }
            }
        }

        private UniTask<AudioClip> LoadMusicClipAsync(string name)
        {
            if (musicClips.TryGetValue(name, out var clip))
            {
                return UniTask.FromResult(clip);
            }
            if (!musicLoads.TryGetValue(name, out var pending))
            {
                pending = new AsyncLazy<AudioClip>(() => LoadMusicAssetAsync(name));
                musicLoads.Add(name, pending);
            }
            return pending.Task;
        }

        private async UniTask<AudioClip> LoadMusicAssetAsync(string name)
        {
            var token = lifetime.Token;
            AudioClip clip = null;
            bool retained = false;
            try
            {
                clip = await musicLoader.LoadAssetAsync<AudioClip>(bank.GetMusicAddress(name));
                token.ThrowIfCancellationRequested();
                if (clip == null)
                {
                    throw new InvalidOperationException("音乐资源加载失败：" + name);
                }
                // 在数据加载前持有共享 Clip，防止重叠宿主释放其他实例仍需使用的数据。
                RetainMusic(name, clip);
                retained = true;
                await LoadClipAsync(clip);
                token.ThrowIfCancellationRequested();
                musicClips.Add(name, clip);
                return clip;
            }
            catch
            {
                if (retained)
                {
                    ReleaseMusic(clip);
                }
                else if (clip != null)
                {
                    musicLoader.ReleaseAsset(clip);
                }
                throw;
            }
            finally
            {
                musicLoads.Remove(name);
            }
        }

        private void WarmMusic(string key)
        {
            SetWarmMusic(key);
            WarmMusicAsync(key).Forget();
        }

        private void SetWarmMusic(string key)
        {
            warmMusicKey = key;
            warmStart = warmLoop = null;
        }

        private async UniTask WarmMusicAsync(string key)
        {
            try
            {
                await PrepareMusicAsync(key);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                Debug.LogWarning("音乐预热失败，播放时将重试：" + exception.Message, this);
            }
            finally
            {
                ReleaseUnusedMusic();
            }
        }

        /// <summary>绑定本场规则事件，取消上一场订阅。</summary>
        /// <param name="value">已组装且尚未开始倒计时的比赛。</param>
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

        /// 退订本场事件并停止持续引擎与音效，音乐由下一次播放请求接管。
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
            intro = paused = false;
            rouletteTime = 0;
            popupCount = 0;
            popupSize = 0;
            popupSum = Vector3.zero;
            lastPlayed.Clear();
            foreach (var source in effects)
            {
                if (source != null)
                {
                    source.Stop();
                }
            }
            foreach (var source in engines)
            {
                if (source != null)
                {
                    source.Stop();
                    source.volume = 0;
                }
            }
            Array.Clear(ends, 0, ends.Length);
            Array.Clear(cpuKart, 0, cpuKart.Length);
        }

        /// <summary>播放标题或大奖赛领奖音乐，重复请求不重播。</summary>
        /// <param name="podium">true 为大奖赛领奖，默认标题曲。</param>
        public void PlayMenu(bool podium = false) => PlayMusic(podium ? "podium" : "title", 0.4f);

        /// <summary>播放章节开场，并提前准备比赛曲。</summary>
        /// <param name="chapterId">有效章节标识。</param>
        public void PlayIntro(string chapterId)
        {
            intro = true;
            chapter = chapterId;
            PlayMusic("intro_" + chapterId, 0.8f);
            WarmMusic(chapterId);
        }

        /// 结束赛前演出，启用引擎并播放第一声倒计时。
        public void BeginCountdown()
        {
            intro = false;
            StopMusic(0.9f);
            Play("count", important: true);
        }

        /// 恢复持续声音，不重复历史比赛事件。
        public void RestoreNetworkPlayback()
        {
            if (session == null)
            {
                return;
            }
            intro = false;
            playerFinished = session.Player.Finished || session.Race.State == KagitKartRaceState.Done;
            finalLap = session.Race.Entries.First(entry => entry.Vehicle == session.Player).Progress.Lap >= session.Race.Laps;
            for (int i = 0; i < session.Karts.Count; i++)
            {
                previousPlace[i] = session.Karts[i].Place;
            }
            PlayMusic(playerFinished ? "results" : finalLap ? "final_" + chapter : chapter);
        }

        /// <summary>设置本 Demo 音量，沿用平方曲线与公共总线。</summary>
        /// <param name="musicLevel">音乐滑块0到1。</param>
        /// <param name="effectsLevel">音效与引擎滑块0到1。</param>
        /// <param name="isMuted">仅本Demo静音。</param>
        public void SetVolumes(float musicLevel, float effectsLevel, bool isMuted)
        {
            musicVolume = Mathf.Clamp01(musicLevel);
            sfxVolume = Mathf.Clamp01(effectsLevel);
            muted = isMuted;
        }

        /// 原作笔帽点击。
        public void Click() => Play("click", gap: 0.04f, important: true);

        /// 原作翻页反馈。
        public void PageTurn() => Play("pageTurn", gap: 0.15f, important: true);

        /// <summary>合并同一波纸景弹起反馈。</summary>
        /// <param name="sourcePosition">原作坐标，内部转换Unity坐标。</param>
        /// <param name="size">景物尺寸。</param>
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
            for (int i = 0; i < music.Length; i++)
            {
                if (music[i].clip == null)
                {
                    continue;
                }
                int pair = i / 2;
                if (now >= pairEnd[pair] && music[i].clip != null)
                {
                    music[i].Stop();
                    music[i].clip = null;
                }
                float fade = Mathf.Clamp01((float)(now - pairStart[pair]) / Mathf.Max(0.001f, pairFade[pair]));
                if (!double.IsPositiveInfinity(pairEnd[pair]))
                {
                    fade *= Mathf.Clamp01((float)(pairEnd[pair] - now) / Mathf.Max(0.001f, pairOutFade[pair]));
                }
                music[i].volume = musicGain * fade * 0.75f;
            }
            for (int i = 0; i < effects.Length; i++)
            {
                if (effects[i].clip == null)
                {
                    continue;
                }
                effects[i].volume = effectGains[i] * (effectMusic[i] ? musicGain : muted ? 0
                    : sfxVolume * sfxVolume * (effectUi[i] ? globalUi : globalEffects)) * 0.75f;
                if (now >= ends[i] && effects[i].clip != null)
                {
                    effects[i].Stop();
                    effects[i].clip = null;
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
            ReleaseUnusedMusic();
        }

        private void StopMusic(float fade)
        {
            ++musicGeneration;
            for (int pair = 0; pair < 2; pair++)
            {
                RetireMusicPair(pair, AudioSettings.dspTime, fade);
            }
            musicKey = null;
        }

        private void RetireMusicPair(int pair, double at, float fade)
        {
            pairEnd[pair] = Math.Min(pairEnd[pair], at + fade);
            pairOutFade[pair] = fade;
        }

        private void PlayMusic(string key, float fade = 0.15f, double at = 0)
        {
            if (musicKey == key)
            {
                return;
            }
            musicKey = key;
            PlayMusicAsync(key, fade, at, ++musicGeneration).Forget();
        }

        private async UniTask PlayMusicAsync(string key, float fade, double at, int generation)
        {
            try
            {
                await PrepareMusicAsync(key);
                if (generation != musicGeneration || lifetime.IsCancellationRequested)
                {
                    return;
                }
                double now = AudioSettings.dspTime;
                double start = Math.Max(now + 0.08, at);
                RetireMusicPair(musicPair, at > now ? start : now, fade);
                musicPair = 1 - musicPair;
                for (int i = 0; i < 2; i++)
                {
                    music[musicPair * 2 + i].Stop();
                }
                musicStart = pairStart[musicPair] = start;
                pairFade[musicPair] = fade;
                pairOutFade[musicPair] = 0;
                pairEnd[musicPair] = double.PositiveInfinity;
                var first = music[musicPair * 2];
                first.clip = musicClips["music_" + key + "_start"];
                first.loop = false;
                first.PlayScheduled(start);
                var loop = music[musicPair * 2 + 1];
                loop.clip = musicClips["music_" + key + "_loop"];
                loop.loop = true;
                loop.PlayScheduled(start + (double)first.clip.samples / first.clip.frequency);
                if (warmMusicKey == key)
                {
                    warmMusicKey = null;
                    warmStart = warmLoop = null;
                }
                if (key.StartsWith("final_", StringComparison.Ordinal))
                {
                    WarmMusic("results");
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                if (generation == musicGeneration)
                {
                    musicKey = null;
                }
                Debug.LogException(exception, this);
            }
            finally
            {
                ReleaseUnusedMusic();
            }
        }

        private void ReleaseUnusedMusic()
        {
            if (bank == null || lifetime.IsCancellationRequested)
            {
                return;
            }
            unusedMusic.Clear();
            foreach (var clip in ownedMusic.Keys)
            {
                bool used = false;
                foreach (var source in music)
                {
                    if (source != null && source.clip == clip)
                    {
                        used = true;
                    }
                }
                if (!used && warmMusicKey != null)
                {
                    used = clip == warmStart || clip == warmLoop;
                }
                if (!used && pendingMusic.Count == 0 && !loading.ContainsKey(clip))
                {
                    unusedMusic.Add(clip);
                }
            }
            foreach (var clip in unusedMusic)
            {
                ReleaseMusic(clip);
            }
        }

        private void RetainMusic(string name, AudioClip clip)
        {
            if (ownedMusic.ContainsKey(clip))
            {
                return;
            }
            ownedMusic.Add(clip, name);
            musicOwners.TryGetValue(clip, out int owners);
            musicOwners[clip] = owners + 1;
        }

        private void ReleaseMusic(AudioClip clip)
        {
            if (!ownedMusic.TryGetValue(clip, out var name))
            {
                return;
            }
            ownedMusic.Remove(clip);
            musicClips.Remove(name);
            int owners = musicOwners[clip] - 1;
            if (owners > 0)
            {
                musicOwners[clip] = owners;
            }
            else
            {
                musicOwners.Remove(clip);
#if !UNITY_WEBGL || UNITY_EDITOR
                if (clip != null)
                {
                    clip.UnloadAudioData();
                }
#endif
            }
            musicLoader.ReleaseAsset(clip);
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
            string bucket = rateKey ?? name;
            if (distant)
            {
                if (!worldBuckets.TryGetValue(bucket, out string key))
                {
                    key = bucket + ":world";
                    worldBuckets.Add(bucket, key);
                }
                bucket = key;
            }
            double now = AudioSettings.dspTime;
            if (lastPlayed.TryGetValue(bucket, out double last) && now - last < gap)
            {
                return false;
            }
            int slot = -1;
            for (int i = 0; i < ends.Length; i++)
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
            source.Stop();
            bool isMusic = name.StartsWith("sting_", StringComparison.Ordinal);
            string sample = name;
            if (name == "boost")
            {
                sample = "boost_" + (kart?.BoostSource switch { "pad" => 1, "start" => 2, "trick" => 3, "draft" => 4, _ => 0 });
            }
            source.clip = isMusic ? bank.GetClip(sample) : effectClips.TryGetValue(sample, out var clip)
                ? clip : throw new ArgumentException("纸艺卡丁车音频缺失：sfx_" + sample, nameof(name));
            source.panStereo = pan;
            source.pitch = name == "boost" ? Mathf.Clamp(1.1f - (kart?.LastBoostDuration ?? 1) * 0.1f, 0.85f, 1.1f) : 1;
            effectGains[slot] = gain * intensity;
            effectMusic[slot] = isMusic;
            effectUi[slot] = name == "click" || name == "pageTurn";
            source.volume = effectGains[slot] * (isMusic ? musicVolume * musicVolume * duck * globalMusic
                : sfxVolume * sfxVolume * (effectUi[slot] ? globalUi : globalEffects)) * 0.75f;
            if (at > now)
            {
                source.PlayScheduled(at);
            }
            else source.Play();
            ends[slot] = Math.Max(now, at) + source.clip.length / source.pitch;
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
            // 暂停只冻结比赛与引擎，音乐继续播放并降音量。
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
                        if (signal.Number == session.Race.Laps - 1)
                        {
                            WarmMusic("final_" + chapter);
                        }
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
                    string hit = kart.SpinCause switch
                    {
                        "gum" => "hit_gum", "plane" => "hit_plane", "rocket" or "homing" => "hit_homing",
                        "scissors" => "hit_scissors", "foil" => "hit_foil", "burnout" => "hit_burnout", _ => "hit_default"
                    };
                    Play(hit, kart, gain: kart.IsPlayer ? 1 : 0.7f, important: kart.IsPlayer);
                    break;
                case KagitKartVehicleSignal.Inked:
                    if (kart.IsPlayer)
                    {
                        Play("ink", important: true);
                    }
                    break;
                case KagitKartVehicleSignal.DriftTier:
                    Play(kart.DriftTier <= 1 ? "tier_0" : kart.DriftTier == 2 ? "tier_1" : "tier_2",
                        kart, gain: kart.IsPlayer ? 1 : 0.35f, gap: 0.01f, rateKey: "tier");
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
                    string use = signal.Item switch
                    {
                        "rocket" or "rocket3" => "use_rocket", "gum" => "use_gum", "plane" => "use_plane",
                        "homing" => "use_homing", "ink" => "use_ink", "foil" => "use_foil",
                        "scissors" => "use_scissors", _ => "use_" + signal.Item
                    };
                    Play(use, signal.Kart, gain: mine ? 1 : 0.7f, gap: 0.02f);
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
            float level = paused || intro ? 0 : session.Race.State == KagitKartRaceState.Done ? 0.5f : 1;
            var kart = session.Player;
            float speed = Mathf.Abs(kart.Speed), ratio = speed / kart.BaseTop;
            float throttle = Mathf.Clamp01(kart.Controls.Throttle);
            float nextRpm = Rpm(ratio, throttle) + (!kart.Grounded ? 0.12f * throttle : 0) + (kart.BoostTime > 0 ? 0.1f : 0);
            rpm = Mathf.Lerp(rpm, nextRpm, 1 - Mathf.Exp(-dt / 0.05f));
            float drive = Mathf.Clamp01((rpm - 0.25f) / 0.65f);
            float gain = level * (kart.Rescuing ? 0.2f : 1);
            float pan = kart.SteerInput * 0.12f;
            SetEngine(engines[0], Mathf.Lerp(0.9f, 1.25f, drive), gain * (1 - drive), pan, dt);
            SetEngine(engines[1], Mathf.Clamp(Mathf.Pow(2, (rpm - 0.8f) * 1.1f), 0.65f, 1.6f), gain * drive, pan, dt);
            SetEngine(engines[2], 1 + kart.DriftTier * 0.08f, gain * (kart.Drifting && kart.Grounded ? 0.55f : 0), pan, dt);
            UpdateCpus(level, dt);
        }

        private void SetEngine(AudioSource source, float pitch, float gain, float pan, float dt)
        {
            float target = muted ? 0 : gain * sfxVolume * sfxVolume * globalEffects;
            source.volume = Mathf.Lerp(source.volume, target, 1 - Mathf.Exp(-dt / 0.06f));
            source.pitch = Mathf.Lerp(source.pitch, Mathf.Clamp(pitch, 0.5f, 2), 1 - Mathf.Exp(-dt / 0.05f));
            source.panStereo = pan;
            if (source.volume > 0.0001f && !source.isPlaying)
            {
                source.Play();
            }
            else if (source.volume <= 0.0001f && source.isPlaying)
            {
                source.Stop();
            }
        }

        private void UpdateCpus(float level, float dt)
        {
            Array.Clear(nearest, 0, nearest.Length);
            Array.Fill(cpuDistance, float.PositiveInfinity);
            for (int kartIndex = 0; kartIndex < session.Karts.Count; kartIndex++)
            {
                var kart = session.Karts[kartIndex];
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
                }
            }
            for (int i = 0; i < cpuKart.Length; i++)
            {
                var kart = cpuKart[i];
                if (kart == null)
                {
                    SetEngine(engines[i + 3], 1, 0, 0, dt);
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
                float volume = distance > 160 ? 0 : level * 0.065f * Mathf.Pow(Mathf.Min(1, 10 / Mathf.Max(distance, 10)), 1.15f) * (0.6f + 0.4f * throttle);
                float pan = Mathf.Clamp(Vector3.Dot(offset / distance, listener.transform.right) * 0.85f, -0.85f, 0.85f);
                SetEngine(engines[i + 3], frequency / 160, volume * 1.5f, pan, dt);
            }
        }

        private void StopSources()
        {
            foreach (var source in music)
            {
                if (source != null)
                {
                    source.Stop();
                }
            }
            foreach (var source in effects)
            {
                if (source != null)
                {
                    source.Stop();
                }
            }
            foreach (var source in engines)
            {
                if (source != null)
                {
                    source.Stop();
                }
            }
        }

        private void OnDestroy()
        {
            initialized = false;
            lifetime.Cancel();
            Unbind();
            StopSources();
            unusedMusic.Clear();
            unusedMusic.AddRange(ownedMusic.Keys);
            foreach (var clip in unusedMusic)
            {
                ReleaseMusic(clip);
            }
            unusedMusic.Clear();
            musicLoader?.Dispose();
            lifetime.Dispose();
        }
    }
}
