using UnityEngine;
using UnityEngine.InputSystem;

namespace Hotfix.DroneFlight
{
    /// <summary>机械抓钩键盘输入适配器。</summary>
    [RequireComponent(typeof(DroneMechanicalHook))]
    public sealed class DroneHookInput : MonoBehaviour
    {
        private DroneMechanicalHook hook;

        private void Awake()
        {
            hook = GetComponent<DroneMechanicalHook>();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || hook == null || !keyboard.hKey.wasPressedThisFrame)
            {
                return;
            }

            if (hook.IsClosed)
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
