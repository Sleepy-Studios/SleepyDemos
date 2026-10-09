using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Core.Runtime
{
    /// 本机偏好与文件快照的公共读写入口；保存立即落盘，不持有业务对象缓存。
    public static class LocalDataManager
    {
        private const int MaximumPreferenceCharacters = 128 * 1024;
        private const int MaximumFileBytes = 16 * 1024 * 1024;
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            MaxDepth = 64
        };
        /// <summary>读取独立本机值；缺失、损坏或校验失败返回默认值，不自动写入默认记录。</summary>
        /// <typeparam name="T">基础类型、集合或普通数据对象。</typeparam>
        /// <param name="key">非空本机键，由业务统一定义。</param>
        /// <param name="defaultValue">无有效记录时使用的默认值。</param>
        /// <param name="validate">可选领域校验；校验失败应抛出 ArgumentException、FormatException 或 NotSupportedException。</param>
        /// <returns>本次反序列化的独立对象，或默认值。</returns>
        public static T LoadData<T>(string key, T defaultValue = default, Action<T> validate = null) => LoadData(key, defaultValue, out _, validate);
        /// <summary>读取本机值并返回默认回退原因；缺失记录没有警告。</summary>
        /// <typeparam name="T">基础类型、集合或普通数据对象。</typeparam>
        /// <param name="key">非空本机键。</param>
        /// <param name="defaultValue">无有效记录时使用的默认值。</param>
        /// <param name="warning">损坏或校验失败时的提示；有效或缺失时为 null。</param>
        /// <param name="validate">可选领域校验。</param>
        public static T LoadData<T>(string key, T defaultValue, out string warning, Action<T> validate = null)
        {
            ValidateKey(key);
            warning = null;
            if (!PlayerPrefs.HasKey(key))
                return defaultValue;
            try
            {
                string json = PlayerPrefs.GetString(key);
                if (string.IsNullOrWhiteSpace(json) || json.Length > MaximumPreferenceCharacters)
                {
                    warning = "本地记录无效，已使用默认值。";
                    return defaultValue;
                }

                var value = JsonConvert.DeserializeObject<T>(json, JsonSettings);
                if (value == null)
                {
                    warning = "本地记录为空，已使用默认值。";
                    return defaultValue;
                }

                validate?.Invoke(value);
                return value;
            }
            catch (Exception exception) when (IsInvalidData(exception))
            {
                warning = "本地记录损坏，已使用默认值：" + exception.Message;
                return defaultValue;
            }
        }

        /// <summary>校验并立即保存本机值；失败恢复本键内存值，异常交由业务显示。</summary>
        /// <typeparam name="T">基础类型、集合或普通数据对象。</typeparam>
        /// <param name="key">非空本机键。</param>
        /// <param name="value">待保存的合法完整值。</param>
        /// <param name="validate">可选领域校验，在修改原记录前执行。</param>
        public static void SaveData<T>(string key, T value, Action<T> validate = null)
        {
            ValidateKey(key);
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            validate?.Invoke(value);
            string json = JsonConvert.SerializeObject(value, JsonSettings);
            if (json.Length > MaximumPreferenceCharacters)
                throw new ArgumentException("本机记录超过允许大小。", nameof(value));
            bool existed = PlayerPrefs.HasKey(key);
            string previous = existed ? PlayerPrefs.GetString(key) : null;
            try
            {
                PlayerPrefs.SetString(key, json);
                PlayerPrefs.Save();
            }
            catch
            {
                if (existed)
                    PlayerPrefs.SetString(key, previous);
                else
                    PlayerPrefs.DeleteKey(key);
                throw;
            }
        }

        /// <summary>读取 JSON 文件；缺失、空内容、非法 JSON 或过大记录返回 null，实际 IO 失败继续抛出。</summary>
        /// <param name="relativePath">相对于存储根目录的文件路径，不允许绝对路径或越出根目录。</param>
        /// <param name="directory">默认 persistentDataPath；测试可传独立临时根目录。</param>
        /// <returns>有效 JSON；不存在有效记录时为 null。</returns>
        public static string LoadFile(string relativePath, string directory = null)
        {
            string path = GetFilePath(relativePath, directory);
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (stream.Length <= 0 || stream.Length > MaximumFileBytes)
                    return null;
                using var reader = new StreamReader(stream, Utf8, true);
                string json = reader.ReadToEnd();
                ValidateJson(json);
                return json;
            }
            catch (Exception exception) when (exception is FileNotFoundException || exception is DirectoryNotFoundException || exception is JsonException || exception is DecoderFallbackException)
            {
                return null;
            }
        }

        /// <summary>原子保存合法 JSON；无备份和迁移，失败保留现有主档并清理本次临时文件。</summary>
        /// <param name="relativePath">相对于存储根目录的文件路径。</param>
        /// <param name="json">业务已经完成领域校验的完整快照。</param>
        /// <param name="directory">默认 persistentDataPath；测试可传独立临时根目录。</param>
        public static void SaveFile(string relativePath, string json, string directory = null)
        {
            string path = GetFilePath(relativePath, directory);
            ValidateJson(json);
            byte[] bytes = Utf8.GetBytes(json);
            if (bytes.Length > MaximumFileBytes)
                throw new ArgumentException("存档超过允许大小。", nameof(json));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }

                if (File.Exists(path))
                    File.Replace(temporary, path, null);
                else
                    File.Move(temporary, path);
            }
            finally
            {
                if (File.Exists(temporary))
                    try
                    {
                        File.Delete(temporary);
                    }
                    catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
                    {
                        Debug.LogWarning("本次存档临时文件清理失败：" + exception.Message);
                    }
            }
        }

        /// <summary>取得同一安全路径的最后写入时间，供存档列表展示。</summary>
        /// <param name="relativePath">相对于存储根目录的文件路径。</param>
        /// <param name="directory">默认 persistentDataPath；测试可传独立临时根目录。</param>
        public static DateTime GetFileWriteTimeUtc(string relativePath, string directory = null) => File.GetLastWriteTimeUtc(GetFilePath(relativePath, directory));
        private static void ValidateKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("本机存储键不能为空。", nameof(key));
        }

        private static string GetFilePath(string relativePath, string directory)
        {
            if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
                throw new ArgumentException("存档必须使用非空相对路径。", nameof(relativePath));
            string root = Path.GetFullPath(directory ?? Application.persistentDataPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string path = Path.GetFullPath(Path.Combine(root, relativePath));
            if (!path.StartsWith(root, Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                throw new ArgumentException("存档路径不能越出存储目录。", nameof(relativePath));
            return path;
        }

        private static void ValidateJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new JsonSerializationException("存档 JSON 不能为空。");
            using var reader = new JsonTextReader(new StringReader(json))
            {
                MaxDepth = 64
            };
            JToken.ReadFrom(reader);
            if (reader.Read())
                throw new JsonSerializationException("存档包含多个 JSON 值。");
        }

        private static bool IsInvalidData(Exception exception) => exception is JsonException || exception is ArgumentException || exception is FormatException || exception is NotSupportedException;
    }
}
