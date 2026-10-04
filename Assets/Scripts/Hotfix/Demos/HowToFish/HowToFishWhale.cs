using System;
using UnityEngine;

namespace Hotfix.HowToFish
{
    /// 两阶段鲸鱼共用的冲撞和跃击；变异阶段从喷气孔发射熔岩。
    [RequireComponent(typeof(HowToFishWorldItem))]
    public sealed class HowToFishWhale : MonoBehaviour, IHowToFishBoss
    {
        private enum AttackPhase { Rest, Charge, Jump }
        [SerializeField] private Transform tail;
        [SerializeField] private Transform blowhole;
        [SerializeField] private HowToFishProjectile lavaPrefab;
        [SerializeField] private float escapeSeconds = 180;
        [SerializeField] private float chargeSpeed = 12;
        [SerializeField] private float chargeDamage = 28;
        [SerializeField] private float slamDamage = 36;
        [SerializeField] private float burnSeconds = 3;
        [SerializeField] private float burnDamagePerSecond = 12;
        [Tooltip("自制模型的跳跃速度校准值，不直接套用源网格的力。")]
        [SerializeField] private float jumpSpeed = 11;
        [Tooltip("来源未公开转阶段齐射数量，暂按常规三组共12颗校准。")]
        [SerializeField] private int transitionLavaCount = 12;
        private HowToFishWorldItem item;
        private HowToFishBossTransition transition;
        private HowToFishPlayer player;
        private Collider shape;
        private Transform effectsRoot;
        private RigidbodyConstraints originalConstraints;
        private AttackPhase phase;
        private Vector3 direction;
        private float remaining;
        private float phaseTime = 1.3f;
        private float lavaIn = 2;
        private int jumpsRemaining;
        private int lavaShotsRemaining;
        private bool transitionStarted;
        private float contactAt;

        public HowToFishWorldItem Item => item;
        public bool IsFighting => player != null && item.IsAlive && !item.Body.isKinematic;
        public float EscapeFraction => Mathf.Clamp01(remaining / escapeSeconds);
        public string Hint => transition != null && transition.IsProtected ? "转阶段保护 · 继续躲避鲸体与熔岩" :
            phase == AttackPhase.Jump ? "鲸鱼跃起 · 立刻远离落点" :
            phase == AttackPhase.Charge ? "鲸鱼冲撞 · 横向躲开" : IsMutated ? "变异鲸停顿 · 注意地面熔岩" : "鲸鱼停顿 · 准备下一次攻击";
        private bool IsMutated => item.Creature?.Motion == HowToFishCreatureMotion.MagmaWhale;
        private bool IsSecondPhase => transition != null && transition.IsSecondPhase;

        private void Awake()
        {
            item = GetComponent<HowToFishWorldItem>();
            transition = GetComponent<HowToFishBossTransition>();
            shape = GetComponent<Collider>();
            originalConstraints = GetComponent<Rigidbody>().constraints;
            remaining = escapeSeconds;
            item.Defeated += OnDefeated;
        }

        /// <summary>绑定本次遭遇；变异鲸必须配置发射点和熔岩 Prefab。</summary>
        /// <param name="target">当前场景的玩家。</param>
        public void Initialize(HowToFishPlayer target)
        {
            if (target == null || shape == null || tail == null ||
                (IsMutated && (blowhole == null || lavaPrefab == null)))
                throw new InvalidOperationException("鲸鱼遭遇缺少玩家、碰撞体、尾部或熔岩引用。");
            player = target;
            if (IsMutated) effectsRoot = new GameObject("WhaleEffects").transform;
        }

