using UnityEngine;

namespace Hotfix.DroneFlight
{
    /// 只把统一输入帧交给现有相机，不再自行识别设备。
    [RequireComponent(typeof(DroneCameraRig))]
    public sealed class DroneCameraInput : MonoBehaviour
    {
        private DroneCameraRig cameraRig;
        private DronePlayerInput input;
        private void Awake() { cameraRig = GetComponent<DroneCameraRig>(); input = GetComponent<DronePlayerInput>(); }
        private void Update()
        {
            if (input == null || !input.AcceptsFlightInput) return;
            cameraRig.ApplyLookInput(input.CameraLook.x, input.CameraLook.y, Time.unscaledDeltaTime);
            if (input.Pressed("SwitchCamera")) input.SwitchCamera();
            cameraRig.AdjustFieldOfView(-30 * input.ZoomInput * Time.unscaledDeltaTime);
        }
    }
}
