using System;
using UnityEngine;

namespace Hotfix.HowToFish
{
    /// 普通攻击生物与可选鱼类共用的跃动；只有目录标记为首领时启用逃脱计时和首领栏。
    [RequireComponent(typeof(HowToFishWorldItem))]
    public sealed class HowToFishJumpingFish : MonoBehaviour, IHowToFishBoss
    {
        [SerializeField] private Transform tail;
        [SerializeField] private Transform propeller;
        [SerializeField] private Transform[] limbs = Array.Empty<Transform>();
        [SerializeField] private float escapeSeconds = 180;
        [SerializeField] private float damage = 25;
        [SerializeField] private float damageInterval = 1;
        [SerializeField] private float speed = 6;
        [SerializeField] private float jumpInterval = 2;
        [SerializeField] private float jumpForce = 6;
        [SerializeField] private bool steerInAir = true;
        private HowToFishWorldItem item;
        private HowToFishPlayer player;
        private Collider shape;
        private RigidbodyConstraints originalConstraints;
        private float remaining;
        private float jumpIn = 1;
        private float damageIn;
        private Vector3 direction;

        public HowToFishWorldItem Item => item;
        public bool IsFighting => item.Creature?.IsBoss == true && player != null && item.IsAlive && !item.Body.isKinematic;
        public float EscapeFraction => Mathf.Clamp01(remaining / escapeSeconds);
        public string Hint => damage > 0 ? "鱼身跃动 · 侧向移动，避开贴身接触" : "翻车鱼正在跳跃 · 保持瞄准";

        private void Awake()
        {
            item = GetComponent<HowToFishWorldItem>();
            shape = GetComponent<Collider>();
            originalConstraints = GetComponent<Rigidbody>().constraints;
            remaining = escapeSeconds;
            item.Defeated += OnDefeated;
        }

        /// <summary>绑定当前玩家；伤害与空中转向由当前 Prefab 配置决定。</summary>
        /// <param name="target">此次遭遇的玩家。</param>
        public void Initialize(HowToFishPlayer target)
        {
            if (target == null || shape == null) throw new InvalidOperationException("跃动生物缺少玩家或碰撞体。");
            player = target;
        }

        private void FixedUpdate()
        {
            if (player == null || !item.IsAlive || item.Body.isKinematic) return;
            float delta = Time.fixedDeltaTime;
            if (item.Creature.IsBoss)
            {
                remaining -= delta;
                if (remaining <= 0) { Destroy(gameObject); return; }
            }
            var body = item.Body;
            if (tail != null) tail.localRotation = Quaternion.Euler(0, Mathf.Sin(Time.time * 12) * 25, 0);
            if (propeller != null) propeller.Rotate(Vector3.up, 720 * delta, Space.Self);
            for (int i = 0; i < limbs.Length; i++)
                if (limbs[i] != null) limbs[i].localRotation = Quaternion.Euler(Mathf.Sin(Time.time * 9 + i * Mathf.PI) * 25, 0, 0);
            if (!item.IsHeld)
            {
                body.constraints = originalConstraints | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
                var target = Vector3.ProjectOnPlane(player.transform.position - body.position, Vector3.up).normalized;
                jumpIn -= delta;
                bool canJump = steerInAir || body.position.y <= .1f ||
                    Physics.Raycast(shape.bounds.center, Vector3.down, shape.bounds.extents.y + .15f, ~0, QueryTriggerInteraction.Ignore);
                if (jumpIn <= 0 && canJump)
                {
                    direction = damage > 0 ? target : Quaternion.AngleAxis(UnityEngine.Random.Range(-65, 65), Vector3.up) * target;
                    body.linearVelocity = direction * speed + Vector3.up * jumpForce;
                    jumpIn = jumpInterval;
                }
                if (damage > 0 && steerInAir) direction = Vector3.RotateTowards(direction, target, delta * 1.8f, 0);
                if (direction.sqrMagnitude > .01f)
                {
                    body.MoveRotation(Quaternion.LookRotation(direction));
                    var planar = Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up);
                    if (jumpIn > jumpInterval - 1.2f)
                        body.AddForce(Vector3.ClampMagnitude(direction * speed - planar, 8), ForceMode.Acceleration);
                }
            }
            damageIn -= delta;
            var center = player.transform.position + Vector3.up * .8f;
            if (damage > 0 && damageIn <= 0 && (shape.ClosestPoint(center) - center).sqrMagnitude < .3f)
            { player.Damage(damage); damageIn = damageInterval; }
        }

        private void OnDefeated(HowToFishWorldItem defeated)
        {
            item.Body.constraints = originalConstraints;
            if (tail != null) tail.localRotation = Quaternion.identity;
            foreach (var limb in limbs) if (limb != null) limb.localRotation = Quaternion.identity;
        }

        private void OnDestroy() { if (item != null) item.Defeated -= OnDefeated; }
    }
}
