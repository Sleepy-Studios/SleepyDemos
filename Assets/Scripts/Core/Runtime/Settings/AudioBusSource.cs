using System;
using UnityEngine;

namespace Core.Runtime.Settings
{
    /// 普通音源的公共分类增益；运行时音量变化通过 SetLocalVolume 保留分类乘法。
    [DisallowMultipleComponent, RequireComponent(typeof(AudioSource))]
    public sealed class AudioBusSource : MonoBehaviour
    {
        [SerializeField] private AudioBus bus = AudioBus.Effects;
        private AudioSource source;
        private float localVolume;
        private bool initialized;

        /// 此音源的公共总线。
        public AudioBus Bus => bus;

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            localVolume = source.volume;
            initialized = true;
        }

        private void OnEnable()
        {
            AudioSettingsRuntime.Changed += RefreshGain;
            RefreshGain();
        }

        private void OnDisable()
        {
            AudioSettingsRuntime.Changed -= RefreshGain;
            if (source != null)
            {
                source.volume = localVolume;
            }
        }

        /// <summary>切换公共分类，不影响播放进度。</summary>
        /// <param name="value">音乐、音效或 UI 分类。</param>
        public void SetBus(AudioBus value)
        {
            if (!Enum.IsDefined(typeof(AudioBus), value))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            bus = value;
            RefreshGain();
        }

        /// <summary>改变音源自身音量，公共增益继续作为乘数。</summary>
        /// <param name="value">零到一的有限值。</param>
        public void SetLocalVolume(float value)
        {
            if (!GraphicsPreferences.FiniteRange(value, 0, 1))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            localVolume = value;
            RefreshGain();
        }

        private void RefreshGain()
        {
            if (initialized && isActiveAndEnabled)
            {
                source.volume = localVolume * AudioSettingsRuntime.GetGain(bus);
            }
        }
    }
}
