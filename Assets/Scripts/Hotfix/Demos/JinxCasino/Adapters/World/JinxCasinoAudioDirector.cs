using System;
using System.Collections.Generic;
using Hotfix.JinxCasino.Rules;
using UnityEngine;

namespace Hotfix.JinxCasino.Adapters
{
    [Serializable]
    public sealed class CasinoAudioClipBinding { public string Id; public AudioClip Clip; }

    /// 本场景的原创音乐和反馈音效；只观察已提交状态，恢复存档不重放旧奖励。
    public sealed class JinxCasinoAudioDirector : MonoBehaviour
    {
        [SerializeField] private JinxCasinoController owner;
        [SerializeField] private AudioSource music;
        [SerializeField] private AudioSource sfx;
        [SerializeField] private CasinoAudioClipBinding[] clips = Array.Empty<CasinoAudioClipBinding>();
        [SerializeField, Range(0, 1)] private float volume = 0.7f;
        [SerializeField] private bool muted;
        private readonly HashSet<string> effects = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> requests = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<AudioSource, float> effectSources = new Dictionary<AudioSource, float>();
        private readonly List<AudioSource> expiredSources = new List<AudioSource>();
        private string runId;
        private int settledSequence;
        private CasinoAdventureEnding ending;
        private bool bound;
        private float nextSourceCleanup;
        public float Volume => volume;
        public bool Muted => muted;

        /// <summary>绑定场景内保存的音源和14段原创音频；不新增AudioListener。</summary>
        /// <param name="controller">冒险状态及恢复标志来源。</param>
        /// <param name="musicSource">本场景循环音乐音源。</param>
        /// <param name="sfxSource">本场景界面及规则反馈音源。</param>
        /// <param name="bindings">UiClick/MachineBegin/Win/Lose/Coin/TaskComplete/Event/EndingDignity/EndingTakeover/EndingWithdraw/Horn/Boing/Charge/ClubLoop。</param>
        public void Setup(JinxCasinoController controller, AudioSource musicSource, AudioSource sfxSource, CasinoAudioClipBinding[] bindings)
        {
            Unbind(); owner = controller; music = musicSource; sfx = sfxSource; clips = bindings ?? Array.Empty<CasinoAudioClipBinding>();
            if (Application.isPlaying && isActiveAndEnabled) Bind();
        }

        /// <summary>调整本场景全部音源音量和静音；不会修改用户全局AudioListener设置。</summary>
        /// <param name="value">0到1的整体音量。</param>
        /// <param name="isMuted">明确静音状态。</param>
        public void SetVolume(float value, bool isMuted = false)
        { volume = Mathf.Clamp01(value); muted = isMuted; ApplyVolumes(); }

        /// <summary>效果实例在播放前登记，继承同一音量和静音设置。</summary>
        /// <param name="source">SceneEffects本场景新实例的AudioSource。</param>
        /// <param name="baseVolume">Prefab原始音量，不能传已经乘过本设置的音量。</param>
        public void RegisterEffectSource(AudioSource source, float baseVolume)
        {
            if (source == null || source.gameObject.scene != gameObject.scene) return;
            if (!effectSources.ContainsKey(source)) effectSources.Add(source, Mathf.Clamp01(baseVolume));
            source.volume = effectSources[source] * volume; source.mute = muted;
        }

