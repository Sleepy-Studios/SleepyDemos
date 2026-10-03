using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Core.Runtime;
using Hotfix.JinxCasino.Rules;
using UnityEngine;

namespace Hotfix.JinxCasino.Persistence
{
    /// 永久档案的独立本地存储；profile.json与三个旅程存档隔离，不会用损坏文件覆盖有效备份。
    public sealed class CasinoProfileStore
    {
        private const int MaximumFileBytes = 16 * 1024 * 1024;
        private readonly string rootDirectory;
        /// 最近LoadOrCreate是否使用有效备份；空档案或成功Save后为false。
        public bool UsesBackup { get; private set; }

        /// <summary>创建永久档案存储，不在构造时读写文件。</summary>
        /// <param name="directory">专有档案目录；null使用persistentDataPath/JinxCasino/PrototypeV2/Profile，不读取旧原型档案。</param>
        public CasinoProfileStore(string directory = null)
        {
            rootDirectory = Path.GetFullPath(string.IsNullOrWhiteSpace(directory) ? Path.Combine(Application.persistentDataPath, "JinxCasino", "PrototypeV2", "Profile") : directory);
        }

        /// 读取完整校验档案；只有主/备均不存在才创建新档案，两份损坏时明确报错而不重置成长。
        public CasinoProfile LoadOrCreate()
        {
            string path = Path.Combine(rootDirectory, "profile.json"); UsesBackup = false;
            if (!File.Exists(path) && !File.Exists(path + ".bak")) return CasinoProfile.Create();
            try { return CasinoProfile.Restore(ReadEnvelope(path).Payload); }
            catch (Exception exception) when (IsRecoverableReadError(exception) && File.Exists(path + ".bak"))
            {
                var restored = CasinoProfile.Restore(ReadEnvelope(path + ".bak").Payload); UsesBackup = true; return restored;
            }
        }

        /// <summary>先写并校验临时档案，再原子替换；失败不改主文件与有效备份，相同payload不滚备份。</summary>
        /// <param name="profile">已完成领域校验的永久档案；不得传null。</param>
        public void Save(CasinoProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            string payload = profile.ToJson();
            string path = Path.Combine(rootDirectory, "profile.json"); bool validPrimary = false;
            if (File.Exists(path))
            {
                try
                {
                    var existing = ReadEnvelope(path); validPrimary = true;
                    if (existing.Payload == payload && existing.Checksum == Hash(payload)) { UsesBackup = false; return; }
                }
                catch (Exception exception) when (IsRecoverableReadError(exception)) { }
            }
            var envelope = new ProfileEnvelope { Version = 1, SavedUtc = TimeUtil.ToIso8601(), Payload = payload, Checksum = Hash(payload) };
            Directory.CreateDirectory(rootDirectory);
            string temporary = path + ".new-" + Guid.NewGuid().ToString("N");
            try
            {
                File.WriteAllText(temporary, JsonUtility.ToJson(envelope), new UTF8Encoding(false));
                ReadEnvelope(temporary);
                if (File.Exists(path)) File.Replace(temporary, path, validPrimary ? path + ".bak" : null);
                else File.Move(temporary, path);
                UsesBackup = false;
            }
            finally
            {
                // 只清理本次创建的临时文件，清理失败不替换原始保存异常。
                if (File.Exists(temporary))
                    try { File.Delete(temporary); } catch (Exception exception) when (IsRecoverableReadError(exception)) { }
            }
        }

        private static ProfileEnvelope ReadEnvelope(string path)
        {
            var file = new FileInfo(path);
            if (!file.Exists) throw new FileNotFoundException("永久档案不存在。", path);
            if (file.Length <= 0 || file.Length > MaximumFileBytes) throw new InvalidDataException("永久档案长度非法。");
            ProfileEnvelope envelope;
            try { envelope = JsonUtility.FromJson<ProfileEnvelope>(File.ReadAllText(path, Encoding.UTF8)); }
            catch (ArgumentException exception) { throw new InvalidDataException("永久档案不是合法JSON。", exception); }
            if (envelope == null || envelope.Version != 1 || string.IsNullOrEmpty(envelope.Payload) || !string.Equals(envelope.Checksum, Hash(envelope.Payload), StringComparison.Ordinal))
                throw new InvalidDataException("永久档案版本或校验值错误。");
            CasinoProfile.Restore(envelope.Payload);
            return envelope;
        }
        private static bool IsRecoverableReadError(Exception exception)
            => exception is IOException || exception is InvalidDataException || exception is ArgumentException || exception is UnauthorizedAccessException;
        private static string Hash(string text)
        { using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", string.Empty); }

        [Serializable]
        private sealed class ProfileEnvelope
        {
            public int Version;
            public string SavedUtc;
            public string Payload;
            public string Checksum;
        }
    }
}
