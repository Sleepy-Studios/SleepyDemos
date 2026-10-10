using System;
using UnityEngine;

namespace Hotfix.KagitKart
{
    /// 三种输入共用的原作赛车命令；数值由输入边界归一化。
    public struct KagitKartControls
    {
        public float Steer;
        public float Throttle;
        public float Brake;
        public bool Drift;
        public bool Item;
        public bool LookBack;
    }

    /// 原作规则事件，由表现、音频和比赛会话订阅。
    public enum KagitKartVehicleSignal
    {
        Boost,
        Hit,
        Inked,
        Hop,
        DriftStart,
        DriftTier,
        DriftRelease,
        DriftCancel,
        WallHit,
        Trick,
        Land,
        RescueLift,
        RescueCarry,
        RescueDrop,
        RescueLand
    }

    /// 迁移 kart.js 的运动真源；位置保留原作坐标，禁止直接赋给场景 Transform。
    public sealed class KagitKartVehicle
    {
        private readonly KagitKartConfiguration configuration;
        private readonly KagitKartKartTuning tuning;
        private readonly KagitKartTrackQuery track;
        private readonly KagitKartCharacterStats stats;
        private KagitKartQueryInfo info;
        private KagitKartControls previousControls;
        private float clock;
        private float yawHigh;
        private float verticalSpeed;
        private float airTime;
        private float steerLock;
        private double brakeHeld;
        private float outTime;
        private bool onPad;
        private bool wallContact;
        private float lastWallImpact = -9;
        private bool hopActive;
        private int hopDirection;
        private float trickArmedAt = -9;
        private float trickLeftGroundAt = -9;
        private int trickFromRamp = -1;
        private bool trickDone;
        private bool trickPending;
        private double safeDistance;
        private float safeLateral;
        private float safeTimer;
        private bool safeValid;
        private Vector3 rescueFrom;
        private Vector3 rescueTo;
        private float rescueFromHeading;
        private float rescueHeading;
        private double rescueTime;
        private float touchdownAt = -9;
        private float lastThrottleHeld = -9;
        private bool launchWindow;

        public string Id { get; }
        public bool IsPlayer { get; }
        public KagitKartCharacter Character { get; }
        public KagitKartControls Controls { get; set; }
        internal KagitKartControls PreviousControls => previousControls;
        internal bool HopActive => hopActive;
        internal bool WallContact => wallContact;
        public int Place { get; internal set; } = 1;
        public double Progress { get; internal set; }
        public bool Finished { get; internal set; }
        public bool Autopilot { get; internal set; }
        public string Item { get; internal set; }
        public int ItemCount { get; internal set; }
        public bool ItemHeld { get; internal set; }
        public double Roulette { get; internal set; }
        public double RouletteDuration { get; internal set; }
        public Vector3 Position { get; internal set; }
        public Vector3 PreviousPosition { get; private set; }
        public Vector3 Velocity { get; internal set; }
        public float Heading { get; private set; }
        public float Speed { get; private set; }
        public float BaseTop { get; private set; }
        public float TopSpeed { get; private set; }
        public float Mass { get; }
        public float RubberBand { get; set; } = 1;
        public bool Pinned { get; set; }
        public bool Grounded { get; private set; } = true;
        public float SteerInput { get; private set; }
        public float VisualYaw { get; private set; }
        public Vector3 Up { get; private set; } = Vector3.up;
        public Quaternion Rotation { get; private set; } = Quaternion.identity;
        public Quaternion PreviousRotation { get; private set; } = Quaternion.identity;
        public KagitKartQueryInfo TrackInfo => info;
        public string Surface { get; private set; } = "road";
        public bool Drifting { get; private set; }
        public int DriftDirection { get; private set; }
        public float DriftCharge { get; private set; }
        public int DriftTier { get; private set; }
        public float BoostTime { get; private set; }
        /// 最近一次加速的实际时长，供原作音效使用；不受重叠加速的 max 影响。
        public float LastBoostDuration { get; private set; }
        /// 最近一次墨水事件时长，供整层渐隐使用；重叠墨水仍按规则保留 max。
        public float LastInkDuration { get; private set; }
        public float AirTime => airTime;
        public float VerticalSpeed => verticalSpeed;
        public float OutTime => outTime;
        public int HopDirection => hopDirection;
        public Vector3 RescueFrom => rescueFrom;
        public Vector3 RescueTo => rescueTo;
        public float RescueHeading => rescueHeading;
        public float BoostStrength { get; private set; }
        public string BoostSource { get; private set; }
        public float SpinTime { get; private set; }
        public string SpinCause { get; private set; }
        public float GraceTime { get; private set; }
        public float InvincibleTime { get; private set; }
        public float InkTime { get; private set; }
        public int RescueStage { get; private set; }
        public bool Rescuing => RescueStage != 0;
        public bool Immune => InvincibleTime > 0 || GraceTime > 0 || Rescuing || SpinTime > 0;
        public float LastImpactSpeed { get; private set; }
        public float LastImpactKept { get; private set; }
        public float LastImpactAngle { get; private set; }
        public KagitKartVehicle LastHitBy { get; private set; }
        public event Action<KagitKartVehicle, KagitKartVehicleSignal> Signaled;

