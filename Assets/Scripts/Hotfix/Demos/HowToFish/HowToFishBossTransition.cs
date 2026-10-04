using UnityEngine;

namespace Hotfix.HowToFish
{
    /// 食人鱼、河豚与变异鲸共用的半血保护；动作与召唤由各首领维护。
    [RequireComponent(typeof(HowToFishWorldItem))]
    public sealed class HowToFishBossTransition : MonoBehaviour
    {
        [Tooltip("转阶段保护时长；河豚/变异鲸约3秒，食人鱼暂按3秒校准。")]
        [SerializeField] private float protectionSeconds = 3;
        private HowToFishWorldItem item;
        private float protectedUntil;

        /// 此次遭遇是否已经进入第二阶段。
        public bool IsSecondPhase { get; private set; }
        /// 当前游戏时间是否仍处于转阶段保护。
        public bool IsProtected => IsSecondPhase && Time.time < protectedUntil;

        private void Awake()
        {
            item = GetComponent<HowToFishWorldItem>();
            item.Damaged += OnDamaged;
        }

        internal float LimitDamage(float damage) => IsProtected ? 0 : IsSecondPhase ? damage :
            Mathf.Min(damage, Mathf.Max(0, item.Health - item.Creature.Health * .5f));

        private void OnDamaged(HowToFishWorldItem target, float damage)
        {
            if (IsSecondPhase || item.Health > item.Creature.Health * .5f) return;
            IsSecondPhase = true;
            protectedUntil = Time.time + protectionSeconds;
        }

        private void OnDestroy() { if (item != null) item.Damaged -= OnDamaged; }
    }
}
