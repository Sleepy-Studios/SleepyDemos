using UnityEngine;

namespace Hotfix.HowToFish
{
    /// 普通鱼和召唤鱼共用的物理跃动、寻水与接触攻击；被持握和死亡时停止驱动。
    [RequireComponent(typeof(HowToFishWorldItem))]
    public sealed class HowToFishFishMotion : MonoBehaviour
    {
        [SerializeField] private float jumpForce = 4;
        [SerializeField] private float towardsWaterForce = 3;
        [SerializeField] private float towardsPlayerForce;
        [SerializeField] private float airTowardsPlayerForce;
        [SerializeField] private float minimumInterval = .3f;
        [SerializeField] private float maximumInterval = .5f;
        [SerializeField] private float damage;
        [SerializeField] private float damageInterval = .5f;
        [SerializeField] private bool attackInAir;
        [SerializeField] private bool jumpOnWater;
        [SerializeField] private float walkSpeed;
        [SerializeField] private bool walkSideways;
        [Tooltip("原始力参数映射到自制模型的速度比例，需要随模型物理尺度校准。")]
        [SerializeField] private float movementScale = .5f;
        private HowToFishWorldItem item;
        private HowToFishPlayer player;
        private Collider shape;
        private float jumpAt;
        private float damageAt;
        private float seekAt;
        private Vector3 waterDirection;

        private void Awake()
        {
            item = GetComponent<HowToFishWorldItem>();
            shape = GetComponent<Collider>();
        }

        /// <summary>绑定当前玩家；野生和首领召唤个体走同一入口。</summary>
        /// <param name="target">世界中的当前玩家。</param>
        public void Initialize(HowToFishPlayer target) => player = target;

        private void FixedUpdate()
        {
            if (player == null || !item.IsAlive || item.IsHeld || item.Body.isKinematic) return;
            var body = item.Body;
            var center = shape.bounds.center;
            bool inWater = center.y < 0;
            bool grounded = false;
            foreach (var hit in Physics.RaycastAll(center, Vector3.down, shape.bounds.extents.y + .12f, ~0, QueryTriggerInteraction.Ignore))
                if (hit.rigidbody != body && hit.normal.y > .6f) { grounded = true; break; }
            var target = Vector3.ProjectOnPlane(player.transform.position - center, Vector3.up).normalized;
            if (towardsWaterForce > 0 && Time.time >= seekAt)
            {
                seekAt = Time.time + 2;
                waterDirection = FindWaterDirection(center);
            }
            var direction = towardsPlayerForce > 0 ? target : waterDirection;
            if (walkSpeed > 0 && grounded)
            {
                var planar = Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up);
                body.AddForce(Vector3.ClampMagnitude(direction * walkSpeed - planar, 20), ForceMode.Acceleration);
            }
            else if (Time.time >= jumpAt && (grounded || inWater && jumpOnWater || !inWater && attackInAir))
            {
                float speed = towardsPlayerForce > 0 ? towardsPlayerForce : towardsWaterForce;
                float vertical = grounded || inWater ? jumpForce * movementScale : body.linearVelocity.y;
                body.linearVelocity = direction * speed * movementScale + Vector3.up * vertical;
                jumpAt = Time.time + Random.Range(Mathf.Min(minimumInterval, maximumInterval), Mathf.Max(minimumInterval, maximumInterval));
            }
            if (!grounded && airTowardsPlayerForce > 0)
                body.AddForce(target * airTowardsPlayerForce * movementScale, ForceMode.Acceleration);
            if (direction.sqrMagnitude > .01f)
            {
                var facing = walkSideways ? Quaternion.AngleAxis(90, Vector3.up) * direction : direction;
                body.MoveRotation(Quaternion.RotateTowards(body.rotation, Quaternion.LookRotation(facing), 240 * Time.fixedDeltaTime));
            }
            var playerCenter = player.transform.position + Vector3.up * .8f;
            if (damage > 0 && Time.time >= damageAt &&
                (shape.ClosestPoint(playerCenter) - playerCenter).sqrMagnitude < .09f)
            {
                player.Damage(damage);
                damageAt = Time.time + damageInterval;
            }
        }

        private Vector3 FindWaterDirection(Vector3 center)
        {
            // ponytail: 每两秒做有限方向探测；复杂曲折河道需要航行网格时再替换。
            for (float distance = 4; distance <= 64; distance *= 2)
                for (int i = 0; i < 12; i++)
                {
                    var direction = Quaternion.AngleAxis(i * 30, Vector3.up) * Vector3.forward;
                    var probe = center + direction * distance;
                    probe.y = 80;
                    bool land = false;
                    foreach (var hit in Physics.RaycastAll(probe, Vector3.down, 80, ~0, QueryTriggerInteraction.Ignore))
                        if (hit.collider.GetComponentInParent<HowToFishWorldItem>() == null &&
                            hit.collider.GetComponentInParent<HowToFishPlayer>() == null && hit.point.y > .05f)
                        { land = true; break; }
                    if (!land) return direction;
                }
            return Vector3.zero;
        }
    }
}
