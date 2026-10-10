using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hotfix.KagitKart
{
    /// 原作赛车互撞与尾流，所有车完成运动后执行一次。
    public sealed class KagitKartCollisions
    {
        private readonly IReadOnlyList<KagitKartVehicle> vehicles;
        private readonly KagitKartKartTuning tuning;
        private readonly float[] impactCooldown;
        private readonly float[] draftCooldown;
        private readonly float[] draftCharge;
        public event Action<KagitKartVehicle, KagitKartVehicle> Bumped;

        /// <summary>创建当前比赛独占的碰撞和尾流状态。</summary>
        /// <param name="vehicles">数量固定的比赛赛车，最多八辆。</param>
        /// <param name="tuning">原作赛车参数。</param>
        public KagitKartCollisions(IReadOnlyList<KagitKartVehicle> vehicles, KagitKartKartTuning tuning)
        {
            this.vehicles = vehicles ?? throw new ArgumentNullException(nameof(vehicles));
            this.tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
            if (vehicles.Count < 1 || vehicles.Count > 8)
            {
                throw new ArgumentOutOfRangeException(nameof(vehicles));
            }
            impactCooldown = new float[vehicles.Count * vehicles.Count];
            draftCooldown = new float[vehicles.Count];
            draftCharge = new float[vehicles.Count];
        }

        /// <summary>按原作质量比例分离碰撞并采样尾流。</summary>
        /// <param name="seconds">固定模拟步长。</param>
        public void Advance(float seconds)
        {
            if (!(seconds > 0) || float.IsInfinity(seconds))
            {
                throw new ArgumentOutOfRangeException(nameof(seconds));
            }
            for (int i = 0; i < impactCooldown.Length; i++)
            {
                if (impactCooldown[i] > 0)
                {
                    impactCooldown[i] -= seconds;
                }
            }
            float diameter = tuning.radius * 2;
            for (int i = 0; i < vehicles.Count; i++)
            {
                var a = vehicles[i];
                if (a.Rescuing || a.Pinned)
                {
                    continue;
                }
                for (int j = i + 1; j < vehicles.Count; j++)
                {
                    var b = vehicles[j];
                    if (b.Rescuing || b.Pinned)
                    {
                        continue;
                    }
                    Vector3 difference = b.Position - a.Position;
                    if (Mathf.Abs(difference.y) > 1.6f)
                    {
                        continue;
                    }
                    difference.y = 0;
                    float squared = difference.sqrMagnitude;
                    if (squared >= diameter * diameter || squared < 0.00000001f)
                    {
                        continue;
                    }
                    float distance = Mathf.Sqrt(squared);
                    Vector3 normal = difference / distance;
                    float inverseA = 1 / a.Mass;
                    float inverseB = 1 / b.Mass;
                    float total = inverseA + inverseB;
                    float overlap = diameter - distance;
                    a.Position -= normal * (overlap * inverseA / total);
                    b.Position += normal * (overlap * inverseB / total);
                    int key = i * vehicles.Count + j;
                    if (impactCooldown[key] > 0)
                    {
                        continue;
                    }
                    impactCooldown[key] = tuning.bumpCooldown;
                    float keep = 1 - tuning.bumpSpeedLoss * 0.6f;
                    a.Velocity = a.Velocity * keep - normal * (tuning.bumpImpulse * inverseA / total);
                    b.Velocity = b.Velocity * keep + normal * (tuning.bumpImpulse * inverseB / total);
                    Bumped?.Invoke(a, b);
                    if (a.InvincibleTime > 0 && b.InvincibleTime <= 0)
                    {
                        b.SpinOut("foil", a);
                    }
                    else if (b.InvincibleTime > 0 && a.InvincibleTime <= 0)
                    {
                        a.SpinOut("foil", b);
                    }
                }
            }
            for (int i = 0; i < vehicles.Count; i++)
            {
                var vehicle = vehicles[i];
                if (draftCooldown[i] > 0)
                {
                    draftCooldown[i] -= seconds;
                }
                if (vehicle.Rescuing || vehicle.Pinned || !vehicle.Grounded)
                {
                    draftCharge[i] = 0;
                    continue;
                }
                bool following = false;
                if (vehicle.Speed >= tuning.draftMinSpeed * vehicle.TopSpeed && draftCooldown[i] <= 0)
                {
                    for (int j = 0; j < vehicles.Count; j++)
                    {
                        if (i == j)
                        {
                            continue;
                        }
                        var other = vehicles[j];
                        if (other.Rescuing || other.BoostTime > 0)
                        {
                            continue;
                        }
                        Vector3 difference = vehicle.Position - other.Position;
                        difference.y = 0;
                        float squared = difference.sqrMagnitude;
                        if (squared > tuning.draftRange * tuning.draftRange || squared < 1)
                        {
                            continue;
                        }
                        float alignment = -Vector3.Dot(difference, KagitKartVehicle.Forward(other.Heading)) / Mathf.Sqrt(squared);
                        if (alignment >= Mathf.Cos(tuning.draftCone))
                        {
                            following = true;
                            break;
                        }
                    }
                }
                if (following)
                {
                    draftCharge[i] += seconds / tuning.draftCharge;
                    if (draftCharge[i] >= 1)
                    {
                        draftCharge[i] = 0;
                        draftCooldown[i] = tuning.draftCooldown;
                        var boost = tuning.boosts["draft"];
                        vehicle.ApplyBoost(boost[0], boost[1], "draft");
                    }
                }
                else if (draftCharge[i] > 0)
                {
                    draftCharge[i] = Mathf.Max(0, draftCharge[i] - seconds / tuning.draftDecay);
                }
            }
        }

        /// <summary>读取尾流蓄力供 HUD 和纸屑特效显示。</summary>
        /// <param name="index">当前比赛赛车索引。</param>
        public float GetDraftCharge(int index)
        {
            if ((uint)index >= draftCharge.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            return draftCharge[index];
        }
    }
}
