using System;
using UnityEngine;

namespace Hotfix.HowToFish
{
    /// 火山口实际触发区：接收完整鲸尸，灼伤玩家并加热普通鱼获。
    [RequireComponent(typeof(Collider))]
    public sealed class HowToFishVolcanoCrater : MonoBehaviour
    {
        [SerializeField] private Transform bossSpawn;
        /// 下一阶段鲸鱼的火山口出场位置，必须在熔岩触发区外。
        public Vector3 BossSpawnPosition => bossSpawn.position;
        [SerializeField] private float damagePerSecond = 25;
        /// 鲸尸进入熔岩时交由世界验证并开启下一阶段；未消费前可重试。
        public event Action<HowToFishWorldItem> WhaleOffered;

        private void OnTriggerStay(Collider other)
        {
            var player = other.GetComponentInParent<HowToFishPlayer>();
            if (player != null) { player.Damage(damagePerSecond * Time.fixedDeltaTime); return; }
            var item = other.GetComponentInParent<HowToFishWorldItem>();
            if (item == null || item.IsConsumed) return;
            if (item.DefinitionId == "BowheadWhale" && !item.IsAlive && !item.IsCooked)
            {
                if (!item.IsHeld) WhaleOffered?.Invoke(item);
                return;
            }
            if (item.IsAlive) item.Hit(damagePerSecond * Time.fixedDeltaTime, Vector3.zero);
            else item.Heat(Time.fixedDeltaTime * .2f);
        }
    }
}
