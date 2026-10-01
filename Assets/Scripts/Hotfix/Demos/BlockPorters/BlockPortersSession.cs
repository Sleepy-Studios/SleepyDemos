using System;
using System.Collections.Generic;

namespace Hotfix.BlockPorters
{
    public enum BlockPortersStatus { Playing, Failed, Won }

    public readonly struct PorterCell
    {
        public readonly int X;
        public readonly int Y;
        public PorterCell(int x, int y) { X = x; Y = y; }
    }

    [Serializable]
    public struct PorterTeamDefinition
    {
        public int Color;
        public int Count;
        public PorterTeamDefinition(int color, int count) { Color = color; Count = count; }
    }

    public sealed class BlockPortersLevelData
    {
        public readonly int Width;
        public readonly int Height;
        public readonly int[] Cells;
        public readonly PorterTeamDefinition[][] Columns;
        public readonly int Capacity;
        public readonly int ColorCount;

        public BlockPortersLevelData(int width, int height, int[] cells,
            PorterTeamDefinition[][] columns, int capacity, int colorCount)
        {
            if (width < 1 || height < 1 || width > 32 || height > 32)
                throw new ArgumentException("棋盘尺寸必须为 1–32。");
            if (cells == null || cells.Length != width * height || columns == null || (columns.Length != 4 && columns.Length != 5))
                throw new ArgumentException("棋盘或四／五列队伍配置不完整。");
            if (capacity < 1 || capacity > 5 || colorCount < 1 || colorCount > 12)
                throw new ArgumentException("初始任务位必须为 1–5，颜色数量必须为 1–12。");
            Width = width; Height = height; Capacity = capacity; ColorCount = colorCount;
            Cells = (int[])cells.Clone();
            Columns = new PorterTeamDefinition[columns.Length][];
            var bricks = new int[colorCount];
            var people = new int[colorCount];
            foreach (int color in Cells)
            {
                if (color < -1 || color >= colorCount) throw new ArgumentException("非法方块颜色。");
                if (color >= 0) bricks[color]++;
            }
            for (int column = 0; column < columns.Length; column++)
            {
                if (columns[column] == null) throw new ArgumentException("队列不能为 null。");
                Columns[column] = (PorterTeamDefinition[])columns[column].Clone();
                foreach (var team in Columns[column])
                {
                    if (team.Color < 0 || team.Color >= colorCount || team.Count < 1 || team.Count > 8)
                        throw new ArgumentException("每队人数必须为 1–8，颜色必须在色表内。");
                    people[team.Color] += team.Count;
                }
            }
            int total = 0;
            for (int color = 0; color < colorCount; color++)
            {
                if (bricks[color] != people[color]) throw new ArgumentException("各颜色队伍人数必须等于方块数。");
                total += bricks[color];
            }
            if (total == 0) throw new ArgumentException("关卡不能为空。");
        }
    }

    public sealed class PorterTeam
    {
        public int Id { get; internal set; }
        public int Slot { get; internal set; }
        public int Color { get; internal set; }
        public int Count { get; internal set; }
        public int Delivered { get; internal set; }
        public int InFlight { get; internal set; }
        public int Waiting => Count - Delivered - InFlight;
    }

    public sealed class PorterJob
    {
        public int Id { get; internal set; }
        public int TeamId { get; internal set; }
        public int CellIndex { get; internal set; }
        public int Color { get; internal set; }
        public PorterCell[] Path { get; internal set; }
        public bool IsPickedUp { get; internal set; }
    }

    /// 纯规则会话；预约不打开道路，抬起才移除方块，入坑才计入交付。
    public sealed class BlockPortersSession
    {
        private readonly BlockPortersLevelData level;
        private readonly int[] cells;
        private readonly int[] columnHeads;
        private readonly List<PorterTeam> teams = new();
        private readonly Dictionary<int, PorterJob> jobs = new();
        private readonly HashSet<int> reservedCells = new();
        private readonly int[] parents;
        private readonly int[] searchQueue;
        private readonly int[] unavailableAtRevision;
        private int boardRevision;
        private bool pathsDirty = true;
        private int nextTeamId;
        private int nextJobId;

        public int Width => level.Width;
        public int Height => level.Height;
        public int Capacity { get; private set; }
        public int Delivered { get; private set; }
        public int Total { get; }
        public int InFlight => jobs.Count;
        public int ColumnCount => level.Columns.Length;
        public int UnlockedExtraSlots { get; private set; }
        public BlockPortersStatus Status { get; private set; }
        public IReadOnlyList<PorterTeam> Teams => teams;

