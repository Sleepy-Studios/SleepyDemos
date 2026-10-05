using Core.Runtime;
using Hotfix.HowToFish;
namespace Hotfix
{
    [Module("HowToFish")] [UIBind("HowToFishJournalView")]
    public sealed partial class HowToFishJournalView : View<HowToFishWorld>
    {
        protected override void OnShow() { base.OnShow(); HowToFishJournalPresenter_HowToFishJournalView.Bind(params1); }
        protected override void OnHide() { HowToFishJournalPresenter_HowToFishJournalView.Unbind(); base.OnHide(); }
        protected override void OnDestroy() { if (HowToFishJournalPresenter_HowToFishJournalView != null) HowToFishJournalPresenter_HowToFishJournalView.Unbind(); base.OnDestroy(); }
    }
}
