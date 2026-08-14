using UnityEngine;

namespace Hotfix.DroneFlight
{
    internal enum DroneGrappleState { Open, Closing, Contacting, AssistedGrip, Releasing, Broken }

    /// <summary>旧六爪抓钩兼容壳；新四爪模块由 DroneGrappleModule 实现。</summary>
    public sealed class DroneMechanicalHook : MonoBehaviour
    {
        internal bool IsClosed { get; private set; }
        internal DroneGrappleState State { get; private set; } = DroneGrappleState.Open;
        internal bool CloseAndTryAttach()
        {
            IsClosed = true;
            State = DroneGrappleState.Closing;
            return false;
        }
        internal void OpenAndRelease() => ResetOpen();
        internal void ResetOpen()
        {
            IsClosed = false;
            State = DroneGrappleState.Open;
        }
        internal void ShowHint(string message) { }
    }
}
