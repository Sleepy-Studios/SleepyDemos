using System;
using Core.Runtime;
using Cysharp.Threading.Tasks;
using UnityEngine;
using System.Threading;

namespace Hotfix.DroneFlight
{
    /// <summary>仅通过正式 UIManager 管理当前 DroneFlight 会话拥有的 View 实例。</summary>
    public sealed class DroneFlightUIController : MonoBehaviour
    {
        private DroneFlightViewData viewData;
        private DroneFlightVehicleSelectView vehicleSelectView;
        private DroneFlightVehicleSelectionData selectionData;
        private DroneFlightHudView hudView;
        private DroneFlightDebugView debugView;
        private DroneFlightHelpView helpView;
        private bool helpRequested;
        private bool helpChanging;
        private bool debugChanging;
        private CancellationTokenSource overlayLifetime = new();
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
        private void OnDestroy()
        {
            isShuttingDown = true;
            overlayLifetime.Cancel(); overlayLifetime.Dispose();
            input?.SetHelpOpen(false);
            input?.SetDebugOpen(false);
            ConfigureInput(null);
        }
        private void OnPresentation(string command)
        {
            if (isShuttingDown || viewData == null) return;
            switch (command)
            {
                case "Help":
                    helpRequested = !helpRequested;
                    if (!helpChanging) UpdateHelpAsync().Forget();
                    break;
                case "DebugDraw": ToggleDebugDraw(); break;
                case "DebugPanel": ToggleDebugPanelAsync().Forget(); break;
                case "CopyTelemetry": input.GetComponent<DroneTelemetryRecorder>()?.CopySummary(); break;
            }
        }

        internal async UniTask<bool> ShowVehicleSelectAsync(
            Action<DroneVehicleKind> onSelected, Action onBack, CancellationToken cancellationToken)
        {
            selectionData = new DroneFlightVehicleSelectionData(onSelected, onBack);
            return await RestoreVehicleSelectAsync(cancellationToken);
        }

        private async UniTask<bool> RestoreVehicleSelectAsync(CancellationToken cancellationToken = default)
        {
            isShuttingDown = false;
            var result = await UIManager.Instance.ShowAsync<DroneFlightVehicleSelectView,
                DroneFlightVehicleSelectionData>(
                selectionData,
                new UIShowOptions(animated: true, hidePrevious: false),
                cancellationToken);
            if (result.Status == UIOperationStatus.Canceled)
            {
                return false;
            }

            if (result.Status is not UIOperationStatus.Succeeded and not UIOperationStatus.Ignored)
            {
                Debug.LogError($"[DroneFlight] 无法打开机型选择：{result.Exception?.Message ?? result.Status.ToString()}", this);
                return false;
            }

            vehicleSelectView = result.View as DroneFlightVehicleSelectView;
            return vehicleSelectView != null;
        }

        internal void SetSelectionFeedback(string message)
        {
            if (selectionData != null) selectionData.Feedback = message;
            vehicleSelectView?.SetBusy(false, message);
        }

        internal async UniTask CompleteVehicleSelectAsync()
        {
            var result = await UIManager.Instance.CloseAsync(vehicleSelectView, false);
            if (result.Status is not (UIOperationStatus.Succeeded or UIOperationStatus.Ignored))
                throw result.Exception ?? new InvalidOperationException("机型选择页关闭失败。");
            vehicleSelectView = null;
            selectionData = null;
        }

        internal async UniTask ClearFlightViewsAsync()
        {
            await StopOverlaysAsync();
            if (debugDrawRenderer != null) debugDrawRenderer.enabled = false;
            await CloseExpectedAsync(helpView);
            await CloseExpectedAsync(debugView);
            await CloseExpectedAsync(hudView);
            debugView = null;
            helpView = null;
            isDebugPanelVisible = false;
            hudView = null;
            viewData = null;
            debugDrawRenderer = null;
            ConfigureInput(null);
        }

