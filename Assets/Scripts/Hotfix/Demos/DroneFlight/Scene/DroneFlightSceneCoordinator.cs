using System;
using System.Threading;
using Core.Runtime;
using Cysharp.Threading.Tasks;
using Hotfix.SceneManagement;
using UnityEngine;

namespace Hotfix.DroneFlight
{
    /// <summary>场景级机型选择、单实例生成和会话切换协调器。</summary>
    public sealed class DroneFlightSceneCoordinator : MonoBehaviour
    {
        internal const string GrappleVariantAddress = "LoadResources/Demos/drone_flight/Prefabs/DroneGrappleVariant";

        internal const string HarpoonVariantAddress = "LoadResources/Demos/drone_flight/Prefabs/DroneHarpoonVariant";

        internal const string PlainDroneAddress = "LoadResources/Demos/drone_flight/Prefabs/DronePrototype";
        [SerializeField]
        private Camera playerCamera;
        [SerializeField]
        private Transform spawnPoint;
        [SerializeField]
        private DroneFlightDemoExit demoExit;

        private IResourceLoader resourceLoader;

        private GameObject currentDrone;

        private DronePlayerInput currentInput;

        private CancellationTokenSource lifetimeCancellation;

        private DroneFlightData data;
        private View selectionView;
        private View hudView;
        private DroneFlightViewData viewData;
        private bool hasSelection;

        private bool isChangingScene => data.Mode == DroneFlightSessionMode.Leaving;

        private bool isStarting => data.Mode == DroneFlightSessionMode.Loading;

        private string sessionId => data.SessionId;

        private void Awake()
        {
            lifetimeCancellation = new CancellationTokenSource();
            var previous = GlobalData.Get<DroneFlightData>();
            if (previous != null)
            {
                previous.Handler.Dispose();
                GlobalData.Remove<DroneFlightData>();
            }
            data = GlobalData.Add<DroneFlightData>();
            // 场景协调组件按同对象组合，生命周期固定但不属于 View Prefab 子节点。
            demoExit ??= GetComponent<DroneFlightDemoExit>();
            if (demoExit != null)
            {
                demoExit.ExitRequested += HandleExitRequested;
            }
        }

        private void Start()
        {
            BeginAsync(lifetimeCancellation.Token).Forget();
        }

        private void OnDestroy()
        {
            lifetimeCancellation?.Cancel();
            lifetimeCancellation?.Dispose();
            if (demoExit != null)
            {
                demoExit.ExitRequested -= HandleExitRequested;
            }

            ReleaseCurrentDrone();
            resourceLoader?.Dispose();
            data?.Handler.Dispose();
            foreach (var view in new[] { hudView, selectionView })
                if (view != null)
                    UIManager.Instance.CloseAsync(view, false).Forget();
            data?.ClearData();
            if (ReferenceEquals(GlobalData.Get<DroneFlightData>(), data))
                GlobalData.Remove<DroneFlightData>();
        }

        internal void Configure(Camera waitingCamera, Transform point)
        {
            playerCamera = waitingCamera;
            spawnPoint = point;
        }

        private async UniTaskVoid BeginAsync(CancellationToken cancellationToken)
        {
            if (!await DemoIslandEditorBootstrap.EnsureReadyAsync(cancellationToken))
            {
                Debug.LogError("[DroneFlight] Demo 运行时初始化失败，无法打开机型选择。", this);
                return;
            }

            var navigator = GameSceneNavigator.Instance;
            if (navigator == null)
            {
                Debug.LogError("[DroneFlight] 场景导航未初始化。", this);
                return;
            }

            await navigator.WaitUntilStableAsync(GameSceneId.DroneFlight, cancellationToken);
            data.Handler.ConfigureSelection(selection => StartSelectedAsync(selection).Forget(), () => ChangeSceneAsync(false).Forget());
            hasSelection = true;
            await ShowVehicleSelectAsync(cancellationToken);
        }

