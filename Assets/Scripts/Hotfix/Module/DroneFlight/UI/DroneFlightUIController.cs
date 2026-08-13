using Core.Runtime;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hotfix.DroneFlight
{
    /// <summary>在 DroneFlight 场景生命周期内通过正式 UIManager 管理 HUD 与调试 View。</summary>
    public sealed class DroneFlightUIController : MonoBehaviour
    {
        private bool isDebugVisible;
        private bool isShuttingDown;

        private void Start()
        {
            ShowHudAsync().Forget();
        }

        private void Update()
        {
            if (isShuttingDown || Keyboard.current == null || !Keyboard.current.f3Key.wasPressedThisFrame)
            {
                return;
            }

            ToggleDebugAsync().Forget();
        }

        private void OnDestroy()
        {
            isShuttingDown = true;
            CloseViewsAsync().Forget();
        }

        private async UniTaskVoid ShowHudAsync()
        {
            var result = await UIManager.Instance.ShowAsync<DroneFlightHudView>(
                new UIShowOptions(animated: false, hidePrevious: false));
            if (result.Status is not UIOperationStatus.Succeeded and not UIOperationStatus.Ignored)
            {
                Debug.LogError($"[DroneFlight] 无法打开正式 HUD：{result.Exception?.Message ?? result.Status.ToString()}", this);
            }
        }

        private async UniTaskVoid ToggleDebugAsync()
        {
            if (isDebugVisible)
            {
                var close = await UIManager.Instance.CloseAsync<DroneFlightDebugView>(false);
                if (close.Status is UIOperationStatus.Succeeded or UIOperationStatus.Ignored or UIOperationStatus.Canceled)
                {
                    isDebugVisible = false;
                }

                return;
            }

            var show = await UIManager.Instance.ShowAsync<DroneFlightDebugView>(
                new UIShowOptions(animated: false, hidePrevious: false));
            if (show.Status is UIOperationStatus.Succeeded or UIOperationStatus.Ignored)
            {
                isDebugVisible = true;
            }
        }

        private async UniTaskVoid CloseViewsAsync()
        {
            await UIManager.Instance.CloseAsync<DroneFlightDebugView>(false);
            await UIManager.Instance.CloseAsync<DroneFlightHudView>(false);
        }
    }
}
