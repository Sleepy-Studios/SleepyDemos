using UnityEngine;

namespace Hotfix.HowToFish
{
    /// 烤架热区，同时处理物理鱼获和没有物理碰撞体的第一人称工具。
    [RequireComponent(typeof(BoxCollider))]
    public sealed class HowToFishGrill : MonoBehaviour
    {
        [SerializeField] private float heatPerSecond = .04f;
        [SerializeField] private Light embers;
        private BoxCollider heatArea;
        private HowToFishSession session;
        private HowToFishPlayer player;

        public Vector3 CookingPosition => transform.TransformPoint(heatArea.center);

        /// <summary>绑定本局权限与玩家；场景退出后组件随场景一起销毁。</summary>
        /// <param name="owner">当前会话。</param>
        /// <param name="currentPlayer">当前玩家，用于计算手持工具是否伸入热区。</param>
        public void Initialize(HowToFishSession owner, HowToFishPlayer currentPlayer)
        {
            session = owner; player = currentPlayer;
            heatArea = GetComponent<BoxCollider>();
        }

        private void FixedUpdate()
        {
            bool lit = session?.State.hasGrill == true;
            if (embers != null) embers.enabled = lit;
            if (!lit || player.Equipment == null) return;
            var local = transform.InverseTransformPoint(player.EquipmentCenter) - heatArea.center;
            var half = heatArea.size * .5f;
            if (Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z)
                player.HeatEquipment(heatPerSecond * Time.fixedDeltaTime);
        }

        private void OnTriggerStay(Collider other)
        {
            if (session?.State.hasGrill != true) return;
            other.GetComponentInParent<HowToFishWorldItem>()?.Heat(heatPerSecond * Time.fixedDeltaTime);
        }
    }
}
