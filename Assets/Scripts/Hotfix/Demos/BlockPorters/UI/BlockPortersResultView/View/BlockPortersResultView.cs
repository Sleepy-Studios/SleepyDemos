using Core.Runtime;
using Hotfix.BlockPorters;
namespace Hotfix
{
    [Module("BlockPorters")]
    [Mvc("BlockPortersResultView")]
    public sealed partial class BlockPortersResultView : View<BlockPortersController>
    {
        protected override void OnShow() { base.OnShow(); BlockPortersResultPresenter_BlockPortersResultView.Bind(params1); }
        protected override void OnHide() { BlockPortersResultPresenter_BlockPortersResultView.Unbind(); base.OnHide(); }
        protected override void OnDestroy()
        {
            if (BlockPortersResultPresenter_BlockPortersResultView != null) BlockPortersResultPresenter_BlockPortersResultView.Unbind();
            base.OnDestroy();
        }
    }
}
