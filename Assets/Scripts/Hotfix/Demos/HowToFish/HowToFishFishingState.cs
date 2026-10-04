using System;

namespace Hotfix.HowToFish
{
    /// 抛竿与收线阶段；物理位置和鱼线显示由场景中的钓竿组件负责。
    public enum HowToFishFishingPhase { Idle, Charging, Flying, Waiting, Bite, Reeling, Landed, Escaped }

    /// 可独立测试的钓鱼规则。当前时长与张力参数为公开资料缺口下的推定值。
    public sealed class HowToFishFishingState
    {
        private float timer;
        private float waitSeconds;
        private float strength;
        private float fightClock;
        private bool pullBack;

        public HowToFishFishingPhase Phase { get; private set; }
        public float Charge { get; private set; }
        public float Tension { get; private set; }
        public float Progress { get; private set; }
        public float Distance { get; private set; }
        public bool IsActive => Phase != HowToFishFishingPhase.Idle && Phase != HowToFishFishingPhase.Landed && Phase != HowToFishFishingPhase.Escaped;

        /// 空闲时开始抛竿蓄力。
        public bool BeginCharge()
        {
            if (IsActive) return false;
            Reset();
            Phase = HowToFishFishingPhase.Charging;
            return true;
        }

        /// 释放抛竿并返回由蓄力决定的飞行距离。
        public float Cast()
        {
            if (Phase != HowToFishFishingPhase.Charging) throw new InvalidOperationException("只有蓄力状态可以抛竿。");
            Distance = 5 + Charge * 22;
            timer = 0;
            Phase = HowToFishFishingPhase.Flying;
            return Distance;
        }

        /// <summary>浮漂入水后开始等待鱼讯。</summary>
        /// <param name="delay">咬钩等待时长。</param>
        /// <param name="fishStrength">当前鱼获强度。</param>
        public void EnterWater(float delay, float fishStrength)
        {
            if (Phase != HowToFishFishingPhase.Flying) throw new InvalidOperationException("浮漂尚未处于飞行阶段。");
            if (!FinitePositive(delay) || !FinitePositive(fishStrength)) throw new ArgumentOutOfRangeException(nameof(delay));
            waitSeconds = delay;
            strength = fishStrength;
            timer = 0;
            Phase = HowToFishFishingPhase.Waiting;
        }

        /// <summary>咬钩窗口内开始收线。</summary>
        /// <param name="usePullBack">普通鱼竿使用连按拉竿；蟹竿使用持续收线张力。</param>
        public bool Hook(bool usePullBack = false)
        {
            if (Phase != HowToFishFishingPhase.Bite) return false;
            Phase = HowToFishFishingPhase.Reeling;
            timer = 0;
            pullBack = usePullBack;
            Tension = pullBack ? 0 : 0.25f;
            return true;
        }

        /// 普通鱼竿一次按下带来一次拉竿进度；持续按住不会重复计数。
        public void Pull()
        {
            if (Phase != HowToFishFishingPhase.Reeling || !pullBack) return;
            timer = 0;
            Progress = Math.Min(1, Progress + .12f / (.6f + strength * .4f));
        }

        /// <summary>推进计时和张力；过紧断线，松线过久会脱钩。</summary>
        /// <param name="deltaTime">非负且有限的游戏帧时长。</param>
        /// <param name="reeling">玩家是否按住收线。</param>
        public void Tick(float deltaTime, bool reeling)
        {
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0) throw new ArgumentOutOfRangeException(nameof(deltaTime));
            if (!IsActive) return;
            timer += deltaTime;
            switch (Phase)
            {
                case HowToFishFishingPhase.Charging:
                    Charge = Math.Min(1, timer / 1.1f);
                    break;
                case HowToFishFishingPhase.Flying:
                    if (timer > 8) Escape();
                    break;
                case HowToFishFishingPhase.Waiting:
                    if (timer >= waitSeconds) { timer = 0; Phase = HowToFishFishingPhase.Bite; }
                    break;
                case HowToFishFishingPhase.Bite:
                    if (timer > 2.2f) Escape();
                    break;
                case HowToFishFishingPhase.Reeling:
                    if (pullBack)
                    {
                        if (Progress >= 1) Phase = HowToFishFishingPhase.Landed;
                        else if (timer >= 5) Escape();
                        break;
                    }
                    fightClock += deltaTime;
                    float surge = 0.55f + 0.45f * (float)Math.Sin(fightClock * 2.3f);
                    Tension = Math.Max(0, Math.Min(1, Tension + deltaTime * (reeling ? 0.16f * strength + surge * 0.20f : -0.55f)));
                    if (reeling)
                    {
                        timer = 0;
                        Progress = Math.Min(1, Progress + deltaTime * 0.21f / (0.6f + strength * 0.4f));
                    }
                    if (Tension >= 1 || timer >= 5) { Escape(); break; }
                    if (Progress >= 1) Phase = HowToFishFishingPhase.Landed;
                    break;
            }
        }

        /// 放弃当前鱼获并收回钓竿。
        public void Reset()
        {
            Phase = HowToFishFishingPhase.Idle;
            timer = 0;
            fightClock = 0;
            pullBack = false;
            Charge = 0;
            Tension = 0;
            Progress = 0;
            Distance = 0;
        }

        private void Escape() => Phase = HowToFishFishingPhase.Escaped;
        private static bool FinitePositive(float value) => value > 0 && !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
