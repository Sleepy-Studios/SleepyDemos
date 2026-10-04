using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hotfix.HowToFish
{
    /// 河豚的直线滚动、半血膨胀与毒区；原作未公开的数值保留为可调参数。
    [RequireComponent(typeof(HowToFishWorldItem), typeof(SphereCollider))]
    public sealed class HowToFishPufferfish : MonoBehaviour, IHowToFishBoss
    {
        [SerializeField] private Transform ball;
        [SerializeField] private HowToFishDamagePool poisonPrefab;
        [SerializeField] private float escapeSeconds = 150;
        [SerializeField] private float chargeSpeed = 8;
        [SerializeField] private float damage = 22;
        private readonly List<HowToFishDamagePool> pools = new List<HowToFishDamagePool>();
        private HowToFishWorldItem item;
        private HowToFishBossTransition transition;
        private HowToFishPlayer player;
        private SphereCollider shape;
        private Vector3 baseCenter;
        private float baseRadius;
        private float remaining;
        private float groundedSeconds;
        private float stuckSeconds;
        private float contactAt;
        private float poisonAt;
        private RigidbodyConstraints originalConstraints;

        public HowToFishWorldItem Item => item;
        public bool IsFighting => player != null && item.IsAlive && !item.Body.isKinematic;
        public bool IsEnraged => item != null && item.Creature != null && item.Health <= item.Creature.Health * .5f;
        public float EscapeFraction => Mathf.Clamp01(remaining / escapeSeconds);
        public string Hint => transition.IsProtected ? "转阶段保护 · 身体与毒区仍有危险" :
            IsEnraged ? "远离紫色毒区，绕树躲避" : "滚动冲撞 · 利用树干掩护";

        private void Awake()
        {
            item = GetComponent<HowToFishWorldItem>();
            transition = GetComponent<HowToFishBossTransition>();
            shape = GetComponent<SphereCollider>();
            baseCenter = shape.center; baseRadius = shape.radius;
            originalConstraints = GetComponent<Rigidbody>().constraints;
            remaining = escapeSeconds;
            item.Defeated += OnDefeated;
        }

        /// <summary>将首领绑定到玩家；缺失表现或毒区资源时拒绝静默降级。</summary>
        /// <param name="target">当前玩家。</param>
        public void Initialize(HowToFishPlayer target)
        {
            if (ball == null || poisonPrefab == null) throw new InvalidOperationException("河豚缺少球体挂点或毒区 Prefab。");
            player = target;
            ApplySize(.45f);
        }

        private void FixedUpdate()
        {
            if (!IsFighting) return;
            remaining -= Time.fixedDeltaTime;
            if (remaining <= 0) { Destroy(gameObject); return; }
            ApplySize(Mathf.Lerp(.45f, 2.25f, 1 - item.Health / item.Creature.Health));
            item.Body.constraints = originalConstraints | RigidbodyConstraints.FreezeRotation;
            var body = item.Body;
            var offset = player.transform.position - shape.bounds.center;
            var direction = Vector3.ProjectOnPlane(offset, Vector3.up).normalized;
            bool grounded = false;
            foreach (var hit in Physics.RaycastAll(shape.bounds.center, Vector3.down, shape.bounds.extents.y + .15f, ~0, QueryTriggerInteraction.Ignore))
                if (hit.rigidbody != body && hit.normal.y > .6f) { grounded = true; break; }
            groundedSeconds = grounded ? groundedSeconds + Time.fixedDeltaTime : 0;
            var planar = Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up);
            stuckSeconds = grounded && planar.sqrMagnitude < .25f && offset.sqrMagnitude > 4 ? stuckSeconds + Time.fixedDeltaTime : 0;
            body.AddForce(direction * (grounded ? IsEnraged ? 6 : 7.5f : 3.5f), ForceMode.Acceleration);
            if (planar.magnitude > chargeSpeed) body.linearVelocity = planar.normalized * chargeSpeed + Vector3.up * body.linearVelocity.y;
            if (groundedSeconds >= .75f && (offset.y > 1 || stuckSeconds >= 1))
            {
                body.linearVelocity = planar + Vector3.up * 7;
                groundedSeconds = stuckSeconds = 0;
            }
            ball.Rotate(Vector3.Cross(Vector3.up, direction), planar.magnitude * Time.fixedDeltaTime / shape.radius * Mathf.Rad2Deg, Space.World);
            var playerCenter = player.transform.position + Vector3.up * .8f;
            if (Time.time >= contactAt && (shape.ClosestPoint(playerCenter) - playerCenter).sqrMagnitude < .09f)
            { contactAt = Time.time + .5f; player.Damage(damage); }
            pools.RemoveAll(pool => pool == null);
            if (IsEnraged && Time.time >= poisonAt && pools.Count < 12)
            {
                poisonAt = Time.time + .9f;
                if (Physics.Raycast(item.transform.position + Vector3.up * .2f, Vector3.down, out var ground, 2, ~0, QueryTriggerInteraction.Ignore))
                {
                    var pool = Instantiate(poisonPrefab, ground.point + Vector3.up * .03f, Quaternion.identity);
                    pool.Initialize(player); pools.Add(pool);
                }
            }
        }

        private void ApplySize(float scale)
        {
            ball.localScale = Vector3.one * scale;
            ball.localPosition = baseCenter * scale;
            shape.center = baseCenter * scale; shape.radius = baseRadius * scale;
        }

        private void OnDefeated(HowToFishWorldItem defeated)
        {
            item.Body.constraints = originalConstraints;
            ClearPools();
        }

        private void ClearPools()
        {
            foreach (var pool in pools) if (pool != null) Destroy(pool.gameObject);
            pools.Clear();
        }

        private void OnDestroy()
        {
            if (item != null) item.Defeated -= OnDefeated;
            ClearPools();
        }
    }
}
