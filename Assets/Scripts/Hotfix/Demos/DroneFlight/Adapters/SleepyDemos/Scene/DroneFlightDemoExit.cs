using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hotfix.DroneFlight.Adapters.SleepyDemos
{
    /// <summary>在 DroneFlight 场景中按 Backspace 返回 Hub。</summary>
    public sealed class DroneFlightDemoExit : MonoBehaviour
    {
        internal event Action ExitRequested;

        private void Update()
        {
            if (Keyboard.current == null || !Keyboard.current.backspaceKey.wasPressedThisFrame)
            {
                return;
            }

            ExitRequested?.Invoke();
        }
    }
}
