using System;
using System.IO;
using System.Text;
using Core.Runtime.Platform.TapTap;
using Newtonsoft.Json;
using UnityEngine;

namespace Core.Runtime
{
    /// 本机偏好与文件快照的公共读写入口；保存立即落盘，不持有业务对象缓存。
    public static class LocalDataManager
    {
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
        /// <param name="validate">读取边界的可选业务校验；失败统一视为没有数据。</param>
        /// <returns>本次反序列化的独立对象，或默认值。</returns>
        public static T LoadData<T>(string key, T defaultValue = default, Action<T> validate = null)
        {
            ValidateKey(key);
            try
            {
#if UNITY_WEBGL && SLEEPY_TAPTAP && !UNITY_EDITOR
                string json = TapTapStorage.LoadValue(key);
                if (string.IsNullOrEmpty(json))
                {
                    return defaultValue;
                }
#else
                if (!PlayerPrefs.HasKey(key))
                {
                    return defaultValue;
                }
                string json = PlayerPrefs.GetString(key);
#endif
                var value = JsonConvert.DeserializeObject<T>(json, JsonSettings);
                if (value == null)
                {
                    return defaultValue;
                }
                validate?.Invoke(value);
                return value;
            }
            catch (Exception)
            {
                return defaultValue;
            }
        }

        /// <summary>校验并立即保存本机值；原生保存失败恢复本键内存值，浏览器存储失败保留旧记录并抛出供业务显示。</summary>
        /// <typeparam name="T">基础类型、集合或普通数据对象。</typeparam>
        /// <param name="key">非空本机键。</param>
        /// <param name="value">待保存的合法完整值。</param>
        /// <param name="validate">可选领域校验，在修改原记录前执行。</param>
        public static void SaveData<T>(string key, T value, Action<T> validate = null)
        {
            ValidateKey(key);
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }
            validate?.Invoke(value);
            string json = JsonConvert.SerializeObject(value, JsonSettings);
#if UNITY_WEBGL && SLEEPY_TAPTAP && !UNITY_EDITOR
            TapTapStorage.SaveValue(key, json);
#else
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
                {
                    PlayerPrefs.SetString(key, previous);
                }
                else
                {
                    PlayerPrefs.DeleteKey(key);
                }
                throw;
            }
#endif
        }

        /// <summary>读取文件内容；读取失败或空内容返回 null，业务负责解析与校验。</summary>
        /// <param name="relativePath">相对于存储根目录的文件路径，不允许绝对路径或越出根目录。</param>
        /// <param name="directory">默认 persistentDataPath；测试可传独立临时根目录。</param>
        /// <returns>文件文本；没有数据时为 null。</returns>
        public static string LoadFile(string relativePath, string directory = null)
        {
            string path = GetFilePath(relativePath, directory);
            try
            {
#if UNITY_WEBGL && SLEEPY_TAPTAP && !UNITY_EDITOR
                string content = TapTapStorage.LoadFile(path);
#else
                string content = File.ReadAllText(path, Utf8);
#endif
                return string.IsNullOrWhiteSpace(content) ? null : content;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>完整快照写入后替换目标；浏览器使用原子记录，失败抛出供业务提示，不创建备份。</summary>
        /// <param name="relativePath">相对于存储根目录的文件路径。</param>
        /// <param name="json">业务已经完成领域校验的完整快照。</param>
        /// <param name="directory">默认 persistentDataPath；测试可传独立临时根目录。</param>
        public static void SaveFile(string relativePath, string json, string directory = null)
        {
            string path = GetFilePath(relativePath, directory);
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new ArgumentException("存档内容不能为空。", nameof(json));
            }
#if UNITY_WEBGL && SLEEPY_TAPTAP && !UNITY_EDITOR
            Utf8.GetByteCount(json);
            TapTapStorage.SaveFile(path, json);
#else
            byte[] bytes = Utf8.GetBytes(json);
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
                {
                    File.Replace(temporary, path, null);
                }
                else
                {
                    File.Move(temporary, path);
                }
            }
            finally
            {
                if (File.Exists(temporary))
                {
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
#endif
        }

        /// <summary>取得同一安全路径的最后写入时间，供存档列表展示。</summary>
        /// <param name="relativePath">相对于存储根目录的文件路径。</param>
        /// <param name="directory">默认 persistentDataPath；测试可传独立临时根目录。</param>
        public static DateTime GetFileWriteTimeUtc(string relativePath, string directory = null)
        {
            string path = GetFilePath(relativePath, directory);
#if UNITY_WEBGL && SLEEPY_TAPTAP && !UNITY_EDITOR
            return TapTapStorage.GetFileWriteTimeUtc(path);
#else
            return File.GetLastWriteTimeUtc(path);
#endif
        }
        private static void ValidateKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("本机存储键不能为空。", nameof(key));
            }
        }

        private static string GetFilePath(string relativePath, string directory)
        {
            if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
            {
                throw new ArgumentException("存档必须使用非空相对路径。", nameof(relativePath));
            }
            string root = Path.GetFullPath(directory ?? Application.persistentDataPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string path = Path.GetFullPath(Path.Combine(root, relativePath));
            if (!path.StartsWith(root, Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            {
                throw new ArgumentException("存档路径不能越出存储目录。", nameof(relativePath));
            }
#if UNITY_WEBGL && SLEEPY_TAPTAP && !UNITY_EDITOR
            return TapTapStorage.ToRelativePath(path, Application.persistentDataPath);
#else
            return path;
#endif
        }
    }
}
