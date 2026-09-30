using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Hotfix.BlockPorters;

namespace Hotfix.Editor.BlockPorters
{
    public sealed class PorterGeneratedLevel
    {
        public PorterTeamDefinition[][] Columns;
        public PorterAnalysis Analysis;
        public bool MeetsDifficulty;
        public string Diagnostic;
    }

    public static class BlockPortersLevelGenerator
    {
        /// <summary>从可剥离轨迹安排队伍，再作有界扰动并使用共享调度器验证。</summary>
        /// <param name="width">棋盘宽度。</param>
        /// <param name="height">棋盘高度。</param>
        /// <param name="cells">不会被修改的色号网格。</param>
        /// <param name="colorCount">1–12 个色号。</param>
        /// <param name="settings">难度、固定种子及候选/搜索预算。</param>
        /// <param name="token">取消后不返回已验证候选。</param>
        public static PorterGeneratedLevel Generate(int width, int height, int[] cells, int colorCount, PorterRecipeSettings settings, CancellationToken token = default)
        {
            var random = new Random(settings.Seed);
            var trace = Peel(width, height, cells, settings.Difficulty, random, token);
            PorterGeneratedLevel best = null;
            int candidates = Math.Min(24, Math.Max(1, settings.Candidates));
            for (int attempt = 0; attempt < candidates; attempt++)
            {
                token.ThrowIfCancellationRequested();
                var columns = Enumerable.Range(0, 4).Select(_ => new List<PorterTeamDefinition>()).ToArray();
                for (int t = 0; t < trace.Length; t++)
                {
                    int col = settings.Difficulty == PorterDifficulty.Easy && attempt == 0 ? t % 4
                        : settings.Difficulty == PorterDifficulty.Hard && attempt == 0 ? trace[t].Color % 4 : random.Next(4);
                    columns[col].Add(trace[t]);
                }
                // 只扰动少量隐藏位置，每个候选重新解算，绝不沿用扰动前的答案。
                int swaps = settings.Difficulty == PorterDifficulty.Easy ? 0 : (settings.Difficulty == PorterDifficulty.Normal ? 2 : 6);
                for (int i = 0; i < swaps * (attempt > 0 ? 1 : 0); i++)
                {
                    var column = columns[random.Next(4)];
                    if (column.Count < 2) continue;
                    int a = random.Next(column.Count), b = random.Next(column.Count);
                    (column[a], column[b]) = (column[b], column[a]);
                }
                if (attempt > 0 && settings.Difficulty != PorterDifficulty.Easy)
                    PerturbQuotas(columns, random, settings.Difficulty);
                var queues = columns.Select(c => c.ToArray()).ToArray();
                var data = new BlockPortersLevelData(width, height, cells, queues, 5, colorCount);
                var analysis = BlockPortersAnalysis.Solve(data, settings.MaxStates, settings.SearchSeconds, token);
                if (analysis.State == PorterSolvability.Solvable)
                    BlockPortersAnalysis.Measure(data, analysis, settings.Seed, settings.PolicyRuns, token);
                bool matches = Matches(settings.Difficulty, analysis);
                var candidate = new PorterGeneratedLevel { Columns = queues, Analysis = analysis, MeetsDifficulty = matches,
                    Diagnostic = matches ? "符合目标策略难度" : "没有达到目标策略压力；调整队伍配额、队列或局部颜色后重新分析。" };
                if (best == null || Score(candidate) > Score(best)) best = candidate;
                if (matches) return candidate;
            }
            return best;
        }