        /// <summary>使用同一原作配置与道路查询创建独立赛车状态。</summary>
        /// <param name="configuration">已加载的原作配置。</param>
        /// <param name="track">当前比赛道路查询。</param>
        /// <param name="character">原作角色属性。</param>
        /// <param name="speedClass">80、120 或 200。</param>
        /// <param name="isPlayer">玩家使用原作更高的加速上限。</param>
        public KagitKartVehicle(KagitKartConfiguration configuration, KagitKartTrackQuery track,
            KagitKartCharacter character, int speedClass, bool isPlayer)
        {
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.track = track ?? throw new ArgumentNullException(nameof(track));
            Character = character ?? throw new ArgumentNullException(nameof(character));
            stats = character.stats ?? throw new ArgumentException("赛车角色属性缺失。", nameof(character));
            tuning = configuration.kart;
            Id = character.id;
            IsPlayer = isPlayer;
            Mass = 1 + tuning.statWeight * (stats.weight - 3);
            SetClass(speedClass);
        }

        /// <summary>切换速度组别，保持原作角色速度权重。</summary>
        /// <param name="speedClass">80、120 或 200。</param>
        public void SetClass(int speedClass)
        {
            if (!configuration.classes.TryGetValue(speedClass.ToString(), out var value))
            {
                throw new ArgumentOutOfRangeException(nameof(speedClass));
            }
            BaseTop = value.top * (1 + tuning.statSpeed * (stats.speed - 3));
            TopSpeed = BaseTop;
            yawHigh = value.yawHigh > 0 ? value.yawHigh : tuning.yawHigh;
        }

        /// <summary>用于起跑网格和救援定位，清理运动及插值历史。</summary>
        /// <param name="position">原作规则坐标。</param>
        /// <param name="heading">原作弧度朝向。</param>
        public void PlaceAt(Vector3 position, float heading)
        {
            RescueStage = 0;
            rescueTime = 0;
            outTime = 0;
            SpinTime = 0;
            SpinCause = null;
            steerLock = 0;
            launchWindow = false;
            hopActive = false;
            trickFromRamp = -1;
            trickPending = false;
            wallContact = false;
            CancelDrift();
            Position = position;
            Heading = heading;
            Velocity = Vector3.zero;
            Speed = 0;
            verticalSpeed = 0;
            Grounded = true;
            airTime = 0;
            SteerInput = 0;
            VisualYaw = 0;
            track.Query(Position, -1, ref info);
            if (info.HasGround)
            {
                position.y = info.GroundY;
                Position = position;
            }
            Up = info.Up;
            PreviousPosition = Position;
            UpdateRotation(1, true);
            PreviousRotation = Rotation;
        }

        /// <summary>应用原作加速；重叠加速只延长时间和提高强度，不相加。</summary>
        /// <param name="duration">加速秒数。</param>
        /// <param name="strength">原作强度。</param>
        /// <param name="source">规则来源，用于特效和音效。</param>
        public void ApplyBoost(float duration, float strength, string source)
        {
            BoostStrength = BoostTime > 0 ? Mathf.Max(BoostStrength, strength) : strength;
            BoostTime = Mathf.Max(BoostTime, duration);
            BoostSource = source;
            LastBoostDuration = duration;
            Emit(KagitKartVehicleSignal.Boost);
        }

