using System;
using Hotfix.JinxCasino.Persistence;
using UnityEngine;

namespace Hotfix.JinxCasino.Adapters
{
    public sealed partial class JinxCasinoController
    {
        private CasinoLocalPreferences localPreferences = new CasinoLocalPreferences();
        private CasinoLocalPreferencesStore preferencesStore;
        /// 设置面板或宿主读到的独立本机偏好副本，不属于规则资金状态。
        public CasinoLocalPreferences LocalPreferences => localPreferences.Copy();
        public string LocalPreferencesWarning => preferencesStore?.LastLoadWarning;
        /// 只在加载、预览或用户保存偏好时通知，不随游戏时钟触发。
        public event Action LocalPreferencesChanged;

        /// <summary>设置入口绑定时读取偏好；损坏值保持在原键直到用户确认保存。</summary>
        /// <param name="store">可注入独立测试键的存储；为空使用正式本机键。</param>
        public void LoadLocalPreferences(CasinoLocalPreferencesStore store = null)
        {
            preferencesStore = store ?? new CasinoLocalPreferencesStore();
            ApplyLocalPreferences(preferencesStore.Load());
        }

        /// <summary>预览偏好而不写盘；取消设置时应用进入面板前的副本。</summary>
        /// <param name="preferences">经过有限值和版本校验的输入、布局和音量偏好。</param>
        public void ApplyLocalPreferences(CasinoLocalPreferences preferences)
        {
            if (preferences == null || !preferences.IsValid) throw new ArgumentException("本地偏好无效。", nameof(preferences));
            localPreferences = preferences.Copy();
            immersionInput?.ApplySettings(localPreferences.ToInputSettings());
            GetComponent<JinxCasinoAudioDirector>()?.SetVolume(localPreferences.Volume, localPreferences.Muted);
            LocalPreferencesChanged?.Invoke();
        }

        /// <summary>用户点击保存后先成功写盘，再更新本机输入和音频。</summary>
        /// <param name="preferences">本次明确确认的偏好副本。</param>
        public void SaveLocalPreferences(CasinoLocalPreferences preferences)
        {
            preferencesStore ??= new CasinoLocalPreferencesStore();
            preferencesStore.Save(preferences);
            ApplyLocalPreferences(preferences);
        }

    }
}
