using Core.Runtime;
using Hotfix.JinxCasino;
namespace Hotfix
{
    [Module("JinxCasino")] [UIBind("JinxCasinoSaveView")]
    public sealed partial class JinxCasinoSaveView : View<JinxCasinoController>
    {
        protected override void OnShow() { base.OnShow(); JinxCasinoSavePresenter_JinxCasinoSaveView.Bind(params1); }
        protected override void OnHide() { JinxCasinoSavePresenter_JinxCasinoSaveView.Unbind(); base.OnHide(); }
        protected override void OnDestroy() { if (JinxCasinoSavePresenter_JinxCasinoSaveView != null) JinxCasinoSavePresenter_JinxCasinoSaveView.Unbind(); base.OnDestroy(); }
    }
}
