using System;
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
            var payload = JsonUtility.ToJson(data);
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
                if (new FileInfo(path).Length > MaximumFileBytes) throw new FormatException("存档大小异常。");
                var envelope = JsonUtility.FromJson<SaveEnvelope>(File.ReadAllText(path, Encoding.UTF8));
                if (envelope == null || envelope.format < 1) throw new FormatException("存档头无效。");
                if (envelope.format != 1) throw new NotSupportedException("存档由较新版本创建。");
                if (string.IsNullOrEmpty(envelope.payload) || !string.Equals(envelope.checksum, Checksum(envelope.payload), StringComparison.Ordinal))
                    throw new FormatException("存档校验失败。");
                data = JsonUtility.FromJson<HowToFishSaveData>(envelope.payload);
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
