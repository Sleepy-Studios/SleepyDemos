using System;
using System.Text;

namespace Hotfix.HowToFish
{
    public enum HowToFishKillMethod { Melee, Ranged, Explosion }

    /// 致死一击的现场事实；距离和头部位置由实际物理命中提供。
    public struct HowToFishKillHit
    {
        public HowToFishKillMethod Method;
        public float Health, MaximumHealth, Damage, CameraDistance, PlayerDistance;
        public bool Endangered, Boss, FirstPlayerHit, Headshot, PlayerAirborne, TargetAirborne, LastBullet;
    }

    /// 单人击杀奖励与短时连击。使用游戏时间，因此暂停不推进窗口。
    public sealed class HowToFishKillScore
    {
        private float lastTurn = float.NegativeInfinity;
        private float accumulatedYaw;
        private float spinUntil = float.NegativeInfinity;
        private bool aiming;
        private float aimChangedAt = float.NegativeInfinity;
        private float lastAttack = float.NegativeInfinity;
        private float attackDuration;
        private float lastKill = float.NegativeInfinity;
        private int priorKills;

        /// <summary>记录实际相机水平转动；停转超过半秒会中断累计。</summary>
        /// <param name="yaw">本帧输入产生的水平角度，不包含传送。</param>
        /// <param name="now">当前游戏时间。</param>
        public void RecordLook(float yaw, float now)
        {
            if (Math.Abs(yaw) < .001f) return;
            if (now - lastTurn > .5f) accumulatedYaw = 0;
            lastTurn = now;
            accumulatedYaw += yaw;
            if (Math.Abs(accumulatedYaw) < 320) return;
            spinUntil = now + 1;
            accumulatedYaw = 0;
        }

        /// <summary>记录瞄准状态的真正变化，重复读取不会延长快瞄窗口。</summary>
        /// <param name="isAiming">是否正在瞄准。</param>
        /// <param name="now">当前游戏时间。</param>
        public void RecordAim(bool isAiming, float now)
        {
            if (aiming == isAiming) return;
            aiming = isAiming; aimChangedAt = now;
        }

        /// <summary>每次实际攻击累加配置间隔；空弹匣和冷却中的输入不算攻击。</summary>
        /// <param name="interval">当前武器的攻击间隔。</param>
        /// <param name="now">当前游戏时间。</param>
        public void RecordAttack(float interval, float now)
        {
            if (!(interval > 0) || float.IsInfinity(interval)) throw new ArgumentOutOfRangeException(nameof(interval));
            if (now - lastAttack > 30) attackDuration = 0;
            lastAttack = now; attackDuration += interval;
        }

        /// <summary>只在确认致死一击时结算；倍率相乘，不以成就的五倍门槛作为上限。</summary>
        /// <param name="hit">此次致死一击的有效现场数据。</param>
        /// <param name="now">当前游戏时间。</param>
        /// <returns>最终倍率与用于提示的奖励名称。</returns>
        public (float Multiplier, string Bonuses) Score(HowToFishKillHit hit, float now)
        {
            if (!(hit.Health > 0 && hit.MaximumHealth >= hit.Health && hit.Damage >= hit.Health) ||
                float.IsInfinity(hit.MaximumHealth) || float.IsInfinity(hit.Damage) ||
                !(hit.CameraDistance >= 0 && hit.PlayerDistance >= 0) ||
                float.IsInfinity(hit.CameraDistance) || float.IsInfinity(hit.PlayerDistance) ||
                (uint)hit.Method > (uint)HowToFishKillMethod.Explosion)
                throw new ArgumentException("击杀奖励只能接收有效的致死命中。", nameof(hit));
            priorKills = now - lastKill <= 3 ? Math.Min(priorKills + 1, 10) : 0;
            lastKill = now;
            bool finallyBonus = attackDuration > 5;
            attackDuration = 0;
            if (hit.Method == HowToFishKillMethod.Explosion) return (1.25f, "爆炸");
            float multiplier = 1;
            int count = 0;
            var labels = new StringBuilder();
            void Add(bool condition, float value, string label)
            {
                if (!condition) return;
                multiplier *= value; count++;
                if (labels.Length > 0) labels.Append(" · ");
                labels.Append(label);
            }
            Add(hit.Endangered, 1.25f, "濒危");
            Add(priorKills > 0, 1 + priorKills * .05f, "连杀");
            Add(now <= spinUntil, 1.5f, "旋转");
            Add(finallyBonus, 1.1f, "终于击杀");
            Add(hit.FirstPlayerHit && hit.Health <= hit.MaximumHealth * .3f, 1.3f, "抢杀");
            if (hit.Method == HowToFishKillMethod.Melee) Add(true, 1.05f, "近战");
            else
            {
                Add(!aiming && now - aimChangedAt >= .25f, 1.2f, "盲射");
                Add(aiming && now - aimChangedAt <= .5f, 1.1f, "快瞄");
                Add(hit.CameraDistance >= 25, 1.3f, "远射");
                Add(hit.Headshot, 1.25f, "爆头");
                if (hit.PlayerAirborne && hit.TargetAirborne) Add(true, 1.5f, "空战");
                else if (hit.PlayerAirborne) Add(true, 1.25f, "空中射击");
                else Add(hit.TargetAirborne, 1.25f, "飞鱼射击");
                Add(hit.Health >= hit.MaximumHealth && hit.Damage >= hit.MaximumHealth, 1.25f, "一击毙命");
                Add(hit.PlayerDistance < 2, 1.1f, "贴身");
                Add(!hit.Boss && hit.Damage - hit.Health > 70, 1.25f, "过量伤害");
                Add(hit.LastBullet, 1.25f, "最后一发");
                Add(count == 0, 1.01f, "新手");
            }
            Add(count >= 5, 2, "惊艳");
            return (multiplier, labels.ToString());
        }
    }
}
