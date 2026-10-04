using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Hotfix.HowToFish
{
    /// 本地三槽存档。损坏主档只报告恢复选项，不自动覆盖用户数据。
    public sealed class HowToFishSaveStore
    {
        private const int MaximumFileBytes = 4 * 1024 * 1024;
        private readonly string directory;
        /// 最近一次共享皮肤恢复或待同步说明；供世界入口显示，不隐藏备份恢复。
        public string SkinProfileNotice { get; private set; }

        /// <summary>创建本地存档仓库。</summary>
        /// <param name="directory">专属于此 Demo 的存档目录；调用方使用 persistentDataPath 下的子目录。</param>
        public HowToFishSaveStore(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("存档目录不能为空。", nameof(directory));
            this.directory = Path.GetFullPath(directory);
        }

        /// <summary>读取存档并报告是否可以恢复备份。</summary>
        /// <param name="slot">存档槽，范围 0–2。</param>
        public HowToFishLoadResult Load(int slot)
        {
            var path = SlotPath(slot);
            if (!File.Exists(path))
            {
                if (TryRead(path + ".bak", out var backup, out var backupError))
                    return new HowToFishLoadResult(HowToFishLoadStatus.RecoveryAvailable, backup, "主档缺失，可恢复上一份备份。");
                if (backupError is NotSupportedException)
                    return new HowToFishLoadResult(HowToFishLoadStatus.UnsupportedVersion, null, backupError.Message);
                if (backupError != null || File.Exists(path + ".bak"))
                    return new HowToFishLoadResult(HowToFishLoadStatus.Corrupt, null, backupError?.Message ?? "备份不可读取。");
                return new HowToFishLoadResult(HowToFishLoadStatus.Empty, null, null);
            }
            if (TryRead(path, out var data, out var error))
                return new HowToFishLoadResult(HowToFishLoadStatus.Ready, data, null);
            // 新版本存档不降级成旧备份，避免旧客户端丢失新进度。
            if (error is NotSupportedException)
                return new HowToFishLoadResult(HowToFishLoadStatus.UnsupportedVersion, null, error.Message);
            if (TryRead(path + ".bak", out var recovery, out _))
                return new HowToFishLoadResult(HowToFishLoadStatus.RecoveryAvailable, recovery, error?.Message ?? "主档不可读取，可恢复备份。");
            return new HowToFishLoadResult(HowToFishLoadStatus.Corrupt, null, error?.Message ?? "存档不可读取。");
        }

        /// <summary>原子保存并保留上一份有效主档；损坏主档必须先显式恢复。</summary>
        /// <param name="slot">存档槽，范围 0–2。</param>
        /// <param name="data">安全检查点快照。</param>
        public void Save(int slot, HowToFishSaveData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            data.Validate();
            var path = SlotPath(slot);
            var current = Load(slot);
            if (current.Status != HowToFishLoadStatus.Empty && current.Status != HowToFishLoadStatus.Ready)
                throw new IOException("目标存档需要恢复或处理，拒绝自动覆盖。");
            var profile = MergeSkinUnlocks(data);
            // 槽快照是此次玩法操作的提交点；共享档案失败可从它重新合并，不能回滚已经消费的鱼获。
            WritePayload(path, JsonUtility.ToJson(data));
            try { SaveSkinProfile(profile); }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            { SkinProfileNotice = "皮肤奖励已随航程保存，共享档案待下次进入时同步：" + exception.Message; }
        }

        /// <summary>新世界与继续世界共用：合并玩家档案和三槽有效解锁，补齐未完成的共享写入。</summary>
        /// <param name="data">即将建立会话的槽快照；不修改该槽的装备与船体选择。</param>
        /// <returns>已经完成合并与保存的共享档案，包含全局服装选择。</returns>
        public HowToFishSkinProfile LoadSharedSkins(HowToFishSaveData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            data.Validate();
            var profile = MergeSkinUnlocks(data);
            SaveSkinProfile(profile);
            return profile;
        }

        private HowToFishSkinProfile MergeSkinUnlocks(HowToFishSaveData data)
        {
            var profile = LoadSkinProfile(out bool recovered);
            SkinProfileNotice = recovered ? "共享皮肤档案已从备份恢复，损坏原件已保留。" : null;
            var merged = new HashSet<string>(profile.unlockedSkins, StringComparer.Ordinal);
            var outfits = new HashSet<string>(profile.unlockedOutfits, StringComparer.Ordinal);
            merged.UnionWith(data.unlockedSkins);
            outfits.UnionWith(data.unlockedOutfits);
            for (int slot = 0; slot < 3; slot++)
            {
                var saved = Load(slot);
                if (saved.Data != null)
                {
                    merged.UnionWith(saved.Data.unlockedSkins);
                    outfits.UnionWith(saved.Data.unlockedOutfits);
                }
            }
            profile.unlockedSkins = new List<string>(merged);
            profile.unlockedSkins.Sort(StringComparer.Ordinal);
            data.unlockedSkins = new List<string>(profile.unlockedSkins);
            profile.unlockedOutfits = new List<string>(outfits);
            profile.unlockedOutfits.Sort(StringComparer.Ordinal);
            data.unlockedOutfits = new List<string>(profile.unlockedOutfits);
            return profile;
        }

        /// <summary>读取三槽共享皮肤；损坏主档只从有效备份恢复，保留损坏原件并报告恢复。</summary>
        /// <param name="recovered">是否执行了备份恢复；调用方须显示此状态。</param>
        public HowToFishSkinProfile LoadSkinProfile(out bool recovered)
        {
            recovered = false;
            string path = Path.Combine(directory, "PlayerSkins.json");
            if (TryReadProfile(path, out var profile, out var error)) return profile;
            if (error is NotSupportedException) throw error;
            if (TryReadProfile(path + ".bak", out profile, out var backupError))
            {
                RestoreFileFromBackup(path);
                recovered = true;
                return profile;
            }
            if (error != null || backupError != null || File.Exists(path) || File.Exists(path + ".bak"))
                throw new IOException("共享皮肤主档与备份不可读取，拒绝覆盖。", error ?? backupError);
            return new HowToFishSkinProfile();
        }

        /// <summary>原子保存共享解锁并保留备份；损坏档案必须先通过读取入口恢复，不能被新数据覆盖。</summary>
        /// <param name="profile">完整解锁与全局服装选择；装备和船的当前皮肤仍保存在各槽。</param>
        public void SaveSkinProfile(HowToFishSkinProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            profile.Validate();
            string path = Path.Combine(directory, "PlayerSkins.json");
            if (File.Exists(path))
            {
                if (!TryReadProfile(path, out var current, out var error)) throw new IOException("共享皮肤档案需要恢复，拒绝覆盖。", error);
                foreach (string skin in current.unlockedSkins)
                    if (!profile.unlockedSkins.Contains(skin)) throw new InvalidOperationException("共享皮肤保存不能删除已有解锁。");
                foreach (string outfit in current.unlockedOutfits)
                    if (!profile.unlockedOutfits.Contains(outfit)) throw new InvalidOperationException("共享服装保存不能删除已有解锁。");
                if (current.unlockedSkins.Count == profile.unlockedSkins.Count &&
                    current.unlockedOutfits.Count == profile.unlockedOutfits.Count &&
                    current.selectedOutfitId == profile.selectedOutfitId) return;
            }
            else if (File.Exists(path + ".bak")) throw new IOException("共享皮肤主档缺失，须先恢复备份。");
            WritePayload(path, JsonUtility.ToJson(profile));
        }

        private void WritePayload(string path, string payload)
        {
            var envelope = new SaveEnvelope { format = 1, payload = payload, checksum = Checksum(payload) };
            var bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(envelope));
            if (bytes.Length > MaximumFileBytes) throw new IOException("存档超过允许大小。");
            Directory.CreateDirectory(directory);
            var temporary = path + ".tmp";
            try
            {
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
                else File.Move(temporary, path);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }

        /// <summary>用户选择恢复后保留损坏原件，并原子恢复已校验的备份。</summary>
        /// <param name="slot">存档槽，范围 0–2。</param>
        public void RestoreBackup(int slot)
        {
            var path = SlotPath(slot);
            if (Load(slot).Status != HowToFishLoadStatus.RecoveryAvailable)
                throw new InvalidOperationException("此存档没有待恢复的有效备份。");
            var backupPath = path + ".bak";
            if (!TryRead(backupPath, out _, out var error)) throw new IOException("备份已不可读。", error);
            RestoreFileFromBackup(path);
        }

        private static void RestoreFileFromBackup(string path)
        {
            var backupPath = path + ".bak";
            var temporary = path + ".restore";
            try
            {
                File.Copy(backupPath, temporary, true);
                if (File.Exists(path))
                    File.Replace(temporary, path, path + ".corrupt-" + Guid.NewGuid().ToString("N"));
                else File.Move(temporary, path);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }

        private string SlotPath(int slot)
        {
            if (slot < 0 || slot > 2) throw new ArgumentOutOfRangeException(nameof(slot));
            return Path.Combine(directory, "Slot" + (slot + 1) + ".json");
        }

        private static bool TryRead(string path, out HowToFishSaveData data, out Exception error)
        {
            data = null;
            error = null;
            if (!File.Exists(path)) return false;
            try
            {
                data = JsonUtility.FromJson<HowToFishSaveData>(ReadPayload(path));
                if (data == null) throw new FormatException("存档内容为空。");
                data.Validate();
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException ||
                                              exception is ArgumentException || exception is FormatException || exception is NotSupportedException)
            {
                data = null;
                error = exception;
                return false;
            }
        }

        private static bool TryReadProfile(string path, out HowToFishSkinProfile profile, out Exception error)
        {
            profile = null;
            error = null;
            if (!File.Exists(path)) return false;
            try
            {
                profile = JsonUtility.FromJson<HowToFishSkinProfile>(ReadPayload(path));
                if (profile == null) throw new FormatException("共享皮肤档案内容为空。");
                profile.Validate();
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException ||
                                              exception is ArgumentException || exception is FormatException || exception is NotSupportedException)
            { profile = null; error = exception; return false; }
        }

        private static string ReadPayload(string path)
        {
            if (new FileInfo(path).Length > MaximumFileBytes) throw new FormatException("存档大小异常。");
            var envelope = JsonUtility.FromJson<SaveEnvelope>(File.ReadAllText(path, Encoding.UTF8));
            if (envelope == null || envelope.format < 1) throw new FormatException("存档头无效。");
            if (envelope.format != 1) throw new NotSupportedException("存档由较新版本创建。");
            if (string.IsNullOrEmpty(envelope.payload) || !string.Equals(envelope.checksum, Checksum(envelope.payload), StringComparison.Ordinal))
                throw new FormatException("存档校验失败。");
            return envelope.payload;
        }

        private static string Checksum(string payload)
        {
            using (var sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(payload)));
        }

        [Serializable]
        private sealed class SaveEnvelope
        {
            public int format;
            public string payload;
            public string checksum;
        }
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
            if (version != 1) throw new NotSupportedException("不支持的共享皮肤档案版本。");
            HowToFishSaveData.ValidateUnlockedSkins(unlockedSkins);
            unlockedOutfits ??= new List<string>();
            HowToFishSaveData.ValidateUnlockedOutfits(unlockedOutfits);
            if (string.IsNullOrEmpty(selectedOutfitId)) selectedOutfitId = HowToFishOutfitCatalog.DefaultId;
            if (!HowToFishOutfitCatalog.IsUnlocked(selectedOutfitId, unlockedOutfits)) throw new FormatException("所选服装无效或尚未解锁。");
        }
    }

    /// 读档状态；RecoveryAvailable 必须由 UI 提供明确的恢复选择。
    public enum HowToFishLoadStatus { Empty, Ready, RecoveryAvailable, Corrupt, UnsupportedVersion }

    /// 读档结果，错误信息可显示于存档菜单。
    public readonly struct HowToFishLoadResult
    {
        public HowToFishLoadStatus Status { get; }
        public HowToFishSaveData Data { get; }
        public string Error { get; }

        internal HowToFishLoadResult(HowToFishLoadStatus status, HowToFishSaveData data, string error)
        {
            Status = status;
            Data = data;
            Error = error;
        }
    }
}
