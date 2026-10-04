using UnityEngine;

namespace Hotfix.HowToFish
{
    /// 蜘蛛蟹上岸后的冲撞、远距跳跃和逃脱；数值为可调的原型推定。
    [RequireComponent(typeof(HowToFishWorldItem))]
    public sealed class HowToFishSpiderCrab : MonoBehaviour, IHowToFishBoss
    {
        [SerializeField] private float escapeSeconds = 90;
        [SerializeField] private float windupSeconds = .8f;
        [SerializeField] private float stunSeconds = 2.5f;
        [SerializeField] private float sidewaysSpeed = 2;
        [SerializeField] private float clawDelay = .35f;
        [SerializeField] private float clawCooldown = 1;
        [SerializeField] private float chargeSpeed = 8;
        [SerializeField] private float damage = 18;
        private HowToFishWorldItem item;
        private HowToFishPlayer player;
        private float remaining;
        private float phaseTime;
        private Vector3 attackDirection;
        private float clawAt = -1;
        private float clawReadyAt;
        private float sidewaysDirection = 1;
        private RigidbodyConstraints originalConstraints;

        /// 当前攻击阶段，供动作和 HUD 使用。
        public HowToFishCrabPhase Phase { get; private set; }
        /// 逃脱条剩余比例，暂停期间不下降。
        public float EscapeFraction => Mathf.Clamp01(remaining / escapeSeconds);
        /// 仅上岸且仍存活时进入首领战。
        public bool IsFighting => player != null && item.IsAlive && !item.Body.isKinematic;
        public HowToFishWorldItem Item => item;
        /// 已经锁定玩家的爪击；硬直只阻止新的锁定。
        public bool IsClawQueued => clawAt >= 0;
        public string Hint => IsClawQueued ? "蟹钳已锁定 · 即将命中" : Phase switch
        {
            HowToFishCrabPhase.Windup => "准备冲撞",
            HowToFishCrabPhase.Charging => "向侧面躲避",
            HowToFishCrabPhase.Jumping => "跳跃！",
            _ => "硬直 · 攻击机会"
        };

        private void Awake()
        {
            item = GetComponent<HowToFishWorldItem>();
            originalConstraints = item.GetComponent<Rigidbody>().constraints;
            remaining = escapeSeconds;
            phaseTime = windupSeconds;
            item.Defeated += OnDefeated;
        }

        /// <summary>绑定当前玩家；钓鱼收线阶段由刚体运动学状态阻止提前攻击。</summary>
        /// <param name="target">当前会话玩家。</param>
        public void Initialize(HowToFishPlayer target) => player = target;

        private void FixedUpdate()
        {
            if (!IsFighting) return;
            var body = item.Body;
            body.constraints = originalConstraints | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            remaining -= Time.fixedDeltaTime;
            if (remaining <= 0) { Destroy(gameObject); return; }
            phaseTime -= Time.fixedDeltaTime;
            if (IsClawQueued && Time.time >= clawAt)
            {
                clawAt = -1;
                player.Damage(damage);
            }
            if (!IsClawQueued && Phase != HowToFishCrabPhase.Stunned && Time.time >= clawReadyAt &&
                Vector3.Distance(body.worldCenterOfMass, player.transform.position + Vector3.up * .8f) <= 1.65f)
            {
                clawAt = Time.time + clawDelay;
                clawReadyAt = Time.time + clawCooldown;
            }
            var offset = player.transform.position - body.position;
            offset.y = 0;
            switch (Phase)
            {
                case HowToFishCrabPhase.Windup:
                    var sideways = Vector3.Cross(Vector3.up, offset.normalized) * sidewaysDirection;
                    body.linearVelocity = sideways * sidewaysSpeed + Vector3.up * body.linearVelocity.y;
                    if (offset.sqrMagnitude > .01f)
                        body.MoveRotation(Quaternion.RotateTowards(body.rotation, Quaternion.LookRotation(offset), 240 * Time.fixedDeltaTime));
                    if (phaseTime > 0) break;
                    if (Random.value > .8f) { sidewaysDirection = -sidewaysDirection; phaseTime = windupSeconds; break; }
                    attackDirection = offset.normalized;
                    if (offset.sqrMagnitude > 64)
                    {
                        const float flightSeconds = 1.1f;
                        var velocity = offset / flightSeconds;
                        velocity.y = (player.transform.position.y - body.position.y - Physics.gravity.y * flightSeconds * flightSeconds * .5f) / flightSeconds;
                        body.linearVelocity = Vector3.ClampMagnitude(velocity, 22);
                        Phase = HowToFishCrabPhase.Jumping;
                        phaseTime = flightSeconds;
                    }
                    else { Phase = HowToFishCrabPhase.Charging; phaseTime = 1; }
                    break;
                case HowToFishCrabPhase.Charging:
                    body.linearVelocity = attackDirection * chargeSpeed + Vector3.up * body.linearVelocity.y;
                    if (phaseTime <= 0) Stun();
                    break;
                case HowToFishCrabPhase.Jumping:
                    if (phaseTime <= 0) Stun();
                    break;
                case HowToFishCrabPhase.Stunned:
                    body.linearVelocity = new Vector3(0, body.linearVelocity.y, 0);
                    if (phaseTime <= 0) { Phase = HowToFishCrabPhase.Windup; phaseTime = windupSeconds; }
                    break;
            }
        }

        private void Stun()
        {
            Phase = HowToFishCrabPhase.Stunned;
            phaseTime = stunSeconds;
        }

        private void OnDefeated(HowToFishWorldItem defeated) => item.Body.constraints = originalConstraints;
        private void OnDestroy() { if (item != null) item.Defeated -= OnDefeated; }
    }

    /// 首领预警与可攻击窗口。
    public enum HowToFishCrabPhase { Windup, Charging, Jumping, Stunned }
}
