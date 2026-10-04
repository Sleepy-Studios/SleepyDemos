using Core.Runtime;
using Hotfix.HowToFish;
namespace Hotfix
{
    [Module("HowToFish")] [Mvc("HowToFishEndingView")]
    public sealed partial class HowToFishEndingView : View<HowToFishWorld>
    {
        protected override void OnShow() { base.OnShow(); HowToFishPausePresenter_HowToFishEndingView.Bind(params1); }
        protected override void OnHide() { HowToFishPausePresenter_HowToFishEndingView.Unbind(); base.OnHide(); }
        protected override void OnDestroy() { if (HowToFishPausePresenter_HowToFishEndingView != null) HowToFishPausePresenter_HowToFishEndingView.Unbind(); base.OnDestroy(); }
    }
}
