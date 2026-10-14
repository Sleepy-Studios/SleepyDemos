using System;
using System.Collections.Generic;
using Core.Runtime.Inputs;

namespace Hotfix.KagitKart
{
    /// 本场完整模拟链路；场景宿主负责公共资源、页面、输入和表现生命周期。
    public sealed partial class KagitKartSession : IDisposable
    {
        private readonly KagitKartSimulationClock clock = new();
        private readonly KagitKartVehicle[] karts;
        private readonly Action<double> simulate;
        private KagitKartControls playerControls;
        private Func<KagitKartControls> readPlayerInput;
        private bool disposed;
        private float lastNetworkSnapshot;
        internal bool Networked { get; private set; }

        public KagitKartTrackQuery Track { get; }
        public KagitKartRace Race { get; }
        public KagitKartItems Items { get; }
        public KagitKartAi Ai { get; }
        public KagitKartCollisions Collisions { get; }
        public KagitKartVehicle Player { get; }
        public IReadOnlyList<KagitKartVehicle> Karts => karts;
        public LocalPauseState Pause { get; } = new();
        public double RaceTime { get; private set; }
        public float BumpImpulse { get; }
        public double Interpolation => Networked ? UnityEngine.Mathf.Clamp01((UnityEngine.Time.unscaledTime - lastNetworkSnapshot) * 30)
            : Pause.IsPaused || Race.State == KagitKartRaceState.Done ? 1 : clock.Alpha;

        /// <summary>组装本场规则，原作随机网格与大奖赛逆积分网格均在这里生成。</summary>
        /// <param name="source">已校验的源码配置及八角色。</param>
        /// <param name="data">已校验赛道数据。</param>
        /// <param name="plan">本赛道原作 AI 路线。</param>
        /// <param name="mode">三种原作模式。</param>
        /// <param name="characterId">玩家角色。</param>
        /// <param name="speedClass">80、120 或 200。</param>
        /// <param name="seed">独立比赛种子。</param>
        /// <param name="grandPrix">大奖赛四章状态，可为空。</param>
        /// <param name="ghost">计时赛最佳幽灵，可为空。</param>
        public KagitKartSession(KagitKartSourceCatalog source, KagitKartTrackData data, KagitKartAiPlan plan,
            KagitKartRaceMode mode, string characterId, int speedClass, uint seed,
            KagitKartGrandPrix grandPrix = null, KagitKartGhostData ghost = null, string[] grid = null, string[] humanIds = null)
        {
            if (source == null || data == null || plan == null)
            {
                throw new ArgumentNullException(nameof(source));
            }
            var character = Array.Find(source.roster, value => value.id == characterId);
            if (character == null && (characterId != null || humanIds == null))
            {
                throw new ArgumentException("玩家角色无效。", nameof(characterId));
            }
            var tuning = source.config.track;
            BumpImpulse = source.config.kart.bumpImpulse;
            Track = new KagitKartTrackQuery(data, tuning.gridCell, tuning.waterDrop, tuning.deskDrop);
            var random = new KagitKartRandom(seed);
            var gridRandom = random.Fork("grid");
            var others = new List<KagitKartCharacter>();
            foreach (var value in source.roster)
            {
                    if (value != character)
                {
                    others.Add(value);
                }
            }
            for (int i = others.Count - 1; i > 0; i--)
            {
                int j = (int)Math.Floor(gridRandom.Next() * (i + 1));
                (others[i], others[j]) = (others[j], others[i]);
            }
            if (mode == KagitKartRaceMode.TimeTrial)
            {
                others.Clear();
            }
            if (character != null) others.Add(character);
            if (mode == KagitKartRaceMode.GrandPrix && grandPrix != null && grandPrix.Index > 0)
            {
                others.Clear();
                var standings = grandPrix.SortedStandings();
                for (int i = standings.Count - 1; i >= 0; i--)
                {
                    string id = standings[i].CharacterId;
                    others.Add(Array.Find(source.roster, value => value.id == id));
                }
            }
            if (grid != null)
            {
                others.Clear();
                foreach (string id in grid)
                {
                    var entry = Array.Find(source.roster, value => value.id == id);
                    if (entry == null || others.Contains(entry)) throw new ArgumentException("联机网格角色缺失或重复。", nameof(grid));
                    others.Add(entry);
                }
            }
            Networked = humanIds != null;
            var slots = Track.GridSlots(others.Count);
            karts = new KagitKartVehicle[slots.Length];
            for (int i = 0; i < karts.Length; i++)
            {
                var kart = new KagitKartVehicle(source.config, Track, others[i], speedClass, others[i] == character,
                    humanIds != null && Array.IndexOf(humanIds, others[i].id) >= 0);
                kart.PlaceAt(slots[i].Position, slots[i].Heading);
                karts[i] = kart;
                if (kart.IsPlayer)
                {
                    Player = kart;
                }
            }
            try
            {
                Race = new KagitKartRace(source.config, Track, karts, mode, grandPrix, ghost, Networked);
                Items = new KagitKartItems(source.config, Track, karts, random.Fork("items"), speedClass, mode == KagitKartRaceMode.TimeTrial);
                Collisions = new KagitKartCollisions(karts, source.config.kart);
                Ai = new KagitKartAi(source.config, Track, plan, Race, Items, karts, random.Fork("ai"), speedClass);
            }
            catch
            {
                Ai?.Dispose();
                Items?.Dispose();
                Race?.Dispose();
                throw;
            }
            simulate = Simulate;
            Pause.Changed += ClearInput;
        }

