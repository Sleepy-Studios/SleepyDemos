using Core.Runtime;
using Core.Runtime.Inputs;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hotfix.HowToFish
{
    /// 独立页面保存的业务显示与绑定，生命周期由对应 View 管理。
    public sealed class HowToFishOutfitsPresenter : MonoBehaviour
    {
        [SerializeField] private Button[] outfitCards;
        [SerializeField] private Image[] outfitIcons;
        [SerializeField] private TextMeshProUGUI[] outfitLabels;
        [SerializeField] private TextMeshProUGUI outfitDetails;
        [SerializeField] private Button outfitWear;
        [SerializeField] private Button outfitBack;
        [SerializeField] private UITab outfitTabs;
        private HowToFishWorld world;
        private UIMenuScope menu;
        private int selectedOutfit;
        private bool outfitOpen;
        private void Awake()
        {
            menu = GetComponent<UIMenuScope>();
            outfitBack.onClick.AddListener(CloseOutfits);
            outfitWear.onClick.AddListener(WearOutfit);
            foreach (var button in GetComponentsInChildren<Button>(true)) button.onClick.AddListener(() => world?.PlayUiSound());
        }
        /// <summary>显示前绑定服装目录及当前选择。</summary>
        /// <param name="owner">当前世界。</param>
        public void Bind(HowToFishWorld owner)
        {
            Unbind(); world = owner; outfitOpen = true;
            world.SetEditingSettings(true); world.Changed += RefreshOutfits; menu.Canceled += CloseOutfits;
            outfitTabs.Register(SelectOutfit);
            selectedOutfit = 0;
            for (int i = 0; i < outfitIcons.Length; i++)
            {
                var outfit = HowToFishOutfitCatalog.All[i];
                outfitIcons[i].sprite = world.Catalog.FindOutfit(outfit.Id).Icon;
                if (outfit.Id == world.SelectedOutfitId) selectedOutfit = i;
            }
            outfitTabs.SetIndex(selectedOutfit, false); RefreshOutfits();
            EventSystem.current?.SetSelectedGameObject(outfitCards[selectedOutfit].gameObject);
        }
        /// 隐藏时释放世界、焦点作用域和选择回调。
        public void Unbind()
        {
            if (world != null) { world.Changed -= RefreshOutfits; world.SetEditingSettings(false); }
            if (menu != null) menu.Canceled -= CloseOutfits;
            if (outfitTabs != null) outfitTabs.Unregister(SelectOutfit);
            outfitOpen = false; world = null;
        }
        private void Update()
        {
            if (world == null) return;
            var selected = EventSystem.current?.currentSelectedGameObject;
            for (int i = 0; i < outfitCards.Length; i++)
                if (i != selectedOutfit && outfitCards[i].gameObject == selected) { SelectOutfit(i); break; }
        }
        private void CloseOutfits() => world?.UI.CloseOutfits();
        private void OnDestroy() => Unbind();
        private void SelectOutfit(int index)
        {
            selectedOutfit = index;
            outfitTabs.SetIndex(index, false);
            RefreshOutfits();
        }

        private void WearOutfit()
        {
            if (world == null || !outfitOpen) return;
            if (!world.TrySelectOutfit(HowToFishOutfitCatalog.All[selectedOutfit].Id)) return;
            RefreshOutfits();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(outfitCards[selectedOutfit].gameObject);
        }

        private void RefreshOutfits()
        {
            if (world == null) return;
            for (int i = 0; i < outfitCards.Length; i++)
            {
                var outfit = HowToFishOutfitCatalog.All[i];
                bool unlocked = HowToFishOutfitCatalog.IsUnlocked(outfit.Id, world.Session.State.unlockedOutfits);
                bool equipped = outfit.Id == world.SelectedOutfitId;
                outfitLabels[i].text = outfit.Name + (equipped ? " · 已穿戴" : unlocked ? "" : " · 未解锁");
                outfitIcons[i].color = unlocked ? Color.white : new Color(.42f, .42f, .42f, 1);
                // 锁定卡片仍可聚焦，查看条件；只有穿戴操作按解锁状态禁用。
            }
            var selected = HowToFishOutfitCatalog.All[selectedOutfit];
            bool canWear = HowToFishOutfitCatalog.IsUnlocked(selected.Id, world.Session.State.unlockedOutfits);
            outfitDetails.text = selected.Name + "\n" + selected.UnlockHint +
                (selected.Id == world.SelectedOutfitId ? "\n当前穿戴" : canWear ? "\n已解锁" : "\n尚未解锁");
            outfitWear.interactable = canWear && selected.Id != world.SelectedOutfitId;
        }

    }
}
