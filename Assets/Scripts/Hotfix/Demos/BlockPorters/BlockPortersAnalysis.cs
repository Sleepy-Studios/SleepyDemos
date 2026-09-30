using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace Hotfix.BlockPorters
{
    public enum PorterSolvability { Solvable, Unsolvable, Unknown }
    public sealed class PorterAnalysis
    {
        public PorterSolvability State;
        public int[] Solution = Array.Empty<int>();
        public int States;
        public int CriticalChoices;
        public int MaxWaitingTeams;
        public int DeadlockChoices;
        public int RandomWins;
        public int GreedyWins;
        public int PolicyRuns;
        public string Message;
    }

    /// 稳定点上的有界搜索；从同一个调度器取得可回放参考解。
    public static class BlockPortersAnalysis
    {
        /// <summary>搜索无需复活的参考解，预算耗尽或取消返回 Unknown。</summary>
        /// <param name="data">人数守恒的规则数据。</param>
        /// <param name="maxStates">默认最多 20,000 个状态。</param>
        /// <param name="seconds">默认最多 5 秒，不将超限视为无解。</param>
        /// <param name="token">取消后不返回已验证结果。</param>
        public static PorterAnalysis Solve(BlockPortersLevelData data, int maxStates = 20000, double seconds = 5, CancellationToken token = default)
        {
            var result = new PorterAnalysis();
            var watch = Stopwatch.StartNew();
            var visited = new HashSet<string>();
            var stack = new Stack<(BlockPortersSession session, int[] trace)>();
            stack.Push((new BlockPortersSession(data), Array.Empty<int>()));
            while (stack.Count > 0)
            {
                if (token.IsCancellationRequested || result.States >= maxStates || watch.Elapsed.TotalSeconds >= seconds)
                { result.State = PorterSolvability.Unknown; result.Message = token.IsCancellationRequested ? "已取消" : "搜索预算耗尽，未确认"; return result; }
                var node = stack.Pop();
                if (!visited.Add(node.session.StableKey())) continue;
                result.States++;
                if (node.session.Status == BlockPortersStatus.Won)
                { result.State = PorterSolvability.Solvable; result.Solution = node.trace; result.Message = "已找到无需复活的参考解"; return result; }
                if (node.session.Status == BlockPortersStatus.Failed || node.session.Teams.Count >= node.session.Capacity) continue;
                var children = new List<(BlockPortersSession session, int[] trace)>();
                for (int col = 0; col < 4; col++)
                {
                    if (!node.session.Peek(col).HasValue) continue;
                    var child = node.session.CloneStable();
                    var scheduler = new BlockPortersScheduler(child);
                    scheduler.Dispatch(col); scheduler.Settle();
                    children.Add((child, node.trace.Concat(new[] { col }).ToArray()));
                }
                // 先探索取得进展的分支；不能取得进展的派队也保留，以覆盖列头开路。
                foreach (var child in children.OrderBy(c => c.session.Delivered)) stack.Push(child);
            }
            result.State = PorterSolvability.Unsolvable; result.Message = "已穷尽稳定点选择，无需复活时无解"; return result;
        }

        /// <summary>参考解和固定种子策略评估；数据不会被改写。</summary>
        /// <param name="data">需要与参考解对应的定义。</param>
        /// <param name="analysis">已可解的搜索结果，原地写入难度指标。</param>
        /// <param name="seed">策略采样种子。</param>
        /// <param name="runs">每种策略最多 100 次，总计最多 200 次。</param>
        /// <param name="token">中断后拒绝沿用可解标签。</param>
        public static void Measure(BlockPortersLevelData data, PorterAnalysis analysis, int seed, int runs = 100, CancellationToken token = default)
        {
            if (analysis.State != PorterSolvability.Solvable) return;
            var scheduler = new BlockPortersScheduler(new BlockPortersSession(data));
            foreach (int choice in analysis.Solution)
            {
                int losses = 0, risky = 0, options = 0;
                for (int col = 0; col < 4; col++)
                {
                    if (!scheduler.Session.Peek(col).HasValue) continue;
                    options++;
                    var alternative = new BlockPortersScheduler(scheduler.Session.CloneStable());
                    alternative.Dispatch(col); alternative.Settle();
                    if (alternative.Session.Status == BlockPortersStatus.Failed) losses++;
                    if (alternative.Session.Teams.Count > scheduler.Session.Teams.Count) risky++;
                    analysis.MaxWaitingTeams = Math.Max(analysis.MaxWaitingTeams, alternative.Session.Teams.Count);
                }
                if (options > 1 && risky > 0) analysis.CriticalChoices++;
                analysis.DeadlockChoices += losses;
                if (!scheduler.Dispatch(choice)) { analysis.State = PorterSolvability.Unknown; analysis.Message = "参考解失效"; return; }
                scheduler.Settle();
                analysis.MaxWaitingTeams = Math.Max(analysis.MaxWaitingTeams, scheduler.Session.Teams.Count);
            }
            if (scheduler.Session.Status != BlockPortersStatus.Won) { analysis.State = PorterSolvability.Unknown; analysis.Message = "参考解未通关"; return; }
            int bounded = Math.Min(100, Math.Max(0, runs));
            var random = new Random(seed);
            for (int i = 0; i < bounded; i++)
            {
                if (token.IsCancellationRequested) { analysis.State = PorterSolvability.Unknown; analysis.Message = "已取消"; return; }
                if (PlayPolicy(data, random, false, out int randomPeak)) analysis.RandomWins++;
                if (PlayPolicy(data, random, true, out int greedyPeak)) analysis.GreedyWins++;
                analysis.MaxWaitingTeams = Math.Max(analysis.MaxWaitingTeams, Math.Max(randomPeak, greedyPeak));
                analysis.PolicyRuns += 2;
            }
        }

        private static bool PlayPolicy(BlockPortersLevelData data, Random random, bool greedy, out int maxWaiting)
        {
            maxWaiting = 0;
            var scheduler = new BlockPortersScheduler(new BlockPortersSession(data));
            int limit = data.Columns.Sum(c => c.Length);
            while (limit-- > 0 && scheduler.Session.Status == BlockPortersStatus.Playing)
            {
                if (scheduler.Session.Teams.Count >= scheduler.Session.Capacity) break;
                var choices = Enumerable.Range(0, 4).Where(c => scheduler.Session.Peek(c).HasValue).ToArray();
                if (choices.Length == 0) break;
                int chosen = choices[random.Next(choices.Length)];
                if (greedy)
                {
                    int best = -1;
                    foreach (int col in choices.OrderBy(_ => random.Next()))
                    {
                        var probe = new BlockPortersScheduler(scheduler.Session.CloneStable());
                        probe.Dispatch(col); probe.Settle();
                        int score = probe.Session.Delivered;
                        if (score > best) { best = score; chosen = col; }
                    }
                }
                scheduler.Dispatch(chosen); scheduler.Settle();
                maxWaiting = Math.Max(maxWaiting, scheduler.Session.Teams.Count);
            }
            return scheduler.Session.Status == BlockPortersStatus.Won;
        }
    }
}
