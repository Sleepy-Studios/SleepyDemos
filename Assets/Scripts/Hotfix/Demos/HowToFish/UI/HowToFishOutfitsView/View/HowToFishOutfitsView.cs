using Core.Runtime;
using Hotfix.HowToFish;
using Core.Runtime.Inputs;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hotfix
{
    /// 独立页面保存的业务显示与绑定，生命周期由对应 View 管理。
    [Module("HowToFish")]
    [UIBind("HowToFishOutfitsView")]
    public sealed partial class HowToFishOutfitsView : View
    {
        private Button[] outfitCards;

        private Image[] outfitIcons;

        private TextMeshProUGUI[] outfitLabels;

        private TextMeshProUGUI outfitDetails;

        private Button outfitWear;

        private Button outfitBack;

        private UITab outfitTabs;

        private HowToFishWorld world;

        private HowToFishData data;

        private UIMenuScope menu;

        private int selectedOutfit;

        private bool outfitOpen;

        protected override void OnGameObjectInitialize()
        {
            BindData<HowToFishData>(OnData);
            BindUpdate(Update);
            outfitCards = new UnityEngine.UI.Button[]
            {
                Button_OutfitBadman,
                Button_OutfitBikini,
                Button_OutfitFisherman,
                Button_OutfitSailor,
                Button_OutfitLighthouseKeeper,
                Button_OutfitSwampMan,
                Button_OutfitSwampLady,
                Button_OutfitKioskLady,
                Button_OutfitTourist,
                Button_OutfitGrillMaster,
                Button_OutfitAndrei,
                Button_OutfitJacob,
                Button_OutfitGunstoreClerc,
                Button_OutfitScaredGuyInShorts,
                Button_OutfitStoreGrandma,
                Button_OutfitMilitary,
                Button_OutfitScientist,
                Button_OutfitBean
            };
            outfitIcons = new UnityEngine.UI.Image[]
            {
                Image_Icon,
                Image_Icon1,
                Image_Icon2,
                Image_Icon3,
                Image_Icon4,
                Image_Icon5,
                Image_Icon6,
                Image_Icon7,
                Image_Icon8,
                Image_Icon9,
                Image_Icon10,
                Image_Icon11,
                Image_Icon12,
                Image_Icon13,
                Image_Icon14,
                Image_Icon15,
                Image_Icon16,
                Image_Icon17
            };
            outfitLabels = new TMPro.TextMeshProUGUI[]
            {
                TextMeshProUGUI_OutfitBadmanLabel,
                TextMeshProUGUI_OutfitBikiniLabel,
                TextMeshProUGUI_OutfitFishermanLabel,
                TextMeshProUGUI_OutfitSailorLabel,
                TextMeshProUGUI_OutfitLighthouseKeeperLabel,
                TextMeshProUGUI_OutfitSwampManLabel,
                TextMeshProUGUI_OutfitSwampLadyLabel,
                TextMeshProUGUI_OutfitKioskLadyLabel,
                TextMeshProUGUI_OutfitTouristLabel,
                TextMeshProUGUI_OutfitGrillMasterLabel,
                TextMeshProUGUI_OutfitAndreiLabel,
                TextMeshProUGUI_OutfitJacobLabel,
                TextMeshProUGUI_OutfitGunstoreClercLabel,
                TextMeshProUGUI_OutfitScaredGuyInShortsLabel,
                TextMeshProUGUI_OutfitStoreGrandmaLabel,
                TextMeshProUGUI_OutfitMilitaryLabel,
                TextMeshProUGUI_OutfitScientistLabel,
                TextMeshProUGUI_OutfitBeanLabel
            };
            outfitDetails = TextMeshProUGUI_OutfitDetails;
            outfitWear = Button_WearOutfit;
            outfitBack = Button_CloseOutfits;
            outfitTabs = UITab_HowToFishOutfitsView;
            menu = gameObject.GetComponent<UIMenuScope>();
            menu.Canceled += CloseOutfits;
            AddBinding(() => menu.Canceled -= CloseOutfits);
            outfitTabs.Register(SelectOutfit);
            AddBinding(() => outfitTabs.Unregister(SelectOutfit));
            outfitBack.onClick.AddListener(CloseOutfits);
            outfitWear.onClick.AddListener(WearOutfit);
            foreach (var button in gameObject.GetComponentsInChildren<Button>(true))
                button.onClick.AddListener(() => world?.PlayUiSound());
        }

        /// <summary>显示前绑定服装目录及当前选择。</summary>
        /// <param name="owner">当前世界。</param>
        public void SetData(HowToFishWorld owner)
        {
            world = owner;
            data = owner?.Data;
            outfitOpen = true;
            GlobalData.Dispatch(new HowToFishSetEditingSettingsAction(world, true));
            selectedOutfit = 0;
            for (int i = 0; i < outfitIcons.Length; i++)
            {
                var outfit = HowToFishOutfitCatalog.All[i];
                outfitIcons[i].sprite = world.Catalog.FindOutfit(outfit.Id).Icon;
                if (outfit.Id == data.SelectedOutfitId)
                    selectedOutfit = i;
            }

            outfitTabs.SetIndex(selectedOutfit, false);
            RefreshOutfits();
            EventSystem.current?.SetSelectedGameObject(outfitCards[selectedOutfit].gameObject);
        }

        /// 隐藏时释放世界、焦点作用域和选择回调。
        private void Unbind()
        {
            if (world != null)
            {
                if (!world.Data.IsExiting && ReferenceEquals(GlobalData.Get<HowToFishData>(), world.Data))
                    CloseOutfits();
                GlobalData.Dispatch(new HowToFishSetEditingSettingsAction(world, false));
            }
            outfitOpen = false;
            world = null;
            data = null;
        }

        private void Update()
        {
            if (world == null)
                return;
            var selected = EventSystem.current?.currentSelectedGameObject;
            for (int i = 0; i < outfitCards.Length; i++)
                if (i != selectedOutfit && outfitCards[i].gameObject == selected)
                {
                    SelectOutfit(i);
                    break;
                }
        }

        private void CloseOutfits() => GlobalData.Dispatch(new HowToFishUiAction(world, HowToFishUiCommand.CloseOutfits));

        protected override void OnDestroy()
        {
            Unbind();
            base.OnDestroy();
        }

        private void SelectOutfit(int index)
        {
            selectedOutfit = index;
            outfitTabs.SetIndex(index, false);
            RefreshOutfits();
        }

        private void WearOutfit()
        {
            if (world == null || !outfitOpen)
                return;
            var request = new HowToFishTrySelectOutfitAction(world, HowToFishOutfitCatalog.All[selectedOutfit].Id);
            GlobalData.Dispatch(request);
            if (!request.Result)
                return;
            RefreshOutfits();
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(outfitCards[selectedOutfit].gameObject);
        }

        private void RefreshOutfits()
        {
            if (world == null)
                return;
            for (int i = 0; i < outfitCards.Length; i++)
            {
                var outfit = HowToFishOutfitCatalog.All[i];
                bool unlocked = data.IsOutfitUnlocked(outfit.Id);
                bool equipped = outfit.Id == data.SelectedOutfitId;
                outfitLabels[i].text = outfit.Name + (equipped ? " · 已穿戴" : unlocked ? "" : " · 未解锁");
                outfitIcons[i].color = unlocked ? Color.white : new Color(.42f, .42f, .42f, 1);
                // 锁定卡片仍可聚焦，查看条件；只有穿戴操作按解锁状态禁用。
            }

            var selected = HowToFishOutfitCatalog.All[selectedOutfit];
            bool canWear = data.IsOutfitUnlocked(selected.Id);
            outfitDetails.text = selected.Name + "\n" + selected.UnlockHint + (selected.Id == data.SelectedOutfitId ? "\n当前穿戴" : canWear ? "\n已解锁" : "\n尚未解锁");
            outfitWear.interactable = canWear && selected.Id != data.SelectedOutfitId;
        }

        protected override void OnHide()
        {
            Unbind();
            base.OnHide();
        }

        private void OnData(HowToFishData value)
        {
            world = value.Scene;
            data = value;
            RefreshOutfits();
        }
    }
}
