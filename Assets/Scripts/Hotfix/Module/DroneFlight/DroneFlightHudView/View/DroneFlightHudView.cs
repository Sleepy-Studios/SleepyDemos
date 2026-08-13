namespace Hotfix
{
    using Core.Runtime;
    using DroneFlight;

    [Module("DroneFlight")]
    [Mvc("DroneFlightHudView")]
    public partial class DroneFlightHudView : View
    {
        protected override void OnGameObjectInitialize()
        {
        }

        protected override void OnShow()
        {
            base.OnShow();
            gameObject.GetComponent<DroneHudPresenter>()?.BindContext(DroneFlightSceneContext.Current);
        }
    }
}
