using System;
using UnityEngine;

namespace Hotfix.HowToFish
{
    public enum HowToFishStationKind { Product, Sell, Keeper, BoatWheel, Grill, Anvil, ForestLady, Tourist, GrillMaster, Islander, Scientist, MilitaryDeparture, AmmoUpgrade, Attachment, InventoryUpgrade, MotorUpgrade, BoatRadar }

    /// 场景中的实体商店、任务人物和工作台，不承担背包或任务状态。
    public sealed class HowToFishStation : MonoBehaviour
    {
        [SerializeField] private HowToFishStationKind kind;
        [SerializeField] private string label;
        [SerializeField] private string itemId;
        [SerializeField] private int island;
        [SerializeField] private HowToFishAttachment attachment;
        [SerializeField] private int motorTier;

        public HowToFishStationKind Kind => kind;
        public string Label => label;
        public string ItemId => itemId;
        public int Island => island;
        public HowToFishAttachment Attachment => attachment;
        public int MotorTier => motorTier;

        /// 物品接触任务人物后尝试交付；由世界统一判断任务与出售规则。
        public event Action<HowToFishStation, HowToFishWorldItem> DeliveryRequested;

        private void OnCollisionEnter(Collision collision) => Deliver(collision);
        private void OnCollisionStay(Collision collision) => Deliver(collision);

        private void Deliver(Collision collision)
        {
            if (kind != HowToFishStationKind.Keeper && kind != HowToFishStationKind.ForestLady &&
                kind != HowToFishStationKind.Tourist && kind != HowToFishStationKind.Islander && kind != HowToFishStationKind.Scientist &&
                kind != HowToFishStationKind.GrillMaster) return;
            var item = collision.collider.GetComponentInParent<HowToFishWorldItem>();
            if (item != null && !item.IsHeld && !item.IsConsumed) DeliveryRequested?.Invoke(this, item);
        }
    }
}
