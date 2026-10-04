using UnityEngine;

namespace Hotfix.HowToFish
{
    /// 首领落物沿抛物线扫掠碰撞，屋顶先命中时不能伤害下面的玩家。
    public sealed class HowToFishProjectile : MonoBehaviour
    {
        [SerializeField] private float damage = 12;
        [SerializeField] private float radius = .15f;
        [SerializeField] private HowToFishDamagePool impactPool;
        private Transform effectsRoot;
        private HowToFishPlayer player;
        private Vector3 velocity;
        private float remaining = 6;

        /// <summary>设定落物目标与初速度。</summary>
        /// <param name="target">可受伤害的当前玩家。</param>
        /// <param name="initialVelocity">发射时世界速度。</param>
        /// <param name="effectParent">落地效果的所有者；遭遇结束时统一清理。</param>
        public void Initialize(HowToFishPlayer target, Vector3 initialVelocity, Transform effectParent = null)
        { player = target; velocity = initialVelocity; effectsRoot = effectParent; }

        private void FixedUpdate()
        {
            if (player == null) { Destroy(gameObject); return; }
            float delta = Time.fixedDeltaTime;
            remaining -= delta;
            if (remaining <= 0) { Destroy(gameObject); return; }
            velocity += Physics.gravity * delta;
            var step = velocity * delta;
            if (step.sqrMagnitude > 0 && Physics.SphereCast(transform.position, radius, step.normalized,
                out var hit, step.magnitude, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.GetComponentInParent<HowToFishPlayer>() == player)
                {
                    if (impactPool != null) impactPool.ApplyTo(player);
                    player.Damage(damage);
                }
                if (impactPool != null && hit.normal.y > .35f && hit.collider.GetComponentInParent<HowToFishPlayer>() == null)
                {
                    var pool = Instantiate(impactPool, hit.point + hit.normal * .025f, Quaternion.FromToRotation(Vector3.up, hit.normal), effectsRoot);
                    pool.Initialize(player);
                }
                Destroy(gameObject);
                return;
            }
            transform.position += step;
        }
    }
}
