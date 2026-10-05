using Core.Runtime;
using Hotfix.HowToFish;
using Core.Runtime.Inputs;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hotfix
{
    /// 独立页面保存的业务显示与绑定，生命周期由对应 View 管理。
    [Module("HowToFish")]
    [UIBind("HowToFishMainMenuView")]
    public sealed partial class HowToFishMainMenuView : View
    {
        private TextMeshProUGUI menuTitle;

        private Button[] slots;

        private TextMeshProUGUI[] slotLabels;

        private Button[] newGames;

        private Button back;

        private Button settingsButton;

        private HowToFishWorld world;

        private HowToFishData data;

        private UIMenuScope menu;

        protected override void OnGameObjectInitialize()
        {
            BindData<HowToFishData>(OnData);
            menuTitle = TextMeshProUGUI_MenuTitle;
            slots = new UnityEngine.UI.Button[]
            {
                Button_Slot0,
                Button_Slot1,
                Button_Slot2
            };
            slotLabels = new TMPro.TextMeshProUGUI[]
            {
                TextMeshProUGUI_Slot0Label,
                TextMeshProUGUI_Slot1Label,
                TextMeshProUGUI_Slot2Label
            };
            newGames = new UnityEngine.UI.Button[]
            {
                Button_New0,
                Button_New1,
                Button_New2
            };
            back = Button_ReturnHub;
            settingsButton = Button_OpenSettings;
            menu = gameObject.GetComponent<UIMenuScope>();
            menu.Canceled += Back;
            AddBinding(() => menu.Canceled -= Back);
            for (int i = 0; i < slots.Length; i++)
            {
                int index = i;
                slots[i].onClick.AddListener(() => ContinueSlot(index));
                newGames[i].onClick.AddListener(() => NewSlot(index));
            }

            back.onClick.AddListener(Back);
            settingsButton.onClick.AddListener(() => GlobalData.Dispatch(new HowToFishUiAction(world, HowToFishUiCommand.OpenSettings)));
            foreach (var button in gameObject.GetComponentsInChildren<Button>(true))
                button.onClick.AddListener(() => world?.PlayUiSound());
        }

        /// <summary>显示前绑定三槽菜单。</summary>
        /// <param name="owner">当前世界。</param>
        public void SetData(HowToFishWorld owner)
        {
            world = owner;
            data = owner?.Data;
        }

        // 隐藏时清理本页持有的场景引用。

        private void Unbind()
        {
            world = null;
            data = null;
        }

        private void Back() => GlobalData.Dispatch(new HowToFishUiAction(world, HowToFishUiCommand.Exit));

        private void Refresh()
        {
            if (world == null)
                return;
            menuTitle.text = "渔力全开\n<size=22>单人航程</size>";
            for (int i = 0; i < slots.Length; i++)
            {
                var saved = world.Data.SlotInfos[i];
                slotLabels[i].text = saved.Status switch
                {
                    HowToFishLoadStatus.Empty => $"存档 {i + 1} · 新的航程",
                    HowToFishLoadStatus.Ready => $"存档 {i + 1} · 继续  ${saved.Data.money}",
                    HowToFishLoadStatus.RecoveryAvailable => $"存档 {i + 1} · 恢复备份",
                    HowToFishLoadStatus.UnsupportedVersion => $"存档 {i + 1} · 版本不支持",
                    _ => $"存档 {i + 1} · 数据损坏"
                };
                slots[i].interactable = saved.Status != HowToFishLoadStatus.Corrupt && saved.Status != HowToFishLoadStatus.UnsupportedVersion;
                newGames[i].interactable = saved.Status == HowToFishLoadStatus.Ready || saved.Status == HowToFishLoadStatus.Empty;
            }
        }

        private void ContinueSlot(int index)
        {
            GlobalData.Dispatch(new HowToFishUiAction(world, HowToFishUiCommand.ContinueSlot, index));
        }

        private void NewSlot(int index) => GlobalData.Dispatch(new HowToFishUiAction(world, HowToFishUiCommand.NewSlot, index));

        protected override void OnDestroy()
        {
            Unbind();
            base.OnDestroy();
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
            Refresh();
        }
    }
}
