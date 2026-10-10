using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hotfix.KagitKart
{
    /// 原作每种情境提示只显示一次，首次教程另存 SeenTutorial。
    public enum KagitKartHintKind
    {
        Corner,
        Tier,
        Ramp,
        Item,
        Portrait
    }

    /// hints.js 的教程计时与提示队列；不创建页面、不暂停比赛、不处理输入。
    public sealed class KagitKartHints
    {
        private readonly HashSet<KagitKartHintKind> seen;
        private readonly Queue<KagitKartHintKind> queue = new();
        private float[] turns;
        private float[] ramps;
        private double trackLength;
        private float tutorialLeft;
        private float hintLeft;

        /// 首次四行操作卡是否显示。
        public bool TutorialVisible { get; private set; }
        /// 当前显示的情境提示，空表示没有提示。
        public KagitKartHintKind? Current { get; private set; }
        /// 可见内容变化，宿主刷新已有Flux绑定。
        public event Action Changed;
        /// 七秒结束或结果视图关闭教程，宿主保存SeenTutorial。
        public event Action TutorialCompleted;
        /// 新提示真正显示，宿主尽力保存一次性历史。
        public event Action HistoryChanged;

        /// <summary>从独立本机历史初始化，非法提示标识拒绝进入当前结构。</summary>
        /// <param name="history">已显示过的提示；默认空历史。</param>
        public KagitKartHints(IEnumerable<KagitKartHintKind> history = null)
        {
            seen = new HashSet<KagitKartHintKind>();
            if (history == null)
            {
                return;
            }
            foreach (var kind in history)
            {
                if (!Enum.IsDefined(typeof(KagitKartHintKind), kind))
                {
                    throw new ArgumentException("驾驶提示历史无效。", nameof(history));
                }
                seen.Add(kind);
            }
        }

        /// 保存只包含已真正显示的提示，排队提示不提前记为已看。
        public KagitKartHintKind[] CopyHistory()
        {
            var values = new KagitKartHintKind[seen.Count];
            seen.CopyTo(values);
            return values;
        }

        /// <summary>按原作 i+50/i+10 采样预计算弯道，保留启用特技的跳台位置。</summary>
        /// <param name="track">本场已校验的源码赛道几何。</param>
        public void Bind(KagitKartTrackGeometry track)
        {
            turns = ramps = null;
            int n = track?.sampleCount ?? 0;
            if (track?.samples?.tx == null || n < 60)
            {
                return;
            }
            turns = new float[n];
            for (int i = 0; i < n; i++)
            {
                int a = (i + 50) % n;
                int b = (i + 10) % n;
                float difference = Mathf.Atan2(track.samples.tx[a], track.samples.tz[a])
                    - Mathf.Atan2(track.samples.tx[b], track.samples.tz[b]);
                turns[i] = Mathf.Abs(Mathf.Atan2(Mathf.Sin(difference), Mathf.Cos(difference)));
            }
            var positions = new List<float>();
            foreach (var ramp in track.ramps)
            {
                if (ramp.trick)
                {
                    positions.Add(ramp.t);
                }
            }
            ramps = positions.ToArray();
            trackLength = track.length;
        }

        /// 重赛、离场或释放比赛时取消教程，不提前写 SeenTutorial，历史提示仍保留。
        public void Unbind()
        {
            turns = ramps = null;
            queue.Clear();
            TutorialVisible = false;
            Current = null;
            Changed?.Invoke();
        }

        /// <summary>只在开始倒计时3时弹出首次操作卡，倒计时本身不消耗7秒。</summary>
        /// <param name="number">倒计时事件数值。</param>
        /// <param name="seenTutorial">当前已保存的首次教程状态。</param>
        public void OnCountdown(int number, bool seenTutorial)
        {
            if (number != 3 || TutorialVisible || seenTutorial)
            {
                return;
            }
            TutorialVisible = true;
            tutorialLeft = 7;
            Changed?.Invoke();
        }

        /// <summary>玩家进入一档漂移火花时排队；高档与其他车手由调用方过滤。</summary>
        /// <param name="tier">实际漂移档位。</param>
        public void OnDriftTier(int tier)
        {
            if (tier == 1)
            {
                Once(KagitKartHintKind.Tier);
            }
        }

        /// 玩家真正获得道具时排队，撞道具箱和轮盘开始不算获得。
        public void OnItemGet() => Once(KagitKartHintKind.Item);

        /// <summary>复现原作视图规则；暂停时dt为0，先教程后情境提示。</summary>
        /// <param name="dt">非负有限表现时间；暂停或后台为0，不使用比赛固定步计时。</param>
        /// <param name="raceView">倒计时、比赛或冲线期间为true；赛前和结算为false。</param>
        /// <param name="viewAvailable">当前不是加载空视图，决定竖屏提示能否显示。</param>
        /// <param name="running">仅正在比赛时消耗教程7秒并检查弯道、跳台。</param>
        /// <param name="paused">为true时不从队列弹出新提示。</param>
        /// <param name="portrait">触控已开启且屏高大于屏宽1.1倍。</param>
        /// <param name="info">玩家当前真实道路查询结果。</param>
        /// <param name="speed">玩家实际速度；弯道提示要求大于8。</param>
        public void Advance(float dt, bool raceView, bool viewAvailable, bool running, bool paused,
            bool portrait, KagitKartQueryInfo info, float speed)
        {
            if (!float.IsFinite(dt) || dt < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(dt));
            }
            bool oldTutorial = TutorialVisible;
            var oldHint = Current;
            if (Current.HasValue && !raceView && Current != KagitKartHintKind.Portrait)
            {
                Current = null;
            }
            if (portrait)
            {
                Once(KagitKartHintKind.Portrait);
            }
            if (TutorialVisible)
            {
                if (running)
                {
                    tutorialLeft -= dt;
                }
                if (tutorialLeft <= 0 || !raceView)
                {
                    TutorialVisible = false;
                    TutorialCompleted?.Invoke();
                }
            }
            if (raceView && running && turns != null)
            {
                if (!seen.Contains(KagitKartHintKind.Corner) && speed > 8 && info.Index >= 0
                    && turns[info.Index % turns.Length] > 0.6f)
                {
                    Once(KagitKartHintKind.Corner);
                }
                if (!seen.Contains(KagitKartHintKind.Ramp))
                {
                    foreach (float ramp in ramps)
                    {
                        double ahead = ((ramp - info.T) % 1 + 1) % 1 * trackLength;
                        if (ahead > 20 && ahead < 80)
                        {
                            Once(KagitKartHintKind.Ramp);
                            break;
                        }
                    }
                }
            }
            if (Current.HasValue)
            {
                hintLeft -= dt;
                if (hintLeft <= 0)
                {
                    Current = null;
                }
            }
            bool canShow = raceView || queue.Count > 0 && queue.Peek() == KagitKartHintKind.Portrait && viewAvailable;
            if (!Current.HasValue && queue.Count > 0 && canShow && !paused && !TutorialVisible)
            {
                Current = queue.Dequeue();
                seen.Add(Current.Value);
                hintLeft = Current == KagitKartHintKind.Portrait ? 4.5f : 3.8f;
                HistoryChanged?.Invoke();
            }
            if (oldTutorial != TutorialVisible || oldHint != Current)
            {
                Changed?.Invoke();
            }
        }

        private void Once(KagitKartHintKind kind)
        {
            if (!seen.Contains(kind) && !queue.Contains(kind) && Current != kind)
            {
                queue.Enqueue(kind);
            }
        }
    }
}
