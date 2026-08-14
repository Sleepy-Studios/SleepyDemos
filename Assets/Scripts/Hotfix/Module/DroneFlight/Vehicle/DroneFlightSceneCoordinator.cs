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
                currentDrone.transform.localScale = Vector3.one;
                if (!DroneSpawnPlacement.TryPlaceOnGround(
                        currentDrone,
                        spawnPoint,
                        DroneSpawnPlacement.DefaultGroundClearanceMeters,
                        out _))
                {
                    Debug.LogError("[DroneFlight] 无法从四个起落架脚部计算安全出生高度。", currentDrone);
                    return;
                }

                var controller = currentDrone.GetComponent<DroneFlightController>();
                var body = currentDrone.GetComponent<Rigidbody>();
                var cameraRig = currentDrone.GetComponentInChildren<DroneCameraRig>(true);
                currentInput = currentDrone.GetComponent<DronePlayerInput>();
                var landingGear = currentDrone.GetComponent<DroneLandingGearController>();
                var equipmentHost = currentDrone.GetComponent<DroneEquipmentHost>();
                var equipmentInput = currentDrone.GetComponent<DroneHookInput>();
                var remote = currentDrone.GetComponent<DroneRemoteControllerExperience>();
                var context = currentDrone.GetComponent<DroneFlightSceneContext>();
                var module = FindEquipmentModule(currentDrone);
                var requiresEquipmentModule = selection != DroneVehicleKind.Plain;
                if (context == null || controller == null || body == null || equipmentHost == null
                    || requiresEquipmentModule && module == null)
                {
                    Debug.LogError("[DroneFlight] 所选机型缺少 Context、飞控、刚体或装备宿主。", currentDrone);
                    return;
                }

                var telemetry = currentDrone.GetComponent<DroneFlightUiTelemetrySource>()
                                ?? currentDrone.AddComponent<DroneFlightUiTelemetrySource>();
                var debugRenderer = currentDrone.GetComponent<DroneFlightDebugDrawRenderer>()
                                    ?? currentDrone.AddComponent<DroneFlightDebugDrawRenderer>();
                equipmentHost.Configure(controller, body, cameraRig != null ? cameraRig.OutputCamera : null, module);
                equipmentInput?.Configure(equipmentHost, landingGear, remote);
                remote?.Configure(playerCamera, cameraRig, currentInput, controller, equipmentInput);
                // 生成首帧先屏蔽玩法 Update，避免消费进入 Play 或点击机型前残留的按键边沿。
                if (remote != null)
                {
                    remote.enabled = false;
                }
                if (currentInput != null)
                {
                    currentInput.enabled = false;
                }
                if (equipmentInput != null)
                {
                    equipmentInput.enabled = false;
                }
                context.Configure(
                    controller,
                    currentInput,
                    cameraRig,
                    remote,
                    equipmentHost,
                    landingGear,
                    telemetry,
                    debugRenderer);
                PrepareDockedEquipment(currentDrone, selection);
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                controller.SetArmed(false);

                telemetry.Configure(context);
                debugRenderer.Configure(context);
                currentInput.ReloadRequested += HandleReloadRequested;

                currentDrone.SetActive(false);
                currentDrone.transform.SetParent(null, true);
                currentDrone.SetActive(true);
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                Physics.SyncTransforms();
                await UniTask.Yield(PlayerLoopTiming.FixedUpdate, cancellationToken);
                remote?.ReturnToWaiting();
                controller.SetArmed(false);
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                if (remote != null)
                {
                    remote.enabled = true;
                }
                await uiController.ShowFlightViewsAsync(telemetry, debugRenderer, sessionId);
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

        private static MonoBehaviour FindEquipmentModule(GameObject drone)
        {
            foreach (var candidate in drone.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (candidate is IDroneEquipmentModule)
                {
                    return candidate;
                }
            }
            return null;
        }

        private static void PrepareDockedEquipment(GameObject drone, DroneVehicleKind selection)
        {
            if (selection == DroneVehicleKind.Plain)
            {
                return;
            }

            if (selection == DroneVehicleKind.Grapple)
            {
                foreach (var sensor in drone.GetComponentsInChildren<DroneGrappleContactSensor>(true))
                {
                    foreach (var collider in sensor.GetComponentsInChildren<Collider>(true))
                    {
                        collider.enabled = false;
                    }
                }
                return;
            }

            var projectile = drone.GetComponentInChildren<DroneHarpoonProjectile>(true);
            if (projectile == null)
            {
                return;
            }
            var body = projectile.GetComponent<Rigidbody>();
            var colliderComponent = projectile.GetComponent<Collider>();
            if (body != null)
            {
                body.isKinematic = true;
                body.useGravity = false;
            }
            if (colliderComponent != null)
            {
                colliderComponent.enabled = false;
            }
        }
    }
}