        /// <summary>提交一帧输入并推进独立模拟；暂停不积压、不消耗道具或比赛随机流。</summary>
        /// <param name="frameSeconds">宿主未缩放的帧时间。</param>
        /// <param name="controls">公共输入边界转换出的原作命令。</param>
        /// <returns>本帧固定步数量。</returns>
        public int AdvanceFrame(double frameSeconds, KagitKartControls controls)
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(KagitKartSession));
            }
            playerControls = controls;
            bool frozen = Pause.IsPaused || Race.State == KagitKartRaceState.Done;
            if (frozen)
            {
                ClearInput();
            }
            return clock.Advance(frameSeconds, frozen, simulate);
        }

        /// <summary>由真实输入在每个固定步读取，短按锁存不会因零步帧丢失或在追赶步中重复。</summary>
        /// <param name="frameSeconds">未缩放帧时间。</param>
        /// <param name="readControls">宿主持有的输入读取委托；仅当前帧借用，不保存到退出后。</param>
        public int AdvanceInputFrame(double frameSeconds, Func<KagitKartControls> readControls)
        {
            if (readControls == null)
            {
                throw new ArgumentNullException(nameof(readControls));
            }
            readPlayerInput = readControls;
            try
            {
                return AdvanceFrame(frameSeconds, default(KagitKartControls));
            }
            finally
            {
                readPlayerInput = null;
            }
        }

        /// 设备切换或退出时清空统一命令，道具与运动层在中性输入下释放保持状态。
        public void ClearInput()
        {
            playerControls = default;
            Items.ClearInput();
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }
            disposed = true;
            Pause.Changed -= ClearInput;
            ClearInput();
            Ai.Dispose();
            Items.Dispose();
            Race.Dispose();
        }

        private void Simulate(double seconds)
        {
            if (Race.State == KagitKartRaceState.Done)
            {
                return;
            }
            if (Race.State != KagitKartRaceState.Countdown)
            {
                RaceTime += seconds;
            }
            if (!Player.Autopilot)
            {
                Player.Controls = readPlayerInput != null ? readPlayerInput() : playerControls;
            }
            AdvanceRules(seconds);
        }

        private void AdvanceRules(double seconds)
        {
            Ai.Advance(seconds, RaceTime);
            foreach (var kart in karts)
            {
                kart.Advance((float)seconds);
            }
            Collisions.Advance((float)seconds);
            Items.Advance(seconds, RaceTime, Race.State != KagitKartRaceState.Countdown);
            Race.Advance(seconds, RaceTime);
        }

        /// <summary>由网络服务器唯一固定步推进，不使用本机积压丢步时钟。</summary>
        /// <param name="seconds">网络固定步长。</param>
        /// <param name="inputs">以角色 ID 映射的可信输入。</param>
        public void AdvanceNetwork(float seconds, IReadOnlyDictionary<string, KagitKartControls> inputs)
        {
            if (disposed || Race.State == KagitKartRaceState.Done) return;
            if (Race.State != KagitKartRaceState.Countdown) RaceTime += seconds;
            foreach (var kart in karts)
            {
                if (kart.IsHuman && !kart.Autopilot)
                {
                    kart.Controls = inputs.TryGetValue(kart.Id, out var controls) ? controls : default;
                }
            }
            AdvanceRules(seconds);
        }

        internal void Predict(float seconds, KagitKartControls controls, bool replay)
        {
            if (disposed || Player == null || Player.Autopilot || Race.State == KagitKartRaceState.Done) return;
            // 单次表现由权威事件确认；预测和重放只改变可回滚的运动状态。
            Player.SuppressSignals = true;
            try
            {
                Player.Controls = controls;
                Player.Advance(seconds);
            }
            finally
            {
                Player.SuppressSignals = false;
            }
        }
    }
}