        internal async UniTask<bool> ShowFlightViewsAsync(
            DroneFlightUiTelemetrySource telemetrySource,
            DroneFlightDebugDrawRenderer renderer,
            string sessionId)
        {
            isShuttingDown = false;
            RenewOverlayLifetime();
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
            await StopOverlaysAsync();
            if (debugDrawRenderer != null) debugDrawRenderer.enabled = false;
            await CloseExpectedAsync(helpView);
            await CloseExpectedAsync(vehicleSelectView);
            await CloseExpectedAsync(debugView);
            await CloseExpectedAsync(hudView);
            vehicleSelectView = null;
            debugView = null;
            helpView = null;
            hudView = null;
            isDebugPanelVisible = false;
            isDebugDrawVisible = false;
        }

        internal async UniTask<bool> RestoreFlightViewsAsync()
        {
            if (viewData == null)
            {
                return selectionData != null && await RestoreVehicleSelectAsync();
            }

            isShuttingDown = false;
            RenewOverlayLifetime();
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

        private async UniTask ToggleDebugPanelAsync()
        {
            if (debugChanging) return;
            debugChanging = true;
            try
            {
                if (isDebugPanelVisible)
                {
                    var close = await UIManager.Instance.CloseAsync(debugView, false);
                    if (close.Status is UIOperationStatus.Succeeded or UIOperationStatus.Ignored or UIOperationStatus.Canceled)
                    {
                        debugView = null;
                        isDebugPanelVisible = false;
                        input?.SetDebugOpen(false);
                    }
                    return;
                }

                input?.SetDebugOpen(true);
                var show = await UIManager.Instance.ShowAsync<DroneFlightDebugView, DroneFlightViewData>(
                    viewData,
                    new UIShowOptions(animated: false, hidePrevious: false), overlayLifetime.Token);
                if (isShuttingDown)
                {
                    await CloseExpectedAsync(show.View);
                    return;
                }
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
            finally
            {
                if (isShuttingDown || debugView == null) input?.SetDebugOpen(false);
                debugChanging = false;
            }
        }

        private async UniTask UpdateHelpAsync()
        {
            helpChanging = true;
            var owner = input;
            try
            {
                while (!isShuttingDown && helpRequested != (helpView != null))
                {
                    if (helpRequested)
                    {
                        owner?.SetHelpOpen(true);
                        var show = await UIManager.Instance.ShowAsync<DroneFlightHelpView, DroneFlightViewData>(
                            viewData, new UIShowOptions(animated: false, hidePrevious: false), overlayLifetime.Token);
                        if (isShuttingDown) { await CloseExpectedAsync(show.View); break; }
                        if (show.Status is UIOperationStatus.Succeeded or UIOperationStatus.Ignored)
                            helpView = show.View as DroneFlightHelpView;
                        else
                        {
                            helpRequested = false;
                            owner?.SetHelpOpen(false);
                            if (show.Status == UIOperationStatus.Failed)
                                Debug.LogError($"[DroneFlight] 操作指南打开失败：{show.Exception?.Message}", this);
                        }
                    }
                    else
                    {
                        var close = await UIManager.Instance.CloseAsync(helpView, false);
                        if (close.Status == UIOperationStatus.Failed)
                        {
                            helpRequested = true;
                            Debug.LogError($"[DroneFlight] 操作指南关闭失败：{close.Exception?.Message}", this);
                            break;
                        }
                        helpView = null;
                        owner?.SetHelpOpen(false);
                    }
                }
            }
            finally
            {
                if (isShuttingDown || helpView == null) owner?.SetHelpOpen(false);
                helpChanging = false;
            }
        }

        private async UniTask StopOverlaysAsync()
        {
            isShuttingDown = true;
            helpRequested = false;
            overlayLifetime.Cancel();
            await UniTask.WaitUntil(() => !helpChanging && !debugChanging);
            input?.SetHelpOpen(false);
            input?.SetDebugOpen(false);
        }

        private void RenewOverlayLifetime()
        {
            if (!overlayLifetime.IsCancellationRequested) return;
            overlayLifetime.Dispose(); overlayLifetime = new CancellationTokenSource();
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
