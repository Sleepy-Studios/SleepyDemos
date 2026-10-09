using System;
using System.IO;
using Core.Runtime;
using Hotfix.JinxCasino.Rules;
using UnityEngine;

namespace Hotfix.JinxCasino.Persistence
{
    /// 永久档案的领域校验；文件读写统一交给公共本地存储。
    public sealed class CasinoProfileStore
    {
        private readonly string directory;
        /// <summary>绑定永久档案目录，不在构造时读取或创建文件。</summary>
        /// <param name="directory">默认 persistentDataPath/JinxCasino/Profile；测试可指定独立目录。</param>
        public CasinoProfileStore(string directory = null)
        {
            this.directory = directory ?? Path.Combine(Application.persistentDataPath, LocalDataKeys.CasinoProfileDirectory);
        }

        /// 空档或损坏内容使用新用户档案；读取本身不写入文件。
        public CasinoProfile LoadOrCreate()
        {
            string json = LocalDataManager.LoadFile(LocalDataKeys.CasinoProfile, directory);
            if (json == null)
                return CasinoProfile.Create();
            try
            {
                return CasinoProfile.Restore(json);
            }
            catch (Exception exception) when (exception is ArgumentException || exception is FormatException || exception is NotSupportedException)
            {
                return CasinoProfile.Create();
            }
        }

        /// <summary>校验并保存完整成长档案，保存失败不提交内存中的候选。</summary>
        /// <param name="profile">本次候选档案。</param>
        public void Save(CasinoProfile profile)
        {
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));
            string json = profile.ToJson();
            CasinoProfile.Restore(json);
            LocalDataManager.SaveFile(LocalDataKeys.CasinoProfile, json, directory);
        }
    }
}