        /// <summary>按原作原因应用打转，免疫状态不会被重复打断。</summary>
        /// <param name="cause">原作 spins 中的原因标识。</param>
        /// <param name="by">攻击来源，可为空。</param>
        /// <returns>是否接受本次打转。</returns>
        public bool SpinOut(string cause, KagitKartVehicle by = null)
        {
            if (Immune)
            {
                return false;
            }
            if (!tuning.spins.TryGetValue(cause, out var spin))
            {
                spin = tuning.spins["default"];
            }
            SpinTime = spin.time;
            SpinCause = cause;
            LastHitBy = by;
            Velocity *= spin.speed;
            Speed *= spin.speed;
            BoostTime = 0;
            CancelDrift();
            Emit(KagitKartVehicleSignal.Hit);
            return true;
        }

        /// <summary>设置玻璃纸护盾的无敌时长。</summary>
        /// <param name="duration">秒数。</param>
        public void SetInvincible(float duration) => InvincibleTime = Mathf.Max(InvincibleTime, duration);

        /// <summary>设置打转或救援后的短暂保护。</summary>
        /// <param name="duration">秒数。</param>
        public void SetGrace(float duration) => GraceTime = Mathf.Max(GraceTime, duration);

        /// <summary>应用墨水遮挡时长，表现层根据该状态绘制。</summary>
        /// <param name="duration">秒数。</param>
        public void ApplyInk(float duration)
        {
            LastInkDuration = duration;
            InkTime = Mathf.Max(InkTime, duration);
            Emit(KagitKartVehicleSignal.Inked);
        }

        /// 取消漂移，不发放释放加速。
        public void CancelDrift()
        {
            bool active = Drifting;
            Drifting = false;
            DriftDirection = 0;
            DriftCharge = 0;
            DriftTier = 0;
            if (active)
            {
                Emit(KagitKartVehicleSignal.DriftCancel);
            }
        }

        /// 从最后稳定道路点开始纸鹤救援。
        public bool StartRescue()
        {
            if (Rescuing)
            {
                return false;
            }
            CancelDrift();
            BoostTime = 0;
            SpinTime = 0;
            hopActive = false;
            RescueStage = 1;
            rescueTime = 0;
            rescueFrom = Position;
            rescueFrom.y = Mathf.Max(rescueFrom.y, track.PageY - 2);
            rescueFromHeading = Heading;
            float t = safeValid ? (float)(safeDistance / track.Track.data.length) : info.T;
            float lateral = safeValid ? safeLateral : 0;
            float width = track.Track.data.samples.halfWidth[info.Index];
            lateral = Mathf.Clamp(lateral, -tuning.respawnLateral * width, tuning.respawnLateral * width);
            var point = track.PointAt(t, lateral);
            rescueTo = point.Position;
            rescueHeading = point.Heading;
            Velocity = Vector3.zero;
            Speed = 0;
            verticalSpeed = 0;
            outTime = 0;
            Emit(KagitKartVehicleSignal.RescueLift);
            return true;
        }

        /// <summary>推进一个规则步；与场景 Update、Unity 物理和全局时间无关。</summary>
        /// <param name="seconds">正的固定步长，正常比赛使用 1/120 秒。</param>
        public void Advance(float seconds)
        {
            if (!(seconds > 0) || float.IsInfinity(seconds))
            {
                throw new ArgumentOutOfRangeException(nameof(seconds));
            }
            PreviousPosition = Position;
            PreviousRotation = Rotation;
            clock += seconds;
            if (Controls.Throttle > 0.5f)
            {
                lastThrottleHeld = clock;
            }
            UpdateTimers(seconds);
            if (Rescuing)
            {
                AdvanceRescue(OriginalStep(seconds));
                UpdateRotation(seconds);
            }
            else if (Pinned)
            {
                Velocity = Vector3.zero;
                Speed = 0;
                UpdateRotation(seconds);
            }
            else
            {
                AdvanceMotion(seconds);
            }
            previousControls = Controls;
        }

