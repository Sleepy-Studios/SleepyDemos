using Core.Runtime;
using Hotfix.HowToFish;
namespace Hotfix
{
    [Module("HowToFish")] [Mvc("HowToFishMainMenuView")]
    public sealed partial class HowToFishMainMenuView : View<HowToFishWorld>
    {
        protected override void OnShow() { base.OnShow(); HowToFishMainMenuPresenter_HowToFishMainMenuView.Bind(params1); }
        protected override void OnHide() { HowToFishMainMenuPresenter_HowToFishMainMenuView.Unbind(); base.OnHide(); }
        protected override void OnDestroy() { if (HowToFishMainMenuPresenter_HowToFishMainMenuView != null) HowToFishMainMenuPresenter_HowToFishMainMenuView.Unbind(); base.OnDestroy(); }
    }
}
