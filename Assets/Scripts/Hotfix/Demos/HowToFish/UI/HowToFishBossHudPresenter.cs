using Core.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Hotfix.HowToFish
{
    /// HUD 独立区域，只维护已保存的本区域显示。
    public sealed class HowToFishBossHudPresenter : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI bossStatus;
        [SerializeField] private Image bossHealth;
        [SerializeField] private Image bossEscape;
        private UIProgressBar bossHealthBar, bossEscapeBar;
        private void Awake() { bossHealthBar = bossHealth.GetComponent<UIProgressBar>(); bossEscapeBar = bossEscape.GetComponent<UIProgressBar>(); }
        /// <summary>刷新当前世界的区域显示。</summary>
        /// <param name="world">当前 HUD 已绑定的世界。</param>
        public void Refresh(HowToFishWorld world)
        {
            var boss = world.ActiveBoss;
            bool fighting = boss != null && !world.IsPaused;
            gameObject.SetActive(fighting);
            if (fighting)
            {
                var creature = boss.Item;
                bossHealthBar.SetValue(creature.Health / creature.Creature.Health);
                bossEscapeBar.SetValue(boss.EscapeFraction);
                bossStatus.text = $"{creature.Creature.DisplayName}  {creature.Health:0} / {creature.Creature.Health:0} · {boss.Hint}";
            }
        }
    }
}