        /// <summary>调试和规则测试定位，保留原作清理与保速语义。</summary>
        /// <param name="t">闭环比例。</param>
        /// <param name="lateral">横向米数。</param>
        /// <param name="keepSpeed">是否沿新车头保留非负速度。</param>
        public void Teleport(float t, float lateral = 0, bool keepSpeed = true)
        {
            float speed = keepSpeed ? Mathf.Max(0, Speed) : 0;
            var point = track.PointAt(t, lateral);
            PlaceAt(point.Position, point.Heading);
            Velocity = Forward(Heading) * speed;
            Speed = speed;
            safeValid = false;
            safeTimer = 0;
        }

        /// <summary>原作右手坐标转换到 Unity 场景坐标，仅表现层调用。</summary>
        /// <param name="source">规则位置、方向或网格顶点。</param>
        public static Vector3 ToUnity(Vector3 source) => new Vector3(-source.x, source.y, source.z);

        /// <summary>读取原作车头方向。</summary>
        /// <param name="heading">弧度。</param>
        public static Vector3 Forward(float heading) => new Vector3(Mathf.Sin(heading), 0, Mathf.Cos(heading));

        private static Vector3 Right(float heading) => new Vector3(-Mathf.Cos(heading), 0, Mathf.Sin(heading));

        private void AdvanceMotion(float dt)
        {
            if (launchWindow)
            {
                if (clock - touchdownAt > tuning.respawnLaunchWindow)
                {
                    launchWindow = false;
                }
                else if (lastThrottleHeld >= touchdownAt - tuning.respawnLaunchWindow)
                {
                    launchWindow = false;
                    Boost("respawn", "respawn");
                }
            }
            bool spinning = SpinTime > 0;
            bool boosting = BoostTime > 0;
            float target = spinning || steerLock > 0 ? 0 : Controls.Steer;
            bool growing = Mathf.Abs(target) > Mathf.Abs(SteerInput) && Sign(target) != -Sign(SteerInput);
            SteerInput = Mathf.MoveTowards(SteerInput, target, dt / (growing ? tuning.steerRampIn : tuning.steerRampOut));
            TopSpeed = BaseTop * RubberBand * (InvincibleTime > 0 ? 1 + tuning.invincibleTopGain : 1);
            float boostGain = IsPlayer ? tuning.playerBoostTopGain : tuning.boostTopGain;
            float cap = TopSpeed;
            if (boosting)
            {
                cap *= 1 + boostGain * BoostStrength;
            }
            else if (Surface == "offroad")
            {
                cap *= tuning.offroadCap + tuning.statOffroad * (stats.offroad - 3);
            }
            else if (Surface == "out")
            {
                cap *= tuning.outCap;
            }
            else if (Surface == "sand")
            {
                cap *= tuning.sandCap;
            }
            Vector3 forward = Forward(Heading);
            Vector3 right = Right(Heading);
            float f = Vector3.Dot(Velocity, forward);
            float lateral = Vector3.Dot(Velocity, right);
            float throttle = spinning ? 0 : Controls.Throttle;
            float brake = spinning ? 0 : Controls.Brake;
            brakeHeld = brake > 0.5f ? brakeHeld + OriginalStep(dt) : 0;
            if (Grounded)
            {
                float acceleration = Mathf.Log(5) / (tuning.accelTime * (1 - tuning.statAccel * (stats.accel - 3)));
                if (boosting)
                {
                    float boostAcceleration = TopSpeed * boostGain / tuning.boostRampTime;
                    f = Mathf.MoveTowards(f, cap, Mathf.Max(boostAcceleration, Mathf.Max((cap - f) * acceleration, 0)) * dt);
                }
                else if (brake > 0.1f && f > 0.5f)
                {
                    f = Mathf.Max(0, f - (brakeHeld >= tuning.brakeDelay ? tuning.brakeDecel : tuning.softBrakeDecel) * brake * dt);
                }
                else if (brake > 0.1f && throttle < 0.1f)
                {
                    f = Mathf.MoveTowards(f, -tuning.reverseMax, tuning.reverseAccel * brake * dt);
                }
                else if (throttle > 0.01f && f > -0.5f)
                {
                    if (f < cap * throttle)
                    {
                        f += (cap * throttle - f) * (1 - Mathf.Exp(-acceleration * dt));
                    }
                    else
                    {
                        f = Mathf.MoveTowards(f, cap * throttle, tuning.coastDecel * dt);
                    }
                }
                else
                {
                    f = Mathf.MoveTowards(f, 0, (throttle > 0.01f ? tuning.brakeDecel : tuning.coastDecel) * dt);
                }
                if (f > cap)
                {
                    f = Mathf.MoveTowards(f, cap, tuning.overCapDecel * dt);
                }
                if (wallContact)
                {
                    f = Mathf.Max(0, f - tuning.wallScrape * dt);
                }
            }
            bool driftPressed = Controls.Drift && !previousControls.Drift && !spinning;
            var ramp = info.RampId >= 0 ? track.Track.data.ramps[info.RampId] : null;
            if (driftPressed && Grounded && ramp != null && ramp.trick)
            {
                trickArmedAt = clock;
            }
            else if (driftPressed && Grounded && !Drifting && f > tuning.driftMinStart)
            {
                verticalSpeed = tuning.hopVy;
                Grounded = false;
                airTime = 0;
                hopActive = true;
                hopDirection = Mathf.Abs(Controls.Steer) > tuning.driftSteerDeadzone ? Sign(Controls.Steer) : 0;
                Emit(KagitKartVehicleSignal.Hop);
            }
            else if (driftPressed && !Grounded && trickFromRamp >= 0 && !trickDone && clock - trickLeftGroundAt <= tuning.trickAfter)
            {
                DoTrick();
            }
            if (hopActive && !Grounded && Mathf.Abs(Controls.Steer) > tuning.driftSteerDeadzone)
            {
                hopDirection = Sign(Controls.Steer);
            }
            UpdateDrift(f, spinning, dt);
            float handling = 1 + tuning.statHandling * (stats.handling - 3);
            if (Drifting)
            {
                float steer = SteerInput * DriftDirection;
                float yaw = steer >= 0
                    ? Mathf.LerpUnclamped(tuning.driftYawNeutral, tuning.driftYawIn, steer)
                    : Mathf.LerpUnclamped(tuning.driftYawNeutral, tuning.driftYawOut, -steer);
                Heading -= DriftDirection * yaw * dt;
            }
            else
            {
                float speed = Mathf.Abs(f);
                float fraction = Mathf.Clamp01((speed - tuning.yawLowSpeed) / Mathf.Max(1, TopSpeed - tuning.yawLowSpeed));
                float yaw = Mathf.LerpUnclamped(tuning.yawLow, yawHigh, fraction) * handling * Mathf.Clamp01(speed / tuning.yawFullSpeed);
                if (!Grounded)
                {
                    yaw *= tuning.airYaw;
                }
                if (Surface == "sand")
                {
                    yaw *= tuning.sandYaw;
                }
                Heading -= SteerInput * yaw * (f >= 0 ? 1 : -1) * dt;
            }
            Vector3 oldVelocity = forward * f + right * lateral;
            Vector3 nextForward = Forward(Heading);
            Vector3 nextRight = Right(Heading);
            float f2 = Vector3.Dot(oldVelocity, nextForward);
            float lat2 = Vector3.Dot(oldVelocity, nextRight);
            float grip = (Drifting ? tuning.driftGrip : tuning.grip) * (1 + tuning.statGrip * (stats.handling - 3));
            if (Surface == "ice")
            {
                grip *= tuning.iceGrip;
            }
            if (!Grounded)
            {
                grip *= 0.15f;
            }
            Velocity = nextForward * f2 + nextRight * (lat2 * Mathf.Exp(-grip * dt));
            Position += Velocity * dt;
            track.Query(Position, info.Index, ref info);
            ResolveWall(f2, dt);
            AdvanceVertical(ramp, dt);
            if (Rescuing)
            {
                UpdateRotation(dt);
                return;
            }
            Surface = Grounded ? info.Surface : Surface;
            if (Grounded && info.Surface == "boost")
            {
                var boost = tuning.boosts["pad"];
                if (!onPad)
                {
                    ApplyBoost(boost[0], boost[1], "pad");
                }
                else
                {
                    BoostTime = Mathf.Max(BoostTime, boost[0]);
                    BoostStrength = Mathf.Max(BoostStrength, boost[1]);
                }
                onPad = true;
            }
            else
            {
                onPad = false;
            }
            outTime = Grounded && Surface == "out" ? outTime + dt : 0;
            if (outTime >= tuning.outRespawn)
            {
                StartRescue();
            }
            if (Grounded && info.OnRoad && info.RampId < 0)
            {
                safeTimer += dt;
                if (safeTimer >= tuning.respawnSafeTime)
                {
                    safeDistance = info.Distance;
                    safeLateral = info.Lateral;
                    safeValid = true;
                }
            }
            else
            {
                safeTimer = 0;
            }
            Speed = Vector3.Dot(Velocity, Forward(Heading));
            UpdateRotation(dt);
        }