        /// <summary>初版难度门槛仅描述模拟策略，不代表真实玩家通过率。</summary>
        /// <param name="difficulty">目标难度。</param>
        /// <param name="analysis">已验证的统计结果。</param>
        public static bool Matches(PorterDifficulty difficulty, PorterAnalysis analysis)
        {
            if (analysis.State != PorterSolvability.Solvable) return false;
            if (difficulty == PorterDifficulty.Easy) return true;
            if (analysis.PolicyRuns == 0) return false;
            double randomRate = analysis.RandomWins / (analysis.PolicyRuns / 2.0);
            return analysis.MaxWaitingTeams >= (difficulty == PorterDifficulty.Hard ? 2 : 1)
                && randomRate < (difficulty == PorterDifficulty.Hard ? .8 : .98);
        }
        private static double Score(PorterGeneratedLevel item) => (item.Analysis.State == PorterSolvability.Solvable ? 10000 : 0)
            + item.Analysis.MaxWaitingTeams * 100 + item.Analysis.DeadlockChoices - item.Analysis.RandomWins;

        private static void PerturbQuotas(List<PorterTeamDefinition>[] columns, Random random, PorterDifficulty difficulty)
        {
            int changes = difficulty == PorterDifficulty.Hard ? 8 : 4;
            for (int i = 0; i < changes; i++)
            {
                int firstColumn = random.Next(4);
                if (columns[firstColumn].Count == 0) continue;
                int firstIndex = random.Next(columns[firstColumn].Count);
                var first = columns[firstColumn][firstIndex];
                var partners = new List<(int column, int index)>();
                for (int c = 0; c < 4; c++) for (int t = 0; t < columns[c].Count; t++)
                    if ((c != firstColumn || t != firstIndex) && columns[c][t].Color == first.Color) partners.Add((c, t));
                if (partners.Count == 0) continue;
                var other = partners[random.Next(partners.Count)];
                int total = first.Count + columns[other.column][other.index].Count;
                int minimum = Math.Max(1, total - 8), maximum = Math.Min(8, total - 1);
                int preferred = difficulty == PorterDifficulty.Normal ? 4 : 2;
                if (preferred <= maximum) minimum = Math.Max(minimum, preferred);
                int amount = random.Next(minimum, maximum + 1);
                columns[firstColumn][firstIndex] = new PorterTeamDefinition(first.Color, amount);
                columns[other.column][other.index] = new PorterTeamDefinition(first.Color, total - amount);
            }
        }

        private static PorterTeamDefinition[] Peel(int width, int height, int[] source, PorterDifficulty difficulty, Random random, CancellationToken token)
        {
            var board = (int[])source.Clone(); var result = new List<PorterTeamDefinition>();
            while (board.Any(c => c >= 0))
            {
                token.ThrowIfCancellationRequested();
                var exposed = Exposed(width, height, board);
                int color = board[exposed[random.Next(exposed.Count)]];
                int quota = difficulty == PorterDifficulty.Easy ? random.Next(6, 9) : difficulty == PorterDifficulty.Normal ? random.Next(4, 9) : random.Next(2, 9);
                int removed = 0;
                while (removed < quota)
                {
                    var reachable = Exposed(width, height, board).Where(i => board[i] == color).ToArray();
                    if (reachable.Length == 0) break;
                    int n = Math.Min(quota - removed, reachable.Length);
                    for (int i = 0; i < n; i++) board[reachable[i]] = -1;
                    removed += n;
                }
                result.Add(new PorterTeamDefinition(color, removed));
            }
            return result.ToArray();
        }

        private static List<int> Exposed(int width, int height, int[] board)
        {
            int stride = width + 2; var seen = new bool[stride * (height + 2)];
            var queue = new Queue<int>(); queue.Enqueue(0); seen[0] = true;
            var cells = new SortedSet<int>();
            while (queue.Count > 0)
            {
                int p = queue.Dequeue(), x = p % stride, y = p / stride;
                foreach (int next in new[] { x > 0 ? p - 1 : -1, x < width + 1 ? p + 1 : -1, y > 0 ? p - stride : -1, y < height + 1 ? p + stride : -1 })
                {
                    if (next < 0 || seen[next]) continue;
                    int nx = next % stride - 1, ny = next / stride - 1;
                    if (nx >= 0 && nx < width && ny >= 0 && ny < height && board[ny * width + nx] >= 0) cells.Add(ny * width + nx);
                    else { seen[next] = true; queue.Enqueue(next); }
                }
            }
            return cells.ToList();
        }
    }
}
