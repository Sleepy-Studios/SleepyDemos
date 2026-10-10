using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Hotfix.KagitKart
{
    [Serializable]
    public sealed class KagitKartAiStyle
    {
        public string name;
        public double offset, usage, driftMin, tierHold, aim, gumBack, box, draft, mistake, bump, save;
        public double[] hold;
    }

    [Serializable]
    public sealed class KagitKartAiCorner
    {
        public int index, from, to, sign, dir;
        public double arc, turn;
    }

    [Serializable]
    public sealed class KagitKartAiShortcut
    {
        public int i, j;
    }

    [Serializable]
    public sealed class KagitKartAiShortcutProbe
    {
        public bool ok;
        public int sideI, jc;
        public double lead, aimLat, outT, t, road;
    }

    /// 原作 AI 的静态路线和原作查询探测结果；不用简化路线替代实际规划。
    [Serializable]
    public sealed class KagitKartAiPlan
    {
        public string sourceCommit;
        public string trackId;
        public int sampleCount;
        public float[] lineLat, lineK, limL, limR, fade;
        public int[] noDrift, trickLip, nextCorner;
        public double[] cum;
        public KagitKartAiCorner[] corners;
        public KagitKartAiShortcut[] shortcuts;
        public Dictionary<string, KagitKartAiShortcutProbe[]> probes;
        public Dictionary<string, KagitKartAiStyle> styles;
        public KagitKartAiStyle defaultStyle;

        /// <summary>加载固定源码生成的数据并与本场赛道采样核对。</summary>
        /// <param name="json">独立路线资产 JSON。</param>
        /// <param name="track">本场赛道。</param>
        public static KagitKartAiPlan Parse(string json, KagitKartTrackData track)
        {
            if (string.IsNullOrWhiteSpace(json) || track == null)
            {
                throw new ArgumentException("智能车手路线数据缺失。", nameof(json));
            }
            var plan = JsonConvert.DeserializeObject<KagitKartAiPlan>(json,
                new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None, MaxDepth = 64 });
            if (plan == null || plan.sourceCommit != KagitKartCatalog.SourceCommit || plan.trackId != track.definition.id ||
                plan.sampleCount != track.data.sampleCount || plan.corners == null || plan.shortcuts == null ||
                plan.styles == null || plan.styles.Count != 8 || plan.defaultStyle == null || plan.probes == null)
            {
                throw new ArgumentException("智能车手路线版本或赛道不匹配。", nameof(json));
            }
            foreach (var values in new[] { plan.lineLat, plan.lineK, plan.limL, plan.limR, plan.fade })
            {
                if (values == null || values.Length != plan.sampleCount)
                {
                    throw new ArgumentException("智能车手路线采样不完整。", nameof(json));
                }
                foreach (var value in values)
                {
                    if (float.IsNaN(value) || float.IsInfinity(value))
                    {
                        throw new ArgumentException("智能车手路线数值非法。", nameof(json));
                    }
                }
            }
            foreach (var values in new[] { plan.noDrift, plan.trickLip, plan.nextCorner })
            {
                if (values == null || values.Length != plan.sampleCount)
                {
                    throw new ArgumentException("智能车手路线索引不完整。", nameof(json));
                }
            }
            if (plan.cum == null || plan.cum.Length != plan.sampleCount + 1)
            {
                throw new ArgumentException("智能车手弯道累计角缺失。", nameof(json));
            }
            foreach (double value in plan.cum)
            {
                if (!Finite(value))
                {
                    throw new ArgumentException("智能车手弯道角度非法。", nameof(json));
                }
            }
            for (int i = 0; i < plan.sampleCount; i++)
            {
                if (plan.limL[i] > plan.limR[i] || plan.fade[i] < 0 || plan.fade[i] > 1 ||
                    plan.noDrift[i] < 0 || plan.noDrift[i] > 1 || plan.nextCorner[i] < -1 ||
                    plan.nextCorner[i] >= plan.corners.Length || plan.trickLip[i] < -1 || plan.trickLip[i] >= track.data.ramps.Length)
                {
                    throw new ArgumentException("智能车手路线范围非法。", nameof(json));
                }
            }
            foreach (var corner in plan.corners)
            {
                if (corner == null || !Index(corner.from, plan.sampleCount) || !Index(corner.to, plan.sampleCount) ||
                    corner.dir != 1 && corner.dir != -1 || !Finite(corner.arc) || !Finite(corner.turn) || corner.arc <= 0 || corner.turn < 0)
                {
                    throw new ArgumentException("智能车手弯道数据非法。", nameof(json));
                }
            }
            foreach (var shortcut in plan.shortcuts)
            {
                if (shortcut == null || !Index(shortcut.i, plan.sampleCount) || !Index(shortcut.j, plan.sampleCount))
                {
                    throw new ArgumentException("智能车手捷径索引非法。", nameof(json));
                }
            }
            foreach (string cls in new[] { "80", "120", "200" })
            {
                if (!plan.probes.TryGetValue(cls, out var probes) || probes.Length != plan.shortcuts.Length)
                {
                    throw new ArgumentException("智能车手速度组别探测缺失。", nameof(json));
                }
                foreach (var probe in probes)
                {
                    if (probe == null || !Index(probe.jc, plan.sampleCount) || probe.sideI != 1 && probe.sideI != -1 ||
                        !Finite(probe.aimLat) || !Finite(probe.lead) || !Finite(probe.outT) || !Finite(probe.t) || !Finite(probe.road))
                    {
                        throw new ArgumentException("智能车手捷径探测非法。", nameof(json));
                    }
                }
            }
            foreach (var pair in plan.styles)
            {
                KagitKartCatalog.CharacterName(pair.Key);
                ValidateStyle(pair.Value);
            }
            ValidateStyle(plan.defaultStyle);
            return plan;
        }

        private static void ValidateStyle(KagitKartAiStyle style)
        {
            if (style == null || style.hold == null || style.hold.Length != 2 || style.hold[0] < 0 || style.hold[1] < style.hold[0])
            {
                throw new ArgumentException("智能车手性格数据缺失。");
            }
            foreach (double value in new[] { style.offset, style.usage, style.driftMin, style.tierHold, style.aim,
                style.gumBack, style.box, style.draft, style.mistake, style.bump, style.save, style.hold[0], style.hold[1] })
            {
                if (!Finite(value))
                {
                    throw new ArgumentException("智能车手性格参数非法。");
                }
            }
        }

        private static bool Index(int value, int count) => value >= 0 && value < count;
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
