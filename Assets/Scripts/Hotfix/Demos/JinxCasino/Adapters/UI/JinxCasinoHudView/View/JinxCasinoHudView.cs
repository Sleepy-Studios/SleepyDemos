using Core.Runtime;
using Hotfix.JinxCasino.Adapters;
using Hotfix.JinxCasino.Adapters.UI;

namespace Hotfix
{
    [Module("JinxCasino")]
    [Mvc("JinxCasinoHudView")]
    public sealed partial class JinxCasinoHudView : View<JinxCasinoController>
    {
        private JinxCasinoHudPresenter presenter;
        protected override void OnShow()
        {
            base.OnShow();
            // ComponentItemIndex 的稳定类型绑定由 Builder / MvcBind 生成，不在这里按节点路径查找。
            presenter = JinxCasinoHudPresenter_JinxCasinoHudView;
            presenter.Bind(params1);
        }
        protected override void OnHide() { presenter?.Unbind(); base.OnHide(); }
        protected override void OnDestroy() { presenter?.Unbind(); base.OnDestroy(); }
    }
}
