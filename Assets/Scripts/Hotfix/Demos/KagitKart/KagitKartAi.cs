using System;
using System.Collections.Generic;

namespace Hotfix.KagitKart
{
    /// ai.js 的实时车手；路线规划由原版生成资产提供，随机流按车手隔离。
    public sealed class KagitKartAi : IDisposable
    {
        private const int DriveMode = 0, DriftMode = 1, ShortcutMode = 2;
        private const double EntryYaw = 0.85, ReleaseYaw = 0.8;
        private readonly KagitKartConfiguration configuration;
        private readonly KagitKartKartTuning tuning;
        private readonly KagitKartTrackQuery track;
        private readonly KagitKartTrackSamples samples;
        private readonly KagitKartAiPlan plan;
        private readonly KagitKartRace race;
        private readonly KagitKartItems items;
        private readonly IReadOnlyList<KagitKartVehicle> karts;
        private readonly KagitKartVehicle player;
        private readonly Driver[] drivers;
        private readonly KagitKartAiShortcutProbe[] shortcuts;
        private readonly float[] hazardDistance = new float[48], hazardLateral = new float[48], hazardRadius = new float[48];
        private readonly int[] hazardHints = new int[48];
        private readonly float[] boxDistance = new float[64], boxLateral = new float[64];
        private readonly double yawHigh, rubberK, spacing, length;
        private readonly int sampleCount;
        private int hazardCount, boxCount, stepCount;
        private double now;
        private bool disposed;
        private KagitKartQueryInfo query;

        private sealed class Driver
        {
            public KagitKartVehicle Kart;
            public KagitKartRandom Random;
            public KagitKartAiStyle Style;
            public float[] Profile;
            public double Skill, Sn, Handling, LineQ, StartAt, Offset, SteerMin, YawMin, DriftMin, TierHold;
            public double MistakeProbability, Reaction, LaunchProbability, TrickProbability;
            public double Dynamic, DriftTime, DriftTurn, LastHeading, DriftSteer, EarlyAt = 0.5, ReleaseAt = -9;
            public int Mode, Corner = -1, Mistake, Phase;
            public double StuckTime, ReverseTime, ReverseSteer, InkNoise, InkTarget, InkTime;
            public int RespawnRoll = -1;
            public string ItemPlan;
            public double ItemTime, HoldFor, NextTry, HeldFor, LastAtPlayer = -99;
            public bool BackPreferred;
            public double ThreatUntil = -1;
            public string ThreatKind;
            public int Shortcut = -1, TrickPressed;
            public double ShortcutTime;
            public bool ShortcutCommitted, ShortcutPast, ShortcutFired;
            public double WatchdogTime, WatchdogDistance = -1, WatchdogProgress;
        }

        /// <summary>创建本场七名 CPU 与玩家完赛自动驾驶，订阅本场道具预警。</summary>
        /// <param name="configuration">原作配置。</param>
        /// <param name="track">当前道路查询。</param>
        /// <param name="plan">与赛道匹配的原作路线规划。</param>
        /// <param name="race">当前比赛规则。</param>
        /// <param name="items">当前道具规则。</param>
        /// <param name="karts">固定网格顺序的赛车。</param>
        /// <param name="random">比赛独立 ai 随机流。</param>
        /// <param name="speedClass">80、120 或 200。</param>
        public KagitKartAi(KagitKartConfiguration configuration, KagitKartTrackQuery track, KagitKartAiPlan plan,
            KagitKartRace race, KagitKartItems items, IReadOnlyList<KagitKartVehicle> karts, KagitKartRandom random, int speedClass)
        {
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.track = track ?? throw new ArgumentNullException(nameof(track));
            this.plan = plan ?? throw new ArgumentNullException(nameof(plan));
            this.race = race ?? throw new ArgumentNullException(nameof(race));
            this.items = items ?? throw new ArgumentNullException(nameof(items));
            this.karts = karts ?? throw new ArgumentNullException(nameof(karts));
            if (random == null || karts.Count < 1 || karts.Count > 8 || plan.trackId != track.Track.definition.id ||
                plan.sampleCount != track.Track.data.sampleCount || !configuration.classes.TryGetValue(speedClass.ToString(), out var cls) ||
                !plan.probes.TryGetValue(speedClass.ToString(), out shortcuts))
            {
                throw new ArgumentException("智能车手的比赛或速度组别不匹配。");
            }
            tuning = configuration.kart;
            samples = track.Track.data.samples;
            sampleCount = plan.sampleCount;
            spacing = track.Track.data.spacing;
            length = track.Track.data.length;
            yawHigh = cls.yawHigh > 0 ? cls.yawHigh : tuning.yawHigh;
            rubberK = cls.rubberK;
            int cpuCount = 0;
            for (int i = 0; i < karts.Count; i++)
            {
                if (karts[i].IsPlayer)
                {
                    player = karts[i];
                }
                else
                {
                    cpuCount++;
                }
            }
            double[] tiers = new double[cpuCount];
            double[] sourceTiers = { 0.92, 0.92, 0.8, 0.8, 0.8, 0.68, 0.68 };
            Array.Copy(sourceTiers, tiers, cpuCount);
            for (int i = tiers.Length - 1; i > 0; i--)
            {
                int j = (int)Math.Floor(random.Next() * (i + 1));
                (tiers[i], tiers[j]) = (tiers[j], tiers[i]);
            }
            drivers = new Driver[karts.Count];
            int seen = 0;
            for (int i = 0; i < drivers.Length; i++)
            {
                var kart = karts[i];
                drivers[i] = CreateDriver(kart, random.Fork("driver:" + kart.Id), kart.IsPlayer ? 0.92 : tiers[seen++]);
                drivers[i].Phase = i & 1;
            }
            Array.Fill(hazardHints, -1);
            BuildBoxes();
            items.Signaled += OnItemSignal;
        }

