using System;
using System.IO;
using Core.Runtime;
using Hotfix.JinxCasino.Rules;
using UnityEngine;

namespace Hotfix.JinxCasino.Persistence
{
    /// 赌场三槽的领域快照与摘要；实际读写由公共本地存储负责。
    public sealed class CasinoLocalSaveStore
    {
        private readonly string directory;
        /// <summary>绑定存档目录，不读取旧原型目录或备份。</summary>
        /// <param name="directory">默认 persistentDataPath/JinxCasino；测试可传独立目录。</param>
        public CasinoLocalSaveStore(string directory = null)
        {
            this.directory = directory ?? Path.Combine(Application.persistentDataPath, LocalDataKeys.CasinoDirectory);
        }

        /// <summary>保存完整冒险，保留未结算机台与请求去重数据。</summary>
        /// <param name="slot">范围 1–3。</param>
        /// <param name="session">当前冒险聚合。</param>
        public void Save(int slot, CasinoAdventureSession session)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            string path = LocalDataKeys.CasinoSlot(slot);
            string json = session.ToSnapshotJson();
            CasinoAdventureSession.Restore(json);
            LocalDataManager.SaveFile(path, json, directory);
        }

        /// <summary>读取合法冒险；空档或损坏内容返回 null，不修改当前冒险。</summary>
        /// <param name="slot">范围 1–3。</param>
        public CasinoAdventureSession Load(int slot)
        {
            string json = LocalDataManager.LoadFile(LocalDataKeys.CasinoSlot(slot), directory);
            if (json == null)
                return null;
            try
            {
                return CasinoAdventureSession.Restore(json);
            }
            catch (Exception exception) when (exception is ArgumentException || exception is FormatException || exception is NotSupportedException)
            {
                return null;
            }
        }

        /// <summary>从已校验快照生成槽摘要，损坏内容按空槽显示。</summary>
        /// <param name="slot">范围 1–3。</param>
        public CasinoSaveSlotInfo GetInfo(int slot)
        {
            var session = Load(slot);
            if (session == null)
                return new CasinoSaveSlotInfo
                {
                    Slot = slot,
                    IsEmpty = true
                };
            var state = session.CaptureState();
            return new CasinoSaveSlotInfo
            {
                Slot = slot,
                SavedUtc = LocalDataManager.GetFileWriteTimeUtc(LocalDataKeys.CasinoSlot(slot), directory).ToString("O"),
                Mode = state.Mode.ToString(),
                StageIndex = state.StageIndex,
                Coins = state.Coins
            };
        }
    }

    /// 存档页面展示的领域摘要。
    public sealed class CasinoSaveSlotInfo
    {
        public int Slot;
        public bool IsEmpty;
        public string SavedUtc;
        public string Mode;
        public int StageIndex;
        public long Coins;
        public string Error;
    }
}
