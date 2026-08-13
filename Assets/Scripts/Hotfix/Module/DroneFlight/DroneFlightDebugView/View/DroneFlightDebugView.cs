namespace Hotfix
{
    using Core.Runtime;
    using DroneFlight;

    [Module("DroneFlight")]
    [Mvc("DroneFlightDebugView")]
    public partial class DroneFlightDebugView : View
    {
        protected override void OnGameObjectInitialize()
        {
        }

        protected override void OnShow()
        {
            base.OnShow();
            gameObject.GetComponent<DroneDebugPresenter>()?.BindContext(DroneFlightSceneContext.Current);
        }
    }
}
