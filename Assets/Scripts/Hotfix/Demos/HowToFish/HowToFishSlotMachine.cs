using UnityEngine;

namespace Hotfix.HowToFish
{
    /// 接收玩家已放手的死 Drip 实体，世界负责消费、开奖与保存。
    public sealed class HowToFishSlotMachine : MonoBehaviour
    {
        [Range(0, 4)] [SerializeField] private int island;
        private HowToFishWorld world;
        private Transform[] reels;
        private Quaternion[] reelRotations;
        private Transform lever;
        private Quaternion leverRotation;
        private float spinRemaining;
        private string result;

        public int Island => island;
        public bool Busy => spinRemaining > 0;

        private void Awake()
        {
            reels = new Transform[3];
            reelRotations = new Quaternion[3];
            var root = transform.parent != null ? transform.parent : transform;
            foreach (var part in root.GetComponentsInChildren<Transform>(true))
            {
                for (int i = 0; i < reels.Length; i++)
                    if (part.name == "Reel" + i) { reels[i] = part; reelRotations[i] = part.localRotation; }
                if (part.name == "Lever") lever = part;
            }
            if (lever != null) leverRotation = lever.localRotation;
        }

        /// <summary>绑定所属单人世界；场景开始时调用一次。</summary>
        /// <param name="owner">负责验证进度及保存的世界。</param>
        public void Initialize(HowToFishWorld owner) => world = owner;

        private void OnTriggerEnter(Collider other) => Deliver(other);
        private void OnTriggerStay(Collider other) => Deliver(other);

        private void Deliver(Collider other)
        {
            if (world == null || Busy) return;
            var item = other.GetComponentInParent<HowToFishWorldItem>();
            if (item == null) return;
            spinRemaining = 2;
            if (!world.TryPlaySlotMachine(island, item, out result)) spinRemaining = 0;
        }

        private void Update()
        {
            if (!Busy || Time.deltaTime <= 0) return;
            spinRemaining = Mathf.Max(0, spinRemaining - Time.deltaTime);
            // 两秒滚动和杆角度只用于自制机台演出，奖励已在投料时落盘。
            for (int i = 0; i < reels.Length; i++)
                if (reels[i] != null)
                {
                    if (Busy) reels[i].Rotate(720 * (spinRemaining / 2 + .1f) * Time.deltaTime, 0, 0, Space.Self);
                    else reels[i].localRotation = reelRotations[i];
                }
            if (lever != null) lever.localRotation = leverRotation * Quaternion.Euler(-30 * Mathf.Clamp01(spinRemaining), 0, 0);
            if (!Busy && world != null) world.Notify(result);
        }
    }
}
