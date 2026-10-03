using System;
using Core.Runtime;
using Cysharp.Threading.Tasks;
using Hotfix.DroneFlight;
using UnityEngine;
using System.Threading;

namespace Hotfix.DroneFlight.Adapters
{
    /// <summary>仅通过正式 UIManager 管理当前 DroneFlight 会话拥有的 View 实例。</summary>
    public sealed class DroneFlightUIController : MonoBehaviour
    {
        private DroneFlightViewData viewData;
        private DroneFlightVehicleSelectView vehicleSelectView;
        private DroneFlightHudView hudView;
        private DroneFlightDebugView debugView;
        private DroneFlightDebugDrawRenderer debugDrawRenderer;
        private bool isDebugPanelVisible;
        private bool isDebugDrawVisible;
        private bool isShuttingDown;

        private DronePlayerInput input;
        internal void ConfigureInput(DronePlayerInput value)
        {
            if (input != null) input.PresentationRequested -= OnPresentation;
            input = value;
            if (input != null) input.PresentationRequested += OnPresentation;
        }
        private void OnDestroy() => ConfigureInput(null);
        private void OnPresentation(string command)
        {
            if (isShuttingDown || viewData == null) return;
            switch (command)
            {
                case "Help": hudView?.ToggleControls(); break;
                case "DebugDraw": ToggleDebugDraw(); break;
                case "DebugPanel": ToggleDebugPanelAsync().Forget(); break;
                case "CopyTelemetry": input.GetComponent<DroneTelemetryRecorder>()?.CopySummary(); break;
            }
        }

        internal async UniTask<DroneVehicleKind?> ShowVehicleSelectAsync(
            CancellationToken cancellationToken = default)
        {
            var completion = new UniTaskCompletionSource<DroneVehicleKind>();
            var data = new DroneFlightVehicleSelectionData(value => completion.TrySetResult(value));
            var result = await UIManager.Instance.ShowAsync<DroneFlightVehicleSelectView,
                DroneFlightVehicleSelectionData>(
                data,
                new UIShowOptions(animated: true, hidePrevious: false),
                cancellationToken);
            if (result.Status == UIOperationStatus.Canceled)
            {
                return null;
            }

            if (result.Status is not UIOperationStatus.Succeeded and not UIOperationStatus.Ignored)
            {
                Debug.LogError($"[DroneFlight] 无法打开机型选择：{result.Exception?.Message ?? result.Status.ToString()}", this);
                return null;
            }

            vehicleSelectView = result.View as DroneFlightVehicleSelectView;
            try
            {
                var selection = await completion.Task.AttachExternalCancellation(
                    cancellationToken);
                await CloseExpectedAsync(vehicleSelectView);
                vehicleSelectView = null;
                return selection;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        internal async UniTask<bool> ShowFlightViewsAsync(
            DroneFlightUiTelemetrySource telemetrySource,
            DroneFlightDebugDrawRenderer renderer,
            string sessionId)
        {
            isShuttingDown = false;
            debugDrawRenderer = renderer;
            isDebugDrawVisible = false;
            if (debugDrawRenderer != null) debugDrawRenderer.enabled = false;
            viewData = new DroneFlightViewData(telemetrySource, sessionId, input);
            var result = await UIManager.Instance.ShowAsync<DroneFlightHudView, DroneFlightViewData>(
                viewData,
                new UIShowOptions(animated: false, hidePrevious: false));
            if (result.Status is UIOperationStatus.Succeeded or UIOperationStatus.Ignored)
            {
                hudView = result.View as DroneFlightHudView;
            hudView?.gameObject.GetComponent<DroneControlsPresenter>()?.Bind(input);
                return true;
            }

            if (result.Status == UIOperationStatus.Failed)
            {
                Debug.LogError($"[DroneFlight] HUD 打开失败：{result.Exception?.Message}", this);
            }

            return false;
        }

        internal async UniTask CloseOwnedViewsAsync()
        {
            isShuttingDown = true;
            if (debugDrawRenderer != null) debugDrawRenderer.enabled = false;
            await CloseExpectedAsync(vehicleSelectView);
            await CloseExpectedAsync(debugView);
            await CloseExpectedAsync(hudView);
            vehicleSelectView = null;
            debugView = null;
            hudView = null;
            isDebugPanelVisible = false;
            isDebugDrawVisible = false;
        }

        internal async UniTask<bool> RestoreFlightViewsAsync()
        {
            if (viewData == null)
            {
                return false;
            }

            isShuttingDown = false;
            var result = await UIManager.Instance.ShowAsync<DroneFlightHudView, DroneFlightViewData>(
                viewData,
                new UIShowOptions(animated: false, hidePrevious: false));
            hudView = result.View as DroneFlightHudView;
            hudView?.gameObject.GetComponent<DroneControlsPresenter>()?.Bind(input);
            return result.Status is UIOperationStatus.Succeeded or UIOperationStatus.Ignored;
        }

        private void ToggleDebugDraw()
        {
            isDebugDrawVisible = !isDebugDrawVisible;
            if (debugDrawRenderer != null) debugDrawRenderer.enabled = isDebugDrawVisible;
        }

        private async UniTaskVoid ToggleDebugPanelAsync()
        {
            if (isDebugPanelVisible)
            {
                var close = await UIManager.Instance.CloseAsync(debugView, false);
                if (close.Status is UIOperationStatus.Succeeded or UIOperationStatus.Ignored or UIOperationStatus.Canceled)
                {
                    debugView = null;
                    isDebugPanelVisible = false;
                }

                return;
            }

            var show = await UIManager.Instance.ShowAsync<DroneFlightDebugView, DroneFlightViewData>(
                viewData,
                new UIShowOptions(animated: false, hidePrevious: false));
            if (show.Status is UIOperationStatus.Succeeded or UIOperationStatus.Ignored)
            {
                debugView = show.View as DroneFlightDebugView;
                isDebugPanelVisible = true;
            }
            else if (show.Status == UIOperationStatus.Failed)
            {
                Debug.LogError($"[DroneFlight] F3 调试 View 打开失败：{show.Exception?.Message}", this);
            }
        }

        private static async UniTask CloseExpectedAsync(View view)
        {
            if (view != null)
            {
                await UIManager.Instance.CloseAsync(view, false);
            }
        }
    }

}