        /// <summary>在车辆运动前生成命令，不消费表现或网格随机流。</summary>
        /// <param name="seconds">独立比赛时钟的固定步长。</param>
        /// <param name="raceTime">不含倒计时和暂停的比赛时间。</param>
        public void Advance(double seconds, double raceTime)
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(KagitKartAi));
            }
            if (!Finite(seconds) || seconds <= 0 || seconds > 0.1 || !Finite(raceTime) || raceTime < now)
            {
                throw new ArgumentOutOfRangeException(nameof(seconds));
            }
            now = raceTime;
            stepCount++;
            RefreshHazards();
            foreach (var driver in drivers)
            {
                var kart = driver.Kart;
                if (kart.IsHuman && !kart.Autopilot)
                {
                    continue;
                }
                var controls = kart.Controls;
                controls.LookBack = false;
                if (race.State == KagitKartRaceState.Countdown)
                {
                    controls = new KagitKartControls { Throttle = race.TimeToGo <= driver.StartAt ? 1 : 0 };
                    kart.Controls = controls;
                    continue;
                }
                RubberBand(driver);
                if (kart.Rescuing)
                {
                    if (driver.RespawnRoll < 0)
                    {
                        driver.RespawnRoll = driver.Random.Next() < driver.LaunchProbability ? 1 : 0;
                    }
                    kart.Controls = new KagitKartControls { Throttle = kart.RescueStage == 3 && driver.RespawnRoll == 1 ? 1 : 0 };
                    driver.Mode = DriveMode;
                    driver.StuckTime = driver.ReverseTime = driver.WatchdogTime = driver.WatchdogProgress = 0;
                    driver.WatchdogDistance = -1;
                    continue;
                }
                driver.RespawnRoll = -1;
                Drive(driver, ref controls, seconds);
                UseItems(driver, ref controls, seconds);
                kart.Controls = controls;
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }
            disposed = true;
            items.Signaled -= OnItemSignal;
            foreach (var driver in drivers)
            {
                driver.Kart.RubberBand = 1;
                driver.Kart.Controls = default;
            }
        }

        private Driver CreateDriver(KagitKartVehicle kart, KagitKartRandom random, double skill)
        {
            var style = kart.IsPlayer ? plan.defaultStyle : plan.styles[kart.Id];
            double sn = Clamp((skill - 0.68) / 0.24, 0, 1);
            double roll = random.Next() * (kart.IsPlayer ? 1 : 100);
            double low, high;
            int perfect = sn > 0.75 ? 50 : sn > 0.25 ? 35 : 20;
            int good = 35;
            int none = sn > 0.75 ? 10 : sn > 0.25 ? 20 : 30;
            if (kart.IsPlayer || roll < perfect)
            {
                low = 0.7;
                high = 0.95;
            }
            else if (roll < perfect + good)
            {
                low = 0.35;
                high = 0.6;
            }
            else if (roll < perfect + good + none)
            {
                low = 0;
                high = 0.25;
            }
            else
            {
                low = 2.1;
                high = 2.8;
            }
            double usage = Clamp(0.86 + 0.12 * sn + style.usage, 0.8, 1);
            double handling = 1 + tuning.statHandling * (kart.Character.stats.handling - 3);
            var profile = new float[sampleCount];
            double top = kart.BaseTop * 1.06;
            for (int i = 0; i < profile.Length; i++)
            {
                profile[i] = (float)CornerSpeed(Math.Abs(plan.lineK[i]) / 0.92, top, handling, usage);
            }
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = sampleCount - 1; i >= 0; i--)
                {
                    double next = profile[i == sampleCount - 1 ? 0 : i + 1];
                    double limit = Math.Sqrt(next * next + 2 * 14 * spacing);
                    if (limit < profile[i])
                    {
                        profile[i] = (float)limit;
                    }
                }
            }
            double minimum = Lerp(-0.6, -1, sn);
            return new Driver
            {
                Kart = kart, Random = random, Style = style, Skill = skill, Sn = sn, Profile = profile, Handling = handling,
                LineQ = Lerp(0.6, 1, sn), StartAt = Range(random, low, high),
                Offset = Range(random, -1.8, 1.8) * style.offset * (kart.IsPlayer ? 0.3 : 1), SteerMin = minimum,
                YawMin = DriftYaw(minimum), DriftMin = (Lerp(58, 38, sn) + style.driftMin) * Math.PI / 180,
                TierHold = style.tierHold * (0.4 + 0.6 * sn),
                MistakeProbability = Clamp((0.04 + 0.11 * (1 - sn)) * style.mistake, 0.04, 0.15),
                Reaction = Lerp(0.55, 0.15, sn), LaunchProbability = Lerp(0.55, 0.95, sn), TrickProbability = Lerp(0.6, 0.97, sn)
            };
        }

        private double CornerSpeed(double curvature, double top, double handling, double usage)
        {
            if (curvature < 1e-4)
            {
                return 1e3;
            }
            double span = Math.Max(1, top - tuning.yawLowSpeed);
            double speed = handling * (tuning.yawLow + (tuning.yawLow - yawHigh) * tuning.yawLowSpeed / span) /
                (curvature + handling * (tuning.yawLow - yawHigh) / span);
            if (speed > top)
            {
                speed = Math.Max(top, handling * yawHigh / curvature);
            }
            return speed * usage;
        }

        private void RubberBand(Driver driver)
        {
            var kart = driver.Kart;
            double rb = 1;
            if (!kart.IsPlayer && player != null)
            {
                var tuning = configuration.rubberBand;
                double gap = player.Progress - kart.Progress;
                if (gap > tuning.behindFrom)
                {
                    rb = Math.Min(1 + rubberK * (gap - tuning.behindFrom) / 100, tuning.max);
                    rb = Math.Min(rb, Math.Max(1, player.BaseTop * tuning.playerCap / Math.Max(1, kart.BaseTop)));
                }
                else if (gap < -tuning.aheadFrom)
                {
                    rb = Math.Max(1 + rubberK * (gap + tuning.aheadFrom) / 100, tuning.aheadMin);
                }
            }
            kart.RubberBand = (float)rb;
        }

        private void BuildBoxes()
        {
            boxCount = Math.Min(items.Boxes.Count, boxDistance.Length);
            for (int i = 0; i < boxCount; i++)
            {
                track.Query(items.Boxes[i].Position, -1, ref query);
                boxDistance[i] = (float)query.Distance;
                boxLateral[i] = query.Lateral;
            }
        }

        private void RefreshHazards()
        {
            hazardCount = Math.Min(items.Hazards.Count, hazardDistance.Length);
            for (int i = 0; i < hazardCount; i++)
            {
                var hazard = items.Hazards[i];
                track.Query(hazard.Position, hazardHints[i], ref query);
                hazardHints[i] = query.Index;
                hazardDistance[i] = (float)query.Distance;
                hazardLateral[i] = query.Lateral;
                hazardRadius[i] = hazard.Radius;
            }
        }

        private void OnItemSignal(KagitKartItemEvent signal)
        {
            if (signal.Kind != "threat")
            {
                return;
            }
            foreach (var driver in drivers)
            {
                if (driver.Kart != signal.Kart)
                {
                    continue;
                }
                double until = now + (signal.Eta != 0 ? signal.Eta : 0.5) + 0.25;
                if (until > driver.ThreatUntil)
                {
                    driver.ThreatUntil = until;
                    driver.ThreatKind = signal.Item;
                }
                return;
            }
        }

        private void EndDrift(Driver driver)
        {
            driver.Mode = DriveMode;
            driver.ReleaseAt = now;
        }

        private void Drive(Driver driver, ref KagitKartControls controls, double dt)
        {
            var kart = driver.Kart;
            var info = kart.TrackInfo;
            int i0 = info.Index;
            double speed = Math.Max(0, kart.Speed), top = kart.TopSpeed, myDistance = info.Distance, myLateral = info.Lateral;
            int cornerIndex = plan.nextCorner[i0];
            if (cornerIndex != driver.Corner)
            {
                driver.Corner = cornerIndex;
                driver.Mistake = 0;
                if (cornerIndex >= 0 && driver.Random.Next() < driver.MistakeProbability)
                {
                    driver.Mistake = 1 + (int)Math.Floor(driver.Random.Next() * 4);
                }
            }
            var corner = cornerIndex >= 0 ? plan.corners[cornerIndex] : null;
            double want = 0;
            for (int i = 0; i < karts.Count; i++)
            {
                var other = karts[i];
                if (other == kart || other.Rescuing)
                {
                    continue;
                }
                double ahead = Along(myDistance, other.TrackInfo.Distance);
                double lateral = other.TrackInfo.Lateral - myLateral;
                if (ahead > -2.5 && ahead < 2.5 && Math.Abs(lateral) < 4)
                {
                    if (driver.Style.bump > 0 && Math.Abs(lateral) > 2.3)
                    {
                        want += Math.Sign(lateral) * 0.8 * driver.Style.bump;
                    }
                    else if (driver.Style.bump < 0.5)
                    {
                        want -= (lateral >= 0 ? 1 : -1) * (3.2 - Math.Abs(lateral)) * 0.8;
                    }
                    continue;
                }
                double range = 16 + 1.5 * Math.Max(0, speed - other.Speed);
                if (ahead <= 0 || ahead > range)
                {
                    continue;
                }
                if (Math.Abs(lateral) < 2.6 && other.Speed < speed + 1)
                {
                    want += (lateral >= 0 ? -1 : 1) * (2.8 - Math.Abs(lateral)) * (1.3 - ahead / range);
                }
                else if (ahead > 5 && ahead < 16 && Math.Abs(lateral) < 5 && other.Speed > 0.6 * top)
                {
                    want += lateral * 0.12 * driver.Style.draft;
                }
            }
            for (int i = 0; i < hazardCount; i++)
            {
                double ahead = Along(myDistance, hazardDistance[i]);
                if (ahead < 0 || ahead > 30)
                {
                    continue;
                }
                double baseLine = plan.lineLat[Index(i0 + Round(ahead / spacing))] + driver.Offset + driver.Dynamic;
                double lateral = hazardLateral[i] - baseLine;
                double clearance = hazardRadius[i] + 1.6;
                if (Math.Abs(lateral) < clearance)
                {
                    want += (lateral >= 0 ? -1 : 1) * (clearance - Math.Abs(lateral)) * 1.4 * (0.6 + 0.4 * driver.Sn);
                }
            }
            if (kart.Item == null && kart.Roulette <= 0)
            {
                double best = 1e9;
                for (int i = 0; i < boxCount; i++)
                {
                    if (!items.Boxes[i].Active)
                    {
                        continue;
                    }
                    double ahead = Along(myDistance, boxDistance[i]);
                    if (ahead < 4 || ahead > 45)
                    {
                        continue;
                    }
                    double baseLine = plan.lineLat[Index(i0 + Round(ahead / spacing))] + driver.Offset;
                    double lateral = boxLateral[i] - baseLine;
                    if (Math.Abs(lateral) <= 4.5 * driver.Style.box && Math.Abs(lateral) < Math.Abs(best))
                    {
                        best = lateral;
                    }
                }
                if (best < 1e9)
                {
                    want += best;
                }
            }
            if (driver.Mistake == 1 && corner != null)
            {
                want += corner.dir * -3.5;
            }
            driver.Dynamic += (Clamp(want, -6, 6) - driver.Dynamic) * Math.Min(1, dt * 3.5);
            int pursuit = Index(i0 + Round((6 + speed * 0.42) / spacing));
            double lat = Clamp(plan.lineLat[pursuit] * driver.LineQ + driver.Offset * plan.fade[pursuit] + driver.Dynamic,
                plan.limL[pursuit] - 0.8, plan.limR[pursuit] + 0.8);
            double tx = samples.px[pursuit] + samples.hx[pursuit] * lat;
            double tz = samples.pz[pursuit] + samples.hz[pursuit] * lat;
            bool hasRocket = (kart.Item == "rocket" || kart.Item == "rocket3") && kart.Roulette <= 0;
            int nearShortcut = -1;
            double toShortcut = 0;
            if (hasRocket && driver.Mode != ShortcutMode)
            {
                for (int i = 0; i < plan.shortcuts.Length; i++)
                {
                    if (!shortcuts[i].ok)
                    {
                        continue;
                    }
                    double toEntry = Along(myDistance, samples.dist[plan.shortcuts[i].i]);
                    if (toEntry > 0 && toEntry < 45 + speed * 0.8)
                    {
                        nearShortcut = i;
                        toShortcut = toEntry;
                        break;
                    }
                }
                if (nearShortcut >= 0 && driver.Mode == DriftMode)
                {
                    EndDrift(driver);
                }
            }
            bool shortcutBrake = false;
            if (driver.Mode == ShortcutMode)
            {
                var shortcut = plan.shortcuts[driver.Shortcut];
                var probe = shortcuts[driver.Shortcut];
                driver.ShortcutTime += dt;
                double toEntry = Along(myDistance, samples.dist[shortcut.i]);
                bool committed = !info.OnRoad || kart.BoostTime > 0.1;
                if (committed && toEntry <= 0.5 && !driver.ShortcutCommitted)
                {
                    driver.ShortcutCommitted = true;
                    driver.ShortcutTime = 0;
                }
                double rel = Along(samples.dist[shortcut.j], myDistance);
                bool onFar = rel > -60 && rel < 150;
                double alignError = Math.Abs(AngleDiff(samples.heading[onFar ? info.Index : probe.jc], kart.Heading));
                bool abort = driver.ShortcutCommitted ? driver.ShortcutTime > 6 : driver.ShortcutTime > 4 || kart.WallContact ||
                    toEntry <= 0.5 && !committed || toEntry < -15 && info.OnRoad && rel < -20;
                if (abort || driver.ShortcutCommitted && onFar && info.OnRoad && (alignError < 0.6 || rel > 25))
                {
                    driver.Mode = DriveMode;
                    driver.Shortcut = -1;
                }
                else if (toEntry > 0.5)
                {
                    int index = Index(shortcut.i + 2);
                    double sl = probe.sideI * (samples.halfWidth[index] - 1.5);
                    tx = samples.px[index] + samples.hx[index] * sl;
                    tz = samples.pz[index] + samples.hz[index] * sl;
                }
                else
                {
                    int index = probe.jc;
                    double sl = probe.aimLat;
                    if (driver.ShortcutPast || Hypot(samples.px[index] + samples.hx[index] * sl - kart.Position.x,
                        samples.pz[index] + samples.hz[index] * sl - kart.Position.z) < 10)
                    {
                        driver.ShortcutPast = true;
                        index = Index((onFar ? info.Index : probe.jc) + Round(12 / spacing));
                        sl = plan.lineLat[index];
                    }
                    tx = samples.px[index] + samples.hx[index] * sl;
                    tz = samples.pz[index] + samples.hz[index] * sl;
                    shortcutBrake = kart.BoostTime <= 0 && kart.Surface != "out" && onFar && speed > 12 && alignError > 0.6;
                }
            }
            else if (nearShortcut >= 0 && driver.Mode == DriveMode && toShortcut < 30 + speed * 0.8)
            {
                driver.Mode = ShortcutMode;
                driver.Shortcut = nearShortcut;
                driver.ShortcutTime = 0;
                driver.ShortcutCommitted = driver.ShortcutPast = driver.ShortcutFired = false;
            }
            double error = AngleDiff(Math.Atan2(tx - kart.Position.x, tz - kart.Position.z), kart.Heading);
            double curvature = driver.Mode == ShortcutMode ? 0 : plan.lineK[pursuit];
            double pace = kart.IsPlayer || plan.noDrift[i0] != 0 ? 1 : Lerp(0.94, 1, driver.Sn);
            double throttle = pace, brake = 0;
            if (driver.Mode == DriveMode)
            {
                double targetSpeed = driver.Profile[Index(i0 + (int)Math.Ceiling((2 + speed * 0.15) / spacing))];
                if (driver.Mistake == 2 && corner != null)
                {
                    targetSpeed = Math.Min(targetSpeed, top * 0.82);
                }
                if (speed > targetSpeed + 0.3)
                {
                    throttle = 0;
                }
                if (speed > targetSpeed + 2.5)
                {
                    brake = 1;
                }
            }
            else if (shortcutBrake)
            {
                throttle = 0;
                brake = 1;
            }
            double steer = Clamp(-error * 2.4 - curvature * speed / Math.Max(0.5, yawHigh * driver.Handling) * 0.5, -1, 1);
            bool drift = false;
            var ramp = info.RampId >= 0 ? track.Track.data.ramps[info.RampId] : null;
            bool onTrick = ramp != null && ramp.trick;
            int lipRamp = plan.trickLip[i0];
            if (driver.Mode == DriftMode && plan.noDrift[i0] != 0)
            {
                EndDrift(driver);
            }
            if (onTrick)
            {
                if (driver.TrickPressed == 0 && Along(myDistance, ramp.d1) < Math.Max(1.5, speed * 0.12) && kart.Grounded)
                {
                    driver.TrickPressed = driver.Random.Next() < driver.TrickProbability ? 1 : 2;
                }
            }
            else if (kart.Grounded && info.RampId < 0 && lipRamp < 0)
            {
                driver.TrickPressed = 0;
            }
            if (driver.TrickPressed == 1 && (onTrick || !kart.Grounded))
            {
                drift = true;
            }
            if (driver.Mode == DriftMode)
            {
                if (!kart.Drifting || DriftStep(driver, dt))
                {
                    EndDrift(driver);
                }
                else
                {
                    drift = true;
                    steer = driver.DriftSteer * kart.DriftDirection;
                }
            }
            else if (driver.Mode == DriveMode && nearShortcut < 0 && plan.noDrift[i0] == 0 && lipRamp < 0 && !onTrick &&
                driver.Mistake != 4 && info.OnRoad && kart.Grounded && speed > Math.Max(12, tuning.driftMinStart + 2) &&
                now - driver.ReleaseAt > 0.35 && !controls.Drift && kart.SpinTime <= 0 && driver.ReverseTime <= 0 && ((stepCount + driver.Phase) & 1) == 0)
            {
                int direction = DriftStart(driver, speed);
                if (direction != 0)
                {
                    driver.Mode = DriftMode;
                    driver.DriftTime = driver.DriftTurn = 0;
                    driver.LastHeading = kart.Heading;
                    drift = true;
                    steer = direction * 0.25;
                    if (driver.Mistake == 3)
                    {
                        driver.EarlyAt = Lerp(0.35, 0.7, driver.Random.Next());
                    }
                }
            }
            if (driver.ReverseTime > 0)
            {
                driver.ReverseTime -= dt;
                throttle = 0;
                brake = 1;
                steer = driver.ReverseSteer;
                drift = false;
                if (driver.Mode == DriftMode)
                {
                    EndDrift(driver);
                }
            }
            if (Math.Abs(kart.Speed) < 1.5 && !kart.Pinned && kart.SpinTime <= 0 && (throttle > 0.5 || driver.ReverseTime > 0))
            {
                driver.StuckTime += dt;
                if (driver.StuckTime > 1 && driver.StuckTime - dt <= 1 && kart.WallContact && driver.ReverseTime <= 0)
                {
                    driver.ReverseTime = 0.7;
                    driver.ReverseSteer = error > 0 ? 1 : -1;
                }
                if (driver.StuckTime >= 3)
                {
                    kart.StartRescue();
                    driver.StuckTime = driver.ReverseTime = 0;
                    driver.Mode = DriveMode;
                }
            }
            else if (Math.Abs(kart.Speed) >= 1.5)
            {
                driver.StuckTime = 0;
            }
            if (driver.WatchdogDistance >= 0)
            {
                driver.WatchdogProgress += Clamp(Along(driver.WatchdogDistance, myDistance), -5, 5);
            }
            driver.WatchdogDistance = myDistance;
            driver.WatchdogTime += dt;
            if (driver.WatchdogTime >= 4)
            {
                if (driver.WatchdogProgress < 8 && !kart.Pinned && !kart.Rescuing)
                {
                    kart.StartRescue();
                    driver.Mode = DriveMode;
                    driver.ReverseTime = 0;
                }
                driver.WatchdogTime = driver.WatchdogProgress = 0;
            }
            double dot = Math.Sin(kart.Heading) * info.Tangent.x + Math.Cos(kart.Heading) * info.Tangent.z;
            if (dot < -0.2 && driver.ReverseTime <= 0)
            {
                steer = error > 0 ? -1 : 1;
                drift = false;
                if (driver.Mode == DriftMode)
                {
                    EndDrift(driver);
                }
            }
            if (kart.InkTime > 0)
            {
                driver.InkTime -= dt;
                if (driver.InkTime <= 0)
                {
                    driver.InkTime = 0.22;
                    driver.InkTarget = Range(driver.Random, -0.3, 0.3);
                }
                driver.InkNoise += (driver.InkTarget - driver.InkNoise) * Math.Min(1, dt * 8);
                steer = Clamp(steer + driver.InkNoise, -1, 1);
            }
            else
            {
                driver.InkNoise = 0;
            }
            controls.Steer = (float)steer;
            controls.Throttle = (float)throttle;
            controls.Brake = (float)brake;
            controls.Drift = drift;
        }

        private double LineTurnFrom(int index, int direction, double minimum)
        {
            double turn = 0;
            int gap = 0, maxGap = Round(6 / spacing);
            for (int n = 0; n < 400; n++)
            {
                double curvature = -direction * plan.lineK[Index(index + n)];
                if (curvature >= minimum)
                {
                    turn += curvature * spacing;
                    gap = 0;
                }
                else if (++gap > maxGap)
                {
                    break;
                }
                else if (curvature > 0)
                {
                    turn += curvature * spacing;
                }
            }
            return turn;
        }

        private int DriftStart(Driver driver, double speed)
        {
            int landing = driver.Kart.TrackInfo.Index;
            int direction = plan.lineK[Index(landing)] > 0 ? -1 : 1;
            double needed = driver.YawMin * EntryYaw / speed;
            int window = Math.Max(3, Round(speed * 0.4 / spacing));
            for (int n = 0; n <= window; n += 2)
            {
                if (-direction * plan.lineK[Index(landing + n)] < needed)
                {
                    return 0;
                }
            }
            return LineTurnFrom(Index(landing), direction, driver.YawMin * ReleaseYaw / speed) < driver.DriftMin ? 0 : direction;
        }

        private bool DriftStep(Driver driver, double dt)
        {
            var kart = driver.Kart;
            var info = kart.TrackInfo;
            int i0 = info.Index, direction = kart.DriftDirection;
            double speed = Math.Max(8, kart.Speed);
            driver.DriftTime += dt;
            driver.DriftTurn += Math.Abs(AngleDiff(kart.Heading, driver.LastHeading));
            driver.LastHeading = kart.Heading;
            if (driver.DriftTime > 9 || driver.Mistake == 3 && driver.DriftTime > 0.8 && kart.DriftTier >= 1 &&
                driver.DriftTime > driver.EarlyAt * 3 || driver.ThreatKind == "scissors" && now < driver.ThreatUntil &&
                driver.ThreatUntil - now < 0.45 && kart.DriftTier >= 1 || Math.Abs(info.Lateral) - samples.halfWidth[i0] > 0.2)
            {
                return true;
            }
            double velocityHeading = kart.Speed > 1 ? Math.Atan2(kart.Velocity.x, kart.Velocity.z) : kart.Heading;
            int j = Index(i0 + Round((4 + speed * 0.3) / spacing));
            double lat = plan.lineLat[j] * driver.LineQ * 0.9 + driver.Offset * plan.fade[j] + 0.5 * driver.Dynamic;
            if (driver.Mistake == 1)
            {
                lat -= direction * 3;
            }
            lat = Clamp(lat, -samples.halfWidth[j] + 1.2, samples.halfWidth[j] - 1.2);
            double tx = samples.px[j] + samples.hx[j] * lat, tz = samples.pz[j] + samples.hz[j] * lat;
            double errorIn = -direction * AngleDiff(Math.Atan2(tx - kart.Position.x, tz - kart.Position.z), velocityHeading);
            double steer = Clamp(SteerForYaw(-direction * plan.lineK[j] * speed + 2.6 * errorIn), driver.SteerMin, 1);
            double inside = direction * info.Lateral;
            double inward = Math.Sin(-direction * AngleDiff(velocityHeading, samples.heading[i0])) * speed;
            if (inside > samples.halfWidth[i0] - 1.2 && inward > 0.5)
            {
                steer = driver.SteerMin;
            }
            else if (-inside > samples.halfWidth[i0] - 1.2 && inward < -0.5)
            {
                steer = 1;
            }
            double maximum = -1;
            int window = Round(speed * 0.35 / spacing);
            for (int n = 0; n <= window; n += 2)
            {
                maximum = Math.Max(maximum, -direction * plan.lineK[Index(i0 + n)]);
            }
            if (maximum * speed < driver.YawMin * ReleaseYaw && errorIn < 0.03)
            {
                if (kart.DriftTier >= 3)
                {
                    return true;
                }
                double needed = (tuning.driftTiers[kart.DriftTier] - kart.DriftCharge) / DriftRate(driver.SteerMin);
                if (needed > driver.TierHold)
                {
                    return true;
                }
                double psi = -direction * AngleDiff(velocityHeading, samples.heading[i0]) + driver.YawMin * needed;
                double drift = speed * needed * (-direction * AngleDiff(velocityHeading, samples.heading[i0]) + 0.5 * driver.YawMin * needed);
                if (direction * info.Lateral + drift + speed * psi * Math.Max(0, psi) / (2 * yawHigh * driver.Handling) > samples.halfWidth[i0] - 1)
                {
                    return true;
                }
                driver.DriftSteer = driver.SteerMin;
                return false;
            }
            driver.DriftSteer = steer;
            return false;
        }

        private void UseItems(Driver driver, ref KagitKartControls controls, double dt)
        {
            var kart = driver.Kart;
            controls.Item = false;
            if (kart.Item == null || kart.Roulette > 0 || kart.SpinTime > 0)
            {
                driver.ItemPlan = null;
                driver.HeldFor = 0;
                return;
            }
            if (driver.ItemPlan != kart.Item)
            {
                driver.ItemPlan = kart.Item;
                driver.ItemTime = 0;
                driver.HoldFor = Range(driver.Random, driver.Style.hold[0], driver.Style.hold[1]);
                driver.NextTry = driver.HeldFor = 0;
                driver.BackPreferred = driver.Random.Next() < 0.35 * driver.Style.gumBack;
            }
            driver.ItemTime += dt;
            bool threat = now < driver.ThreatUntil || HomingAt(kart);
            if (kart.Item == "gum")
            {
                if (threat && driver.ItemTime > driver.Reaction)
                {
                    if (driver.HeldFor > 0 || !kart.PreviousControls.Item)
                    {
                        controls.Item = true;
                        driver.HeldFor += dt;
                    }
                    return;
                }
                if (driver.HeldFor > 0)
                {
                    driver.HeldFor = 0;
                    return;
                }
                var behind = ClosestBehind(kart, 18, out double distance, out double lateral);
                if (driver.ItemTime > driver.HoldFor && behind != null && distance < 14 * driver.Style.gumBack && Math.Abs(lateral) < 3 &&
                    (behind != player || now - driver.LastAtPlayer > 6))
                {
                    if (behind == player)
                    {
                        driver.LastAtPlayer = now;
                    }
                    Press(driver, ref controls);
                }
                else if (driver.ItemTime > driver.HoldFor + 9)
                {
                    Press(driver, ref controls);
                }
                return;
            }
            if (driver.ItemTime < driver.HoldFor && driver.Mode != ShortcutMode && !threat || now < driver.NextTry)
            {
                return;
            }
            bool straight = StraightAhead(kart, 55);
            switch (kart.Item)
            {
                case "rocket":
                case "rocket3":
                    if (driver.Mode == ShortcutMode)
                    {
                        var shortcut = plan.shortcuts[driver.Shortcut];
                        if (!driver.ShortcutFired && kart.BoostTime <= 0.15 &&
                            (Along(kart.TrackInfo.Distance, samples.dist[shortcut.i]) < 3 || !kart.TrackInfo.OnRoad))
                        {
                            Press(driver, ref controls);
                            driver.ShortcutFired = controls.Item;
                        }
                        return;
                    }
                    if (driver.ThreatKind == "scissors" && now < driver.ThreatUntil && driver.ThreatUntil - now < 0.9 && kart.BoostTime <= 0)
                    {
                        Press(driver, ref controls);
                        return;
                    }
                    if (driver.Mode == DriftMode || kart.BoostTime > 0 ||
                        (kart.Item == "rocket" || kart.ItemCount <= 1) && ShortcutAhead(kart, 400))
                    {
                        return;
                    }
                    bool catchUp = kart.Place >= 5 || GapAhead(kart) > 35;
                    bool saving = driver.Style.save > 0.6 && !catchUp && driver.ItemTime < driver.HoldFor + 6;
                    if (straight && !saving || catchUp && straight || driver.ItemTime > driver.HoldFor + 8)
                    {
                        Press(driver, ref controls);
                    }
                    return;
                case "foil":
                    if (threat || straight && kart.Place >= 4 || straight && driver.Style.save < 0.5 || driver.ItemTime > driver.HoldFor + 10)
                    {
                        Press(driver, ref controls);
                    }
                    return;
                case "plane":
                    var target = AimTarget(kart, 55, 0.12);
                    if (target != null && (target != player || now - driver.LastAtPlayer > 6) && driver.Random.Next() < 0.25 * driver.Style.aim)
                    {
                        if (target == player)
                        {
                            driver.LastAtPlayer = now;
                        }
                        Press(driver, ref controls);
                        return;
                    }
                    var behind = ClosestBehind(kart, 16, out _, out double lateral);
                    if (driver.BackPreferred && behind != null && Math.Abs(lateral) < 2 && (behind != player || now - driver.LastAtPlayer > 6))
                    {
                        if (behind == player)
                        {
                            driver.LastAtPlayer = now;
                        }
                        Press(driver, ref controls, true);
                        return;
                    }
                    if (driver.ItemTime > driver.HoldFor + 7 && kart.Place > 1)
                    {
                        Press(driver, ref controls);
                    }
                    return;
                case "homing":
                    if (kart.Place <= 1)
                    {
                        if (driver.ItemTime > driver.HoldFor + 12)
                        {
                            Press(driver, ref controls);
                        }
                        return;
                    }
                    var ahead = KartAhead(kart);
                    if (ahead == player && now - driver.LastAtPlayer <= 6)
                    {
                        return;
                    }
                    if (driver.Random.Next() < 0.5 * driver.Style.aim * (0.5 + driver.Sn) * dt * 4 || driver.ItemTime > driver.HoldFor + 3)
                    {
                        if (ahead == player)
                        {
                            driver.LastAtPlayer = now;
                        }
                        Press(driver, ref controls);
                    }
                    return;
                case "ink":
                    if (kart.Place > 2 || kart.Place > 1 && driver.ItemTime > driver.HoldFor + 3)
                    {
                        if (player != null && player.Place < kart.Place)
                        {
                            if (now - driver.LastAtPlayer <= 6)
                            {
                                return;
                            }
                            driver.LastAtPlayer = now;
                        }
                        Press(driver, ref controls);
                    }
                    return;
                case "scissors":
                    if (kart.Place <= 1)
                    {
                        return;
                    }
                    var leader = Leader();
                    if (leader == player && now - driver.LastAtPlayer <= 6)
                    {
                        return;
                    }
                    if (leader == player)
                    {
                        driver.LastAtPlayer = now;
                    }
                    Press(driver, ref controls);
                    return;
            }
        }

        private void Press(Driver driver, ref KagitKartControls controls, bool backward = false)
        {
            if (driver.Kart.PreviousControls.Item)
            {
                return;
            }
            controls.Item = true;
            if (backward)
            {
                controls.Brake = 1;
            }
            else if (controls.Brake > 0.5f)
            {
                controls.Brake = 0;
            }
            driver.NextTry = now + 1;
        }

        private bool HomingAt(KagitKartVehicle kart)
        {
            for (int i = 0; i < items.Projectiles.Count; i++)
            {
                var projectile = items.Projectiles[i];
                if (projectile.Target == kart && projectile.Owner != kart && (projectile.Kind == "homing" || projectile.Kind == "plane"))
                {
                    return true;
                }
            }
            return false;
        }

        private KagitKartVehicle ClosestBehind(KagitKartVehicle kart, double range, out double distance, out double lateral)
        {
            KagitKartVehicle best = null;
            distance = range;
            lateral = 0;
            for (int i = 0; i < karts.Count; i++)
            {
                var other = karts[i];
                if (other == kart || other.Rescuing || other.Finished)
                {
                    continue;
                }
                double gap = kart.Progress - other.Progress;
                if (gap > 0 && gap < distance)
                {
                    distance = gap;
                    best = other;
                }
            }
            if (best != null)
            {
                lateral = best.TrackInfo.Lateral - kart.TrackInfo.Lateral;
            }
            return best;
        }

        private KagitKartVehicle KartAhead(KagitKartVehicle kart)
        {
            KagitKartVehicle best = null;
            double distance = double.PositiveInfinity;
            for (int i = 0; i < karts.Count; i++)
            {
                var other = karts[i];
                if (other == kart)
                {
                    continue;
                }
                double gap = other.Progress - kart.Progress;
                if (gap > 0 && gap < distance)
                {
                    distance = gap;
                    best = other;
                }
            }
            return best;
        }

        private double GapAhead(KagitKartVehicle kart)
        {
            var ahead = KartAhead(kart);
            return ahead != null ? ahead.Progress - kart.Progress : 0;
        }

        private KagitKartVehicle Leader()
        {
            for (int i = 0; i < karts.Count; i++)
            {
                if (karts[i].Place == 1)
                {
                    return karts[i];
                }
            }
            return null;
        }

        private KagitKartVehicle AimTarget(KagitKartVehicle kart, double range, double cone)
        {
            double fx = Math.Sin(kart.Heading), fz = Math.Cos(kart.Heading), distance = range * range, cosine = Math.Cos(cone);
            KagitKartVehicle best = null;
            for (int i = 0; i < karts.Count; i++)
            {
                var other = karts[i];
                if (other == kart || other.Rescuing || other.Immune)
                {
                    continue;
                }
                double dx = other.Position.x - kart.Position.x, dz = other.Position.z - kart.Position.z;
                double squared = dx * dx + dz * dz;
                if (squared > distance || squared < 4 || (dx * fx + dz * fz) / Math.Sqrt(squared) < cosine)
                {
                    continue;
                }
                distance = squared;
                best = other;
            }
            return best;
        }

        private bool StraightAhead(KagitKartVehicle kart, double metres)
        {
            int i0 = kart.TrackInfo.Index;
            return Math.Abs(TurnBetween(i0, Index(i0 + Round(metres / spacing)))) < 0.2 &&
                Math.Abs(samples.curvature[i0]) < 1.0 / 120 && Math.Abs(samples.curvature[Index(i0 + Round(10 / spacing))]) < 1.0 / 120 &&
                Math.Abs(samples.curvature[Index(i0 + Round(metres * 0.5 / spacing))]) < 1.0 / 120;
        }

        private bool ShortcutAhead(KagitKartVehicle kart, double metres)
        {
            for (int i = 0; i < plan.shortcuts.Length; i++)
            {
                if (!shortcuts[i].ok)
                {
                    continue;
                }
                double ahead = Along(kart.TrackInfo.Distance, samples.dist[plan.shortcuts[i].i]);
                if (ahead > 0 && ahead < metres)
                {
                    return true;
                }
            }
            return false;
        }

        private int Index(int value) => (value % sampleCount + sampleCount) % sampleCount;
        private double TurnBetween(int from, int to) => to >= from ? plan.cum[to] - plan.cum[from] : plan.cum[sampleCount] - plan.cum[from] + plan.cum[to];

        private double Along(double from, double to)
        {
            double distance = to - from;
            if (distance > length / 2)
            {
                distance -= length;
            }
            else if (distance <= -length / 2)
            {
                distance += length;
            }
            return distance;
        }

        private double DriftYaw(double steer) => steer >= 0 ? tuning.driftYawNeutral + (tuning.driftYawIn - tuning.driftYawNeutral) * steer :
            tuning.driftYawNeutral - (tuning.driftYawNeutral - tuning.driftYawOut) * -steer;
        private double DriftRate(double steer) => steer >= 0 ? 1 + (tuning.driftChargeIn - 1) * steer : 1 - (1 - tuning.driftChargeOut) * -steer;
        private double SteerForYaw(double yaw) => yaw >= tuning.driftYawNeutral ? (yaw - tuning.driftYawNeutral) / (tuning.driftYawIn - tuning.driftYawNeutral) :
            -(tuning.driftYawNeutral - yaw) / (tuning.driftYawNeutral - tuning.driftYawOut);
        private static double AngleDiff(double target, double current) => Math.Atan2(Math.Sin(target - current), Math.Cos(target - current));
        private static double Range(KagitKartRandom random, double low, double high) => low + (high - low) * random.Next();
        private static double Clamp(double value, double low, double high) => Math.Clamp(value, low, high);
        private static double Lerp(double from, double to, double weight) => from + (to - from) * weight;
        private static double Hypot(double x, double z) => Math.Sqrt(x * x + z * z);
        private static int Round(double value) => (int)Math.Floor(value + 0.5);
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
