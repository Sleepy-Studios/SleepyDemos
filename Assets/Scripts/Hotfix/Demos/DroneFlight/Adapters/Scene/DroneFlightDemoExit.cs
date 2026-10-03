using System;
using Core.Runtime.Inputs;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hotfix.DroneFlight.Adapters
{
    /// 机型选择期间有独立退出入口；生成机体后复用当前输入会话。
    public sealed class DroneFlightDemoExit : MonoBehaviour
    {
        [SerializeField] private InputActionAsset actions;
        private InputActionSession selectionInput;
        private DronePlayerInput input;
        internal event Action ExitRequested;
        private void Awake()
        {
            if (actions != null) { selectionInput = new InputActionSession(actions); selectionInput.SetMap("Waiting"); }
        }
        internal void ConfigureInput(DronePlayerInput value) { input = value; selectionInput?.SetMap(value == null ? "Waiting" : null); }
        private void Update()
        {
            if (input != null ? input.Pressed("Exit") : selectionInput?.Pressed("Exit") == true) ExitRequested?.Invoke();
        }
        private void OnDestroy() => selectionInput?.Dispose();
    }
}
