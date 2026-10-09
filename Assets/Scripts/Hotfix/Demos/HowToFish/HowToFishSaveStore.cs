using System;
using System.Collections.Generic;
using System.IO;
using Core.Runtime;
using UnityEngine;

namespace Hotfix.HowToFish
{
    /// 钓鱼三槽与共享外观的业务仓库；文件读写由公共本地存储负责。
    public sealed class HowToFishSaveStore
    {
        private readonly string directory;
        /// 共享档案保存失败时的提示；航程快照仍是本次操作的提交点。
        public string SkinProfileNotice { get; private set; }

        /// <summary>绑定专属于本 Demo 的存档目录。</summary>
        /// <param name="directory">正式目录或独立测试目录。</param>
        public HowToFishSaveStore(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory))
                throw new ArgumentException("存档目录不能为空。", nameof(directory));
            this.directory = directory;
        }

        /// <summary>读取合法槽快照；缺失或损坏内容按新航程处理。</summary>
        /// <param name="slot">范围 0–2。</param>
        public HowToFishLoadResult Load(int slot)
        {
            string json = LocalDataManager.LoadFile(LocalDataKeys.HowToFishSlot(slot), directory);
            if (json == null)
                return new HowToFishLoadResult(HowToFishLoadStatus.Empty, null);
            try
            {
                var data = new HowToFishSaveData
                {
                    version = 0,
                    equipmentSlots = null,
                    unlockedSkins = null,
                    unlockedOutfits = null,
                    inventory = null,
                    completedQuests = null,
                    discoveredCreatures = null,
                    defeatedCreatures = null,
                    defeatedDripCreatures = null,
                    worldItems = null
                };
                JsonUtility.FromJsonOverwrite(json, data);
                data.Validate();
                return new HowToFishLoadResult(HowToFishLoadStatus.Ready, data);
            }
            catch (Exception exception) when (IsInvalidData(exception))
            {
                return new HowToFishLoadResult(HowToFishLoadStatus.Empty, null);
            }
        }

        /// <summary>保存合法检查点并同步跨槽外观解锁。</summary>
        /// <param name="slot">范围 0–2。</param>
        /// <param name="data">本次安全检查点。</param>
        public void Save(int slot, HowToFishSaveData data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            data.Validate();
            string path = LocalDataKeys.HowToFishSlot(slot);
            var profile = MergeSkinUnlocks(data);
            LocalDataManager.SaveFile(path, JsonUtility.ToJson(data), directory);
            try
            {
                SaveSkinProfile(profile);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                SkinProfileNotice = "皮肤奖励已随航程保存，共享档案待下次进入时同步：" + exception.Message;
            }
        }

        /// <summary>建立航程时合并共享档案与三个有效槽的外观解锁。</summary>
        /// <param name="data">即将使用的航程快照。</param>
        public HowToFishSkinProfile LoadSharedSkins(HowToFishSaveData data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            data.Validate();
            var profile = MergeSkinUnlocks(data);
            SaveSkinProfile(profile);
            return profile;
        }

        private HowToFishSkinProfile MergeSkinUnlocks(HowToFishSaveData data)
        {
            var profile = LoadSkinProfile();
            SkinProfileNotice = null;
            var skins = new HashSet<string>(profile.unlockedSkins, StringComparer.Ordinal);
            var outfits = new HashSet<string>(profile.unlockedOutfits, StringComparer.Ordinal);
            skins.UnionWith(data.unlockedSkins);
            outfits.UnionWith(data.unlockedOutfits);
            for (int slot = 0; slot < 3; slot++)
            {
                var saved = Load(slot).Data;
                if (saved == null)
                    continue;
                skins.UnionWith(saved.unlockedSkins);
                outfits.UnionWith(saved.unlockedOutfits);
            }

            profile.unlockedSkins = new List<string>(skins);
            profile.unlockedSkins.Sort(StringComparer.Ordinal);
            profile.unlockedOutfits = new List<string>(outfits);
            profile.unlockedOutfits.Sort(StringComparer.Ordinal);
            data.unlockedSkins = new List<string>(profile.unlockedSkins);
            data.unlockedOutfits = new List<string>(profile.unlockedOutfits);
            return profile;
        }

        /// 读取合法共享档案，缺失或损坏时使用新用户档案。
        public HowToFishSkinProfile LoadSkinProfile()
        {
            string json = LocalDataManager.LoadFile(LocalDataKeys.HowToFishSkins, directory);
            if (json == null)
                return new HowToFishSkinProfile();
            try
            {
                var profile = new HowToFishSkinProfile
                {
                    version = 0
                };
                JsonUtility.FromJsonOverwrite(json, profile);
                profile.Validate();
                return profile;
            }
            catch (Exception exception) when (IsInvalidData(exception))
            {
                return new HowToFishSkinProfile();
            }
        }

        /// <summary>保存共享解锁与服装选择，合法已有解锁不能被候选删除。</summary>
        /// <param name="profile">完整候选共享档案。</param>
        public void SaveSkinProfile(HowToFishSkinProfile profile)
        {
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));
            profile.Validate();
            var current = LoadSkinProfile();
            foreach (string skin in current.unlockedSkins)
                if (!profile.unlockedSkins.Contains(skin))
                    throw new InvalidOperationException("共享皮肤保存不能删除已有解锁。");
            foreach (string outfit in current.unlockedOutfits)
                if (!profile.unlockedOutfits.Contains(outfit))
                    throw new InvalidOperationException("共享服装保存不能删除已有解锁。");
            LocalDataManager.SaveFile(LocalDataKeys.HowToFishSkins, JsonUtility.ToJson(profile), directory);
        }

        private static bool IsInvalidData(Exception exception) => exception is ArgumentException || exception is FormatException || exception is NotSupportedException;
    }

    /// 本机玩家跨世界共享的外观解锁与服装选择；槽快照镜像解锁，不覆盖全局所选服装。
    [Serializable]
    public sealed class HowToFishSkinProfile
    {
        public int version = 1;
        public List<string> unlockedSkins = new List<string>();
        public List<string> unlockedOutfits = new List<string>();
        public string selectedOutfitId = HowToFishOutfitCatalog.DefaultId;
        /// 拒绝未知版本、非法外观与重复解锁。
        public void Validate()
        {
            if (version != 1)
                throw new NotSupportedException("不支持的共享皮肤档案版本。");
            HowToFishSaveData.ValidateUnlockedSkins(unlockedSkins);
            HowToFishSaveData.ValidateUnlockedOutfits(unlockedOutfits);
            if (string.IsNullOrEmpty(selectedOutfitId))
                selectedOutfitId = HowToFishOutfitCatalog.DefaultId;
            if (!HowToFishOutfitCatalog.IsUnlocked(selectedOutfitId, unlockedOutfits))
                throw new FormatException("所选服装无效或尚未解锁。");
        }
    }

    /// 槽只有有效快照或空槽；损坏内容按空槽处理。
    public enum HowToFishLoadStatus
    {
        Empty,
        Ready
    }

    /// 存档页面读取的领域快照。
    public readonly struct HowToFishLoadResult
    {
        public HowToFishLoadStatus Status { get; }
        public HowToFishSaveData Data { get; }

        internal HowToFishLoadResult(HowToFishLoadStatus status, HowToFishSaveData data)
        {
            Status = status;
            Data = data;
        }
    }
}
