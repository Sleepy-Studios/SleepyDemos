using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hotfix.HowToFish
{
    public enum HowToFishRouletteColor { Red, Black, Green }

    /// 三色实物下注台；整组结果保存成功后播放表现，不在动画中重复开奖。
    public sealed class HowToFishRoulette : MonoBehaviour
    {
        [Range(0, 4)] [SerializeField] private int island = 3;
        [SerializeField] private BoxCollider redZone;
        [SerializeField] private BoxCollider blackZone;
        [SerializeField] private BoxCollider greenZone;
        [SerializeField] private Transform wheel;
        [SerializeField] private Transform ball;
        [Min(.1f)] [SerializeField] private float spinSeconds = 3;
        private HowToFishWorld world;
        private bool settling;
        private float spinRemaining;
        private float duration;
        private Quaternion wheelStart;
        private float ballStartAngle;
        private float ballRadius;
        private float ballHeight;
        private string announcement;

        public int Island => island;
        public bool IsSpinning => settling || spinRemaining > 0;
        public int LastPocket { get; private set; }
        public HowToFishRouletteColor LastResult => ColorForPocket(LastPocket);

        /// <summary>绑定当前世界，使用现有输入与存档事务。</summary>
        /// <param name="owner">此台所在的世界。</param>
        public void Initialize(HowToFishWorld owner) => world = owner;

        /// <summary>模型37格的颜色约定：0绿、奇红、偶黑，沿局部+Y递增。</summary>
        /// <param name="pocket">0至36的格号。</param>
        public static HowToFishRouletteColor ColorForPocket(int pocket)
        {
            if (pocket < 0 || pocket >= 37) throw new ArgumentOutOfRangeException(nameof(pocket));
            return pocket == 0 ? HowToFishRouletteColor.Green : pocket % 2 == 1 ? HowToFishRouletteColor.Red : HowToFishRouletteColor.Black;
        }

        /// 收集开局瞬间仍在色区内的合格实体，提交一轮后再播放轮盘与球动画。
        public bool TrySpin()
        {
            if (world == null || !world.HasSession || world.IsPaused || IsSpinning) return false;
            if (world.Session.State.unlockedIsland < island) { world.Notify("此区域的轮盘尚未开放。"); return false; }
            if (wheel == null || ball == null || !(spinSeconds > 0) || float.IsInfinity(spinSeconds))
            { world.Notify("轮盘表现或时长尚未配置。"); return false; }
            if (!CollectBets(out var bets, out string error)) { world.Notify(error); return false; }
            settling = true;
            try
            {
                // ponytail: 原作由物理球终角开奖；当前用均匀37格近似，物理校准完成后替换此抽签。
                int pocket = UnityEngine.Random.Range(0, 37);
                if (!world.TrySettleRoulette(this, bets, ColorForPocket(pocket), out announcement)) return false;
                LastPocket = pocket;
                duration = spinSeconds;
                spinRemaining = duration;
                wheelStart = wheel.localRotation;
                var offset = wheel.InverseTransformPoint(ball.position);
                ballRadius = new Vector2(offset.x, offset.z).magnitude;
                ballHeight = offset.y;
                ballStartAngle = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
                world.Notify("轮盘转动中……本轮押物结果已保存。");
                return true;
            }
            finally { settling = false; }
        }

        /// 显示三色实际押物总价；待拿走的物品不会在下次开局时被继续下注。
        public string StakeText()
        {
            if (IsSpinning) return "轮盘转动中……";
            if (!CollectBets(out var bets, out string error)) return error;
            var totals = new long[3];
            try { foreach (var bet in bets) totals[(int)bet.Value] += bet.Key.SaleValue; }
            catch (InvalidOperationException) { return "押物价值已超出数值范围，请取回。"; }
            return $"红 ${totals[0]} ×2 · 黑 ${totals[1]} ×2 · 绿 ${totals[2]} ×35";
        }

        private bool CollectBets(out Dictionary<HowToFishWorldItem, HowToFishRouletteColor> bets, out string error)
        {
            bets = new Dictionary<HowToFishWorldItem, HowToFishRouletteColor>();
            error = null;
            var zones = new[] { redZone, blackZone, greenZone };
            for (int i = 0; i < zones.Length; i++)
            {
                var zone = zones[i];
                if (zone == null || !zone.enabled || !zone.gameObject.activeInHierarchy)
                { error = "轮盘下注区尚未配置。"; return false; }
                var scale = zone.transform.lossyScale;
                var half = Vector3.Scale(zone.size * .5f, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
                foreach (var shape in Physics.OverlapBox(zone.transform.TransformPoint(zone.center), half, zone.transform.rotation, ~0, QueryTriggerInteraction.Ignore))
                {
                    var item = shape.GetComponentInParent<HowToFishWorldItem>();
                    if (item == null || !item.CanBet) continue;
                    var color = (HowToFishRouletteColor)i;
                    if (bets.TryGetValue(item, out var existing) && existing != color)
                    { error = "同一物品跨越了多个色区，请重新放置。"; return false; }
                    bets[item] = color;
                }
            }
            if (bets.Count > 0) return true;
            error = "把拿过并已放手的可卖死鱼放到红、黑或绿色区。";
            return false;
        }

        private void Update()
        {
            if (spinRemaining <= 0 || Time.deltaTime <= 0) return;
            spinRemaining = Mathf.Max(0, spinRemaining - Time.deltaTime);
            float t = 1 - spinRemaining / duration;
            float eased = 1 - Mathf.Pow(1 - t, 3);
            wheel.localRotation = wheelStart * Quaternion.Euler(0, 1080 * eased, 0);
            float angle = Mathf.Lerp(ballStartAngle, -720 + LastPocket * (360f / 37), eased) * Mathf.Deg2Rad;
            ball.position = wheel.TransformPoint(new Vector3(Mathf.Sin(angle) * ballRadius, ballHeight, Mathf.Cos(angle) * ballRadius));
            if (spinRemaining == 0 && world != null) world.Notify(announcement);
        }
    }
}
