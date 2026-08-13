using UnityEngine;
using UnityEngine.InputSystem;

namespace Hotfix.DroneFlight
{
    /// <summary>机械抓钩键盘输入适配器。</summary>
    public sealed class DroneHookInput : MonoBehaviour
    {
        [SerializeField] private DroneWinchController winch;
        [SerializeField] private DroneMechanicalHook hook;
        [SerializeField] private DroneLandingGearController landingGear;
        [SerializeField] private DroneRemoteControllerExperience controlSession;

        private void Awake()
        {
            hook ??= GetComponent<DroneMechanicalHook>();
        }

        internal void Configure(
            DroneMechanicalHook mechanicalHook,
            DroneWinchController winchController,
            DroneLandingGearController gear = null,
            DroneRemoteControllerExperience session = null)
        {
            hook = mechanicalHook;
            winch = winchController;
            landingGear = gear;
            controlSession = session;
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || hook == null
                || (controlSession != null && controlSession.State != DroneControlSessionState.Active))
            {
                return;
            }

            if (keyboard.lKey.wasPressedThisFrame)
            {
                landingGear?.Toggle();
            }

            if (keyboard.jKey.wasPressedThisFrame)
            {
                winch?.Toggle();
            }

            if (!keyboard.hKey.wasPressedThisFrame)
            {
                return;
            }

            if (winch == null || winch.State is DroneWinchState.Stowed or DroneWinchState.Deploying or DroneWinchState.Retracting)
            {
                hook.ShowHint("请先按 J 放出抓斗");
            }
            else if (hook.IsClosed)
            {
                hook.OpenAndRelease();
            }
            else
            {
                hook.CloseAndTryAttach();
            }
        }
    }
}
