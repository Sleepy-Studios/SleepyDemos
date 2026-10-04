using System.Text;
using Core.Runtime.Inputs;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hotfix.HowToFish
{
    /// 独立页面保存的业务显示与绑定，生命周期由对应 View 管理。
    public sealed class HowToFishJournalPresenter : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI menuTitle;
        [SerializeField] private Button resume;
        [SerializeField] private Button save;
        [SerializeField] private Button back;
        [SerializeField] private TextMeshProUGUI journal;
        private HowToFishWorld world;
        private UIMenuScope menu;
        private readonly StringBuilder journalText = new();
        private void Awake()
        {
            menu = GetComponent<UIMenuScope>(); resume.onClick.AddListener(Close);
            save.onClick.AddListener(() => world?.Save()); back.onClick.AddListener(() => world?.ReturnToHub());
            foreach (var button in GetComponentsInChildren<Button>(true)) button.onClick.AddListener(() => world?.PlayUiSound());
        }
        /// <summary>显示前交付当前发现记录。</summary>
        /// <param name="owner">当前世界。</param>
        public void Bind(HowToFishWorld owner)
        { Unbind(); world = owner; world.Changed += Refresh; menu.Canceled += Close; Refresh(); }
        /// 隐藏时释放订阅。
        public void Unbind() { if (world != null) world.Changed -= Refresh; if (menu != null) menu.Canceled -= Close; world = null; }
        private void Close() => world?.SetPaused(false);
        private void Refresh()
        {
            if (world == null) return;
            menuTitle.text = "鱼类图鉴";
                journalText.Clear();
                foreach (var creature in world.Catalog.Creatures)
                {
                    if (!creature.IsJournalEntry) continue;
                    bool found = world.Session.State.discoveredCreatures.Contains(creature.Id);
                    bool defeated = world.Session.State.defeatedCreatures.Contains(creature.Id);
                    journalText.Append(found ? creature.DisplayName : "未知生物").Append(defeated ? "  ✓" : "  —");
                    if (world.Session.State.defeatedDripCreatures.Contains(creature.Id)) journalText.Append("  珍品");
                    journalText.AppendLine();
                }
                journal.text = journalText.ToString();
        }
        private void OnDestroy() => Unbind();
    }
}