        private void UpdateDrift(float speed, bool spinning, float dt)
        {
            if (!Drifting)
            {
                return;
            }
            if (!Controls.Drift || spinning)
            {
                int tier = DriftTier;
                Drifting = false;
                Emit(KagitKartVehicleSignal.DriftRelease);
                if (tier > 0)
                {
                    Boost("tier" + tier, "drift");
                }
                DriftDirection = 0;
                DriftCharge = 0;
                DriftTier = 0;
            }
            else if (Mathf.Abs(speed) < tuning.driftMinHold)
            {
                CancelDrift();
            }
            else
            {
                float steer = SteerInput * DriftDirection;
                float charge = steer >= 0 ? Mathf.LerpUnclamped(1, tuning.driftChargeIn, steer)
                    : Mathf.LerpUnclamped(1, tuning.driftChargeOut, -steer);
                if (Surface != "offroad" && Surface != "out" && Grounded)
                {
                    DriftCharge += charge * dt;
                }
                int tier = DriftCharge >= tuning.driftTiers[2] ? 3 : DriftCharge >= tuning.driftTiers[1] ? 2
                    : DriftCharge >= tuning.driftTiers[0] ? 1 : 0;
                if (tier > DriftTier)
                {
                    DriftTier = tier;
                    Emit(KagitKartVehicleSignal.DriftTier);
                }
            }
        }

