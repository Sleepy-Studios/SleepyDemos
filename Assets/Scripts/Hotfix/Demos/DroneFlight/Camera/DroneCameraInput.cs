using UnityEngine;
using UnityEngine.InputSystem;

namespace Hotfix.DroneFlight
{
    /// <summary>无人机视角的键盘适配器。</summary>
    [RequireComponent(typeof(DroneCameraRig))]
    public sealed class DroneCameraInput : MonoBehaviour
    {
        private DroneCameraRig cameraRig;

        private void Awake()
        {
            cameraRig = GetComponent<DroneCameraRig>();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (cameraRig == null || keyboard == null)
            {
                return;
            }

            var yaw = ReadAxis(keyboard.leftArrowKey.isPressed, keyboard.rightArrowKey.isPressed);
            var pitch = ReadAxis(keyboard.downArrowKey.isPressed, keyboard.upArrowKey.isPressed);
            cameraRig.ApplyLookInput(yaw, pitch, Time.unscaledDeltaTime);

            if (keyboard.cKey.wasPressedThisFrame)
            {
                if (cameraRig.Mode != DroneCameraMode.HarpoonAim)
                {
                    var publicModeCount = (int)DroneCameraMode.HarpoonAim;
                    cameraRig.SetMode((DroneCameraMode)(((int)cameraRig.Mode + 1) % publicModeCount));
                }
            }

            if (keyboard.equalsKey.isPressed)
            {
                cameraRig.AdjustFieldOfView(-30f * Time.unscaledDeltaTime);
            }
            else if (keyboard.minusKey.isPressed)
            {
                cameraRig.AdjustFieldOfView(30f * Time.unscaledDeltaTime);
            }
        }

        private static float ReadAxis(bool negative, bool positive)
        {
            return (positive ? 1f : 0f) - (negative ? 1f : 0f);
        }
    }
}
