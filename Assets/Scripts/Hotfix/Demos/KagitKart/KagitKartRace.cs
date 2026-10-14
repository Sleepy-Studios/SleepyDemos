using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hotfix.KagitKart
{
    public enum KagitKartRaceMode
    {
        GrandPrix,
        SingleRace,
        TimeTrial
    }

    public enum KagitKartRaceState
    {
        Countdown,
        Running,
        Finishing,
        Done
    }

    public sealed class KagitKartRaceResult
    {
        public string CharacterId { get; internal set; }
        public int Place { get; internal set; }
        public double Time { get; internal set; }
        public bool Estimated { get; internal set; }
        public double? BestLap { get; internal set; }
        public int Points { get; internal set; }
        public int TotalPoints { get; internal set; }
    }

    public sealed class KagitKartRaceEntry
    {
        private readonly double[] paceTimes;
        private readonly double[] paceProgress;
        private int paceHead;
        private int paceCount;
        private double nextPace;
        internal readonly KagitKartCountdown Countdown = new();
        internal int SplitCount;

        public KagitKartVehicle Vehicle { get; }
        public KagitKartRaceProgress Progress { get; }
        public double FinishTime { get; internal set; }
        public bool Estimated { get; internal set; }
        public double? BestLap { get; internal set; }

        internal KagitKartRaceEntry(KagitKartVehicle vehicle, double length, int laps, double distance, int paceSamples)
        {
            Vehicle = vehicle;
            Progress = new KagitKartRaceProgress(length, laps, distance);
            paceTimes = new double[paceSamples];
            paceProgress = new double[paceSamples];
        }

        internal void RecordPace(double time)
        {
            if (time < nextPace)
            {
                return;
            }
            nextPace = time + 0.5;
            paceTimes[paceHead] = time;
            paceProgress[paceHead] = Vehicle.Progress;
            paceHead = (paceHead + 1) % paceTimes.Length;
            paceCount = Math.Min(paceCount + 1, paceTimes.Length);
        }

        internal double Pace()
        {
            if (paceCount >= 2)
            {
                int newest = (paceHead - 1 + paceTimes.Length) % paceTimes.Length;
                int oldest = paceCount < paceTimes.Length ? 0 : paceHead;
                double seconds = paceTimes[newest] - paceTimes[oldest];
                double distance = paceProgress[newest] - paceProgress[oldest];
                if (seconds > 0.5 && distance > 0)
                {
                    return distance / seconds;
                }
            }
            return Math.Max(1, Math.Abs(Vehicle.Speed));
        }
    }

    public readonly struct KagitKartRaceEvent
    {
        public readonly string Kind;
        public readonly KagitKartVehicle Kart;
        public readonly int Number;
        public readonly double Time;
        public readonly double? Split;
        public readonly bool Estimated;

        internal KagitKartRaceEvent(string kind, KagitKartVehicle kart = null, int number = 0,
            double time = 0, double? split = null, bool estimated = false)
        {
            Kind = kind;
            Kart = kart;
            Number = number;
            Time = time;
            Split = split;
            Estimated = estimated;
        }
    }

    /// race.js 的排名、完赛等待与结算；宿主按原作在道具之后更新比赛规则。
    public sealed partial class KagitKartRace : IDisposable
    {
        private readonly KagitKartConfiguration configuration;
        private readonly KagitKartTrackQuery track;
        private readonly double length;
        private readonly KagitKartRaceEntry[] entries;
        private readonly KagitKartRaceEntry[] order;
        private readonly List<KagitKartRaceEntry> finishOrder = new();
        private readonly List<KagitKartRaceEntry> pending = new();
        private readonly List<KagitKartRaceResult> results = new();
        private readonly KagitKartGrandPrix grandPrix;
        private readonly KagitKartGhostData previousGhost;
        private readonly Action<int, int, double>[] checkpointHandlers;
        private double lastTime;
        private bool disposed;
        private readonly bool multiplayer;

        public KagitKartRaceMode Mode { get; }
        public KagitKartRaceState State { get; private set; } = KagitKartRaceState.Countdown;
        public int Laps { get; }
        public int Countdown => entries[0].Countdown.Number;
        public double TimeToGo => entries[0].Countdown.SecondsToGo;
        public KagitKartStartGrade StartGrade { get; private set; }
        public double WaitLeft { get; private set; }
        public double? BestLapTime { get; private set; }
        public KagitKartGhostRecorder GhostRecorder { get; }
        public IReadOnlyList<KagitKartRaceEntry> Entries => entries;
        public IReadOnlyList<KagitKartRaceResult> Results => results;
        public event Action<KagitKartRaceEvent> Signaled;

        /// <summary>为已放在网格上的赛车建立本场规则；计时赛自动记录幽灵。</summary>
        /// <param name="configuration">原作配置。</param>
        /// <param name="track">已加载的赛道。</param>
        /// <param name="karts">网格顺序固定且角色不重复的赛车。</param>
        /// <param name="mode">三种原版比赛模式。</param>
        /// <param name="grandPrix">大奖赛的四场累计状态。</param>
        /// <param name="previousGhost">本赛道及组别经过校验的最佳幽灵，可为空。</param>
        public KagitKartRace(KagitKartConfiguration configuration, KagitKartTrackQuery track,
            IReadOnlyList<KagitKartVehicle> karts, KagitKartRaceMode mode, KagitKartGrandPrix grandPrix = null,
            KagitKartGhostData previousGhost = null, bool multiplayer = false)
        {
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.track = track ?? throw new ArgumentNullException(nameof(track));
            length = track.Track.data.length;
            if (karts == null || karts.Count < 1 || karts.Count > 8 || !Enum.IsDefined(typeof(KagitKartRaceMode), mode))
            {
                throw new ArgumentOutOfRangeException(nameof(karts));
            }
            if (mode == KagitKartRaceMode.GrandPrix &&
                (grandPrix == null || grandPrix.RoundComplete || grandPrix.TrackId != track.Track.definition.id))
            {
                throw new ArgumentException("大奖赛章节状态不匹配。", nameof(grandPrix));
            }
            if (previousGhost != null && !previousGhost.IsValid())
            {
                throw new ArgumentException("最佳幽灵记录损坏。", nameof(previousGhost));
            }
            this.grandPrix = grandPrix;
            this.multiplayer = multiplayer;
            this.previousGhost = previousGhost;
            Mode = mode;
            Laps = track.Track.definition.laps;
            WaitLeft = configuration.race.finishWait;
            entries = new KagitKartRaceEntry[karts.Count];
            order = new KagitKartRaceEntry[karts.Count];
            checkpointHandlers = new Action<int, int, double>[karts.Count];
            var ids = new HashSet<string>();
            int players = 0;
            for (int i = 0; i < karts.Count; i++)
            {
                var kart = karts[i];
                if (kart == null || !ids.Add(kart.Id))
                {
                    throw new ArgumentException("比赛赛车缺失或角色重复。", nameof(karts));
                }
                if (kart.IsPlayer)
                {
                    players++;
                }
            }
            if ((multiplayer ? players > 1 : players != 1) || mode == KagitKartRaceMode.TimeTrial && karts.Count != 1)
            {
                throw new ArgumentException("单人比赛必须且只能有一名玩家。", nameof(karts));
            }
            GhostRecorder = mode == KagitKartRaceMode.TimeTrial ? new KagitKartGhostRecorder() : null;
            int paceSamples = (int)Math.Floor(configuration.race.paceWindow / 0.5 + 0.5) + 1;
            for (int i = 0; i < karts.Count; i++)
            {
                var kart = karts[i];
                var entry = new KagitKartRaceEntry(kart, length, Laps, RelativeDistance(kart), paceSamples);
                entries[i] = order[i] = entry;
                kart.Progress = entry.Progress.Progress;
                kart.Finished = kart.Autopilot = false;
                kart.Pinned = true;
                kart.Place = i + 1;
                checkpointHandlers[i] = (index, lap, time) => Checkpoint(entry, index, time);
                entry.Progress.CheckpointCrossed += checkpointHandlers[i];
            }
        }

        /// <summary>推进规则；宿主在运行阶段推进比赛时钟，暂停期间不调用。</summary>
        /// <param name="seconds">固定模拟步长。</param>
        /// <param name="raceTime">起跑后的独立比赛秒数，倒计时为零。</param>
        public void Advance(double seconds, double raceTime)
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(KagitKartRace));
            }
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds <= 0 || seconds > 0.1 ||
                double.IsNaN(raceTime) || double.IsInfinity(raceTime) || raceTime < lastTime)
            {
                throw new ArgumentOutOfRangeException(nameof(seconds));
            }
            if (State == KagitKartRaceState.Done)
            {
                return;
            }
            lastTime = raceTime;
            if (State == KagitKartRaceState.Countdown)
            {
                StepCountdown();
            }
            else
            {
                foreach (var entry in entries)
                {
                    StepKart(entry, seconds, raceTime);
                }
                UpdatePlaces();
                if (State == KagitKartRaceState.Finishing)
                {
                    WaitLeft -= seconds;
                    if (finishOrder.Count == entries.Length || WaitLeft <= 0)
                    {
                        Conclude(raceTime);
                    }
                }
                else if (finishOrder.Count == entries.Length)
                {
                    Conclude(raceTime);
                }
            }
            // 原作在规则更新之后采样，冲线当步不再录制。
            if (GhostRecorder != null && State != KagitKartRaceState.Countdown)
            {
                foreach (var entry in entries)
                {
                    if (entry.Vehicle.IsPlayer && !entry.Vehicle.Finished)
                    {
                        GhostRecorder.Record(entry.Vehicle.Position, entry.Vehicle.Heading, raceTime);
                    }
                }
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }
            disposed = true;
            for (int i = 0; i < entries.Length; i++)
            {
                entries[i].Progress.CheckpointCrossed -= checkpointHandlers[i];
                entries[i].Vehicle.Controls = default;
                entries[i].Vehicle.Autopilot = false;
                entries[i].Vehicle.Pinned = true;
            }
            Signaled = null;
        }

        private double RelativeDistance(KagitKartVehicle kart) =>
            (kart.TrackInfo.Distance - track.Track.data.startDist + length) % length;

        private void StepCountdown()
        {
            int before = Countdown;
            bool start = false;
            foreach (var entry in entries)
            {
                start = entry.Countdown.Advance(entry.Vehicle.Controls.Throttle > 0.5f);
            }
            if (Countdown != before)
            {
                Emit("countdown", number: Countdown);
            }
            if (!start)
            {
                return;
            }
            State = KagitKartRaceState.Running;
            Emit("raceStart");
            foreach (var entry in entries)
            {
                var kart = entry.Vehicle;
                kart.Pinned = false;
                switch (entry.Countdown.Grade)
                {
                    case KagitKartStartGrade.Perfect:
                        var boost = configuration.kart.boosts["start"];
                        kart.ApplyBoost(boost[0], boost[1], "start");
                        break;
                    case KagitKartStartGrade.Good:
                        var weak = configuration.kart.boosts["weakStart"];
                        kart.ApplyBoost(weak[0], weak[1], "start");
                        break;
                    case KagitKartStartGrade.Burnout:
                        kart.SpinOut("burnout");
                        break;
                }
                if (kart.IsPlayer)
                {
                    StartGrade = entry.Countdown.Grade;
                }
            }
        }

        private void StepKart(KagitKartRaceEntry entry, double seconds, double time)
        {
            var kart = entry.Vehicle;
            int lapBefore = entry.Progress.Lap;
            entry.Progress.Advance(RelativeDistance(kart), kart.Speed, seconds, time);
            kart.Progress = entry.Progress.Progress;
            if (!kart.Finished && entry.Progress.Lap > lapBefore)
            {
                if (lapBefore >= 1)
                {
                    var lapTimes = entry.Progress.LapTimes;
                    double lapTime = lapTimes[lapTimes.Count - 1];
                    entry.BestLap = entry.BestLap.HasValue ? Math.Min(entry.BestLap.Value, lapTime) : lapTime;
                    BestLapTime = BestLapTime.HasValue ? Math.Min(BestLapTime.Value, lapTime) : lapTime;
                    if (kart.IsPlayer)
                    {
                        GhostRecorder?.Mark(time);
                        if (previousGhost != null)
                        {
                            entry.SplitCount++;
                        }
                    }
                }
                if (entry.Progress.Finished)
                {
                    Finish(entry, time);
                }
                else
                {
                    if (entry.Progress.Lap >= 2)
                    {
                        Emit("lap", kart, entry.Progress.Lap, entry.Progress.LapTimes[entry.Progress.LapTimes.Count - 1]);
                    }
                    if (entry.Progress.Lap == Laps && Laps > 1)
                    {
                        Emit("finalLap", kart);
                    }
                }
            }
            entry.RecordPace(time);
            bool wrongBefore = entry.Progress.WrongWay;
            var forward = KagitKartVehicle.Forward(kart.Heading);
            var tangent = kart.TrackInfo.Tangent;
            entry.Progress.UpdateWrongWay(forward.x * tangent.x + forward.z * tangent.z, kart.Speed, kart.Rescuing, seconds);
            if (wrongBefore != entry.Progress.WrongWay)
            {
                Emit("wrongWay", kart, entry.Progress.WrongWay ? 1 : 0);
            }
        }

        private void Checkpoint(KagitKartRaceEntry entry, int index, double time)
        {
            double? split = null;
            if (entry.Vehicle.IsPlayer)
            {
                GhostRecorder?.Mark(time);
                if (previousGhost != null && entry.SplitCount < previousGhost.checkpoints.Length)
                {
                    split = time - previousGhost.checkpoints[entry.SplitCount];
                }
            }
            entry.SplitCount++;
            Emit("checkpoint", entry.Vehicle, index, time, split);
        }

        private void Finish(KagitKartRaceEntry entry, double time)
        {
            entry.Vehicle.Finished = true;
            entry.FinishTime = time;
            finishOrder.Add(entry);
            UpdatePlaces();
            Emit("finish", entry.Vehicle, entry.Vehicle.Place, time);
            if (multiplayer ? entry.Vehicle.IsHuman : entry.Vehicle.IsPlayer)
            {
                entry.Vehicle.Autopilot = true;
                if (State == KagitKartRaceState.Running)
                {
                    State = KagitKartRaceState.Finishing;
                    WaitLeft = configuration.race.finishWait;
                }
            }
        }

        private int Compare(KagitKartRaceEntry a, KagitKartRaceEntry b)
        {
            if (a.Vehicle.Finished && b.Vehicle.Finished)
            {
                return finishOrder.IndexOf(a) - finishOrder.IndexOf(b);
            }
            if (a.Vehicle.Finished)
            {
                return -1;
            }
            if (b.Vehicle.Finished)
            {
                return 1;
            }
            return b.Vehicle.Progress.CompareTo(a.Vehicle.Progress);
        }

        private void UpdatePlaces()
        {
            for (int i = 1; i < order.Length; i++)
            {
                var entry = order[i];
                int j = i - 1;
                while (j >= 0 && Compare(order[j], entry) > 0)
                {
                    order[j + 1] = order[j];
                    j--;
                }
                order[j + 1] = entry;
            }
            for (int i = 0; i < order.Length; i++)
            {
                var kart = order[i].Vehicle;
                if (kart.Place != i + 1)
                {
                    kart.Place = i + 1;
                    Emit("place", kart, kart.Place);
                }
            }
        }

        private void Conclude(double time)
        {
            pending.Clear();
            foreach (var entry in entries)
            {
                if (entry.Vehicle.Finished)
                {
                    continue;
                }
                double remaining = Math.Max(0, Laps * length - entry.Vehicle.Progress);
                double pace = Math.Max(entry.Pace(), 0.5 * entry.Vehicle.TopSpeed);
                entry.FinishTime = time + remaining / pace;
                pending.Add(entry);
            }
            pending.Sort((a, b) => a.FinishTime.CompareTo(b.FinishTime));
            foreach (var entry in pending)
            {
                entry.Vehicle.Finished = true;
                entry.Estimated = true;
                finishOrder.Add(entry);
            }
            UpdatePlaces();
            foreach (var entry in pending)
            {
                Emit("finish", entry.Vehicle, entry.Vehicle.Place, entry.FinishTime, estimated: true);
            }
            for (int i = 0; i < finishOrder.Count; i++)
            {
                var entry = finishOrder[i];
                results.Add(new KagitKartRaceResult
                {
                    CharacterId = entry.Vehicle.Character.id, Place = i + 1, Time = entry.FinishTime,
                    Estimated = entry.Estimated, BestLap = entry.BestLap
                });
            }
            if (Mode == KagitKartRaceMode.GrandPrix)
            {
                grandPrix.Commit(results, configuration.race.points);
            }
            State = KagitKartRaceState.Done;
            foreach (var entry in entries)
            {
                entry.Vehicle.Autopilot = true;
            }
            Emit("raceEnd");
        }

        private bool suppressNetworkSignals;
        private void Emit(string kind, KagitKartVehicle kart = null, int number = 0, double time = 0,
            double? split = null, bool estimated = false) =>
            (suppressNetworkSignals ? null : Signaled)?.Invoke(new KagitKartRaceEvent(kind, kart, number, time, split, estimated));
    }

    public sealed class KagitKartStanding
    {
        public string CharacterId { get; }
        public int Points { get; internal set; }
        public int LastPlace { get; internal set; } = 99;
        public double TotalTime { get; internal set; }

        internal KagitKartStanding(string id) => CharacterId = id;
    }

    /// 四章大奖赛；当前场重赛恢复开赛前快照，结算不可重复累加。
    public sealed class KagitKartGrandPrix
    {
        private static readonly string[] Cup = { "meadow", "bosphorus", "glacier", "desk" };
        private readonly KagitKartStanding[] standings;
        private readonly KagitKartStanding[] sorted;
        private readonly (int points, int place, double time)[] before;
        private bool committed;

        public int Index { get; private set; }
        public string TrackId => Cup[Index];
        public bool Completed => Index == Cup.Length - 1 && committed;
        public bool RoundComplete => committed;

        internal void RestoreNetworkState(int index, bool roundComplete, int[] points, int[] places, double[] times)
        {
            if (index is < 0 or > 3 || points?.Length != 8 || places?.Length != 8 || times?.Length != 8)
                throw new ArgumentException("大奖赛恢复状态无效。");
            Index = index;
            committed = roundComplete;
            for (int i = 0; i < standings.Length; i++)
            {
                standings[i].Points = points[i];
                standings[i].LastPlace = places[i];
                standings[i].TotalTime = times[i];
            }
            Snapshot();
        }

        /// <summary>为完整八角色杯赛创建积分状态。</summary>
        /// <param name="roster">原作八角色，顺序用于完全同分时的稳定排序。</param>
        public KagitKartGrandPrix(IReadOnlyList<KagitKartCharacter> roster)
        {
            if (roster == null || roster.Count != 8)
            {
                throw new ArgumentException("大奖赛必须包含八名角色。", nameof(roster));
            }
            standings = new KagitKartStanding[roster.Count];
            sorted = new KagitKartStanding[roster.Count];
            before = new (int, int, double)[roster.Count];
            var ids = new HashSet<string>();
            for (int i = 0; i < roster.Count; i++)
            {
                if (roster[i] == null || !ids.Add(roster[i].id))
                {
                    throw new ArgumentException("大奖赛角色缺失或重复。", nameof(roster));
                }
                KagitKartCatalog.CharacterName(roster[i].id);
                standings[i] = sorted[i] = new KagitKartStanding(roster[i].id);
            }
            Snapshot();
        }

        /// 按积分、最近一场名次、总时间排序，返回下次排序前有效的只读列表。
        public IReadOnlyList<KagitKartStanding> SortedStandings()
        {
            Array.Copy(standings, sorted, standings.Length);
            for (int i = 1; i < sorted.Length; i++)
            {
                var current = sorted[i];
                int j = i - 1;
                while (j >= 0 && CompareStanding(sorted[j], current) > 0)
                {
                    sorted[j + 1] = sorted[j];
                    j--;
                }
                sorted[j + 1] = current;
            }
            return sorted;
        }

        /// 当前场结算后进入下一章；最后一章保留领奖台排名。
        public bool NextRound()
        {
            if (!committed || Index >= Cup.Length - 1)
            {
                return false;
            }
            Index++;
            committed = false;
            Snapshot();
            return true;
        }

        /// 恢复本场开赛前积分、最近名次和时间，可在结算后重赛。
        public void RestartRound()
        {
            for (int i = 0; i < standings.Length; i++)
            {
                standings[i].Points = before[i].points;
                standings[i].LastPlace = before[i].place;
                standings[i].TotalTime = before[i].time;
            }
            committed = false;
        }

        internal void Commit(IReadOnlyList<KagitKartRaceResult> results, float[] points)
        {
            if (committed)
            {
                throw new InvalidOperationException("本场大奖赛已结算。");
            }
            if (results.Count != standings.Length || points == null || points.Length < standings.Length)
            {
                throw new ArgumentException("大奖赛结算不完整。", nameof(results));
            }
            var indices = new int[results.Count];
            var used = new HashSet<string>();
            for (int i = 0; i < results.Count; i++)
            {
                var result = results[i];
                int index = result == null ? -1 : Array.FindIndex(standings, value => value.CharacterId == result.CharacterId);
                if (index < 0 || result.Place != i + 1 || !used.Add(result.CharacterId) ||
                    double.IsNaN(result.Time) || double.IsInfinity(result.Time) || result.Time <= 0)
                {
                    throw new ArgumentException("大奖赛结算数据无效。", nameof(results));
                }
                indices[i] = index;
            }
            for (int i = 0; i < results.Count; i++)
            {
                var result = results[i];
                var standing = standings[indices[i]];
                result.Points = (int)points[result.Place - 1];
                standing.Points += result.Points;
                standing.LastPlace = result.Place;
                standing.TotalTime += result.Time;
                result.TotalPoints = standing.Points;
            }
            committed = true;
        }

        private void Snapshot()
        {
            for (int i = 0; i < standings.Length; i++)
            {
                before[i] = (standings[i].Points, standings[i].LastPlace, standings[i].TotalTime);
            }
        }

        private static int CompareStanding(KagitKartStanding a, KagitKartStanding b)
        {
            int points = b.Points.CompareTo(a.Points);
            if (points != 0)
            {
                return points;
            }
            int place = a.LastPlace.CompareTo(b.LastPlace);
            return place != 0 ? place : a.TotalTime.CompareTo(b.TotalTime);
        }
    }
}