        private void ResolveWall(float forwardSpeed, float dt)
        {
            bool wasContact = wallContact;
            wallContact = false;
            float wallLine = info.HalfWidth + info.OffroadWidth - tuning.wallInset;
            if (info.EdgeCode != 1 || Mathf.Abs(info.Lateral) <= wallLine)
            {
                return;
            }
            Vector3 normal = info.Right;
            normal.y = 0;
            normal = normal.normalized * Sign(info.Lateral);
            Position -= normal * (Mathf.Abs(info.Lateral) - wallLine);
            float inward = Vector3.Dot(Velocity, normal);
            if (inward > 0)
            {
                Vector3 tangent = Velocity - normal * inward;
                bool impact = inward > tuning.wallImpactSpeed && (!wasContact || clock - lastWallImpact >= tuning.wallImpactCooldown);
                if (impact)
                {
                    LastImpactSpeed = Velocity.magnitude;
                    LastImpactAngle = Mathf.Atan2(inward, tangent.magnitude);
                    bool hard = LastImpactAngle > tuning.wallHardAngle;
                    Velocity = (tangent * tuning.wallTangent - normal * inward * tuning.wallNormal) * (hard ? tuning.wallHardSpeed : 1);
                    LastImpactKept = Velocity.magnitude / Mathf.Max(LastImpactSpeed, 0.001f);
                    if (hard)
                    {
                        steerLock = tuning.wallSteerLock;
                    }
                    lastWallImpact = clock;
                    CancelDrift();
                    Emit(KagitKartVehicleSignal.WallHit);
                }
                else
                {
                    Velocity = tangent;
                }
            }
            float into = Vector3.Dot(Forward(Heading), normal);
            if (into > 0 && into < Mathf.Sin(tuning.wallHardAngle) && SpinTime <= 0)
            {
                float turn = Mathf.Min(Mathf.Asin(into), tuning.wallAlign * dt * Mathf.Clamp01(Mathf.Abs(forwardSpeed) / 5));
                Heading += Sign(Vector3.Dot(Right(Heading), normal)) * turn;
            }
            wallContact = true;
            track.Query(Position, info.Index, ref info);
        }

