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
    [UIBind("HowToFishPauseView")]
    public sealed partial class HowToFishPauseView : View
    {
        private TextMeshProUGUI menuTitle;

        private Button resume;

        private Button save;

        private Button back;

        private Button outfitButton;

        private Button settingsButton;

        private HowToFishWorld world;

        private HowToFishData data;

        private UIMenuScope menu;

        protected override void OnGameObjectInitialize()
        {
            BindData<HowToFishData>(OnData);
            menuTitle = TextMeshProUGUI_MenuTitle;
            resume = Button_Resume;
            save = Button_Save;
            back = Button_ReturnHub;
            outfitButton = Button_OpenOutfits;
            settingsButton = Button_OpenSettings;
            menu = gameObject.GetComponent<UIMenuScope>();
            menu.Canceled += Continue;
            AddBinding(() => menu.Canceled -= Continue);
            resume.onClick.AddListener(Continue);
            save.onClick.AddListener(() => GlobalData.Dispatch(new HowToFishSaveAction(world)));
            back.onClick.AddListener(() => GlobalData.Dispatch(new HowToFishUiAction(world, HowToFishUiCommand.Exit)));
            settingsButton.onClick.AddListener(() => GlobalData.Dispatch(new HowToFishUiAction(world, HowToFishUiCommand.OpenSettings)));
            outfitButton.onClick.AddListener(() => GlobalData.Dispatch(new HowToFishUiAction(world, HowToFishUiCommand.OpenOutfits)));
            foreach (var button in gameObject.GetComponentsInChildren<Button>(true))
                button.onClick.AddListener(() => world?.PlayUiSound());
        }

        /// <summary>显示前绑定暂停或结局的公共动作。</summary>
        /// <param name="owner">当前世界。</param>
        public void SetData(HowToFishWorld owner)
        {
            world = owner;
            data = owner?.Data;
        }

        // 页面隐藏后不再接受当前世界的按钮操作。

        private void Unbind()
        {
            world = null;
            data = null;
        }

        private void Continue()
        {
            if (world?.ShowEnding == true)
                GlobalData.Dispatch(new HowToFishContinueAfterEndingAction(world));
            else
                GlobalData.Dispatch(new HowToFishSetPausedAction(world, false));
        }

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
            menuTitle.text = data.ShowEnding ? "航程完成\n<size=22>已经返回大陆 · 可继续探索</size>" : "已暂停";
            save.gameObject.SetActive(!data.ShowEnding);
        }
    }
}