        private async UniTask StartSelectedAsync(DroneVehicleKind selection)
        {
            if (currentDrone != null || isChangingScene)
                return;
            int version = data.Version;
            var cancellationToken = lifetimeCancellation.Token;
            try
            {
                await SpawnSelectedAsync(selection, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                ReleaseCurrentDrone();
            }
            catch (Exception exception)
            {
                ReleaseCurrentDrone(!cancellationToken.IsCancellationRequested);
                if (cancellationToken.IsCancellationRequested)
                    return;
                Debug.LogError($"[DroneFlight] 机型准备失败：{exception.Message}", this);
                await ClearFlightViewsAsync();
                GlobalData.Dispatch(new DroneFlightFeedbackAction(data, "准备失败，请重试"));
            }
            finally
            {
                if (ReferenceEquals(GlobalData.Get<DroneFlightData>(), data))
                    GlobalData.Dispatch(new DroneFlightSelectionResultAction(version, currentDrone != null));
            }
        }

        private void ReleaseCurrentDrone(bool restoreCamera = false)
        {
            if (currentInput != null)
            {
                currentInput.ReloadRequested -= HandleReloadRequested;
                currentInput.ExitRequested -= HandleExitRequested;
                currentInput.enabled = false;
            }

            data?.Handler.AttachInput(null);
            currentInput = null;
            if (currentDrone != null)
            {
                if (restoreCamera)
                    currentDrone.GetComponent<DroneRemoteControllerExperience>()?.ReturnToWaiting();
                resourceLoader?.ReleaseInstance(currentDrone);
                currentDrone = null;
            }

            if (restoreCamera && demoExit != null)
                demoExit.ConfigureInput(null);
        }

        private async UniTask SpawnSelectedAsync(DroneVehicleKind selection, CancellationToken cancellationToken)
        {
            resourceLoader ??= ResourceServices.CreateLoader();
            var address = selection switch
            {
                DroneVehicleKind.Grapple => GrappleVariantAddress,
                DroneVehicleKind.Harpoon => HarpoonVariantAddress,
                _ => PlainDroneAddress
            };
            var stagingRoot = new GameObject($"DroneSpawnStaging_{sessionId}");
            stagingRoot.SetActive(false);
            try
            {
                currentDrone = await resourceLoader.InstantiateAsync(address, stagingRoot.transform, true);
                cancellationToken.ThrowIfCancellationRequested();
                if (currentDrone == null)
                {
                    throw new InvalidOperationException($"无法实例化机型：{address}");
                }

                currentDrone.name = selection switch
                {
                    DroneVehicleKind.Grapple => "DroneGrappleVariant",
                    DroneVehicleKind.Harpoon => "DroneHarpoonVariant",
                    _ => "DronePrototype"
                };
                // 机体和镜头均为本次运行时生成实例，在组合阶段一次性取得并传入装配器。
                var remote = currentDrone.GetComponent<DroneRemoteControllerExperience>();
                if (remote != null)
                {
                    remote.enabled = false;
                    remote.Configure(playerCamera, currentDrone.GetComponentInChildren<DroneCameraRig>(true), currentDrone.GetComponent<DronePlayerInput>(), currentDrone.GetComponent<DroneFlightController>(), currentDrone.GetComponent<DroneEquipmentInput>());
                }

                if (!DroneFlightVehicleAssembler.TryPrepare(currentDrone, selection, spawnPoint, remote, out var runtime, out var assemblyError))
                {
                    throw new InvalidOperationException(assemblyError);
                }

                currentInput = runtime.Input;
                currentInput.ReloadRequested += HandleReloadRequested;
                currentInput.ExitRequested += HandleExitRequested;
                data.Handler.AttachInput(currentInput);
                demoExit?.ConfigureInput(currentInput);
                runtime.Activate();
                await UniTask.Yield(PlayerLoopTiming.FixedUpdate, cancellationToken);
                if (!await ShowFlightViewsAsync(runtime.Telemetry, runtime.DebugRenderer, sessionId))
                    throw new InvalidOperationException("飞行界面未能准备完成。");
                cancellationToken.ThrowIfCancellationRequested();
                await CompleteVehicleSelectAsync();
                if (remote != null)
                {
                    remote.enabled = true;
                }

                runtime.FinalizeAfterFirstPhysicsStep();
            }
            finally
            {
                if (stagingRoot != null)
                {
                    Destroy(stagingRoot);
                }
            }
        }

        private void HandleReloadRequested()
        {
            ChangeSceneAsync(reload: true).Forget();
        }

        private void HandleExitRequested() => GlobalData.Dispatch(new DroneFlightExitAction());

        private async UniTaskVoid ChangeSceneAsync(bool reload)
        {
            var navigator = GameSceneNavigator.Instance;
            if (navigator == null)
            {
                Debug.LogError("[DroneFlight] 全局场景导航尚未初始化。", this);
                return;
            }

            GlobalData.Dispatch(new DroneFlightBeginLeavingAction(data));
            if (currentInput != null)
            {
                currentInput.enabled = false;
            }

            await CloseOwnedViewsAsync();
            var result = reload ? await navigator.ReloadCurrentAsync() : await navigator.SwitchAsync(GameSceneId.Hub);
            if (result.Status == GameSceneSwitchStatus.Failed)
            {
                GlobalData.Dispatch(new DroneFlightRestoreModeAction(data, currentDrone != null));
                if (currentInput != null)
                {
                    currentInput.enabled = true;
                }

                await RestoreFlightViewsAsync();
                GlobalData.Dispatch(new DroneFlightFeedbackAction(data, "返回失败，请重试"));
                Debug.LogError(reload ? $"[DroneFlight] 重新运行场景失败：{result.Error}" : $"[DroneFlight] 无法返回主界面：{result.Error}", this);
            }
            else if (result.Status is GameSceneSwitchStatus.Busy or GameSceneSwitchStatus.Ignored)
            {
                GlobalData.Dispatch(new DroneFlightRestoreModeAction(data, currentDrone != null));
                if (currentInput != null)
                {
                    currentInput.enabled = true;
                }

                await RestoreFlightViewsAsync();
            }
        }
        private async UniTask<bool> ShowVehicleSelectAsync(CancellationToken token = default)
        {
            GlobalData.Dispatch(new DroneFlightShuttingDownAction(data, false));
            var result = await UIManager.Instance.ShowAsync<DroneFlightVehicleSelectView>(new UIShowOptions(animated: true, hidePrevious: false), token);
            if (result.Status == UIOperationStatus.Failed)
                throw result.Exception;
            selectionView = result.View;
            return result.Status is UIOperationStatus.Succeeded or UIOperationStatus.Ignored;
        }

        private async UniTask CompleteVehicleSelectAsync()
        {
            await CloseViewAsync(selectionView);
            selectionView = null;
            hasSelection = false;
        }

        private async UniTask<bool> ShowFlightViewsAsync(DroneFlightUiTelemetrySource telemetry, DroneFlightDebugDrawRenderer renderer, string id)
        {
            viewData = new DroneFlightViewData(telemetry, id, currentInput);
            data.Handler.ConfigureViews(viewData, renderer, lifetimeCancellation.Token);
            var result = await UIManager.Instance.ShowAsync<DroneFlightHudView, DroneFlightViewData>(viewData,
                new UIShowOptions(animated: false, hidePrevious: false), lifetimeCancellation.Token);
            if (result.Status == UIOperationStatus.Failed)
                throw result.Exception;
            hudView = result.View;
            return result.Status is UIOperationStatus.Succeeded or UIOperationStatus.Ignored;
        }

        private async UniTask ClearFlightViewsAsync()
        {
            await data.Handler.CloseOverlaysAsync();
            await CloseViewAsync(hudView);
            hudView = null;
            viewData = null;
            data.Handler.AttachInput(null);
        }

        private async UniTask CloseOwnedViewsAsync()
        {
            await data.Handler.CloseOverlaysAsync();
            await CloseViewAsync(selectionView);
            await CloseViewAsync(hudView);
            selectionView = hudView = null;
        }

        private async UniTask<bool> RestoreFlightViewsAsync()
        {
            if (viewData == null)
                return hasSelection && await ShowVehicleSelectAsync(lifetimeCancellation.Token);
            GlobalData.Dispatch(new DroneFlightShuttingDownAction(data, false));
            var result = await UIManager.Instance.ShowAsync<DroneFlightHudView, DroneFlightViewData>(viewData,
                new UIShowOptions(animated: false, hidePrevious: false), lifetimeCancellation.Token);
            if (result.Status == UIOperationStatus.Failed)
                throw result.Exception;
            hudView = result.View;
            return result.Status is UIOperationStatus.Succeeded or UIOperationStatus.Ignored;
        }

        private static async UniTask CloseViewAsync(View view)
        {
            if (view == null)
                return;
            var result = await UIManager.Instance.CloseAsync(view, false);
            if (result.Status == UIOperationStatus.Failed)
                throw result.Exception;
        }
    }
}
