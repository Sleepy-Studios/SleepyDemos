using System;
using Hotfix.JinxCasino.Persistence;

namespace Hotfix.JinxCasino.UI
{
    /// 本机偏好的加载、预览与明确保存；输入、音量和布局由订阅者应用。
    public sealed class JinxCasinoLocalSettings
    {
        private CasinoLocalPreferences value = new CasinoLocalPreferences();
        private CasinoLocalPreferencesStore store;

        /// 设置面板与宿主读取独立副本，不直接修改当前预览值。
        public CasinoLocalPreferences Value => value.Copy();
        public string Warning => store.LastLoadWarning;

        /// 只在成功加载、应用预览或保存后通知，不随游戏时钟触发。
        public event Action Changed;

        /// <summary>绑定偏好存储，不读取或写入本机记录。</summary>
        /// <param name="store">可注入独立测试键的存储；为空绑定正式本机键。</param>
        public JinxCasinoLocalSettings(CasinoLocalPreferencesStore store = null)
        {
            this.store = store ?? new CasinoLocalPreferencesStore();
        }

        /// <summary>设置入口显式读取偏好；损坏值保持在原键直到用户确认保存。</summary>
        /// <param name="store">指定时替换当前存储；为空继续使用已绑定的存储。</param>
        public void Load(CasinoLocalPreferencesStore store = null)
        {
            if (store != null) this.store = store;
            Apply(this.store.Load());
        }

        /// <summary>应用偏好副本并通知预览，不写盘；取消时应用进入面板前的副本。</summary>
        /// <param name="preferences">当前版本、有限合法的输入、布局和音量偏好。</param>
        public void Apply(CasinoLocalPreferences preferences)
        {
            if (preferences == null || !preferences.IsValid)
                throw new ArgumentException("本地偏好无效。", nameof(preferences));
            value = preferences.Copy();
            Changed?.Invoke();
        }

        /// <summary>用户明确确认后先成功写盘，再更新当前副本并通知订阅者。</summary>
        /// <param name="preferences">本次明确确认的合法偏好；保存失败保留当前预览值。</param>
        public void Save(CasinoLocalPreferences preferences)
        {
            if (preferences == null || !preferences.IsValid)
                throw new ArgumentException("本地偏好无效。", nameof(preferences));
            var saved = preferences.Copy();
            store.Save(saved);
            Apply(saved);
        }
    }
}