        /// <summary>创建独立会话，复制棋盘状态，不改写关卡配置。</summary>
        /// <param name="definition">已校验的不可空关卡数据。</param>
        public BlockPortersSession(BlockPortersLevelData definition)
        {
            level = definition ?? throw new ArgumentNullException(nameof(definition));
            cells = (int[])level.Cells.Clone();
            columnHeads = new int[level.Columns.Length];
            foreach (int cell in cells) if (cell >= 0) Total++;
            Capacity = level.Capacity;
            parents = new int[(Width + 2) * (Height + 2)];
            searchQueue = new int[parents.Length];
            unavailableAtRevision = new int[level.ColorCount];
            Array.Fill(unavailableAtRevision, -1);
        }

        /// <summary>读取棋盘颜色；已抬起或原本为空的格子返回 -1。</summary>
        /// <param name="index">从左下角开始逐行排列的格子索引。</param>
        public int GetCell(int index) => cells[index];

        /// <summary>查看队列前部，超过剩余队伍时返回 null。</summary>
        /// <param name="column">小于 ColumnCount 的队列编号。</param>
        /// <param name="offset">相对于最前队伍的非负偏移。</param>
        public PorterTeamDefinition? Peek(int column, int offset = 0)
        {
            if (column < 0 || column >= columnHeads.Length || offset < 0) return null;
            int index = columnHeads[column] + offset;
            return index < level.Columns[column].Length ? level.Columns[column][index] : null;
        }

        /// <summary>只派出指定列最前一队；满位、结束或空列返回 null。</summary>
        /// <param name="column">小于 ColumnCount 的队列编号。</param>
        public PorterTeam Dispatch(int column)
        {
            var definition = Peek(column);
            if (Status != BlockPortersStatus.Playing || teams.Count >= Capacity || !definition.HasValue) return null;
            columnHeads[column]++;
            int slot = 0;
            while (!IsSlotAvailable(slot) || teams.Exists(item => item.Slot == slot)) slot++;
            var team = new PorterTeam { Id = ++nextTeamId, Slot = slot, Color = definition.Value.Color, Count = definition.Value.Count };
            teams.Add(team);
            return team;
        }

        /// <summary>为一个待命成员预约可达同色方块，路径终点位于方块旁。</summary>
        /// <param name="teamId">本会话已派队伍的编号。</param>
        /// <param name="job">成功时返回唯一搬运任务；失败时为 null。</param>
        public bool TryAssign(int teamId, out PorterJob job)
        {
            job = null;
            var team = FindTeam(teamId);
            if (Status != BlockPortersStatus.Playing || team == null || team.Waiting <= 0) return false;
            BuildPaths();
            int target = FindTarget(team.Color, out int adjacent);
            if (target < 0) return false;
            var path = new List<PorterCell>();
            int stride = Width + 2;
            for (int point = adjacent; point >= 0; point = parents[point])
                path.Add(new PorterCell(point % stride - 1, point / stride - 1));
            path.Reverse();
            job = new PorterJob { Id = ++nextJobId, TeamId = team.Id, CellIndex = target, Color = team.Color, Path = path.ToArray() };
            jobs.Add(job.Id, job);
            reservedCells.Add(target);
            team.InFlight++;
            return true;
        }

        /// <summary>抬起预约的方块并打开该格道路；重复或无效任务不产生副作用。</summary>
        /// <param name="jobId">TryAssign 返回的唯一任务编号。</param>
        public bool PickUp(int jobId)
        {
            if (Status != BlockPortersStatus.Playing || !jobs.TryGetValue(jobId, out var job) || job.IsPickedUp) return false;
            job.IsPickedUp = true;
            cells[job.CellIndex] = -1;
            reservedCells.Remove(job.CellIndex);
            pathsDirty = true;
            boardRevision++;
            return true;
        }

        /// <summary>入坑后交付，整队完成时释放任务位；重复交付返回 false。</summary>
        /// <param name="jobId">已抬起的唯一任务编号。</param>
        public bool Deliver(int jobId)
        {
            if (Status != BlockPortersStatus.Playing || !jobs.TryGetValue(jobId, out var job) || !job.IsPickedUp) return false;
            var team = FindTeam(job.TeamId);
            jobs.Remove(jobId);
            team.InFlight--; team.Delivered++; Delivered++;
            if (team.Delivered == team.Count) teams.Remove(team);
            if (Delivered == Total) Status = BlockPortersStatus.Won;
            return true;
        }

