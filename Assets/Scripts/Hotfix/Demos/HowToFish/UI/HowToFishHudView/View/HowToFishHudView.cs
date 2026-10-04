using Core.Runtime;
using Hotfix.HowToFish;

namespace Hotfix
{
    [Module("HowToFish")]
    [Mvc("HowToFishHudView")]
    public sealed partial class HowToFishHudView : View<HowToFishWorld>
    {
        protected override void OnShow()
        {
            base.OnShow();
            HowToFishHudPresenter_HowToFishHudView.Bind(params1);
        }

        protected override void OnHide()
        {
            HowToFishHudPresenter_HowToFishHudView.Unbind();
            base.OnHide();
        }

        protected override void OnDestroy()
        {
            if (HowToFishHudPresenter_HowToFishHudView != null) HowToFishHudPresenter_HowToFishHudView.Unbind();
            base.OnDestroy();
        }
    }
}
