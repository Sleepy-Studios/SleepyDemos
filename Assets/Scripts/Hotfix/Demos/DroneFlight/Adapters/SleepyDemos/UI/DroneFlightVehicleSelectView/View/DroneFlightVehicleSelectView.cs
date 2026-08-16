using System;
using Core.Runtime;

namespace Hotfix
{
    using DroneFlight;

    public sealed class DroneFlightVehicleSelectionData
    {
        public DroneFlightVehicleSelectionData(Action<DroneVehicleKind> onSelected)
        {
            OnSelected = onSelected;
        }

        public Action<DroneVehicleKind> OnSelected { get; }
    }

    [Module("DroneFlight")]
    [Mvc("DroneFlightVehicleSelectView")]
    public partial class DroneFlightVehicleSelectView : View<DroneFlightVehicleSelectionData>
    {
        protected override void OnShow()
        {
            base.OnShow();
        }
    
        private void OnPlainButtonClick()
        {
            params1?.OnSelected?.Invoke(DroneVehicleKind.Plain);
        }

        private void OnGrappleButtonClick()
        {
            params1?.OnSelected?.Invoke(DroneVehicleKind.Grapple);
        }

        private void OnHarpoonButtonClick()
        {
            params1?.OnSelected?.Invoke(DroneVehicleKind.Harpoon);
        }
    }
}
