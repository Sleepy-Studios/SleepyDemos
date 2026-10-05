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
            menu.Canceled += Continue; AddBinding(() => menu.Canceled -= Continue);
            resume.onClick.AddListener(Continue);
            save.onClick.AddListener(() => world?.Save());
            back.onClick.AddListener(() => world?.ReturnToHub());
            settingsButton.onClick.AddListener(() => world?.UI.OpenSettings());
            outfitButton.onClick.AddListener(() => world?.UI.OpenOutfits());
            foreach (var button in gameObject.GetComponentsInChildren<Button>(true)) button.onClick.AddListener(() => world?.PlayUiSound());
        }
        /// <summary>显示前绑定暂停或结局的公共动作。</summary>
        /// <param name="owner">当前世界。</param>
        public void SetData(HowToFishWorld owner) => world = owner;
        /// 隐藏时释放取消回调。
        private void Unbind() => world = null;
        private void Continue() { if (world?.ShowEnding == true) world.ContinueAfterEnding(); else world?.SetPaused(false); }
        protected override void OnDestroy() { Unbind(); base.OnDestroy(); }
        protected override void OnHide() { Unbind(); base.OnHide(); }
        private void OnData(HowToFishData value) { world = value.Scene; menuTitle.text = world.ShowEnding ? "航程完成\n<size=22>已经返回大陆 · 可继续探索</size>" : "已暂停"; save.gameObject.SetActive(!world.ShowEnding); }
    }
}
