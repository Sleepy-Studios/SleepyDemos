using Core.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Hotfix.HowToFish
{
    /// HUD 独立区域，只维护已保存的本区域显示。
    public sealed class HowToFishFishingHudPresenter : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI fishingStatus;
        [SerializeField] private Image tension;
        private UIProgressBar tensionBar;
        private void Awake() => tensionBar = tension.GetComponent<UIProgressBar>();
        /// <summary>刷新当前世界的区域显示。</summary>
        /// <param name="world">当前 HUD 已绑定的世界。</param>
        public void Refresh(HowToFishWorld world)
        {
            if (!world.HasSession) { gameObject.SetActive(false); return; }
            var fishing = world.Player.Fishing.State;
            gameObject.SetActive(!world.IsPaused && fishing.IsActive);
            if (!gameObject.activeInHierarchy) return;
            bool pullBack = world.Player.Equipment?.Id == "FishingRod";
            tensionBar.SetValue(fishing.Phase == HowToFishFishingPhase.Charging ? fishing.Charge : pullBack ? fishing.Progress : fishing.Tension);
            tensionBar.SetColor(fishing.Tension > 0.75f ? new Color(0.95f, 0.24f, 0.12f) : new Color(0.94f, 0.76f, 0.24f));
            fishingStatus.text = fishing.Phase switch
            {
                HowToFishFishingPhase.Charging => "松手抛竿",
                HowToFishFishingPhase.Flying => "抛竿",
                HowToFishFishingPhase.Waiting => world.Player.Equipment?.Id == "FishingRod"
                    ? "按住 " + world.Input.BindingLabel("Use") + " 慢收，吸引鱼咬钩" : "等待鱼讯…",
                HowToFishFishingPhase.Bite => world.Player.Equipment?.Id == "FishingRod" ? "咬钩！松开后再按下收线" : "咬钩！按下收线",
                HowToFishFishingPhase.Reeling => pullBack ? $"连按收线 {fishing.Progress:P0}" : $"收线 {fishing.Progress:P0} · 松手降低张力",
                _ => ""
            };
        }
    }
}