        /// 已保存按钮可绑定此方法，不参与领域命令。
        public void PlayUiClick() => Play("UiClick", 0.35f);
        private void OnEnable() { if (Application.isPlaying) Bind(); }
        private void OnDisable()
        {
            Unbind(); if (music != null) music.Stop(); if (sfx != null) sfx.Stop();
            foreach (var source in effectSources.Keys) if (source != null) source.Stop();
            effectSources.Clear();
        }
        private void OnValidate() { volume = Mathf.Clamp01(volume); if (Application.isPlaying) ApplyVolumes(); }
        private void Update()
        {
            if (Time.unscaledTime < nextSourceCleanup) return;
            nextSourceCleanup = Time.unscaledTime + 2; ApplyVolumes();
        }
        private void Bind()
        {
            if (bound || owner == null) return;
            bound = true; ResetBaseline(owner.Game.State); owner.Changed += Refresh;
            if (music != null)
            {
                music.clip = FindClip("ClubLoop"); music.loop = true; music.playOnAwake = false;
                if (music.clip != null) music.Play();
            }
            ApplyVolumes();
        }
        private void Unbind() { if (bound && owner != null) owner.Changed -= Refresh; bound = false; }
        private void ResetBaseline(CasinoAdventureState state)
        {
            runId = state?.RunId; settledSequence = state?.SettledRoundSequence ?? 0; ending = state?.Ending ?? CasinoAdventureEnding.None;
            effects.Clear(); requests.Clear();
            if (state == null) return;
            foreach (var effect in state.Effects) if (effect != null) effects.Add(effect.Id);
            foreach (var request in state.ProcessedRequests) if (request != null) requests.Add(request.RequestId);
        }
        private void Refresh()
        {
            var state = owner.Game.State;
            if (owner.Game.IsRestoring) { ResetBaseline(state); return; }
            if (state == null) { ResetBaseline(null); return; }
            if (runId != state.RunId) { runId = state.RunId; settledSequence = 0; ending = CasinoAdventureEnding.None; effects.Clear(); requests.Clear(); }
            // 较旧快照建立新基线；正常原子结算只会递增。
            if (state.SettledRoundSequence < settledSequence) { ResetBaseline(state); return; }
            foreach (var request in state.ProcessedRequests)
            {
                if (request == null || !requests.Add(request.RequestId) || request.Result == null || !request.Result.Success) continue;
                if (request.Fingerprint != null && request.Fingerprint.StartsWith("bet:", StringComparison.Ordinal)) Play("MachineBegin", 0.6f);
                else if (request.Fingerprint != null && request.Fingerprint.StartsWith("purchase:", StringComparison.Ordinal)) Play("Coin", 0.35f);
            }
            if (state.SettledRoundSequence > settledSequence)
            {
                settledSequence = state.SettledRoundSequence;
                Play(state.LastRoundPayout > state.LastRoundCost ? "Win" : "Lose", 0.7f);
            }
            foreach (var effect in state.Effects)
            {
                if (effect == null || !effects.Add(effect.Id)) continue;
                if (effect.EffectKind == "TaskCompleted") Play("TaskComplete", 0.65f);
                else if (effect.EffectKind == "EventAnnounce") Play("Event", 0.55f);
                else if (effect.EffectKind == "ShieldBlocked" || effect.EffectKind == "EnvironmentProtected") Play("Charge", 0.45f);
                // 整蛊实例音源由SceneEffects播放并登记，不在此处再叠播一遍。
            }
            if (state.Phase == CasinoAdventurePhase.Ended && state.Ending != CasinoAdventureEnding.None && state.Ending != ending)
            {
                ending = state.Ending;
                Play(ending == CasinoAdventureEnding.TakeOver ? "EndingTakeover" : ending == CasinoAdventureEnding.LeaveWithDignity ? "EndingDignity" : "EndingWithdraw", 0.8f);
            }
        }
        private void Play(string id, float strength)
        { var clip = FindClip(id); if (bound && sfx != null && clip != null && !muted) sfx.PlayOneShot(clip, Mathf.Clamp01(strength)); }
        private AudioClip FindClip(string id)
        { foreach (var entry in clips) if (entry != null && entry.Id == id) return entry.Clip; return null; }
        private void ApplyVolumes()
        {
            if (music != null) { music.volume = volume * 0.16f; music.mute = muted; }
            if (sfx != null) { sfx.volume = volume; sfx.mute = muted; }
            expiredSources.Clear();
            foreach (var pair in effectSources)
            {
                if (pair.Key == null) { expiredSources.Add(pair.Key); continue; }
                pair.Key.volume = pair.Value * volume; pair.Key.mute = muted;
            }
            foreach (var source in expiredSources) effectSources.Remove(source);
        }
    }
}
