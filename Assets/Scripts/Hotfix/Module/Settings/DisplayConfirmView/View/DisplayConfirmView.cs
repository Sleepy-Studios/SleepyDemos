using Core.Runtime;

namespace Hotfix
{
    /// 独立 Modal，寿命不依赖即将因语言提交而销毁的旧设置页。
    [Module("Settings")]
    [UIBind("DisplayConfirmView")]
    public partial class DisplayConfirmView : View
    {
        protected override void OnGameObjectInitialize()
        {
            DisplayConfirmScreen_Screen.Initialize();
            BindUpdate(DisplayConfirmScreen_Screen.Tick);
        }

        /// <summary>显示前交付共享确认请求。</summary>
        /// <param name="request">页面外拥有的确认完成源。</param>
        public void SetData(DisplayConfirmationRequest request) => DisplayConfirmScreen_Screen.Bind(request);

        protected override void OnHide()
        {
            DisplayConfirmScreen_Screen.Revert();
            base.OnHide();
        }
        protected override void OnDestroy()
        {
            if (DisplayConfirmScreen_Screen != null)
            {
                DisplayConfirmScreen_Screen.Revert();
            }
            base.OnDestroy();
        }
    }
}
