using System;
using System.Collections.Generic;

namespace Hotfix.BlockPorters
{
    /// 运行时和编辑器共享的虚拟时钟；同一时刻按任务编号处理，抬起才开路。
    public sealed class BlockPortersScheduler
    {
        public sealed class Transport
        {
            public PorterJob Job;
            public double Started;
            public double Arrived;
            public double Pickup;
            public double Returned;
            public double Jump;
            public double Delivered;
        }

        private readonly List<Transport> transports = new();
        public BlockPortersSession Session { get; }
        public double Time { get; private set; }
        public float CellSize => Math.Min(.4f, 6.4f / Math.Max(Session.Width, Session.Height));
        public IReadOnlyList<Transport> Transports => transports;
        public bool IsStable => transports.Count == 0;
        public event Action<Transport> Assigned;
        public event Action<Transport> PickedUp;
        public event Action<Transport> Delivered;

        /// <summary>使用独立规则会话；调用方必须由本调度器处理预约、抬起与交付。</summary>
        /// <param name="session">不能含有调度器之外的在途任务。</param>
        public BlockPortersScheduler(BlockPortersSession session)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
            if (session.InFlight != 0) throw new ArgumentException("新调度器只能接管稳定会话。");
        }

        /// <summary>派出列头并立即分配当前可搬任务；被挡成员只保留逻辑人数。</summary>
        /// <param name="column">0–3 的列号。</param>
        public bool Dispatch(int column)
        {
            if (Session.Dispatch(column) == null) return false;
            Assign(); Session.EvaluateOutcome(); return true;
        }

        /// <summary>推进到虚拟时刻，逐一处理时间和任务 ID 排序的事件。</summary>
        /// <param name="targetTime">不早于当前时间的秒数；暂停时调用方不推进。</param>
        public void AdvanceTo(double targetTime)
        {
            if (targetTime < Time || double.IsNaN(targetTime) || double.IsInfinity(targetTime)) throw new ArgumentOutOfRangeException(nameof(targetTime));
            Assign();
            while (transports.Count > 0)
            {
                Transport next = null;
                double at = double.MaxValue;
                foreach (var item in transports)
                {
                    double due = item.Job.IsPickedUp ? item.Delivered : item.Pickup;
                    if (due < at || (due == at && (next == null || item.Job.Id < next.Job.Id))) { next = item; at = due; }
                }
                if (at > targetTime) break;
                Time = at;
                if (!next.Job.IsPickedUp) { Session.PickUp(next.Job.Id); PickedUp?.Invoke(next); }
                else { Session.Deliver(next.Job.Id); transports.Remove(next); Delivered?.Invoke(next); }
                Assign();
            }
            Time = targetTime;
            Session.EvaluateOutcome();
        }

        /// 自动推进所有在途工作，直到需要玩家再次派队或游戏结束。
        public void Settle()
        {
            Assign();
            while (transports.Count > 0)
            {
                double next = double.MaxValue;
                foreach (var item in transports) next = Math.Min(next, item.Job.IsPickedUp ? item.Delivered : item.Pickup);
                AdvanceTo(next);
            }
            Session.EvaluateOutcome();
        }

        private void Assign()
        {
            foreach (var team in Session.Teams)
                while (Session.TryAssign(team.Id, out var job))
                {
                    double travel = (job.Path.Length - 1) * CellSize / 3.4;
                    var start = job.Path[0];
                    double x = (start.X - (Session.Width - 1) * .5) * CellSize;
                    double z = start.Y * CellSize + (6.4 - Session.Height * CellSize) * .5;
                    // 坑沿统一终点，演出和计划使用同一位置；不依赖角色当前帧位置。
                    double pitTravel = Math.Sqrt(x * x + (z + .8) * (z + .8)) / 3.4;
                    var task = new Transport { Job = job, Started = Time, Arrived = Time + travel,
                        Pickup = Time + travel + .28, Returned = Time + travel * 2 + .28,
                        Jump = Time + travel * 2 + .28 + pitTravel,
                        Delivered = Time + travel * 2 + .28 + pitTravel + .5 };
                    transports.Add(task); Assigned?.Invoke(task);
                }
        }
    }
}
