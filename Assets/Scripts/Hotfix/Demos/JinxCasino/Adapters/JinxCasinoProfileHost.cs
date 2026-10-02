using System;
using Hotfix.JinxCasino.Persistence;
using Hotfix.JinxCasino.Rules;
using UnityEngine;

namespace Hotfix.JinxCasino.Adapters
{
    public sealed partial class JinxCasinoController
    {
        private CasinoProfile profile;
        private CasinoProfileStore profileStore;
        private string profileCommittedRun;
        private float profileRetryAfter;
        private string profileStatus = "正式旅程结束后记录成长；练习不计。";
        private float profileReadRetryAfter;

        /// 永久档案副本；读取失败返回null，绝不以空档案覆盖损坏文件。
        public CasinoProfileData ProfileData
        {
            get { return EnsureProfile() ? profile.Data : null; }
        }
        /// 档案读取、保存及装备的明确反馈。
        public string ProfileStatus => profileStatus;
        /// 永久声望等级，不改变局内收益或移动能力。
        public long ProfileLevel => EnsureProfile() ? profile.Level : 1;

        /// <summary>注入专有档案存储，测试和不同本地用户与三个旅程存档隔离。</summary>
        /// <param name="store">独立档案存储，不改变当前冒险钱包。</param>
        public void SetLocalProfileStore(CasinoProfileStore store)
        {
            profileStore = store ?? throw new ArgumentNullException(nameof(store)); profile = null;
            profileCommittedRun = null; profileRetryAfter = profileReadRetryAfter = 0;
        }

        /// <summary>保存成功后才提交装备候选；未解锁或磁盘失败保留原装备。</summary>
        /// <param name="colorId">解锁配色ID，null保留当前。</param>
        /// <param name="hatId">解锁帽子ID，null保留当前。</param>
        /// <param name="emoteId">解锁表情ID，null保留当前。</param>
        /// <param name="titleId">解锁称号ID，null保留当前。</param>
        public bool EquipProfile(string colorId = null, string hatId = null, string emoteId = null, string titleId = null)
        {
            if (!EnsureProfile()) return false;
            try
            {
                var candidate = CasinoProfile.Restore(profile.ToJson());
                if (!candidate.Equip(colorId, hatId, emoteId, titleId)) { profileStatus = "该外观尚未解锁，原装备保留。"; Changed?.Invoke(); return false; }
                ProfileStore.Save(candidate); profile = candidate; profileStatus = "装扮已保存。";
                Changed?.Invoke(); return true;
            }
            catch (Exception exception) { profileStatus = "装扮保存失败：" + exception.Message; Changed?.Invoke(); return false; }
        }

        private CasinoProfileStore ProfileStore => profileStore ?? (profileStore = new CasinoProfileStore());
        private bool EnsureProfile()
        {
            if (profile != null) return true;
            if (Time.unscaledTime < profileReadRetryAfter) return false;
            try { profile = ProfileStore.LoadOrCreate(); return true; }
            catch (Exception exception) { profileStatus = "档案读取失败，已有文件保留：" + exception.Message; profileReadRetryAfter = Time.unscaledTime + 5; return false; }
        }

        private void RecordFinishedProfile()
        {
            if (adventureState == null || adventureState.Phase != CasinoAdventurePhase.Ended || adventureState.Mode == CasinoAdventureMode.Practice ||
                profileCommittedRun == adventureState.RunId || Time.unscaledTime < profileRetryAfter) return;
            if (!EnsureProfile()) { profileRetryAfter = Time.unscaledTime + 5; return; }
            try
            {
                var candidate = CasinoProfile.Restore(profile.ToJson());
                if (candidate.RecordFinishedRun(adventureState))
                {
                    // 不先修改当前档案，否则写盘失败后RunId去重会吞掉后续重试。
                    ProfileStore.Save(candidate); profile = candidate;
                    profileStatus = "本次正式旅程已记录成长与图鉴。";
                }
                profileCommittedRun = adventureState.RunId;
            }
            catch (Exception exception)
            {
                profileStatus = "成长保存失败，稍后重试：" + exception.Message;
                profileRetryAfter = Time.unscaledTime + 5;
            }
        }
    }
}
