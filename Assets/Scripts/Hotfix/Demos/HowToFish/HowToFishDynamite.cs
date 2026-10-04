using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hotfix.HowToFish
{
    /// 保存剩余引信的实体炸药；点燃后仍能持握，链爆只会提前引爆。
    [RequireComponent(typeof(HowToFishWorldItem))]
    public sealed class HowToFishDynamite : MonoBehaviour
    {
        [Tooltip("玩家爆炸伤害未有确证，200为可调校准值，允许自伤致死。")]
        [SerializeField] private float playerDamage = 200;
        [Tooltip("自制物理模型的爆炸冲力，需随质量和尺度校准。")]
        [SerializeField] private float explosionForce = 20;
        private HowToFishWorldItem item;
        private HowToFishPlayer player;
        private float remainingFuse;
        private bool detonated;

        /// 剩余游戏秒数；零表示未点燃。
        public float RemainingFuse => remainingFuse;
        /// 点燃的炸药不能收回库存。
        public bool IsArmed => remainingFuse > 0;

        private void Awake() => item = GetComponent<HowToFishWorldItem>();

        /// <summary>绑定本单人会话的玩家与击杀计分入口。</summary>
        /// <param name="target">世界中的当前玩家。</param>
        public void Initialize(HowToFishPlayer target) => player = target;

        /// 点燃三秒引信；重复操作不重置已有倒计时。
        public void Ignite()
        {
            if (!detonated && !item.IsConsumed && !IsArmed) remainingFuse = 3;
        }

        /// 受到邻近爆炸后至多再等待0.2秒，已经更短的引信保持不变。
        public void TriggerChainReaction()
        {
            if (!detonated && !item.IsConsumed) remainingFuse = IsArmed ? Mathf.Min(remainingFuse, .2f) : .2f;
        }

        internal void RestoreFuse(float seconds)
        {
            if (!(seconds >= 0 && seconds <= 3)) throw new ArgumentException("炸药引信时间无效。", nameof(seconds));
            remainingFuse = seconds;
        }

        private void Update()
        {
            if (!IsArmed || detonated || item.IsConsumed || Time.deltaTime <= 0) return;
            remainingFuse = Mathf.Max(0, remainingFuse - Time.deltaTime);
            if (remainingFuse > 0) return;
            detonated = true;
            item.TryConsume(Explode);
        }

        private void Explode()
        {
            var center = transform.position;
            var targets = new Dictionary<HowToFishWorldItem, float>();
            var bodies = new HashSet<Rigidbody>();
            bool hitsPlayer = false;
            // 鲸类也按自制碰撞体最近点判距；这是大体积命中的校准近似，不冒称原作特例算法。
            foreach (var shape in Physics.OverlapSphere(center, 5, ~0, QueryTriggerInteraction.Ignore))
            {
                float distanceSquared = (shape.ClosestPoint(center) - center).sqrMagnitude;
                if (player != null && shape.GetComponentInParent<HowToFishPlayer>() == player && distanceSquared <= 4.5f * 4.5f)
                    hitsPlayer = true;
                var target = shape.GetComponentInParent<HowToFishWorldItem>();
                if (target != null && target != item && !target.IsConsumed &&
                    (!targets.TryGetValue(target, out float previous) || distanceSquared < previous))
                    targets[target] = distanceSquared;
                if (shape.attachedRigidbody != null && shape.attachedRigidbody != item.Body) bodies.Add(shape.attachedRigidbody);
            }
            foreach (var entry in targets)
            {
                var target = entry.Key;
                if (target == null || target.IsConsumed || entry.Value > 4.5f * 4.5f) continue;
                target.GetComponent<HowToFishDynamite>()?.TriggerChainReaction();
                if (!target.IsAlive) continue;
                float damage = 200 + Mathf.Floor(target.Creature.Health * .05f);
                if (player != null) player.HitByExplosion(target, damage);
                else target.Hit(damage, Vector3.zero);
            }
            foreach (var body in bodies)
                if (body != null && !body.isKinematic) body.AddExplosionForce(explosionForce, center, 5, 0, ForceMode.Impulse);
            // 范围已缓存；最后结算自伤，避免同步复活改变本次爆炸的目标或爆心。
            if (hitsPlayer && player != null) player.Damage(playerDamage);
            // 水下生成表尚未查证，本入口不猜测生物或珍稀变体生成概率。
        }
    }
}
