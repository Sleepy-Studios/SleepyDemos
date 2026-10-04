using UnityEngine;

namespace Hotfix.HowToFish
{
    /// 毒液与熔岩共用的短时伤害区域；视觉由 Prefab 提供，随游戏暂停冻结。
    public sealed class HowToFishDamagePool : MonoBehaviour
    {
        [SerializeField] private float radius = 1.4f;
        [SerializeField] private float duration = 6;
        [SerializeField] private float damagePerSecond = 8;
        [SerializeField] private bool burning;
        [Tooltip("离开区域后的毒火持续时间；来源未公开精确数值，暂按3秒校准。")]
        [SerializeField] private float statusSeconds = 3;
        private HowToFishPlayer player;
        private float remaining;

        /// <summary>绑定当前玩家并开始区域寿命。</summary>
        /// <param name="target">本会话的玩家。</param>
        public void Initialize(HowToFishPlayer target) { player = target; remaining = duration; }

        /// <summary>区域接触和对应投射物命中共用残留状态，离开区域后仍会自然衰减。</summary>
        /// <param name="target">接受此次毒火状态的玩家。</param>
        public void ApplyTo(HowToFishPlayer target) => target.ApplyDamageStatus(burning, statusSeconds, damagePerSecond);

        private void Update()
        {
            if (player == null) return;
            remaining -= Time.deltaTime;
            if (remaining <= 0) { Destroy(gameObject); return; }
            var offset = player.transform.position - transform.position;
            if (Mathf.Abs(offset.y) < 1.5f && new Vector2(offset.x, offset.z).sqrMagnitude < radius * radius)
                ApplyTo(player);
        }
    }
}
