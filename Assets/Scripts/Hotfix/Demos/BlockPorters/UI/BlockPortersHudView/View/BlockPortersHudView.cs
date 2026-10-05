using Core.Runtime;
using Hotfix.BlockPorters;

namespace Hotfix
{
    [Module("BlockPorters")]
    [Mvc("BlockPortersHudView")]
    public sealed partial class BlockPortersHudView : View<BlockPortersController>
    {
        private BlockPortersHudPresenter presenter;
        protected override void OnShow()
        {
            base.OnShow();
            presenter = BlockPortersHudPresenter_BlockPortersHudView;
            presenter.Bind(params1);
        }
        protected override void OnHide() { presenter.Unbind(); base.OnHide(); }
        protected override void OnDestroy() { if (presenter != null) presenter.Unbind(); base.OnDestroy(); }
    }
}
