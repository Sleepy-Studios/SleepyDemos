using UnityEngine;

namespace Hotfix.HowToFish
{
    /// 蓝鲨的弧形冲撞，死亡记录由世界用于烧烤任务解锁。
    [RequireComponent(typeof(HowToFishWorldItem))]
    public sealed class HowToFishBlueShark : MonoBehaviour, IHowToFishBoss
    {
        [SerializeField] private Transform rollRoot;
        [SerializeField] private float escapeSeconds = 120;
        [SerializeField] private float damage = 20;
        private HowToFishWorldItem item;
        private HowToFishPlayer player;
        private float remaining;
        private float phaseTime = 1;
        private Vector3 direction;
        private bool charging;
        private bool hitPlayer;
        private RigidbodyConstraints originalConstraints;

        public HowToFishWorldItem Item => item;
        public bool IsFighting => player != null && item.IsAlive && !item.Body.isKinematic;
        public float EscapeFraction => Mathf.Clamp01(remaining / escapeSeconds);
        public string Hint => charging ? "螺旋冲撞 · 向侧面躲避" : "蓝鲨正在转向";

        private void Awake()
        {
            item = GetComponent<HowToFishWorldItem>();
            originalConstraints = GetComponent<Rigidbody>().constraints;
            remaining = escapeSeconds;
            item.Defeated += OnDefeated;
        }

        /// <summary>绑定本次遭遇的玩家。</summary>
        /// <param name="target">当前玩家。</param>
        public void Initialize(HowToFishPlayer target) => player = target;

        private void FixedUpdate()
        {
            if (!IsFighting) return;
            remaining -= Time.fixedDeltaTime;
            if (remaining <= 0) { Destroy(gameObject); return; }
            item.Body.constraints = originalConstraints | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            phaseTime -= Time.fixedDeltaTime;
            if (phaseTime <= 0)
            {
                charging = !charging; phaseTime = charging ? 1.2f : 1;
                direction = Vector3.ProjectOnPlane(player.transform.position - item.Body.position, Vector3.up).normalized;
                hitPlayer = false;
            }
            if (!charging)
            {
                item.Body.linearVelocity = new Vector3(0, item.Body.linearVelocity.y, 0);
                return;
            }
            direction = Quaternion.AngleAxis(65 * Time.fixedDeltaTime, Vector3.up) * direction;
            if (direction.sqrMagnitude > .01f) item.Body.MoveRotation(Quaternion.LookRotation(direction));
            item.Body.linearVelocity = direction * 9 + Vector3.up * item.Body.linearVelocity.y;
            if (rollRoot != null) rollRoot.Rotate(Vector3.forward, 480 * Time.fixedDeltaTime, Space.Self);
            if (!hitPlayer && Vector3.Distance(item.Body.worldCenterOfMass, player.transform.position + Vector3.up * .8f) < 1.7f)
            { hitPlayer = true; player.Damage(damage); }
        }

        private void OnDefeated(HowToFishWorldItem defeated) => item.Body.constraints = originalConstraints;
        private void OnDestroy() { if (item != null) item.Defeated -= OnDefeated; }
    }
}