        private void AdvanceVertical(KagitKartRamp ramp, float dt)
        {
            if (Grounded)
            {
                if (!info.HasGround)
                {
                    Grounded = false;
                }
                else
                {
                    float ballistic = Position.y + verticalSpeed * dt - 0.5f * tuning.gravity * dt * dt;
                    if (info.OnLip && trickFromRamp == -2 || ballistic - info.GroundY > tuning.groundSnap)
                    {
                        TakeOff(ramp);
                    }
                    else
                    {
                        verticalSpeed = Mathf.Clamp((info.GroundY - Position.y) / dt, -tuning.maxClimbVy, tuning.maxClimbVy);
                        var position = Position;
                        position.y = info.GroundY;
                        Position = position;
                    }
                }
                if (Grounded)
                {
                    trickFromRamp = info.RampId >= 0 ? -2 : -1;
                }
            }
            if (Grounded)
            {
                return;
            }
            if (trickFromRamp == -2)
            {
                TakeOff(ramp);
            }
            verticalSpeed -= tuning.gravity * dt;
            var airbornePosition = Position;
            airbornePosition.y += verticalSpeed * dt;
            Position = airbornePosition;
            airTime += dt;
            if (info.HasGround && Position.y <= info.GroundY && verticalSpeed <= 0.5f)
            {
                Land();
            }
            else if (!float.IsNegativeInfinity(info.WaterY) && Position.y < info.WaterY - 0.3f)
            {
                StartRescue();
            }
            else if (Position.y < track.KillY)
            {
                StartRescue();
            }
        }

        private void TakeOff(KagitKartRamp ramp)
        {
            Grounded = false;
            airTime = 0;
            bool onRamp = trickFromRamp == -2;
            if (onRamp && ramp != null)
            {
                trickFromRamp = ramp.index;
                trickLeftGroundAt = clock;
                trickDone = !ramp.trick;
                if (ramp.trick && clock - trickArmedAt <= tuning.trickBefore)
                {
                    DoTrick();
                }
            }
            else
            {
                trickFromRamp = -1;
            }
        }

        private void DoTrick()
        {
            if (trickDone)
            {
                return;
            }
            trickDone = true;
            trickPending = true;
            Emit(KagitKartVehicleSignal.Trick);
        }

        private void Land()
        {
            var position = Position;
            position.y = info.GroundY;
            Position = position;
            verticalSpeed = 0;
            Grounded = true;
            Emit(KagitKartVehicleSignal.Land);
            airTime = 0;
            if (trickPending)
            {
                trickPending = false;
                Boost("trick", "trick");
            }
            trickFromRamp = -1;
            if (!hopActive)
            {
                return;
            }
            hopActive = false;
            if (hopDirection != 0 && Controls.Drift && Vector3.Dot(Velocity, Forward(Heading)) > tuning.driftMinHold && SpinTime <= 0)
            {
                Drifting = true;
                DriftDirection = hopDirection;
                DriftCharge = 0;
                DriftTier = 0;
                Emit(KagitKartVehicleSignal.DriftStart);
            }
        }

