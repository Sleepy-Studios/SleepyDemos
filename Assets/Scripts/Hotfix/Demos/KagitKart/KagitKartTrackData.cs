using System;
using Newtonsoft.Json;
using UnityEngine;

namespace Hotfix.KagitKart
{
    /// 原作采样数组；左右方向与表面编码沿用源码。
    [Serializable]
    public sealed class KagitKartTrackSamples
    {
        public float[] px, py, pz, tx, ty, tz, rx, ry, rz, ux, uy, uz;
        public float[] halfWidth, offroad, dist, hx, hz, bank, slopeRight, heading, curvature;
        public int[] edgeLeft, edgeRight, surface, rampLip, padId, rampId;
    }

    /// 赛道几何采样结果，由原作纯数学模块导出。
    [Serializable]
    public sealed class KagitKartTrackGeometry
    {
        public double length;
        public int sampleCount;
        public double spacing;
        public double startDist;
        public int startIndex;
        public float minY;
        public float maxY;
        public KagitKartRamp[] ramps;
        public KagitKartBoostPad[] pads;
        public KagitKartTrackSamples samples;
    }

    /// 原作跳台范围与特技属性。
    [Serializable]
    public sealed class KagitKartRamp
    {
        public int index;
        public float t;
        public float d0;
        public float d1;
        public float length;
        public float height;
        public bool trick;
        public bool popup;
    }

    /// 原作加速带所在赛道范围。
    [Serializable]
    public sealed class KagitKartBoostPad
    {
        public int index;
        public float t;
        public float lateral;
        public float halfWidth;
    }

    /// 赛道定义中的稳定标识；其余原版定义仍完整保存在 JSON 中。
    [Serializable]
    public sealed class KagitKartTrackDefinition
    {
        public string id;
        public int chapter;
        public int laps;
        public float? waterLevel;
        public KagitKartItemRow[] itemRows;
    }

    [Serializable]
    public sealed class KagitKartItemRow
    {
        public float t;
        public int count;
    }

    /// 通过 Loader 获取 TextAsset 后解析，不自行扫描 Resources。
    [Serializable]
    public sealed class KagitKartTrackData
    {
        public string sourceCommit;
        public KagitKartTrackDefinition definition;
        public KagitKartTrackGeometry data;

        /// <summary>读取当前原版导出的赛道数据并检查必需数组。</summary>
        /// <param name="json">Loader 提供的赛道 JSON 文本。</param>
        /// <returns>可以用于规则查询和表现装配的采样数据。</returns>
        public static KagitKartTrackData Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new ArgumentException("赛道数据为空。", nameof(json));
            }
            var track = JsonConvert.DeserializeObject<KagitKartTrackData>(json,
                new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None, MaxDepth = 64 });
            if (track == null || track.sourceCommit != KagitKartCatalog.SourceCommit ||
                track.definition == null || !KagitKartCatalog.IsTrack(track.definition.id) || track.definition.laps != 3 ||
                track.data == null || track.data.samples == null)
                throw new ArgumentException("赛道数据缺失或版本不匹配。", nameof(json));
            var geometry = track.data;
            if (double.IsNaN(geometry.length) || double.IsInfinity(geometry.length) || geometry.length <= 120 ||
                geometry.sampleCount < 8 || geometry.startIndex < 0 || geometry.startIndex >= geometry.sampleCount ||
                double.IsNaN(geometry.spacing) || double.IsInfinity(geometry.spacing) || geometry.spacing <= 0 ||
                Math.Abs(geometry.spacing * geometry.sampleCount - geometry.length) > 0.01 ||
                double.IsNaN(geometry.startDist) || double.IsInfinity(geometry.startDist) ||
                Math.Abs(geometry.startDist - geometry.startIndex * geometry.spacing) > 0.01)
                throw new ArgumentException("赛道长度或起跑线无效。", nameof(json));
            var samples = geometry.samples;
            foreach (float[] values in new[] { samples.px, samples.py, samples.pz, samples.tx, samples.ty, samples.tz,
                samples.rx, samples.ry, samples.rz, samples.ux, samples.uy, samples.uz, samples.halfWidth,
                samples.offroad, samples.dist, samples.hx, samples.hz, samples.bank, samples.slopeRight,
                samples.heading, samples.curvature })
            {
                if (values == null || values.Length != geometry.sampleCount)
                    throw new ArgumentException("赛道采样数组不完整。", nameof(json));
                foreach (float value in values)
                    if (float.IsNaN(value) || float.IsInfinity(value))
                        throw new ArgumentException("赛道包含非法数值。", nameof(json));
            }
            foreach (int[] values in new[] { samples.edgeLeft, samples.edgeRight, samples.surface,
                samples.rampLip, samples.padId, samples.rampId })
                if (values == null || values.Length != geometry.sampleCount)
                    throw new ArgumentException("赛道属性数组不完整。", nameof(json));
            if (geometry.ramps == null || geometry.pads == null)
            {
                throw new ArgumentException("赛道跳台或加速带数据缺失。", nameof(json));
            }
            if (track.definition.itemRows == null || track.definition.itemRows.Length > 64)
            {
                throw new ArgumentException("赛道道具箱数据缺失或超出范围。", nameof(json));
            }
            foreach (var row in track.definition.itemRows)
            {
                if (row == null || float.IsNaN(row.t) || float.IsInfinity(row.t) || row.t < 0 || row.t >= 1 ||
                    row.count < 1 || row.count > 32)
                {
                    throw new ArgumentException("赛道道具箱参数无效。", nameof(json));
                }
            }
            for (int i = 0; i < geometry.sampleCount; i++)
            {
                if (samples.halfWidth[i] <= 0 || samples.offroad[i] < 0 ||
                    samples.edgeLeft[i] < 0 || samples.edgeLeft[i] > 3 ||
                    samples.edgeRight[i] < 0 || samples.edgeRight[i] > 3 ||
                    samples.rampId[i] < -1 || samples.rampId[i] >= geometry.ramps.Length ||
                    samples.padId[i] < -1 || samples.padId[i] >= geometry.pads.Length)
                {
                    throw new ArgumentException("赛道宽度或属性索引无效。", nameof(json));
                }
            }
            foreach (var ramp in geometry.ramps)
            {
                if (ramp == null || ramp.length <= 0 || float.IsNaN(ramp.length) || float.IsInfinity(ramp.length) ||
                    float.IsNaN(ramp.height) || float.IsInfinity(ramp.height) ||
                    float.IsNaN(ramp.d0) || float.IsInfinity(ramp.d0))
                {
                    throw new ArgumentException("赛道跳台参数无效。", nameof(json));
                }
            }
            return track;
        }
    }
}
