using Core.Runtime;
using Hotfix.BlockPorters;
namespace Hotfix
{
    [Module("BlockPorters")]
    [UIBind("BlockPortersSettingsView")]
    public sealed partial class BlockPortersSettingsView : View<BlockPortersController>
    {
        protected override void OnShow() { base.OnShow(); BlockPortersSettingsPresenter_BlockPortersSettingsView.Bind(params1); }
        protected override void OnHide() { BlockPortersSettingsPresenter_BlockPortersSettingsView.Unbind(); base.OnHide(); }
        protected override void OnDestroy()
        {
            if (BlockPortersSettingsPresenter_BlockPortersSettingsView != null) BlockPortersSettingsPresenter_BlockPortersSettingsView.Unbind();
            base.OnDestroy();
        }
    }
}
