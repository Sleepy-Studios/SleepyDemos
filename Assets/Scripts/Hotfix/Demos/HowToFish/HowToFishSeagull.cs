using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hotfix.HowToFish
{
    /// 环境海鸥围绕刷新处飞行，叼走附近无人持有的鱼尸或玩家遗体；死亡和卸载会松开携带物。
    [RequireComponent(typeof(HowToFishWorldItem))]
    public sealed class HowToFishSeagull : MonoBehaviour
    {
        [SerializeField] private Transform leftWing;
        [SerializeField] private Transform rightWing;
        [SerializeField] private Transform carryPoint;
        [SerializeField] private float speed = 5;
        [SerializeField] private float orbitRadius = 12;
        [SerializeField] private float flightHeight = 7;
        private HowToFishWorldItem item;
        private Collider shape;
        private IReadOnlyList<HowToFishWorldItem> items;
        private HowToFishWorldItem target;
        private HowToFishWorldItem cargo;
        private Vector3 home;
        private Vector3 escapePoint;
        private float phase;
        private float searchIn = 3;
        private float carryTime;

        /// 目前由此海鸥叼住的实体；玩家抓走或被消费后返回 null。
        public HowToFishWorldItem CarriedItem => cargo != null && !cargo.IsConsumed && cargo.IsHeldBy(carryPoint) ? cargo : null;

        private void Awake()
        {
            item = GetComponent<HowToFishWorldItem>();
            shape = GetComponent<Collider>();
            item.Defeated += OnDefeated;
        }

        /// <summary>使用世界拥有的实体列表寻找遗留鱼获；不创建额外的全局对象扫描。</summary>
        /// <param name="origin">刷新处下方的地面或水面位置。</param>
        /// <param name="worldItems">当前场景的实体集合，所有权仍由世界管理。</param>
        public void Initialize(Vector3 origin, IReadOnlyList<HowToFishWorldItem> worldItems)
        {
            if (worldItems == null || shape == null || leftWing == null || rightWing == null || carryPoint == null)
                throw new InvalidOperationException("海鸥缺少实体集合、碰撞体或模型挂点。");
            home = origin;
            items = worldItems;
            phase = UnityEngine.Random.Range(0, Mathf.PI * 2);
        }

        private void FixedUpdate()
        {
            if (items == null || !item.IsAlive) return;
            float delta = Time.fixedDeltaTime;
            float flap = Mathf.Sin(Time.time * 9 + phase) * 24;
            leftWing.localRotation = Quaternion.Euler(0, 0, flap);
            rightWing.localRotation = Quaternion.Euler(0, 0, -flap);
            if (item.IsHeld) { DropCargo(); return; }
            var body = item.Body;
            body.useGravity = false;
            if (cargo != null && CarriedItem == null) cargo = null;
            if (target != null && (target.IsConsumed || target.IsAlive || target.IsHeld)) target = null;
            phase += delta * .4f;
            Vector3 destination = home + new Vector3(Mathf.Sin(phase) * orbitRadius, flightHeight + Mathf.Sin(phase * 1.7f), Mathf.Cos(phase) * orbitRadius);
            if (CarriedItem != null)
            {
                carryTime += delta;
                destination = escapePoint;
                if (carryTime > 10) { DropCargo(); searchIn = 8; }
            }
            else if (target != null)
            {
                destination = target.Body.worldCenterOfMass + Vector3.up * .5f;
                if ((carryPoint.position - target.Body.worldCenterOfMass).sqrMagnitude < .65f * .65f && target.TryHold(carryPoint, shape, 0))
                {
                    cargo = target; target = null; carryTime = 0;
                    var away = Vector3.ProjectOnPlane(body.position - home, Vector3.up).normalized;
                    if (away.sqrMagnitude < .01f) away = transform.forward;
                    escapePoint = body.position + away * 45 + Vector3.up * 12;
                }
            }
            else if ((searchIn -= delta) <= 0)
            {
                searchIn = 2;
                float nearest = 18 * 18;
                foreach (var candidate in items)
                {
                    if (candidate == null || candidate == item || candidate.IsConsumed || candidate.IsAlive || candidate.IsHeld ||
                        candidate.transform.position.y < -.3f) continue;
                    var creature = candidate.Creature;
                    if (candidate.DefinitionId != "PlayerRemains" && (creature == null || creature.IgnoredBySeagulls ||
                        creature.IsGroundPickup && candidate.DefinitionId != "Seagull")) continue;
                    float distance = (candidate.Body.worldCenterOfMass - body.position).sqrMagnitude;
                    if (distance < nearest) { nearest = distance; target = candidate; }
                }
            }
            var velocity = Vector3.ClampMagnitude((destination - body.position) * 2, speed);
            if (velocity.sqrMagnitude > .01f && body.SweepTest(velocity.normalized, out var hit, velocity.magnitude * delta + .3f, QueryTriggerInteraction.Ignore))
            {
                var obstacle = hit.collider.GetComponentInParent<HowToFishWorldItem>();
                if (obstacle == null || obstacle != target && obstacle != cargo) velocity = Vector3.up * speed;
            }
            body.linearVelocity = velocity;
            var direction = Vector3.ProjectOnPlane(velocity, Vector3.up);
            if (direction.sqrMagnitude > .01f) body.MoveRotation(Quaternion.LookRotation(direction));
        }

        private void DropCargo()
        {
            if (cargo != null && !cargo.IsConsumed && cargo.IsHeldBy(carryPoint)) cargo.Release(cargo.Body.linearVelocity);
            cargo = null;
        }

        private void OnDefeated(HowToFishWorldItem defeated)
        {
            DropCargo(); target = null;
            item.Body.useGravity = !item.IsHeld;
            leftWing.localRotation = rightWing.localRotation = Quaternion.identity;
        }

        private void OnDestroy()
        {
            DropCargo();
            if (item != null) item.Defeated -= OnDefeated;
        }
    }
}
