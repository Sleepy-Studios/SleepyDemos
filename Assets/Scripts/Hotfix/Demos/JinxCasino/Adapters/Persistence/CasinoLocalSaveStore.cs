using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Hotfix.JinxCasino.Rules;
using UnityEngine;

namespace Hotfix.JinxCasino.Adapters.Persistence
{
    /// 三个独立本地存档槽；领域快照和Unity持久化路径的边界，不保存网络账号或App ID。
    public sealed class CasinoLocalSaveStore
    {
        private const int MaximumFileBytes = 8 * 1024 * 1024;
        private readonly string rootDirectory;

        /// <summary>创建存档访问器，测试可指定独立临时目录。</summary>
        /// <param name="directory">空值使用Application.persistentDataPath下JinxCasino目录，不读写项目资源。</param>
        public CasinoLocalSaveStore(string directory = null)
        {
            rootDirectory = Path.GetFullPath(string.IsNullOrWhiteSpace(directory)
                ? Path.Combine(Application.persistentDataPath, "JinxCasino") : directory);
        }

        /// <summary>将完整规则快照原子写入指定槽，并保留上一次版本作为备份。</summary>
        /// <param name="slot">1到3，超出范围拒绝。</param>
        /// <param name="session">当前冒险聚合；包含未结束机台及已处理请求，不能仅保存钱包。</param>
        public void Save(int slot, CasinoAdventureSession session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            var state = session.CaptureState();
            string payload = session.ToSnapshotJson();
            var envelope = new CasinoSaveEnvelope
            {
                Version = 1, SavedUtc = DateTime.UtcNow.ToString("o"), Payload = payload,
                Checksum = Hash(payload), Mode = state.Mode.ToString(), StageIndex = state.StageIndex, Coins = state.Coins
            };
            string path = GetSlotPath(slot);
            bool validPrimary = false;
            if (File.Exists(path))
            {
                try
                {
                    var previous = ReadEnvelope(path);
                    validPrimary = true;
                    // 退出导航和销毁均会保存；相同完整快照不滚动备份，保留上一个不同检查点。
                    if (previous.Checksum == envelope.Checksum && previous.Payload == payload) return;
                }
                catch (Exception exception) when (IsRecoverableReadError(exception)) { }
            }
            Directory.CreateDirectory(rootDirectory);
            string temporary = path + ".new-" + Guid.NewGuid().ToString("N");
            File.WriteAllText(temporary, JsonUtility.ToJson(envelope), new UTF8Encoding(false));
            // 先验证刚写入的数据，再替换现有槽；中途断电只会留下本次临时文件，原槽仍可读。
            ReadEnvelope(temporary);
            if (File.Exists(path))
            {
                // 从备份恢复后，损坏主文件不能覆盖唯一有效的备份。
                File.Replace(temporary, path, validPrimary ? path + ".bak" : null);
            }
            else File.Move(temporary, path);
        }

        /// <summary>读取并校验指定槽；主文件损坏时尝试上一次原子写入的备份。</summary>
        /// <param name="slot">1到3。</param>
        /// <returns>独立恢复的冒险，空槽或两份均损坏时抛出明确异常。</returns>
        public CasinoAdventureSession Load(int slot)
        {
            string path = GetSlotPath(slot);
            try { return CasinoAdventureSession.Restore(ReadEnvelope(path).Payload); }
            catch (Exception exception) when (IsRecoverableReadError(exception) && File.Exists(path + ".bak"))
            {
                return CasinoAdventureSession.Restore(ReadEnvelope(path + ".bak").Payload);
            }
        }

        /// <summary>返回槽列表展示信息，不加载场景或改变当前冒险。</summary>
        /// <param name="slot">1到3。</param>
        /// <returns>明确区分空槽、有效主文件、可恢复备份和损坏的摘要。</returns>
        public CasinoSaveSlotInfo GetInfo(int slot)
        {
            string path = GetSlotPath(slot);
            if (!File.Exists(path) && !File.Exists(path + ".bak")) return new CasinoSaveSlotInfo { Slot = slot, IsEmpty = true };
            try { return ToInfo(slot, ReadEnvelope(path), false); }
            catch (Exception exception) when (IsRecoverableReadError(exception))
            {
                if (File.Exists(path + ".bak"))
                {
                    try { return ToInfo(slot, ReadEnvelope(path + ".bak"), true); }
                    catch (Exception backupException) when (IsRecoverableReadError(backupException)) { }
                }
                return new CasinoSaveSlotInfo { Slot = slot, Error = "存档校验失败，无法恢复。" };
            }
        }

        private string GetSlotPath(int slot)
        {
            if (slot < 1 || slot > 3) throw new ArgumentOutOfRangeException(nameof(slot), "存档槽必须为1到3。");
            return Path.Combine(rootDirectory, "save-" + slot + ".json");
        }

        private static CasinoSaveEnvelope ReadEnvelope(string path)
        {
            var file = new FileInfo(path);
            if (!file.Exists) throw new FileNotFoundException("该存档槽为空。", path);
            if (file.Length <= 0 || file.Length > MaximumFileBytes) throw new InvalidDataException("存档长度非法。");
            CasinoSaveEnvelope envelope;
            try { envelope = JsonUtility.FromJson<CasinoSaveEnvelope>(File.ReadAllText(path, Encoding.UTF8)); }
            catch (ArgumentException exception) { throw new InvalidDataException("存档不是合法数据。", exception); }
            if (envelope == null || envelope.Version != 1 || string.IsNullOrEmpty(envelope.Payload) ||
                !string.Equals(envelope.Checksum, Hash(envelope.Payload), StringComparison.Ordinal))
                throw new InvalidDataException("存档版本或校验值错误。");
            // UI摘要也应来自可恢复规则快照，不能仅信任封装中的金币/阶段字段。
            var restored = CasinoAdventureSession.Restore(envelope.Payload).CaptureState();
            if (restored.Coins != envelope.Coins || restored.StageIndex != envelope.StageIndex || restored.Mode.ToString() != envelope.Mode)
                throw new InvalidDataException("存档摘要与规则快照不一致。");
            return envelope;
        }

        private static bool IsRecoverableReadError(Exception exception)
            => exception is IOException || exception is InvalidDataException || exception is ArgumentException || exception is UnauthorizedAccessException;

        private static CasinoSaveSlotInfo ToInfo(int slot, CasinoSaveEnvelope envelope, bool recovered)
            => new CasinoSaveSlotInfo
            {
                Slot = slot, SavedUtc = envelope.SavedUtc, Mode = envelope.Mode, StageIndex = envelope.StageIndex,
                Coins = envelope.Coins, UsesBackup = recovered
            };

        private static string Hash(string text)
        {
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", string.Empty);
        }
    }

    [Serializable]
    internal sealed class CasinoSaveEnvelope
    {
        public int Version;
        public string SavedUtc;
        public string Payload;
        public string Checksum;
        public string Mode;
        public int StageIndex;
        public long Coins;
    }

    public sealed class CasinoSaveSlotInfo
    {
        public int Slot;
        public bool IsEmpty;
        public bool UsesBackup;
        public string SavedUtc;
        public string Mode;
        public int StageIndex;
        public long Coins;
        public string Error;
    }
}
