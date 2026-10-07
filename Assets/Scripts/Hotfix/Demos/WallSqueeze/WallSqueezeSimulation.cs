using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hotfix.WallSqueeze
{
    /// 当前规则步的统一结果。
    public enum WallSqueezeResult
    {
        Playing,
        Won,
        Lost
    }

    /// 失败原因；胜利和进行中为 None。
    public enum WallSqueezeLossReason
    {
        None,
        ResidentDeath,
        Timeout
    }

    /// 规则状态，不持有场景、输入或 UI。
    public sealed class WallSqueezeBody
    {
        /// 当前世界中心。
        public Vector2 Center;
        /// 受压轴上的当前尺寸。
        public Vector2 Size;
        /// 未受压尺寸；钻缝怪的高度为普通怪的一半。
        public Vector2 RestSize;
        /// 各轴的压缩下限；护甲怪的 X 轴不能缩短。
        public Vector2 MinimumSize;
        /// 非住户的行为类型。
        public WallSqueezeMonsterType Type;
        /// 蓝色住户，不自主游走。
        public bool Resident;
        /// 死亡后不再参加碰撞。
        public bool Dead;
        /// 当前最小尺寸夹持累计秒数。
        public float Hold;
        internal Vector2 Direction;
    }

    /// 轴对齐推链规则。连续位移按接触约束求解，不依赖刚体力。
    public sealed class WallSqueezeSimulation
    {
        private const float Epsilon = 0.0001f;
        private readonly WallSqueezeSettings settings;
        private readonly WallSqueezeLevel level;
        private readonly List<int> order = new();
        private readonly List<int> affected = new();
        private readonly List<WallSqueezeBody> deaths = new();
        private readonly bool[,] pressured;
        private readonly Vector2[] trialCenters;
        private readonly Vector2[] trialSizes;
        private readonly float[] reductions;
        private System.Random random;
        private float turnClock;

        /// 当前滑墙副本，不修改保存布局。
        public WallSqueezeWallLayout[] Walls { get; }
        /// 当前所有方块，包括已死亡状态供表现层消费。
        public List<WallSqueezeBody> Bodies { get; } = new();
        /// 整步判定结果，住户死亡优先。
        public WallSqueezeResult Result { get; private set; }
        /// 本次 Advance 合并的红怪死亡数。
        public int KilledThisAdvance { get; private set; }
        /// 仍存活的红怪数。
        public int Remaining { get; private set; }
        /// 仍存活的蓝色住户数。
        public int ResidentsAlive { get; private set; }
        /// 实际推进的规则秒数；暂停宿主不调用 Advance。
        public float ElapsedSeconds { get; private set; }
        /// 剩余规则秒数，不限时时为正无穷。
        public float RemainingSeconds => level.TimeLimit > 0 ? Mathf.Max(0, level.TimeLimit - ElapsedSeconds) : float.PositiveInfinity;
        /// 失败的业务原因。
        public WallSqueezeLossReason Reason { get; private set; }

        /// <summary>复制关卡状态，资产不会被规则修改。</summary>
        /// <param name="parameters">有效正数规则参数。</param>
        /// <param name="layout">保存的初始布局。</param>
        public WallSqueezeSimulation(WallSqueezeSettings parameters, WallSqueezeLevel layout)
        {
            settings = parameters != null ? parameters : throw new ArgumentNullException(nameof(parameters));
            level = layout != null ? layout : throw new ArgumentNullException(nameof(layout));
            if (!Positive(settings.Room.x) || !Positive(settings.Room.y) || !Positive(settings.MinimumSize)
                || !Positive(settings.BodySize) || settings.BodySize < settings.MinimumSize || !Positive(settings.HoldSeconds)
                || !Positive(settings.WallSpeed) || !Positive(settings.TurnSeconds) || !Positive(settings.RecoverySpeed)
                || !float.IsFinite(settings.MonsterSpeed) || settings.MonsterSpeed < 0
                || !Positive(settings.SlipperSpeedMultiplier))
            {
                throw new ArgumentException("夹爆它规则参数无效。");
            }
            if (level.Walls == null || level.Walls.Length == 0 || level.Monsters == null || level.Residents == null
                || level.SpecialMonsters == null || level.FixedWalls == null || !float.IsFinite(level.TimeLimit) || level.TimeLimit < 0)
            {
                throw new ArgumentException("关卡必须保存墙和对象数组。");
            }
            Walls = new WallSqueezeWallLayout[level.Walls.Length + level.FixedWalls.Length];
            CopyWalls();
            bool movable = false;
            foreach (var wall in Walls)
            {
                if ((uint)wall.Axis > 1 || !Positive(wall.Size.x) || !Positive(wall.Size.y)
                    || !Finite(wall.Center) || !Finite(wall.Track) || wall.Track.x > wall.Track.y
                    || wall.Center[wall.Axis] < wall.Track.x || wall.Center[wall.Axis] > wall.Track.y
                    || !InsideRoom(wall.Center, wall.Size))
                {
                    throw new ArgumentException("墙轴、初始位置、范围或尺寸无效。");
                }
                movable |= !wall.IsFixed;
            }
            if (!movable)
            {
                throw new ArgumentException("关卡至少需要一面可移动墙。");
            }
            for (int i = 0; i < Walls.Length; i++)
            {
                for (int j = i + 1; j < Walls.Length; j++)
                {
                    if (OverlapCross(Walls[i].Center, Walls[i].Size, Walls[j].Center, Walls[j].Size, 0)
                        && OverlapCross(Walls[i].Center, Walls[i].Size, Walls[j].Center, Walls[j].Size, 1))
                    {
                        throw new ArgumentException("保存的墙初始位置不能交叠。");
                    }
                }
            }
            foreach (var center in level.Monsters)
            {
                AddBody(center, WallSqueezeMonsterType.Normal, false);
            }
            foreach (var monster in level.SpecialMonsters)
            {
                if ((uint)monster.Type > (uint)WallSqueezeMonsterType.Slipper)
                {
                    throw new ArgumentException("保存的怪物类型无效。");
                }
                AddBody(monster.Center, monster.Type, false);
            }
            foreach (var center in level.Residents)
            {
                AddBody(center, WallSqueezeMonsterType.Normal, true);
            }
            trialCenters = new Vector2[Bodies.Count];
            trialSizes = new Vector2[Bodies.Count];
            reductions = new float[Bodies.Count];
            pressured = new bool[Bodies.Count, 2];
            Reset();
        }

        /// 恢复布局、种子、尺寸、计数和夹持时钟。
        public void Reset()
        {
            CopyWalls();
            random = new System.Random(level.Seed);
            turnClock = 0;
            Result = WallSqueezeResult.Playing;
            Reason = WallSqueezeLossReason.None;
            ElapsedSeconds = 0;
            Remaining = level.Monsters.Length + level.SpecialMonsters.Length;
            ResidentsAlive = level.Residents.Length;
            KilledThisAdvance = 0;
            Array.Clear(pressured, 0, pressured.Length);
            for (int i = 0; i < Bodies.Count; i++)
            {
                var body = Bodies[i];
                if (i < level.Monsters.Length)
                {
                    body.Center = level.Monsters[i];
                }
                else if (i < Remaining)
                {
                    body.Center = level.SpecialMonsters[i - level.Monsters.Length].Center;
                }
                else
                {
                    body.Center = level.Residents[i - Remaining];
                }
                body.Size = body.RestSize;
                body.Dead = false;
                body.Hold = 0;
                body.Direction = NextDirection();
            }
        }

        /// <summary>推进规则；目标坐标按保存速度到达，-1 表示没有墙命令。</summary>
        /// <param name="seconds">非负有限时长，暂停时宿主不调用。</param>
        /// <param name="wallIndex">选中墙索引或 -1。</param>
        /// <param name="target">墙允许轴上的绝对目标坐标。</param>
        public void Advance(float seconds, int wallIndex = -1, float target = 0)
        {
            if (!float.IsFinite(seconds) || seconds < 0 || !float.IsFinite(target))
            {
                throw new ArgumentOutOfRangeException(nameof(seconds));
            }
            KilledThisAdvance = 0;
            while (seconds > 0 && Result == WallSqueezeResult.Playing)
            {
                float delta = Mathf.Min(seconds, 1f / 120);
                seconds -= delta;
                ElapsedSeconds += delta;
                Wander(delta);
                ReleaseRetreat(wallIndex, target);
                if ((uint)wallIndex < Walls.Length && !Walls[wallIndex].IsFixed)
                {
                    MoveWall(wallIndex, target, delta);
                }
                Recover(delta);
                CountDeaths(delta);
                if (Result == WallSqueezeResult.Playing && level.TimeLimit > 0 && ElapsedSeconds >= level.TimeLimit - Epsilon)
                {
                    ElapsedSeconds = Mathf.Max(ElapsedSeconds, level.TimeLimit);
                    Result = WallSqueezeResult.Lost;
                    Reason = WallSqueezeLossReason.Timeout;
                }
            }
        }

        private void MoveWall(int index, float target, float delta)
        {
            var wall = Walls[index];
            int axis = wall.Axis;
            float next = Mathf.MoveTowards(wall.Center[axis], Mathf.Clamp(target, wall.Track.x, wall.Track.y), settings.WallSpeed * delta);
            float sign = Mathf.Sign(next - wall.Center[axis]);
            if (Mathf.Abs(next - wall.Center[axis]) <= Epsilon)
            {
                return;
            }
            // 先 sweep 所有墙，包括垂直相交的另一面滑墙。
            for (int i = 0; i < Walls.Length; i++)
            {
                if (i == index || !OverlapCross(wall.Center, wall.Size, Walls[i].Center, Walls[i].Size, axis))
                {
                    continue;
                }
                float edge = Walls[i].Center[axis] - sign * Walls[i].Size[axis] / 2;
                if (sign * (edge - wall.Center[axis]) >= -Epsilon)
                {
                    next = sign > 0 ? Mathf.Min(next, edge - wall.Size[axis] / 2) : Mathf.Max(next, edge + wall.Size[axis] / 2);
                }
            }
            order.Clear();
            affected.Clear();
            Array.Clear(reductions, 0, reductions.Length);
            for (int i = 0; i < Bodies.Count; i++)
            {
                if (!Bodies[i].Dead)
                {
                    order.Add(i);
                }
            }
            order.Sort((a, b) => (sign * Bodies[a].Center[axis]).CompareTo(sign * Bodies[b].Center[axis]));
            Solve(index, next, sign, 0, true);
            if (!Solve(index, next, sign, settings.BodySize, false))
            {
                float low = wall.Center[axis];
                float high = next;
                for (int i = 0; i < 20; i++)
                {
                    float mid = (low + high) / 2;
                    if (Solve(index, mid, sign, settings.BodySize, false))
                    {
                        low = mid;
                    }
                    else
                    {
                        high = mid;
                    }
                }
                next = low;
            }
            // ponytail: 小关卡用 O(n²) 分连通推链；对象大量增加时再改空间索引。
            var pending = new List<int>(affected);
            while (pending.Count > 0)
            {
                var group = new List<int> { pending[0] };
                pending.RemoveAt(0);
                for (int cursor = 0; cursor < group.Count; cursor++)
                {
                    for (int p = pending.Count - 1; p >= 0; p--)
                    {
                        var a = Bodies[group[cursor]];
                        var b = Bodies[pending[p]];
                        if (OverlapCross(a.Center, a.Size, b.Center, b.Size, axis))
                        {
                            group.Add(pending[p]);
                            pending.RemoveAt(p);
                        }
                    }
                }
                float shrinkLow = 0;
                float shrinkHigh = settings.BodySize;
                if (Solve(index, next, sign, 0, false, group))
                {
                    shrinkHigh = 0;
                }
                for (int i = 0; i < 20 && shrinkHigh > 0; i++)
                {
                    float mid = (shrinkLow + shrinkHigh) / 2;
                    if (Solve(index, next, sign, mid, false, group))
                    {
                        shrinkHigh = mid;
                    }
                    else
                    {
                        shrinkLow = mid;
                    }
                }
                foreach (int body in group)
                {
                    reductions[body] = shrinkHigh;
                }
            }
            Solve(index, next, sign, 0, false, new List<int>(), true);
            wall.Center[axis] = next;
            Walls[index] = wall;
            foreach (int i in affected)
            {
                if (trialSizes[i][axis] < Bodies[i].Size[axis] - Epsilon)
                {
                    pressured[i, axis] = true;
                }
                Bodies[i].Center = trialCenters[i];
                Bodies[i].Size = trialSizes[i];
            }
        }

        // 一维约束图按前后顺序求解；同一推链统一减尺寸，最小值封顶。
        private bool Solve(int wallIndex, float wallCoordinate, float sign, float shrink, bool collect, List<int> group = null, bool checkAll = false)
        {
            var wall = Walls[wallIndex];
            int axis = wall.Axis;
            float wallFace = sign * wallCoordinate + wall.Size[axis] / 2;
            float oldFace = sign * wall.Center[axis] + wall.Size[axis] / 2;
            bool valid = true;
            foreach (int i in order)
            {
                var body = Bodies[i];
                Vector2 size = body.Size;
                if (affected.Contains(i))
                {
                    float reduction = group == null || group.Contains(i) ? shrink : reductions[i];
                    size[axis] = Mathf.Max(body.MinimumSize[axis], size[axis] - reduction);
                }
                float center = sign * body.Center[axis];
                // 压缩保留近侧面的位置，由推进约束重新放置，避免围绕旧中心缩小制造假空隙。
                if (affected.Contains(i))
                {
                    center -= (body.Size[axis] - size[axis]) / 2;
                }
                bool moved = false;
                if (sign * body.Center[axis] - body.Size[axis] / 2 >= oldFace - Epsilon && OverlapCross(body.Center, body.Size, wall.Center, wall.Size, axis))
                {
                    float pushed = wallFace + size[axis] / 2;
                    moved = pushed > center + Epsilon;
                    center = Mathf.Max(center, pushed);
                }
                foreach (int previous in order)
                {
                    if (previous == i)
                    {
                        break;
                    }
                    if (!affected.Contains(previous) || !OverlapCross(body.Center, body.Size, Bodies[previous].Center, Bodies[previous].Size, axis))
                    {
                        continue;
                    }
                    float pushed = sign * trialCenters[previous][axis] + trialSizes[previous][axis] / 2 + size[axis] / 2;
                    moved |= pushed > center + Epsilon;
                    center = Mathf.Max(center, pushed);
                }
                if (collect && moved && !affected.Contains(i))
                {
                    affected.Add(i);
                }
                trialCenters[i] = body.Center;
                trialCenters[i][axis] = sign * center;
                trialSizes[i] = size;
                if (!affected.Contains(i))
                {
                    continue;
                }
                float limit = sign > 0 ? settings.Room[axis] : 0;
                float signedLimit = sign * limit;
                for (int w = 0; w < Walls.Length; w++)
                {
                    if (w == wallIndex || !OverlapCross(body.Center, body.Size, Walls[w].Center, Walls[w].Size, axis))
                    {
                        continue;
                    }
                    float edge = sign * Walls[w].Center[axis] - Walls[w].Size[axis] / 2;
                    if (edge >= sign * body.Center[axis] - Epsilon)
                    {
                        signedLimit = Mathf.Min(signedLimit, edge);
                    }
                }
                if (group == null || group.Contains(i) || checkAll)
                {
                    valid &= center + size[axis] / 2 <= signedLimit + Epsilon;
                }
            }
            return valid;
        }

        private void Wander(float delta)
        {
            turnClock += delta;
            if (turnClock >= settings.TurnSeconds)
            {
                turnClock -= settings.TurnSeconds;
                foreach (var body in Bodies)
                {
                    body.Direction = NextDirection();
                }
            }
            for (int i = 0; i < Bodies.Count; i++)
            {
                var body = Bodies[i];
                if (body.Dead || body.Resident)
                {
                    continue;
                }
                int axis = body.Direction.x == 0 ? 1 : 0;
                float speed = body.Type == WallSqueezeMonsterType.Slipper ? settings.MonsterSpeed * settings.SlipperSpeedMultiplier : settings.MonsterSpeed;
                float requested = body.Direction[axis] * speed * delta;
                float allowed = FreeMove(body, axis, requested);
                body.Center[axis] += allowed;
                if (Mathf.Abs(allowed - requested) > Epsilon)
                {
                    body.Direction = -body.Direction;
                }
            }
        }

        private float FreeMove(WallSqueezeBody body, int axis, float requested)
        {
            float sign = Mathf.Sign(requested);
            float face = body.Center[axis] + sign * body.Size[axis] / 2;
            float gap = sign > 0 ? settings.Room[axis] - face : face;
            foreach (var wall in Walls)
            {
                if (OverlapCross(body.Center, body.Size, wall.Center, wall.Size, axis))
                {
                    float distance = sign * (wall.Center[axis] - face) - wall.Size[axis] / 2;
                    if (distance >= -Epsilon * 4)
                    {
                        gap = Mathf.Min(gap, Mathf.Max(0, distance));
                    }
                }
            }
            foreach (var other in Bodies)
            {
                if (other == body || other.Dead || !OverlapCross(body.Center, body.Size, other.Center, other.Size, axis))
                {
                    continue;
                }
                float distance = sign * (other.Center[axis] - face) - other.Size[axis] / 2;
                if (distance >= -Epsilon * 4)
                {
                    gap = Mathf.Min(gap, Mathf.Max(0, distance));
                }
            }
            return sign * Mathf.Min(Mathf.Abs(requested), gap);
        }

        private void Recover(float delta)
        {
            for (int i = 0; i < Bodies.Count; i++)
            {
                var body = Bodies[i];
                if (body.Dead)
                {
                    continue;
                }
                for (int axis = 0; axis < 2; axis++)
                {
                    if (pressured[i, axis] && Supported(body, axis, 1) && Supported(body, axis, -1))
                    {
                        continue;
                    }
                    float missing = Mathf.Min(body.RestSize[axis] - body.Size[axis], settings.RecoverySpeed * delta);
                    if (missing <= Epsilon)
                    {
                        continue;
                    }
                    float positive = FreeMove(body, axis, missing / 2);
                    float negative = -FreeMove(body, axis, -missing / 2);
                    // 允许向空的一侧恢复，另一侧仍保持接触。
                    if (positive + negative < missing)
                    {
                        positive = FreeMove(body, axis, missing - negative);
                        negative = -FreeMove(body, axis, -(missing - positive));
                    }
                    body.Size[axis] += positive + negative;
                    body.Center[axis] += (positive - negative) / 2;
                }
            }
        }

        private void CountDeaths(float delta)
        {
            bool lost = false;
            deaths.Clear();
            for (int i = 0; i < Bodies.Count; i++)
            {
                var body = Bodies[i];
                if (body.Dead)
                {
                    continue;
                }
                bool clamped = false;
                for (int axis = 0; axis < 2; axis++)
                {
                    clamped |= body.MinimumSize[axis] < body.RestSize[axis] - Epsilon
                        && pressured[i, axis] && body.Size[axis] <= body.MinimumSize[axis] + Epsilon * 3
                        && Supported(body, axis, 1) && Supported(body, axis, -1);
                }
                body.Hold = clamped ? body.Hold + delta : 0;
                if (body.Hold + Epsilon < settings.HoldSeconds)
                {
                    continue;
                }
                deaths.Add(body);
            }
            // 整步收集后统一移除碰撞，避免前一个死亡释放空间吞掉同帧多杀。
            foreach (var body in deaths)
            {
                body.Dead = true;
                if (body.Resident)
                {
                    ResidentsAlive--;
                    lost = true;
                }
                else
                {
                    Remaining--;
                    KilledThisAdvance++;
                }
            }
            Result = lost ? WallSqueezeResult.Lost : Remaining == 0 ? WallSqueezeResult.Won : WallSqueezeResult.Playing;
            Reason = lost ? WallSqueezeLossReason.ResidentDeath : WallSqueezeLossReason.None;
        }

        private bool Supported(WallSqueezeBody body, int axis, float sign, int requiredWall = -1)
        {
            float face = body.Center[axis] + sign * body.Size[axis] / 2;
            float boundary = sign > 0 ? settings.Room[axis] : 0;
            if (requiredWall < 0 && Mathf.Abs(face - boundary) < Epsilon * 3)
            {
                return true;
            }
            for (int i = 0; i < Walls.Length; i++)
            {
                if (requiredWall >= 0 && i != requiredWall)
                {
                    continue;
                }
                var wall = Walls[i];
                if (OverlapCross(body.Center, body.Size, wall.Center, wall.Size, axis)
                    && Mathf.Abs(sign * (wall.Center[axis] - face) - wall.Size[axis] / 2) < Epsilon * 3)
                {
                    return true;
                }
            }
            foreach (var other in Bodies)
            {
                if (other != body && !other.Dead && sign * (other.Center[axis] - body.Center[axis]) > 0
                    && OverlapCross(body.Center, body.Size, other.Center, other.Size, axis)
                    && Mathf.Abs(sign * (other.Center[axis] - face) - other.Size[axis] / 2) < Epsilon * 3
                    && Supported(other, axis, sign, requiredWall))
                {
                    return true;
                }
            }
            return false;
        }

        // 在尺寸恢复前采样撤墙后的支撑链；恢复主动填空不会重新制造夹持计时。
        private void ReleaseRetreat(int wallIndex, float target)
        {
            if ((uint)wallIndex >= Walls.Length || Walls[wallIndex].IsFixed)
            {
                return;
            }
            var wall = Walls[wallIndex];
            int axis = wall.Axis;
            if (Mathf.Abs(target - wall.Center[axis]) <= Epsilon)
            {
                return;
            }
            float sign = Mathf.Sign(target - wall.Center[axis]);
            for (int i = 0; i < Bodies.Count; i++)
            {
                var body = Bodies[i];
                if (!body.Dead && sign * (body.Center[axis] - wall.Center[axis]) < 0
                    && Supported(body, axis, sign, wallIndex))
                {
                    pressured[i, axis] = false;
                    body.Hold = 0;
                }
            }
        }

        private Vector2 NextDirection()
        {
            return random.Next(4) switch
            {
                0 => Vector2.left,
                1 => Vector2.right,
                2 => Vector2.up,
                _ => Vector2.down
            };
        }

        private void CopyWalls()
        {
            Array.Copy(level.Walls, Walls, level.Walls.Length);
            for (int i = 0; i < level.FixedWalls.Length; i++)
            {
                var wall = level.FixedWalls[i];
                if ((uint)wall.Axis > 1 || !Finite(wall.Center))
                {
                    throw new ArgumentException("固定墙轴或位置无效。");
                }
                wall.IsFixed = true;
                wall.Track = Vector2.one * wall.Center[wall.Axis];
                Walls[level.Walls.Length + i] = wall;
            }
        }

        private void AddBody(Vector2 center, WallSqueezeMonsterType type, bool resident)
        {
            var size = Vector2.one * settings.BodySize;
            if (type == WallSqueezeMonsterType.Slipper)
            {
                size.y /= 2;
            }
            if (size.x < settings.MinimumSize || size.y < settings.MinimumSize)
            {
                throw new ArgumentException("对象原始尺寸不能小于压缩下限。");
            }
            if (!Finite(center) || !InsideRoom(center, size))
            {
                throw new ArgumentException("对象初始位置须在房间内部。");
            }
            foreach (var wall in Walls)
            {
                if (OverlapCross(center, size, wall.Center, wall.Size, 0) && OverlapCross(center, size, wall.Center, wall.Size, 1))
                {
                    throw new ArgumentException("对象初始位置不能与墙交叠。");
                }
            }
            foreach (var other in Bodies)
            {
                if (OverlapCross(center, size, other.Center, other.RestSize, 0) && OverlapCross(center, size, other.Center, other.RestSize, 1))
                {
                    throw new ArgumentException("对象初始位置不能互相交叠。");
                }
            }
            var minimum = Vector2.one * settings.MinimumSize;
            if (type == WallSqueezeMonsterType.Armored)
            {
                minimum.x = size.x;
            }
            Bodies.Add(new WallSqueezeBody { Center = center, RestSize = size, Size = size, MinimumSize = minimum, Type = type, Resident = resident });
        }

        private bool InsideRoom(Vector2 center, Vector2 size)
        {
            return center.x - size.x / 2 >= -Epsilon && center.y - size.y / 2 >= -Epsilon
                && center.x + size.x / 2 <= settings.Room.x + Epsilon && center.y + size.y / 2 <= settings.Room.y + Epsilon;
        }

        private static bool Finite(Vector2 value) => float.IsFinite(value.x) && float.IsFinite(value.y);
        private static bool Positive(float value) => float.IsFinite(value) && value > 0;

        private static bool OverlapCross(Vector2 a, Vector2 aSize, Vector2 b, Vector2 bSize, int axis)
        {
            int cross = 1 - axis;
            return Mathf.Abs(a[cross] - b[cross]) < (aSize[cross] + bSize[cross]) / 2 - Epsilon;
        }
    }
}