        /// 在调度后判断堵塞；在途任务和仍可分配的目标均阻止提前判负。
        public void EvaluateOutcome()
        {
            if (Status != BlockPortersStatus.Playing || InFlight > 0 || teams.Count < Capacity) return;
            BuildPaths();
            foreach (var team in teams)
                if (team.Waiting > 0 && FindTarget(team.Color, out _) >= 0) return;
            Status = BlockPortersStatus.Failed;
        }

        /// <summary>免费槽沿用关卡容量；左右额外槽使用固定编号 5／6。</summary>
        /// <param name="slot">物理槽编号，不等同于已开放槽的数量。</param>
        public bool IsSlotAvailable(int slot) => slot >= 0 && slot < level.Capacity ||
            slot >= 5 && slot <= 6 && (UnlockedExtraSlots & (1 << (slot - 5))) != 0;

        /// <summary>每侧每关解锁一次，失败时恢复继续，不改动棋盘与已派队伍。</summary>
        /// <param name="side">0 为左侧，1 为右侧。</param>
        public bool TryUnlockExtraSlot(int side)
        {
            if (side < 0 || side > 1 || Status == BlockPortersStatus.Won || (UnlockedExtraSlots & (1 << side)) != 0) return false;
            UnlockedExtraSlots |= 1 << side; Capacity++; Status = BlockPortersStatus.Playing;
            return true;
        }

        /// 复制无在途任务的会话，用于稳定点搜索，不共享可变棋盘或队伍。
        public BlockPortersSession CloneStable()
        {
            if (InFlight != 0) throw new InvalidOperationException("只能复制稳定会话。");
            var copy = new BlockPortersSession(level);
            Array.Copy(cells, copy.cells, cells.Length);
            Array.Copy(columnHeads, copy.columnHeads, columnHeads.Length);
            copy.Capacity = Capacity; copy.Delivered = Delivered; copy.Status = Status; copy.UnlockedExtraSlots = UnlockedExtraSlots;
            copy.nextTeamId = nextTeamId; copy.nextJobId = nextJobId;
            foreach (var team in teams) copy.teams.Add(new PorterTeam { Id = team.Id, Slot = team.Slot, Color = team.Color, Count = team.Count, Delivered = team.Delivered });
            return copy;
        }

        /// 稳定状态的精确搜索键；不依赖哈希碰撞判断等价状态。
        public string StableKey() => Capacity + ":" + UnlockedExtraSlots + ":" + string.Join(",", columnHeads) + ":" + string.Join(",", cells)
            + ":" + string.Join(";", teams.ConvertAll(t => $"{t.Color},{t.Count},{t.Delivered},{t.Slot}"));

        private PorterTeam FindTeam(int id) => teams.Find(team => team.Id == id);

        private int FindTarget(int color, out int adjacent)
        {
            if (unavailableAtRevision[color] == boardRevision) { adjacent = -1; return -1; }
            int stride = Width + 2;
            for (int index = 0; index < cells.Length; index++)
            {
                if (cells[index] != color || reservedCells.Contains(index)) continue;
                int point = (index / Width + 1) * stride + index % Width + 1;
                if (parents[point - stride] != -2) { adjacent = point - stride; return index; }
                if (parents[point - 1] != -2) { adjacent = point - 1; return index; }
                if (parents[point + 1] != -2) { adjacent = point + 1; return index; }
                if (parents[point + stride] != -2) { adjacent = point + stride; return index; }
            }
            adjacent = -1;
            unavailableAtRevision[color] = boardRevision;
            return -1;
        }

        private void BuildPaths()
        {
            if (!pathsDirty) return;
            pathsDirty = false;
            Array.Fill(parents, -2);
            parents[0] = -1;
            int head = 0, tail = 1, stride = Width + 2;
            searchQueue[0] = 0;
            while (head < tail)
            {
                int point = searchQueue[head++], x = point % stride, y = point / stride;
                if (x > 0) Visit(point - 1, point, ref tail);
                if (x < Width + 1) Visit(point + 1, point, ref tail);
                if (y > 0) Visit(point - stride, point, ref tail);
                if (y < Height + 1) Visit(point + stride, point, ref tail);
            }
        }

        private void Visit(int point, int parent, ref int tail)
        {
            if (parents[point] != -2) return;
            int x = point % (Width + 2) - 1, y = point / (Width + 2) - 1;
            if (x >= 0 && x < Width && y >= 0 && y < Height && cells[y * Width + x] >= 0) return;
            parents[point] = parent;
            searchQueue[tail++] = point;
        }
    }
}
