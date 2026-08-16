using System;
using System.Threading;
using Core.Runtime;
using Cysharp.Threading.Tasks;
using Hotfix.DroneFlight;
using Hotfix.SceneManagement;
using UnityEngine;

namespace Hotfix.DroneFlight.Adapters
{
    /// <summary>场景级机型选择、单实例生成和会话切换协调器。</summary>
    public sealed class DroneFlightSceneCoordinator : MonoBehaviour
    {
        internal const string GrappleVariantAddress =
            "LoadResources/Demos/drone_flight/Prefabs/DroneGrappleVariant";
        internal const string HarpoonVariantAddress =
            "LoadResources/Demos/drone_flight/Prefabs/DroneHarpoonVariant";
        internal const string PlainDroneAddress =
            "LoadResources/Demos/drone_flight/Prefabs/DronePrototype";

        [SerializeField] private Camera playerCamera;
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private DroneFlightUIController uiController;
        [SerializeField] private DroneFlightDemoExit demoExit;

        private IResourceLoader resourceLoader;
        private GameObject currentDrone;
        private DronePlayerInput currentInput;
        private CancellationTokenSource lifetimeCancellation;
        private bool isChangingScene;
        private string sessionId;

        private void Awake()
        {
            lifetimeCancellation = new CancellationTokenSource();
            sessionId = Guid.NewGuid().ToString("N");
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
            if (currentInput != null)
            {
                currentInput.ReloadRequested -= HandleReloadRequested;
            }
            if (demoExit != null)
            {
                demoExit.ExitRequested -= HandleExitRequested;
            }

            if (currentDrone != null)
            {
                resourceLoader?.ReleaseInstance(currentDrone);
            }
            resourceLoader?.Dispose();
        }

        internal void Configure(Camera waitingCamera, Transform point, DroneFlightUIController controller)
        {
            playerCamera = waitingCamera;
            spawnPoint = point;
            uiController = controller;
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
            // UIController 是场景根对象上的宿主适配组件，不是 View 内部节点。
            uiController ??= GetComponent<DroneFlightUIController>();
            if (uiController == null)
            {
                Debug.LogError("[DroneFlight] 场景缺少 DroneFlightUIController。", this);
                return;
            }

            var selection = await uiController.ShowVehicleSelectAsync(
                new CancellationTokenProvider(cancellationToken));
            if (!selection.HasValue || cancellationToken.IsCancellationRequested)
            {
                return;
            }

            await SpawnSelectedAsync(selection.Value, cancellationToken);
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
                    Debug.LogError($"[DroneFlight] 无法实例化机型：{address}", this);
                    return;
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
                    remote.Configure(
                        playerCamera,
                        currentDrone.GetComponentInChildren<DroneCameraRig>(true),
                        currentDrone.GetComponent<DronePlayerInput>(),
                        currentDrone.GetComponent<DroneFlightController>(),
                        currentDrone.GetComponent<DroneEquipmentInput>());
                }
                if (!DroneFlightVehicleAssembler.TryPrepare(
                        currentDrone,
                        selection,
                        spawnPoint,
                        remote,
                        out var runtime,
                        out var assemblyError))
                {
                    Debug.LogError($"[DroneFlight] {assemblyError}", currentDrone);
                    return;
                }

                currentInput = runtime.Input;
                currentInput.ReloadRequested += HandleReloadRequested;
                runtime.Activate();
                await UniTask.Yield(PlayerLoopTiming.FixedUpdate, cancellationToken);
                if (remote != null)
                {
                    remote.enabled = true;
                }
                runtime.FinalizeAfterFirstPhysicsStep();
                await uiController.ShowFlightViewsAsync(runtime.Telemetry, runtime.DebugRenderer, sessionId);
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

        private void HandleExitRequested()
        {
            ChangeSceneAsync(reload: false).Forget();
        }

        private async UniTaskVoid ChangeSceneAsync(bool reload)
        {
            if (isChangingScene)
            {
                return;
            }

            var navigator = GameSceneNavigator.Instance;
            if (navigator == null)
            {
                Debug.LogError("[DroneFlight] 全局场景导航尚未初始化。", this);
                return;
            }

            isChangingScene = true;
            if (currentInput != null)
            {
                currentInput.enabled = false;
            }
            await uiController.CloseOwnedViewsAsync();
            var result = reload
                ? await navigator.ReloadCurrentAsync()
                : await navigator.SwitchAsync(GameSceneId.Hub);
            if (result.Status == GameSceneSwitchStatus.Failed)
            {
                isChangingScene = false;
                if (currentInput != null)
                {
                    currentInput.enabled = true;
                }
                await uiController.RestoreFlightViewsAsync();
                Debug.LogError(
                    reload
                        ? $"[DroneFlight] 重新运行场景失败：{result.Error}"
                        : $"[DroneFlight] 无法返回主界面：{result.Error}",
                    this);
            }
            else if (result.Status is GameSceneSwitchStatus.Busy or GameSceneSwitchStatus.Ignored)
            {
                isChangingScene = false;
                if (currentInput != null)
                {
                    currentInput.enabled = true;
                }
                await uiController.RestoreFlightViewsAsync();
            }
        }

    }
}
