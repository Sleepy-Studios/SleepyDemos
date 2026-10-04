using Core.Runtime;
using Hotfix.HowToFish;
namespace Hotfix
{
    [Module("HowToFish")] [Mvc("HowToFishPauseView")]
    public sealed partial class HowToFishPauseView : View<HowToFishWorld>
    {
        protected override void OnShow() { base.OnShow(); HowToFishPausePresenter_HowToFishPauseView.Bind(params1); }
        protected override void OnHide() { HowToFishPausePresenter_HowToFishPauseView.Unbind(); base.OnHide(); }
        protected override void OnDestroy() { if (HowToFishPausePresenter_HowToFishPauseView != null) HowToFishPausePresenter_HowToFishPauseView.Unbind(); base.OnDestroy(); }
    }
}
