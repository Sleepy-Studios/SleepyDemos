using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hotfix.HowToFish
{
    /// 信天翁在诱饵附近盘旋，随机检查可见玩家后追击，并按高度发射五颗齐射。
    [RequireComponent(typeof(HowToFishWorldItem))]
    public sealed class HowToFishAlbatross : MonoBehaviour, IHowToFishBoss
    {
        [SerializeField] private Transform leftWing;
        [SerializeField] private Transform rightWing;
        [SerializeField] private HowToFishProjectile droppingPrefab;
        [SerializeField] private float escapeSeconds = 150;
        [SerializeField] private float diveDamage = 24;
        [SerializeField] private float flyHeight = 24;
        [SerializeField] private float flySpeed = 12;
        [SerializeField] private float diveSpeed = 36;
        [SerializeField] private float acceleration = 10;
        [SerializeField] private float homeRadius = 24;
        [Tooltip("追击超时与波间范围为自制场景校准；来源只公开随机且随高度变化。")]
        [SerializeField] private float diveTimeout = 4;
        [SerializeField] private Vector2 volleyInterval = new Vector2(2, 4);
        private readonly List<HowToFishProjectile> droppings = new List<HowToFishProjectile>();
        private HowToFishWorldItem item;
        private HowToFishPlayer player;
        private Vector3 center;
        private float remaining;
        private float age;
        private float dropIn;
        private int shotsRemaining;
        private float diveCheckIn;
        private float diveRemaining;
        private bool diving;

        public HowToFishWorldItem Item => item;
        public bool IsFighting => player != null && item.IsAlive && !item.Body.isKinematic;
        public float EscapeFraction => Mathf.Clamp01(remaining / escapeSeconds);
        public string Hint => diving ? "信天翁俯冲 · 保持移动，利用地形遮挡" : "空中齐射 · 利用商店屋顶掩护";

        private void Awake()
        {
            item = GetComponent<HowToFishWorldItem>();
            remaining = escapeSeconds;
            item.Defeated += OnDefeated;
        }

        /// <summary>设定遭遇中心与玩家；不跟随玩家无限迁移岛屿。</summary>
        /// <param name="target">当前玩家。</param>
        public void Initialize(HowToFishPlayer target)
        {
            if (droppingPrefab == null || leftWing == null || rightWing == null)
                throw new InvalidOperationException("信天翁缺少翅膀或落物 Prefab 引用。");
            player = target;
            center = transform.position - Vector3.up * 18;
            diveCheckIn = UnityEngine.Random.Range(8f, 12f);
            item.Body.useGravity = false;
            item.Body.constraints = RigidbodyConstraints.FreezeRotation;
        }

        private void FixedUpdate()
        {
            if (!IsFighting) return;
            float delta = Time.fixedDeltaTime;
            remaining -= delta;
            if (remaining <= 0) { Destroy(gameObject); return; }
            age += delta;
            var target = player.transform.position + Vector3.up * .85f;
            float height = item.Body.position.y - center.y;
            diveCheckIn -= delta;
            if (!diving && diveCheckIn <= 0)
            {
                diveCheckIn = UnityEngine.Random.Range(8f, 12f);
                if (Mathf.Abs(height - flyHeight) < 3 && HasClearApproach(target))
                { diving = true; diveRemaining = diveTimeout; }
            }
            if (diving)
            {
                diveRemaining -= delta;
                // 来源允许近距离命中先于阻挡后的放弃追逐。
                if (Vector3.Distance(item.Body.position, target) < 2)
                { player.Damage(diveDamage); diving = false; }
                else if (diveRemaining <= 0 || !HasClearApproach(target)) diving = false;
            }
            var destination = diving ? target : center + new Vector3(Mathf.Sin(age * .35f) * homeRadius, flyHeight, Mathf.Cos(age * .35f) * homeRadius);
            var offset = destination - item.Body.position;
            var desired = Vector3.ClampMagnitude(offset * (diving ? 3 : 2), diving ? diveSpeed : flySpeed);
            item.Body.linearVelocity = Vector3.MoveTowards(item.Body.linearVelocity, desired, acceleration * delta);
            if (item.Body.linearVelocity.sqrMagnitude > .01f) item.Body.MoveRotation(Quaternion.LookRotation(item.Body.linearVelocity));
            float flap = Mathf.Sin(age * 7) * (diving ? 8 : 22);
            leftWing.localRotation = Quaternion.Euler(0, 0, flap);
            rightWing.localRotation = Quaternion.Euler(0, 0, -flap);
            dropIn -= delta;
            if (height >= flyHeight - 12 && dropIn <= 0)
            {
                if (shotsRemaining == 0) shotsRemaining = 5;
                shotsRemaining--;
                dropIn = shotsRemaining > 0 ? .15f : UnityEngine.Random.Range(volleyInterval.x, volleyInterval.y) *
                    Mathf.Lerp(1.5f, 1, Mathf.InverseLerp(flyHeight - 12, flyHeight, height));
                var origin = item.Body.position + Vector3.down * .7f;
                var aim = target + UnityEngine.Random.insideUnitSphere * (target - origin).magnitude * .02f;
                const float travel = 1.6f;
                var drop = Instantiate(droppingPrefab, origin, Quaternion.identity);
                drop.Initialize(player, (aim - origin - .5f * Physics.gravity * travel * travel) / travel);
                droppings.RemoveAll(value => value == null);
                droppings.Add(drop);
            }
        }

        private bool HasClearApproach(Vector3 target)
        {
            var offset = target - item.Body.position;
            foreach (var hit in Physics.RaycastAll(item.Body.position, offset.normalized, offset.magnitude, ~0, QueryTriggerInteraction.Ignore))
                if (hit.collider.GetComponentInParent<HowToFishWorldItem>() == null &&
                    hit.collider.GetComponentInParent<HowToFishPlayer>() == null) return false;
            return true;
        }

        private void OnDefeated(HowToFishWorldItem defeated)
        {
            item.Body.useGravity = true;
            item.Body.constraints = RigidbodyConstraints.None;
            ClearDroppings();
        }

        private void ClearDroppings()
        {
            foreach (var drop in droppings) if (drop != null) Destroy(drop.gameObject);
            droppings.Clear();
        }

        private void OnDestroy()
        {
            if (item != null) item.Defeated -= OnDefeated;
            ClearDroppings();
        }
    }
}
