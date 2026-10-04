using Core.Runtime;
using Hotfix.HowToFish;
namespace Hotfix
{
    [Module("HowToFish")] [Mvc("HowToFishSettingsView")]
    public sealed partial class HowToFishSettingsView : View<HowToFishWorld>
    {
        protected override void OnShow() { base.OnShow(); HowToFishSettingsPresenter_HowToFishSettingsView.Bind(params1); }
        protected override void OnHide() { HowToFishSettingsPresenter_HowToFishSettingsView.Unbind(); base.OnHide(); }
        protected override void OnDestroy() { if (HowToFishSettingsPresenter_HowToFishSettingsView != null) HowToFishSettingsPresenter_HowToFishSettingsView.Unbind(); base.OnDestroy(); }
    }
}
