using System;
using System.IO;
using Core.Runtime;
using Newtonsoft.Json;
using UnityEngine;

namespace Hotfix.KagitKart
{
    [Serializable]
    public sealed class KagitKartTimeTrialRecord
    {
        public double? bestLap;
        public double? bestRace;
        public KagitKartGhostData ghost;
    }

    /// 最佳成绩领域规则；公共存储只负责文件读写。
    public sealed class KagitKartRecordStore
    {
        private readonly string directory;
        private static readonly JsonSerializerSettings JsonSettings = new()
        {
            TypeNameHandling = TypeNameHandling.None,
            MaxDepth = 64
        };

        /// <summary>绑定独立目录，不在构造时创建或读取记录。</summary>
        /// <param name="directory">默认 persistentDataPath/KagitKart；测试可注入临时目录。</param>
        public KagitKartRecordStore(string directory = null)
        {
            this.directory = directory ?? Path.Combine(Application.persistentDataPath, LocalDataKeys.KagitKartDirectory);
        }

        /// <summary>读取并校验完整记录；缺失、读取失败或记录损坏统一返回空记录。</summary>
        /// <param name="track">原作赛道标识。</param>
        /// <param name="speedClass">80、120 或 200。</param>
        /// <param name="character">原作角色标识。</param>
        /// <returns>独立记录，没有有效数据时为空记录。</returns>
        public KagitKartTimeTrialRecord Load(string track, int speedClass, string character)
        {
            string path = LocalDataKeys.KagitKartRecord(track, speedClass, character);
            string json = LocalDataManager.LoadFile(path, directory);
            if (json == null)
            {
                return new KagitKartTimeTrialRecord();
            }
            try
            {
                var record = JsonConvert.DeserializeObject<KagitKartTimeTrialRecord>(json, JsonSettings);
                if (record == null || !Positive(record.bestLap) || !Positive(record.bestRace) ||
                    !ValidGhost(record.ghost) || Math.Abs(record.ghost.total - record.bestRace.Value) > 1e-6)
                {
                    return new KagitKartTimeTrialRecord();
                }
                return record;
            }
            catch (JsonException)
            {
                return new KagitKartTimeTrialRecord();
            }
        }

        /// <summary>单圈与全程独立更新；仅更快全程替换幽灵，写入成功后才返回新记录。</summary>
        /// <param name="track">已完赛赛道标识。</param>
        /// <param name="speedClass">当前速度组别。</param>
        /// <param name="character">玩家角色标识。</param>
        /// <param name="run">本次实际三圈完赛幽灵快照，不能传估算结果。</param>
        /// <param name="previous">本场同赛道、组别与车手的合法记录；为空时从文件读取。</param>
        /// <returns>保存后的记录；校验或 IO 失败抛出，原有文件由公共层保护。</returns>
        public KagitKartTimeTrialRecord SaveBest(string track, int speedClass, string character, KagitKartGhostData run,
            KagitKartTimeTrialRecord previous = null)
        {
            string path = LocalDataKeys.KagitKartRecord(track, speedClass, character);
            if (!ValidGhost(run))
            {
                throw new ArgumentException("计时比赛记录不完整，不能保存。", nameof(run));
            }
            previous ??= Load(track, speedClass, character);
            double lap = Math.Min(run.lapTimes[0], Math.Min(run.lapTimes[1], run.lapTimes[2]));
            var next = new KagitKartTimeTrialRecord
            {
                bestLap = previous.bestLap.HasValue ? Math.Min(previous.bestLap.Value, lap) : lap,
                bestRace = previous.bestRace,
                ghost = previous.ghost
            };
            if (!previous.bestRace.HasValue || run.total < previous.bestRace.Value)
            {
                next.bestRace = run.total;
                next.ghost = run;
            }
            LocalDataManager.SaveFile(path, JsonConvert.SerializeObject(next, JsonSettings), directory);
            return next;
        }

        private static bool Positive(double? value) => value.HasValue &&
            !double.IsNaN(value.Value) && !double.IsInfinity(value.Value) && value.Value > 0;

        private static bool ValidGhost(KagitKartGhostData data) => data != null && data.IsValid() &&
            data.lapTimes.Length == 3 && data.checkpoints.Length == 12;
    }
}
