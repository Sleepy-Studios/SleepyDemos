using Core.Runtime;
using Hotfix.JinxCasino;
namespace Hotfix
{
    [Module("JinxCasino")] [UIBind("JinxCasinoSettingsView")]
    public sealed partial class JinxCasinoSettingsView : View<JinxCasinoController>
    {
        protected override void OnShow() { base.OnShow(); JinxCasinoSettingsPresenter_JinxCasinoSettingsView.Bind(params1); }
        protected override void OnHide() { JinxCasinoSettingsPresenter_JinxCasinoSettingsView.Unbind(); base.OnHide(); }
        protected override void OnDestroy() { if (JinxCasinoSettingsPresenter_JinxCasinoSettingsView != null) JinxCasinoSettingsPresenter_JinxCasinoSettingsView.Unbind(); base.OnDestroy(); }
        public void CancelPreview() => JinxCasinoSettingsPresenter_JinxCasinoSettingsView.CancelPreview();
    }
}
