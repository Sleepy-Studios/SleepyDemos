using Core.Runtime.Inputs;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hotfix.HowToFish
{
    /// 独立页面保存的业务显示与绑定，生命周期由对应 View 管理。
    public sealed class HowToFishPausePresenter : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI menuTitle;
        [SerializeField] private Button resume;
        [SerializeField] private Button save;
        [SerializeField] private Button back;
        [SerializeField] private Button outfitButton;
        [SerializeField] private Button settingsButton;
        private HowToFishWorld world;
        private UIMenuScope menu;
        private void Awake()
        {
            menu = GetComponent<UIMenuScope>();
            resume.onClick.AddListener(Continue);
            save.onClick.AddListener(() => world?.Save());
            back.onClick.AddListener(() => world?.ReturnToHub());
            settingsButton.onClick.AddListener(() => world?.UI.OpenSettings());
            outfitButton.onClick.AddListener(() => world?.UI.OpenOutfits());
            foreach (var button in GetComponentsInChildren<Button>(true)) button.onClick.AddListener(() => world?.PlayUiSound());
        }
        /// <summary>显示前绑定暂停或结局的公共动作。</summary>
        /// <param name="owner">当前世界。</param>
        public void Bind(HowToFishWorld owner)
        { Unbind(); world = owner; menu.Canceled += Continue; menuTitle.text = world.ShowEnding ? "航程完成\n<size=22>已经返回大陆 · 可继续探索</size>" : "已暂停"; save.gameObject.SetActive(!world.ShowEnding); }
        /// 隐藏时释放取消回调。
        public void Unbind() { if (menu != null) menu.Canceled -= Continue; world = null; }
        private void Continue() { if (world?.ShowEnding == true) world.ContinueAfterEnding(); else world?.SetPaused(false); }
        private void OnDestroy() => Unbind();
    }
}