        private void AdvanceRescue(double dt)
        {
            rescueTime += dt;
            float height = tuning.respawnHeight;
            if (RescueStage == 1)
            {
                Position = rescueFrom + Vector3.up * (height * Smooth((float)(rescueTime / tuning.respawnLift)));
                if (rescueTime >= tuning.respawnLift)
                {
                    RescueStage = 2;
                    rescueTime = 0;
                    rescueFrom.y += height;
                    Emit(KagitKartVehicleSignal.RescueCarry);
                }
            }
            else if (RescueStage == 2)
            {
                float fraction = Smooth((float)(rescueTime / tuning.respawnCarry));
                var position = Vector3.LerpUnclamped(rescueFrom, rescueTo, fraction);
                position.y = Mathf.LerpUnclamped(rescueFrom.y, rescueTo.y + height, fraction) + Mathf.Sin(fraction * Mathf.PI) * 2.5f;
                Position = position;
                Heading = LerpAngle(rescueFromHeading, rescueHeading, fraction);
                if (rescueTime >= tuning.respawnCarry)
                {
                    RescueStage = 3;
                    rescueTime = 0;
                    Heading = rescueHeading;
                    Emit(KagitKartVehicleSignal.RescueDrop);
                }
            }
            else
            {
                float fraction = (float)(rescueTime / tuning.respawnDrop);
                Position = rescueTo + Vector3.up * (height * (1 - fraction * fraction));
                if (rescueTime >= tuning.respawnDrop)
                {
                    RescueStage = 0;
                    rescueTime = 0;
                    Position = rescueTo;
                    Heading = rescueHeading;
                    verticalSpeed = 0;
                    Grounded = true;
                    airTime = 0;
                    GraceTime = Mathf.Max(GraceTime, tuning.graceAfterRespawn);
                    touchdownAt = clock;
                    launchWindow = true;
                    safeTimer = 0;
                    track.Query(Position, -1, ref info);
                    Surface = info.Surface;
                    Emit(KagitKartVehicleSignal.RescueLand);
                }
            }
            Velocity = Vector3.zero;
            Speed = 0;
        }

        private void UpdateTimers(float dt)
        {
            if (BoostTime > 0)
            {
                BoostTime -= dt;
                if (BoostTime <= 0)
                {
                    BoostTime = 0;
                    BoostStrength = 0;
                    BoostSource = null;
                }
            }
            if (SpinTime > 0)
            {
                SpinTime -= dt;
                if (SpinTime <= 0)
                {
                    SpinTime = 0;
                    SpinCause = null;
                    GraceTime = Mathf.Max(GraceTime, tuning.graceAfterSpin);
                }
            }
            else if (GraceTime > 0)
            {
                GraceTime = Mathf.Max(0, GraceTime - dt);
            }
            InvincibleTime = Mathf.Max(0, InvincibleTime - dt);
            InkTime = Mathf.Max(0, InkTime - dt);
            steerLock = Mathf.Max(0, steerLock - dt);
        }

        private void UpdateRotation(float dt, bool snap = false)
        {
            Vector3 target = Grounded && !Rescuing ? info.Up : Vector3.up;
            float weight = 1 - Mathf.Pow(2, -dt / (Grounded ? tuning.upDampHalfLife : tuning.airUpHalfLife));
            Up = snap ? target : Vector3.LerpUnclamped(Up, target, weight).normalized;
            float yaw = Drifting ? -DriftDirection * (tuning.driftVisualYaw + tuning.driftVisualSteer * SteerInput * DriftDirection) : 0;
            VisualYaw = snap ? yaw : yaw + (VisualYaw - yaw) * Mathf.Pow(2, -dt / 0.08f);
            Rotation = Quaternion.FromToRotation(Vector3.up, ToUnity(Up)) *
                Quaternion.AngleAxis(-(Heading + VisualYaw) * Mathf.Rad2Deg, Vector3.up);
        }

        private void Boost(string key, string source)
        {
            var boost = tuning.boosts[key];
            ApplyBoost(boost[0], boost[1], source);
        }

        private void Emit(KagitKartVehicleSignal signal) => Signaled?.Invoke(this, signal);
        private static int Sign(float value) => value > 0 ? 1 : value < 0 ? -1 : 0;
        private static double OriginalStep(float seconds) =>
            seconds == (float)KagitKartSimulationClock.Step ? KagitKartSimulationClock.Step : seconds;
        private static float Smooth(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3 - 2 * value);
        }

        private static float LerpAngle(float a, float b, float fraction) => a + Mathf.Atan2(Mathf.Sin(b - a), Mathf.Cos(b - a)) * fraction;
    }
}