        private void FixedUpdate()
        {
            if (!IsFighting) return;
            float delta = Time.fixedDeltaTime;
            remaining -= delta;
            if (remaining <= 0) { Destroy(gameObject); return; }
            var body = item.Body;
            body.constraints = originalConstraints | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            tail.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * 9) * 16, 0, 0);
            if (IsSecondPhase && !transitionStarted)
            {
                transitionStarted = true;
                lavaShotsRemaining = transitionLavaCount;
                lavaIn = 0;
            }
            var playerCenter = player.transform.position + Vector3.up * .8f;
            if (Time.time >= contactAt && (shape.ClosestPoint(playerCenter) - playerCenter).sqrMagnitude < .36f)
            {
                contactAt = Time.time + 1;
                if (IsMutated) player.ApplyDamageStatus(true, burnSeconds, burnDamagePerSecond);
                player.Damage(chargeDamage);
            }
            phaseTime -= delta;
            if (phase == AttackPhase.Rest)
            {
                body.linearVelocity = new Vector3(0, body.linearVelocity.y, 0);
                direction = Vector3.ProjectOnPlane(player.transform.position - body.position, Vector3.up).normalized;
                if (direction.sqrMagnitude > .01f) body.MoveRotation(Quaternion.LookRotation(direction));
                if (phaseTime <= 0)
                {
                    if (player.transform.position.y > shape.bounds.max.y + 1 || UnityEngine.Random.value < (IsSecondPhase ? .5f : .4f))
                    {
                        jumpsRemaining = IsSecondPhase ? 3 : 1;
                        BeginJump();
                    }
                    else { phase = AttackPhase.Charge; phaseTime = 1.1f; }
                }
            }
            else if (phase == AttackPhase.Charge)
            {
                body.linearVelocity = direction * chargeSpeed + Vector3.up * body.linearVelocity.y;
                if (phaseTime <= 0) Rest();
            }
            else if (phaseTime < 2.1f && body.linearVelocity.y <= 0 &&
                Physics.Raycast(shape.bounds.center, Vector3.down, out var ground, shape.bounds.extents.y + .3f, ~0, QueryTriggerInteraction.Ignore) &&
                ground.collider.GetComponentInParent<HowToFishWorldItem>() == null)
            {
                var offset = player.transform.position - body.position;
                if (Mathf.Abs(offset.y) < 3 && new Vector2(offset.x, offset.z).sqrMagnitude < 25) player.Damage(slamDamage);
                if (jumpsRemaining > 0) BeginJump(); else Rest(true);
            }
            else if (phaseTime <= 0) { if (jumpsRemaining > 0) BeginJump(); else Rest(); }
            if (phase == AttackPhase.Jump)
            {
                var target = Vector3.ProjectOnPlane(player.transform.position - body.position, Vector3.up).normalized;
                body.AddForce(target * 3, ForceMode.Acceleration);
            }
            if (!IsMutated) return;
            lavaIn -= delta;
            if (lavaIn > 0) return;
            if (lavaShotsRemaining <= 0) lavaShotsRemaining = 4;
            lavaShotsRemaining--;
            // ponytail: 源资料只公开每组4颗与颗间.5秒；组间暂用6秒，获得精确控制器时再校准。
            lavaIn = lavaShotsRemaining > 0 ? .5f : 6;
            var origin = blowhole.position + Vector3.up * .6f;
            var velocity = (player.transform.position + Vector3.up * .2f - origin) / 1.25f - Physics.gravity * .625f;
            var projectile = Instantiate(lavaPrefab, origin, Quaternion.identity, effectsRoot);
            projectile.Initialize(player, Vector3.ClampMagnitude(velocity, 45), effectsRoot);
        }

        private void BeginJump()
        {
            jumpsRemaining--;
            phase = AttackPhase.Jump;
            phaseTime = 2.8f;
            var offset = Vector3.ProjectOnPlane(player.transform.position - item.Body.position, Vector3.up);
            item.Body.linearVelocity = Vector3.ClampMagnitude(offset / 2.2f, chargeSpeed) + Vector3.up * jumpSpeed;
        }

        private void Rest(bool landed = false) { phase = AttackPhase.Rest; phaseTime = landed ? 1.5f : IsMutated ? .8f : 1.3f; }

        private void OnDefeated(HowToFishWorldItem defeated)
        {
            item.Body.constraints = originalConstraints;
            if (tail != null) tail.localRotation = Quaternion.identity;
            if (effectsRoot != null) Destroy(effectsRoot.gameObject);
        }

        private void OnDestroy()
        {
            if (item != null) item.Defeated -= OnDefeated;
            if (effectsRoot != null) Destroy(effectsRoot.gameObject);
        }
    }
}
