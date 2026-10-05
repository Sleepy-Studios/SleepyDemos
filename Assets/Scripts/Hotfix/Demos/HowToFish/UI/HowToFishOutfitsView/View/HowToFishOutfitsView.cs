using Core.Runtime;
using Hotfix.HowToFish;
namespace Hotfix
{
    [Module("HowToFish")] [UIBind("HowToFishOutfitsView")]
    public sealed partial class HowToFishOutfitsView : View<HowToFishWorld>
    {
        protected override void OnShow() { base.OnShow(); HowToFishOutfitsPresenter_HowToFishOutfitsView.Bind(params1); }
        protected override void OnHide() { HowToFishOutfitsPresenter_HowToFishOutfitsView.Unbind(); base.OnHide(); }
        protected override void OnDestroy() { if (HowToFishOutfitsPresenter_HowToFishOutfitsView != null) HowToFishOutfitsPresenter_HowToFishOutfitsView.Unbind(); base.OnDestroy(); }
    }
}
