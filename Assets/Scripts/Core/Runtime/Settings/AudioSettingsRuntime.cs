using System;
using UnityEngine;

namespace Core.Runtime.Settings
{
    /// 公共音频分类；增益与 Demo 本地分类音量相乘。
    public enum AudioBus { Music, Effects, Ui }

    /// 公共音量只改变增益，不停止播放或拥有 AudioListener。
    public static class AudioSettingsRuntime
    {
        private static AudioPreferences current = new();

        /// 音量实际变化后通知普通音源和独立 DSP 宿主。
        public static event Action Changed;

        /// <summary>应用独立音量候选，不保存偏好。</summary>
        /// <param name="preferences">零到一的有限线性增益。</param>
        public static void Apply(AudioPreferences preferences)
        {
            if (preferences == null)
            {
                throw new ArgumentNullException(nameof(preferences));
            }

            preferences.Validate();
            current = preferences.Copy();
            PublishChanged();
        }

        private static void PublishChanged()
        {
            if (Changed == null)
            {
                return;
            }

            foreach (Action callback in Changed.GetInvocationList())
            {
                try
                {
                    callback();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

        /// <summary>查询主音量、分类音量与静音合成后的线性增益。</summary>
        /// <param name="bus">音乐、音效或 UI 分类。</param>
        /// <returns>零到一的最终公共增益；不含 Demo 自身音量。</returns>
        public static float GetGain(AudioBus bus)
        {
            float gain;
            switch (bus)
            {
                case AudioBus.Music:
                    gain = current.Music;
                    break;
                case AudioBus.Effects:
                    gain = current.Effects;
                    break;
                case AudioBus.Ui:
                    gain = current.Ui;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(bus));
            }

            return current.Muted ? 0 : current.Master * gain;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            current = new AudioPreferences();
            Changed = null;
        }
    }
}
