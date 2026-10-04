using UnityEngine;

namespace Hotfix.HowToFish
{
    /// 金枪鱼上岸后间歇跃击；完整尸体由世界用于诱鸟。
    [RequireComponent(typeof(HowToFishWorldItem))]
    public sealed class HowToFishTuna : MonoBehaviour, IHowToFishBoss
    {
        [SerializeField] private Transform tail;
        [SerializeField] private float escapeSeconds = 120;
        [SerializeField] private float damage = 18;
        private HowToFishWorldItem item;
        private HowToFishPlayer player;
        private float remaining;
        private float jumpIn = 1.5f;
        private bool hitPlayer;
        private RigidbodyConstraints originalConstraints;

        public HowToFishWorldItem Item => item;
        public bool IsFighting => player != null && item.IsAlive && !item.Body.isKinematic;
        public float EscapeFraction => Mathf.Clamp01(remaining / escapeSeconds);
        public string Hint => "金枪鱼跃击 · 横向躲避，保留完整鱼身诱鸟";

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
            if (tail != null) tail.localRotation = Quaternion.Euler(0, Mathf.Sin(Time.time * 13) * 18, 0);
            if (item.IsHeld) return;
            item.Body.constraints = originalConstraints | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            jumpIn -= Time.fixedDeltaTime;
            if (jumpIn <= 0)
            {
                var direction = Vector3.ProjectOnPlane(player.transform.position - item.Body.position, Vector3.up).normalized;
                if (direction.sqrMagnitude > .01f) item.Body.MoveRotation(Quaternion.LookRotation(direction));
                item.Body.linearVelocity = direction * 7 + Vector3.up * 6.5f;
                jumpIn = 2.2f;
                hitPlayer = false;
            }
            if (!hitPlayer && jumpIn > .4f && Vector3.Distance(item.Body.worldCenterOfMass,
                player.transform.position + Vector3.up * .8f) < 1.25f)
            { hitPlayer = true; player.Damage(damage); }
        }

        private void OnDefeated(HowToFishWorldItem defeated)
        {
            item.Body.constraints = originalConstraints;
            if (tail != null) tail.localRotation = Quaternion.identity;
        }

        private void OnDestroy() { if (item != null) item.Defeated -= OnDefeated; }
    }
}
