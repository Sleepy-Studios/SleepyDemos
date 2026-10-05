using Core.Runtime;
using Hotfix.HowToFish;
using System.Text;
using Core.Runtime.Inputs;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hotfix
{
    /// 独立页面保存的业务显示与绑定，生命周期由对应 View 管理。
    [Module("HowToFish")]
    [UIBind("HowToFishJournalView")]
    public sealed partial class HowToFishJournalView : View
    {
        private TextMeshProUGUI menuTitle;
        private Button resume;
        private Button save;
        private Button back;
        private TextMeshProUGUI journal;
        private HowToFishWorld world;
        private UIMenuScope menu;
        private readonly StringBuilder journalText = new();
        protected override void OnGameObjectInitialize()
        {
            BindData<HowToFishData>(OnData);
            menuTitle = TextMeshProUGUI_MenuTitle;
            resume = Button_Resume;
            save = Button_Save;
            back = Button_ReturnHub;
            journal = TextMeshProUGUI_Journal;
            menu = gameObject.GetComponent<UIMenuScope>();
            menu.Canceled += Close; AddBinding(() => menu.Canceled -= Close); resume.onClick.AddListener(Close);
            save.onClick.AddListener(() => world?.Save()); back.onClick.AddListener(() => world?.ReturnToHub());
            foreach (var button in gameObject.GetComponentsInChildren<Button>(true)) button.onClick.AddListener(() => world?.PlayUiSound());
        }
        /// <summary>显示前交付当前发现记录。</summary>
        /// <param name="owner">当前世界。</param>
        public void SetData(HowToFishWorld owner) => world = owner;
        /// 隐藏时释放订阅。
        private void Unbind() => world = null;
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
        protected override void OnDestroy() { Unbind(); base.OnDestroy(); }
        protected override void OnHide() { Unbind(); base.OnHide(); }
        private void OnData(HowToFishData value) { world = value.Scene; Refresh(); }
    }
}
