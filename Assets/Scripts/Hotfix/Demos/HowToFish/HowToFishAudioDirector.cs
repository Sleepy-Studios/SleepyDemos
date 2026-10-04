using System;
using UnityEngine;

namespace Hotfix.HowToFish
{
    /// 与保存的原创音频数组一一对应；不使用原作录音。
    public enum HowToFishSound
    {
        SeaLoop, WindLoop, Footstep, Splash, Cast, ReelLoop, FishStruggleLoop,
        MeleeSwing, GunShot, ShotgunShot, RifleShot, Reload, Explosion, MotorLoop,
        Trade, UiClick, BossEncounter, BossDefeated, Ending, Hit
    }

    /// Demo 场景音效协调；使用保存音源，不在每次动作时创建对象或查找场景。
    public sealed class HowToFishAudioDirector : MonoBehaviour
    {
        [SerializeField] private HowToFishWorld owner;
        [SerializeField] private HowToFishBoat boat;
        [SerializeField] private AudioClip[] clips = Array.Empty<AudioClip>();
        [SerializeField] private AudioSource sea;
        [SerializeField] private AudioSource wind;
        [SerializeField] private AudioSource motor;
        [SerializeField] private AudioSource reel;
        [SerializeField] private AudioSource struggle;
        [SerializeField] private AudioSource ui;
        [SerializeField] private AudioSource[] effects = Array.Empty<AudioSource>();
        [SerializeField, Range(0, 1)] private float volume = .5f;
        [SerializeField] private bool muted;
        private int nextEffect;
        private float nextHitAt;
        private bool bound;
        public float Volume => volume;
        public bool Muted => muted;

        /// <summary>调整本场景声音，不影响 Hub 或全局 AudioListener。</summary>
        /// <param name="value">0至1的整体音量；初始保守值仍待听觉校准。</param>
        /// <param name="isMuted">为 true 时暂停所有本 Demo 声音输出。</param>
        public void SetVolume(float value, bool isMuted = false)
        {
            volume = Mathf.Clamp01(value); muted = isMuted;
            if (ui != null) ui.mute = muted;
            foreach (var source in effects) if (source != null) { source.mute = muted; source.volume = volume; }
        }

        private void OnEnable()
        {
            if (owner == null || owner.Player == null || owner.Player.Fishing == null) return;
            owner.SoundRequested += Play;
            owner.Player.SoundRequested += Play;
            owner.Player.Fishing.SoundRequested += Play;
            owner.Changed += OnWorldChanged;
            bound = true;
            SetVolume(volume, muted);
        }

        private void OnDisable()
        {
            if (bound && owner != null)
            {
                owner.SoundRequested -= Play;
                owner.Changed -= OnWorldChanged;
                if (owner.Player != null)
                {
                    owner.Player.SoundRequested -= Play;
                    if (owner.Player.Fishing != null) owner.Player.Fishing.SoundRequested -= Play;
                }
            }
            bound = false;
            StopGameplay();
            if (ui != null) ui.Stop();
        }

        private void OnWorldChanged()
        {
            if (owner == null || !owner.HasSession || owner.IsPaused) StopGameplay();
        }

        private void Update()
        {
            if (!bound || owner == null || !owner.HasSession || owner.IsPaused || muted)
            { StopGameplay(); return; }
            // 音量、音高和海风强度是自制保守初值，不是原作混音参数。
            SetLoop(sea, HowToFishSound.SeaLoop, true, .16f);
            SetLoop(wind, HowToFishSound.WindLoop, true, .08f);
            SetLoop(motor, HowToFishSound.MotorLoop, boat != null && boat.IsMotorRunning, .2f,
                boat == null ? 1 : .75f + boat.MotorThrottle * .4f);
            bool reeling = owner.Player.Fishing.IsReelingSoundActive;
            SetLoop(reel, HowToFishSound.ReelLoop, reeling, .12f);
            SetLoop(struggle, HowToFishSound.FishStruggleLoop, reeling || owner.Player.HeldItem?.IsAlive == true, .12f);
        }

        private AudioClip Clip(HowToFishSound sound) => (int)sound < clips.Length ? clips[(int)sound] : null;

        private void SetLoop(AudioSource source, HowToFishSound sound, bool active, float gain, float pitch = 1)
        {
            if (source == null) return;
            if (!active) { if (source.isPlaying) source.Stop(); return; }
            source.volume = volume * gain; source.pitch = pitch;
            if (source.clip != Clip(sound)) source.clip = Clip(sound);
            if (!source.isPlaying && source.clip != null) source.Play();
        }

        private void Play(HowToFishSound sound, Vector3 position)
        {
            if (muted || owner == null) return;
            if (sound == HowToFishSound.Hit)
            {
                if (Time.time < nextHitAt) return;
                nextHitAt = Time.time + .08f;
            }
            var clip = Clip(sound);
            if (clip == null) return;
            if (sound == HowToFishSound.UiClick || sound == HowToFishSound.Ending)
            {
                if (ui != null) { ui.volume = volume; ui.PlayOneShot(clip, sound == HowToFishSound.Ending ? .3f : .2f); }
                return;
            }
            if (!owner.HasSession || owner.IsPaused || effects.Length == 0) return;
            var source = effects[nextEffect++ % effects.Length];
            if (source == null) return;
            // 保存好的有限音源轮转，满载时截断最旧音效；枪声只由一次真实开火触发，不逐弹丸发声。
            source.Stop(); source.transform.position = position; source.volume = volume;
            source.PlayOneShot(clip, sound == HowToFishSound.Explosion ? .4f : .3f);
        }

        private void StopGameplay()
        {
            if (sea != null && sea.isPlaying) sea.Stop();
            if (wind != null && wind.isPlaying) wind.Stop();
            if (motor != null && motor.isPlaying) motor.Stop();
            if (reel != null && reel.isPlaying) reel.Stop();
            if (struggle != null && struggle.isPlaying) struggle.Stop();
            foreach (var source in effects) if (source != null && source.isPlaying) source.Stop();
        }
    }
}
