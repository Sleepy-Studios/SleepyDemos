using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hotfix.KagitKart
{
    public sealed class KagitKartItemBox
    {
        public Vector3 Position;
        public Vector3 Up;
        public float Heading;
        public int Row;
        public int Slot;
        public bool Active = true;
        public double TakenAt = -99;
        public KagitKartVehicle TakenBy;
    }

    public sealed class KagitKartGum
    {
        public int Id;
        public Vector3 Position;
        public Vector3 PreviousPosition;
        public Vector3 Up = Vector3.up;
        public int Hint = -1;
        public KagitKartVehicle Owner;
        public KagitKartVehicle Held;
        public double HeldSince;
        public double DroppedAt;
        public double HeldFor;
        public bool Dead;
        public float Radius => 0.75f;
    }

    public sealed class KagitKartProjectile
    {
        public int Id;
        public string Kind;
        public KagitKartVehicle Owner;
        public KagitKartVehicle Target;
        public Vector3 Position;
        public Vector3 PreviousPosition;
        public Vector3 Up = Vector3.up;
        public float Heading;
        public float PreviousHeading;
        public float Speed;
        public float VerticalSpeed;
        public double Age;
        public double Life;
        public int Bounces;
        public double FlipAt = -9;
        public int Hint;
        public double Distance;
        public float Lateral;
        public float HalfWidth;
        public float GroundY;
        public bool Locked;
        public bool Straight;
        public bool Backward;
        public bool Falling;
        public double FallAt;
        public float TurnRate;
        public KagitKartVehicle ThreatKart;
        public int ThreatMask;
        public int Victims;
        public int Phase;
        public double PhaseTime;
        public double Progress;
        public float InitialLateral;
        public Vector3 From;
        public Vector3 To;
        public bool Dead;
    }

    /// 道具规则事件；位置保留源坐标，表现与音频共用该通知。
    public readonly struct KagitKartItemEvent
    {
        public readonly string Kind;
        public readonly string Item;
        public readonly string State;
        public readonly Vector3 Position;
        public readonly KagitKartVehicle Kart;
        public readonly KagitKartVehicle Target;
        public readonly float Eta;
        public readonly bool Backward;

        internal KagitKartItemEvent(string kind, string item, Vector3 position, KagitKartVehicle kart,
            string state = null, KagitKartVehicle target = null, float eta = 0, bool backward = false)
        {
            Kind = kind;
            Item = item;
            Position = position;
            Kart = kart;
            State = state;
            Target = target;
            Eta = eta;
            Backward = backward;
        }
    }

    /// items.js 的固定步道具模拟，场景表现通过只读列表及事件接入。
    public sealed partial class KagitKartItems : IDisposable
    {
        private readonly KagitKartTrackQuery track;
        private readonly KagitKartKartTuning tuning;
        private readonly IReadOnlyList<KagitKartVehicle> karts;
        private readonly KagitKartRandom random;
        private readonly float classTop;
        private readonly double length;
        private readonly List<KagitKartItemBox> boxes = new();
        private readonly List<KagitKartGum> hazards = new();
        private readonly List<KagitKartProjectile> projectiles = new();
        private readonly Stack<KagitKartGum> hazardFree = new();
        private readonly Stack<KagitKartProjectile> projectileFree = new();
        private readonly KagitKartGum[] held;
        private readonly bool[] previousItem;
        private KagitKartQueryInfo query;
        private double now;
        private double lastScissors = -1e9;
        private int nextId = 1;
        private bool disposed;

        public IReadOnlyList<KagitKartItemBox> Boxes => boxes;
        public IReadOnlyList<KagitKartGum> Hazards => hazards;
        public IReadOnlyList<KagitKartProjectile> Projectiles => projectiles;
        public event Action<KagitKartItemEvent> Signaled;

        /// <summary>创建本场比赛道具；计时赛没有道具箱，玩家获得三次火箭。</summary>
        /// <param name="configuration">原作配置。</param>
        /// <param name="track">当前赛道查询。</param>
        /// <param name="karts">比赛期间数量与顺序固定的赛车。</param>
        /// <param name="random">比赛的独立 items 随机流。</param>
        /// <param name="speedClass">80、120 或 200。</param>
        /// <param name="timeTrial">是否为计时赛。</param>
        public KagitKartItems(KagitKartConfiguration configuration, KagitKartTrackQuery track,
            IReadOnlyList<KagitKartVehicle> karts, KagitKartRandom random, int speedClass, bool timeTrial)
        {
            this.track = track ?? throw new ArgumentNullException(nameof(track));
            this.karts = karts ?? throw new ArgumentNullException(nameof(karts));
            this.random = random ?? throw new ArgumentNullException(nameof(random));
            if (configuration == null || !configuration.classes.TryGetValue(speedClass.ToString(), out var speed))
            {
                throw new ArgumentOutOfRangeException(nameof(speedClass));
            }
            if (karts.Count < 1 || karts.Count > 8)
            {
                throw new ArgumentOutOfRangeException(nameof(karts));
            }
            tuning = configuration.kart;
            classTop = speed.top;
            length = track.Track.data.length;
            held = new KagitKartGum[karts.Count];
            previousItem = new bool[karts.Count];
            for (int i = 0; i < karts.Count; i++)
            {
                var kart = karts[i] ?? throw new ArgumentException("比赛赛车缺失。", nameof(karts));
                ClearItem(kart);
                kart.Roulette = kart.RouletteDuration = 0;
                if (timeTrial && kart.IsPlayer)
                {
                    kart.Item = "rocket3";
                    kart.ItemCount = 3;
                }
            }
            if (!timeTrial)
            {
                BuildBoxes();
            }
        }

        /// <summary>迁移原作近领跑者限制后的抽奖行。</summary>
        /// <param name="kart">本场赛车。</param>
        /// <returns>从零开始的抽奖行。</returns>
        public int RowFor(KagitKartVehicle kart)
        {
            int row = karts.Count > 1 ? (int)Math.Floor((kart.Place - 1) * 7.0 / (karts.Count - 1) + 0.5) : 0;
            var leader = Leader();
            if (leader != null && leader != kart && leader.Progress - kart.Progress < 25)
            {
                row = Math.Min(row, 2);
            }
            return Math.Clamp(row, 0, 7);
        }

        /// 首次二十秒后、间隔十五秒且没有其它持有或飞行剪刀时才进入抽奖。
        public bool ScissorsAvailable()
        {
            if (now < 20 || now < lastScissors + 15)
            {
                return false;
            }
            foreach (var projectile in projectiles)
            {
                if (projectile.Kind == "scissors" && !projectile.Dead)
                {
                    return false;
                }
            }
            for (int kartIndex = 0; kartIndex < karts.Count; kartIndex++)
            {
                var kart = karts[kartIndex];
                if (kart.Item == "scissors")
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>在赛车运动之后更新道具；内部保存按键边沿，不能因持续按住重复使用。</summary>
        /// <param name="seconds">固定步长。</param>
        /// <param name="raceTime">比赛独立时钟，不含暂停。</param>
        /// <param name="live">倒计时为 false，其余比赛阶段为 true。</param>
        public void Advance(double seconds, double raceTime, bool live)
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(KagitKartItems));
            }
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds <= 0 || seconds > 0.1 ||
                double.IsNaN(raceTime) || double.IsInfinity(raceTime) || raceTime < now)
            {
                throw new ArgumentOutOfRangeException(nameof(seconds));
            }
            now = raceTime;
            float dt = (float)seconds;
            foreach (var hazard in hazards)
            {
                hazard.PreviousPosition = hazard.Position;
            }
            foreach (var projectile in projectiles)
            {
                projectile.PreviousPosition = projectile.Position;
                projectile.PreviousHeading = projectile.Heading;
            }
            for (int kartIndex = 0; kartIndex < karts.Count; kartIndex++)
            {
                var kart = karts[kartIndex];
                if (kart.Roulette <= 0)
                {
                    continue;
                }
                kart.Roulette = Math.Max(0, kart.Roulette - seconds);
                if (kart.Roulette == 0)
                {
                    kart.Item = KagitKartCatalog.PickItem(RowFor(kart) + 1, random.Next(), ScissorsAvailable());
                    kart.ItemCount = kart.Item == "rocket3" ? 3 : 1;
                    Emit("itemGet", kart.Item, kart.Position, kart);
                }
            }
            foreach (var box in boxes)
            {
                if (!box.Active && now - box.TakenAt >= 1.2)
                {
                    box.Active = true;
                    box.TakenBy = null;
                }
            }
            for (int i = 0; i < karts.Count; i++)
            {
                var kart = karts[i];
                var hazard = held[i];
                if (hazard != null && !kart.ItemHeld)
                {
                    held[i] = null;
                    hazard.Held = null;
                    hazard.DroppedAt = now;
                    hazard.HeldFor = now - hazard.HeldSince;
                }
                if (kart.ItemHeld)
                {
                    StepHeld(i, kart);
                }
                else if (live && kart.Controls.Item && !previousItem[i])
                {
                    Use(i, kart);
                }
                if (live && !kart.Rescuing)
                {
                    PickBoxes(kart);
                }
                previousItem[i] = kart.Controls.Item;
            }
            StepHazards();
            foreach (var projectile in projectiles)
            {
                if (projectile.Dead)
                {
                    continue;
                }
                projectile.Age += seconds;
                switch (projectile.Kind)
                {
                    case "plane":
                    case "homing":
                        StepFlyer(projectile, dt);
                        break;
                    case "ink":
                        StepInk(projectile);
                        break;
                    case "scissors":
                        if (projectile.Age >= projectile.Life)
                        {
                            Kill(projectile, "expire");
                        }
                        else
                        {
                            StepScissors(projectile, seconds);
                        }
                        break;
                }
            }
            FlyerVsFlyer();
            Compact();
        }

        /// 暂停、失焦与输入设备切换时同步中性输入，保留比赛内道具状态。
        public void ClearInput()
        {
            Array.Clear(previousItem, 0, previousItem.Length);
            for (int kartIndex = 0; kartIndex < karts.Count; kartIndex++)
            {
                var kart = karts[kartIndex];
                kart.Controls = default;
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }
            disposed = true;
            for (int kartIndex = 0; kartIndex < karts.Count; kartIndex++)
            {
                var kart = karts[kartIndex];
                ClearItem(kart);
                kart.Roulette = kart.RouletteDuration = 0;
            }
            boxes.Clear();
            hazards.Clear();
            projectiles.Clear();
            hazardFree.Clear();
            projectileFree.Clear();
            Array.Clear(held, 0, held.Length);
            Signaled = null;
        }

        private void BuildBoxes()
        {
            var rows = track.Track.definition.itemRows;
            if (rows == null)
            {
                return;
            }
            for (int r = 0; r < rows.Length; r++)
            {
                var row = rows[r];
                int count = Math.Max(1, row.count);
                float span = track.SampleAt(row.t).HalfWidth * 0.72f;
                for (int j = 0; j < count; j++)
                {
                    float lateral = count == 1 ? 0 : -span + 2 * span * j / (count - 1);
                    var point = track.PointAt(row.t, lateral);
                    track.Query(point.Position, point.Index, ref query);
                    boxes.Add(new KagitKartItemBox
                    {
                        Position = point.Position, Up = query.Up, Heading = point.Heading, Row = r, Slot = j
                    });
                }
            }
        }

        private static void ClearItem(KagitKartVehicle kart)
        {
            kart.Item = null;
            kart.ItemCount = 0;
            kart.ItemHeld = false;
        }

        private KagitKartVehicle Leader(bool unfinished = false)
        {
            KagitKartVehicle best = null;
            for (int kartIndex = 0; kartIndex < karts.Count; kartIndex++)
            {
                var kart = karts[kartIndex];
                if (unfinished && kart.Finished)
                {
                    continue;
                }
                if (best == null || kart.Place < best.Place)
                {
                    best = kart;
                }
            }
            return best;
        }

        private KagitKartVehicle AtPlace(int place)
        {
            for (int kartIndex = 0; kartIndex < karts.Count; kartIndex++)
            {
                var kart = karts[kartIndex];
                if (kart.Place == place)
                {
                    return kart;
                }
            }
            return null;
        }

        private void Use(int index, KagitKartVehicle kart)
        {
            if (kart.Item == null || kart.Roulette > 0 || kart.Rescuing || kart.SpinTime > 0)
            {
                return;
            }
            string item = kart.Item;
            bool backward = kart.Controls.Brake > 0.5f || kart.Controls.LookBack;
            switch (item)
            {
                case "rocket":
                case "rocket3":
                    var boost = tuning.boosts["rocket"];
                    kart.ApplyBoost(boost[0], boost[1], "item");
                    if (item == "rocket3" && kart.ItemCount > 1)
                    {
                        kart.ItemCount--;
                    }
                    else
                    {
                        ClearItem(kart);
                    }
                    Emit("pen", item, kart.Position, kart);
                    break;
                case "gum":
                    StartHeld(index, kart);
                    return;
                case "plane":
                case "homing":
                    ClearItem(kart);
                    SpawnFlyer(kart, item, backward);
                    break;
                case "ink":
                    ClearItem(kart);
                    SpawnInk(kart);
                    break;
                case "foil":
                    ClearItem(kart);
                    kart.SetInvincible(7);
                    break;
                case "scissors":
                    ClearItem(kart);
                    SpawnScissors(kart);
                    break;
                default:
                    ClearItem(kart);
                    return;
            }
            Emit("itemUse", item, kart.Position, kart, backward: backward);
        }

        private KagitKartGum AllocateHazard()
        {
            var hazard = hazardFree.Count > 0 ? hazardFree.Pop() : new KagitKartGum();
            hazard.Id = nextId++;
            hazard.Dead = false;
            hazard.Owner = hazard.Held = null;
            hazard.HeldFor = 0;
            hazard.Hint = -1;
            hazard.Up = Vector3.up;
            hazards.Add(hazard);
            int dropped = 0;
            KagitKartGum oldest = null;
            foreach (var other in hazards)
            {
                if (other.Dead || other.Held != null || other == hazard)
                {
                    continue;
                }
                dropped++;
                if (oldest == null || other.Id < oldest.Id)
                {
                    oldest = other;
                }
            }
            if (dropped >= 20 && oldest != null)
            {
                KillHazard(oldest);
            }
            return hazard;
        }

        private void KillHazard(KagitKartGum hazard)
        {
            if (hazard.Dead)
            {
                return;
            }
            if (hazard.Held != null)
            {
                for (int i = 0; i < karts.Count; i++)
                {
                    if (karts[i] == hazard.Held)
                    {
                        held[i] = null;
                        break;
                    }
                }
                ClearItem(hazard.Held);
                hazard.Held = null;
            }
            hazard.Dead = true;
            Emit("pop", "gum", hazard.Position, null);
        }

        private void PlaceTrailing(KagitKartVehicle kart, KagitKartGum hazard)
        {
            hazard.Position = kart.Position - KagitKartVehicle.Forward(kart.Heading) * 1.9f;
            track.Query(hazard.Position, hazard.Hint >= 0 ? hazard.Hint : kart.TrackInfo.Index, ref query);
            hazard.Hint = query.Index;
            if (query.HasGround && Mathf.Abs(query.GroundY - kart.Position.y) < 3)
            {
                var position = hazard.Position;
                position.y = query.GroundY;
                hazard.Position = position;
            }
            hazard.Up = query.Up;
        }

        private void StartHeld(int index, KagitKartVehicle kart)
        {
            var hazard = AllocateHazard();
            hazard.Held = hazard.Owner = kart;
            hazard.HeldSince = now;
            held[index] = hazard;
            kart.ItemHeld = true;
            PlaceTrailing(kart, hazard);
            hazard.PreviousPosition = hazard.Position;
        }

        private void StepHeld(int index, KagitKartVehicle kart)
        {
            var hazard = held[index];
            if (hazard == null || hazard.Dead)
            {
                held[index] = null;
                kart.ItemHeld = false;
                return;
            }
            if (kart.Rescuing)
            {
                KillHazard(hazard);
                return;
            }
            if (!kart.Controls.Item || kart.SpinTime > 0)
            {
                held[index] = null;
                hazard.Held = null;
                hazard.DroppedAt = now;
                hazard.HeldFor = now - hazard.HeldSince;
                ClearItem(kart);
                Emit("itemUse", "gum", kart.Position, kart, backward: true);
                if (!Ground(hazard.Position, hazard.Hint))
                {
                    KillHazard(hazard);
                }
                return;
            }
            PlaceTrailing(kart, hazard);
        }

        private KagitKartProjectile AllocateProjectile(string kind, KagitKartVehicle owner)
        {
            var projectile = projectileFree.Count > 0 ? projectileFree.Pop() : new KagitKartProjectile();
            projectile.Id = nextId++;
            projectile.Kind = kind;
            projectile.Owner = owner;
            projectile.Target = null;
            projectile.Dead = false;
            projectile.Age = 0;
            projectile.Life = 10;
            projectile.Bounces = 0;
            projectile.FlipAt = -9;
            projectile.VerticalSpeed = 0;
            projectile.Locked = projectile.Straight = projectile.Backward = projectile.Falling = false;
            projectile.TurnRate = 0;
            projectile.ThreatKart = null;
            projectile.ThreatMask = projectile.Victims = projectile.Phase = 0;
            projectile.PhaseTime = 0;
            projectile.Up = Vector3.up;
            projectile.Hint = owner.TrackInfo.Index;
            projectiles.Add(projectile);
            return projectile;
        }

        private void Kill(KagitKartProjectile projectile, string state)
        {
            if (projectile.Dead)
            {
                return;
            }
            projectile.Dead = true;
            if (state != null)
            {
                EmitProjectile(projectile, state, projectile.Target);
            }
        }

        private bool Ground(Vector3 position, int hint)
        {
            track.Query(position, hint, ref query);
            return query.HasGround;
        }

        private void SpawnFlyer(KagitKartVehicle kart, string kind, bool backward)
        {
            var projectile = AllocateProjectile(kind, kart);
            float heading = kart.Heading + (backward ? Mathf.PI : 0);
            projectile.Position = kart.Position + KagitKartVehicle.Forward(heading) * (backward ? 2 : 2.2f) + Vector3.up * 0.75f;
            projectile.Heading = projectile.PreviousHeading = heading;
            projectile.Speed = (kind == "plane" ? 1.7f : 1.45f) * classTop;
            projectile.Life = kind == "plane" ? 6 : 12;
            projectile.Backward = backward;
            projectile.Straight = kind == "homing" && backward;
            if (kind == "homing" && !backward)
            {
                projectile.Target = AtPlace(kart.Place - 1);
            }
            Ground(projectile.Position, projectile.Hint);
            UpdateQuery(projectile);
            projectile.GroundY = query.HasGround ? query.GroundY : kart.Position.y;
            if (query.HasGround)
            {
                var position = projectile.Position;
                position.y = query.GroundY + 0.75f;
                projectile.Position = position;
            }
            projectile.PreviousPosition = projectile.Position;
            EmitProjectile(projectile, "spawn", projectile.Target);
        }

        private void SteerHoming(KagitKartProjectile projectile, float dt)
        {
            var target = projectile.Target;
            if (target != null && target.Rescuing)
            {
                projectile.Target = target = null;
                projectile.Locked = false;
            }
            float desired;
            if (target != null && !projectile.Locked && Vector3.Distance(target.Position, projectile.Position) < 25)
            {
                projectile.Locked = true;
                EmitProjectile(projectile, "lock", target);
            }
            if (target != null && projectile.Locked)
            {
                var aim = target.Position - projectile.Position + target.Velocity * 0.08f;
                desired = Mathf.Atan2(aim.x, aim.z);
            }
            else
            {
                float limit = projectile.HalfWidth * 0.6f;
                float lateral = Mathf.Clamp(target != null ? target.TrackInfo.Lateral : projectile.Lateral * 0.6f, -limit, limit);
                var point = track.PointAt((float)((projectile.Distance + 14) % length / length), lateral);
                var aim = point.Position - projectile.Position;
                desired = Mathf.Atan2(aim.x, aim.z);
            }
            float turn = Mathf.Clamp(WrapAngle(desired - projectile.Heading), -4 * dt, 4 * dt);
            projectile.Heading += turn;
            projectile.TurnRate = turn / dt;
        }

        private void StepFlyer(KagitKartProjectile projectile, float dt)
        {
            if (projectile.Falling)
            {
                projectile.VerticalSpeed -= tuning.gravity * dt;
                projectile.Position += KagitKartVehicle.Forward(projectile.Heading) * (projectile.Speed * 0.6f * dt) +
                    Vector3.up * (projectile.VerticalSpeed * dt);
                if (projectile.Age - projectile.FallAt > 1.2)
                {
                    Kill(projectile, "expire");
                }
                return;
            }
            if (projectile.Kind == "homing" && !projectile.Straight)
            {
                SteerHoming(projectile, dt);
            }
            else
            {
                projectile.TurnRate = 0;
            }
            var forward = KagitKartVehicle.Forward(projectile.Heading);
            projectile.Position += forward * (projectile.Speed * dt);
            Ground(projectile.Position, projectile.Hint);
            UpdateQuery(projectile);
            float band = query.HalfWidth + query.OffroadWidth;
            float lateral = Mathf.Abs(query.Lateral);
            float wallLine = band - 0.55f;
            if (query.EdgeCode == 1 && lateral > wallLine)
            {
                if (projectile.Kind == "homing")
                {
                    Kill(projectile, "expire");
                    return;
                }
                var normal = new Vector3(query.Right.x, 0, query.Right.z).normalized * Mathf.Sign(query.Lateral);
                projectile.Position -= normal * (lateral - wallLine + 0.02f);
                float outward = Vector3.Dot(forward, normal);
                if (outward > 0)
                {
                    var reflected = forward - 2 * outward * normal;
                    projectile.Heading = Mathf.Atan2(reflected.x, reflected.z);
                    projectile.Bounces++;
                    projectile.FlipAt = projectile.Age;
                    if (projectile.Bounces > 4)
                    {
                        Kill(projectile, "expire");
                        return;
                    }
                    EmitProjectile(projectile, "bounce", projectile.Target);
                }
            }
            else if (!query.HasGround)
            {
                projectile.Falling = true;
                projectile.FallAt = projectile.Age;
                projectile.VerticalSpeed = 0;
                return;
            }
            else if (lateral > band + 12)
            {
                Kill(projectile, "expire");
                return;
            }
            projectile.GroundY = query.GroundY;
            projectile.Up = query.Up;
            var position = projectile.Position;
            position.y += (query.GroundY + 0.75f - position.y) * Mathf.Min(1, dt * 12);
            projectile.Position = position;
            if (projectile.Age >= projectile.Life)
            {
                Kill(projectile, "expire");
                return;
            }
            if (HitKarts(projectile))
            {
                return;
            }
            HitGum(projectile);
            if (!projectile.Dead && projectile.Kind == "homing" && projectile.Target != null)
            {
                var target = projectile.Target;
                float direct = Vector3.Distance(target.Position, projectile.Position);
                double gap = direct;
                if (!projectile.Locked)
                {
                    gap = target.TrackInfo.Distance - projectile.Distance;
                    if (gap > length / 2)
                    {
                        gap -= length;
                    }
                    else if (gap < -length / 2)
                    {
                        gap += length;
                    }
                    if (gap < 0)
                    {
                        gap = direct;
                    }
                }
                Threat(projectile, target, (float)(Math.Max(0, gap - 1.65) /
                    Math.Max(4, projectile.Speed - Math.Max(0, target.Speed))));
            }
        }

        private void UpdateQuery(KagitKartProjectile projectile)
        {
            projectile.Hint = query.Index;
            projectile.Distance = query.Distance;
            projectile.Lateral = query.Lateral;
            projectile.HalfWidth = query.HalfWidth;
        }

        private bool HitKarts(KagitKartProjectile projectile)
        {
            for (int kartIndex = 0; kartIndex < karts.Count; kartIndex++)
            {
                var kart = karts[kartIndex];
                if (kart.Rescuing || kart == projectile.Owner && projectile.Age < 0.4)
                {
                    continue;
                }
                var delta = kart.Position - projectile.Position;
                if (delta.x * delta.x + delta.z * delta.z > 1.65f * 1.65f || Mathf.Abs(delta.y + 0.6f) > 1.3f)
                {
                    continue;
                }
                kart.SpinOut(projectile.Kind, projectile.Owner);
                Kill(projectile, null);
                EmitProjectile(projectile, "hit", kart);
                return true;
            }
            return false;
        }

        private void HitGum(KagitKartProjectile projectile)
        {
            foreach (var hazard in hazards)
            {
                var delta = hazard.Position - projectile.Position;
                if (hazard.Dead || delta.x * delta.x + delta.z * delta.z > 1.3f * 1.3f || Mathf.Abs(delta.y) > 1.4f)
                {
                    continue;
                }
                var shield = hazard.Held;
                KillHazard(hazard);
                Kill(projectile, null);
                EmitProjectile(projectile, "hit", shield);
                return;
            }
        }

        private void FlyerVsFlyer()
        {
            for (int i = 0; i < projectiles.Count; i++)
            {
                var a = projectiles[i];
                if (!IsFlyer(a))
                {
                    continue;
                }
                for (int j = i + 1; j < projectiles.Count; j++)
                {
                    var b = projectiles[j];
                    if (!IsFlyer(b) || a.Owner == b.Owner && (a.Age < 0.4 || b.Age < 0.4) ||
                        (a.Position - b.Position).sqrMagnitude > 1.2f * 1.2f)
                    {
                        continue;
                    }
                    Kill(a, "hit");
                    Kill(b, "hit");
                    Emit("pop", a.Kind, a.Position, null);
                    break;
                }
            }
        }

        private static bool IsFlyer(KagitKartProjectile projectile) =>
            !projectile.Dead && !projectile.Falling && (projectile.Kind == "plane" || projectile.Kind == "homing");

        private void SpawnInk(KagitKartVehicle kart)
        {
            var projectile = AllocateProjectile("ink", kart);
            projectile.Life = 0.85;
            for (int i = 0; i < karts.Count; i++)
            {
                if (karts[i] != kart && karts[i].Place < kart.Place)
                {
                    projectile.Victims |= 1 << i;
                }
            }
            projectile.From = kart.Position + Vector3.up * 1.2f;
            var middle = kart.Place > 1 ? AtPlace(Math.Max(1, kart.Place / 2)) : null;
            projectile.To = middle != null ? middle.Position + middle.Velocity * 0.85f :
                kart.Position + KagitKartVehicle.Forward(kart.Heading) * 25;
            var delta = projectile.To - projectile.From;
            float horizontal = new Vector2(delta.x, delta.z).magnitude;
            if (horizontal > 150)
            {
                projectile.To.x = projectile.From.x + delta.x / horizontal * 150;
                projectile.To.z = projectile.From.z + delta.z / horizontal * 150;
            }
            if (Ground(projectile.To, -1))
            {
                projectile.To.y = query.GroundY;
            }
            projectile.Heading = projectile.PreviousHeading = Mathf.Atan2(projectile.To.x - projectile.From.x,
                projectile.To.z - projectile.From.z);
            projectile.Position = projectile.PreviousPosition = projectile.From;
            projectile.GroundY = projectile.To.y;
            EmitProjectile(projectile, "spawn", null);
        }

        private void StepInk(KagitKartProjectile projectile)
        {
            float u = (float)Math.Min(1, projectile.Age / projectile.Life);
            projectile.Position = Vector3.LerpUnclamped(projectile.From, projectile.To, u);
            projectile.Position.y = projectile.From.y + (projectile.To.y + 5.5f - projectile.From.y) * u + Mathf.Sin(Mathf.PI * u) * 3;
            if (u < 1)
            {
                return;
            }
            for (int i = 0; i < karts.Count; i++)
            {
                if ((projectile.Victims & 1 << i) != 0)
                {
                    karts[i].ApplyInk(3.5f);
                }
            }
            Emit("ink", "ink", projectile.Position, null);
            Kill(projectile, "hit");
        }

        private void SpawnScissors(KagitKartVehicle kart)
        {
            var projectile = AllocateProjectile("scissors", kart);
            projectile.Life = 30;
            projectile.Speed = 2.1f * classTop;
            projectile.From = kart.Position + Vector3.up;
            projectile.Position = projectile.PreviousPosition = projectile.From;
            projectile.Progress = kart.Progress;
            projectile.InitialLateral = kart.TrackInfo.Lateral;
            projectile.Heading = projectile.PreviousHeading = kart.Heading;
            projectile.Target = Leader(true);
            lastScissors = now;
            EmitProjectile(projectile, "spawn", projectile.Target);
        }

        private void StepScissors(KagitKartProjectile projectile, double dt)
        {
            if (projectile.Phase < 2)
            {
                var leader = Leader(true);
                if (leader == null)
                {
                    Kill(projectile, "expire");
                    return;
                }
                projectile.Target = leader;
                projectile.Progress += projectile.Speed * dt;
                projectile.PhaseTime += dt;
                if (projectile.Phase == 0 && projectile.PhaseTime >= 0.5)
                {
                    projectile.Phase = 1;
                }
                float lateral = projectile.InitialLateral * (float)Math.Max(0, 1 - projectile.PhaseTime / 1.5);
                double distance = (projectile.Progress % length + length) % length + track.Track.data.startDist;
                var point = track.PointAt((float)(distance % length / length), lateral);
                projectile.GroundY = point.Position.y;
                projectile.Heading = point.Heading;
                var high = point.Position + Vector3.up * 9;
                projectile.Position = projectile.Phase == 0 ?
                    Vector3.LerpUnclamped(projectile.From, high, Mathf.SmoothStep(0, 1, (float)(projectile.PhaseTime / 0.5))) : high;
                double gap = leader.Progress - projectile.Progress;
                if (projectile.Phase == 1 && gap < 18)
                {
                    projectile.Phase = 2;
                    projectile.PhaseTime = 0;
                    projectile.From = projectile.Position;
                    projectile.Locked = true;
                    EmitProjectile(projectile, "lock", leader);
                }
                else
                {
                    double closing = Math.Max(5, projectile.Speed - Math.Max(0, leader.Speed));
                    Threat(projectile, leader, (float)(Math.Max(0, gap - 18) / closing + 0.7 + Math.Max(0, 0.5 - projectile.PhaseTime)));
                    return;
                }
            }
            projectile.PhaseTime += dt;
            var target = projectile.Target;
            float u = (float)Math.Min(1, projectile.PhaseTime / 0.7);
            projectile.To = target.Position + Vector3.up * 0.8f;
            float horizontal = 1 - (1 - u) * (1 - u);
            projectile.Position = new Vector3(
                Mathf.LerpUnclamped(projectile.From.x, projectile.To.x, horizontal),
                Mathf.LerpUnclamped(projectile.From.y, projectile.To.y, u * u),
                Mathf.LerpUnclamped(projectile.From.z, projectile.To.z, horizontal));
            if (u < 0.98f)
            {
                float heading = Mathf.Atan2(projectile.To.x - projectile.Position.x, projectile.To.z - projectile.Position.z);
                if (heading != 0)
                {
                    projectile.Heading = heading;
                }
            }
            projectile.GroundY = target.Position.y;
            Threat(projectile, target, (float)(0.7 - projectile.PhaseTime));
            if (u >= 1)
            {
                for (int kartIndex = 0; kartIndex < karts.Count; kartIndex++)
                {
                    var kart = karts[kartIndex];
                    var delta = kart.Position - target.Position;
                    if (kart.Rescuing || kart.BoostTime > 0 || delta.x * delta.x + delta.z * delta.z > 36 || Mathf.Abs(delta.y) > 3)
                    {
                        continue;
                    }
                    kart.SpinOut("scissors", projectile.Owner);
                }
                Emit("snip", "scissors", target.Position, target);
                Kill(projectile, null);
                EmitProjectile(projectile, "hit", target);
            }
        }

        private void PickBoxes(KagitKartVehicle kart)
        {
            foreach (var box in boxes)
            {
                var delta = box.Position - kart.Position;
                if (!box.Active || delta.x * delta.x + delta.z * delta.z > 1.8f * 1.8f || -delta.y < -1.1f || -delta.y > 1.7f)
                {
                    continue;
                }
                box.Active = false;
                box.TakenAt = now;
                box.TakenBy = kart;
                Emit("itemBox", null, box.Position, kart);
                if (kart.Item == null && kart.Roulette <= 0 && !kart.ItemHeld)
                {
                    kart.Roulette = kart.RouletteDuration = 1.6;
                }
            }
        }

        private void StepHazards()
        {
            foreach (var hazard in hazards)
            {
                if (hazard.Dead)
                {
                    continue;
                }
                for (int kartIndex = 0; kartIndex < karts.Count; kartIndex++)
                {
                    var kart = karts[kartIndex];
                    if (kart == hazard.Held || kart.Rescuing || kart == hazard.Owner && hazard.Held == null && now - hazard.DroppedAt < 0.5)
                    {
                        continue;
                    }
                    var delta = kart.Position - hazard.Position;
                    if (delta.y > 1 || delta.y < -1.5f || delta.x * delta.x + delta.z * delta.z > 1.7f * 1.7f)
                    {
                        continue;
                    }
                    if (kart.InvincibleTime > 0)
                    {
                        KillHazard(hazard);
                        break;
                    }
                    if (kart.Immune)
                    {
                        continue;
                    }
                    kart.SpinOut("gum", hazard.Owner);
                    KillHazard(hazard);
                    break;
                }
            }
        }

        private void Threat(KagitKartProjectile projectile, KagitKartVehicle kart, float eta)
        {
            if (projectile.ThreatKart != kart)
            {
                projectile.ThreatKart = kart;
                projectile.ThreatMask = 0;
            }
            if ((projectile.ThreatMask & 1) == 0 && eta <= 1.5f)
            {
                projectile.ThreatMask |= 1;
                if (eta > 0.5f)
                {
                    Emit("threat", projectile.Kind, projectile.Position, kart, eta: eta);
                }
            }
            if ((projectile.ThreatMask & 2) == 0 && eta <= 0.5f)
            {
                projectile.ThreatMask |= 3;
                Emit("threat", projectile.Kind, projectile.Position, kart, eta: Mathf.Max(0, eta));
            }
        }

        private void Compact()
        {
            int write = 0;
            for (int read = 0; read < projectiles.Count; read++)
            {
                var projectile = projectiles[read];
                if (projectile.Dead)
                {
                    projectile.Owner = projectile.Target = projectile.ThreatKart = null;
                    projectileFree.Push(projectile);
                }
                else
                {
                    projectiles[write++] = projectile;
                }
            }
            projectiles.RemoveRange(write, projectiles.Count - write);
            write = 0;
            for (int read = 0; read < hazards.Count; read++)
            {
                var hazard = hazards[read];
                if (hazard.Dead)
                {
                    hazard.Owner = hazard.Held = null;
                    hazardFree.Push(hazard);
                }
                else
                {
                    hazards[write++] = hazard;
                }
            }
            hazards.RemoveRange(write, hazards.Count - write);
        }

        private void EmitProjectile(KagitKartProjectile projectile, string state, KagitKartVehicle target) =>
            Emit("projectile", projectile.Kind, projectile.Position, projectile.Owner, state, target);

        private void Emit(string kind, string item, Vector3 position, KagitKartVehicle kart,
            string state = null, KagitKartVehicle target = null, float eta = 0, bool backward = false) =>
            Signaled?.Invoke(new KagitKartItemEvent(kind, item, position, kart, state, target, eta, backward));

        private static float WrapAngle(float angle) => Mathf.Repeat(angle + Mathf.PI, Mathf.PI * 2) - Mathf.PI;
    }
}
