using System.Collections;
using UnityEngine;

namespace Hotfix.DroneFlight
{
    /// <summary>独立场景启动后由玩家控制，或自动执行航点巡航。</summary>
    public enum DroneStandaloneMode
    {
        Manual,
        AutomaticCruise
    }

    /// <summary>单次巡航完成后的无人机行为。</summary>
    public enum DroneCruiseCompletionBehavior
    {
        Hover,
        AutomaticLanding
    }

    /// <summary>不依赖资源、UI 和场景服务的 DroneFlight 独立场景入口。</summary>
    public sealed class DroneFlightStandaloneBootstrap : MonoBehaviour
    {
        [Header("场景装配")]
        [SerializeField, InspectorName("无人机成品 Prefab")]
        [Tooltip("纯无人机、抓斗或渔叉成品 Prefab。Bootstrap 使用 Unity Instantiate 直接创建。")]
        private GameObject dronePrefab;

        [SerializeField, InspectorName("出生点")]
        [Tooltip("提供地面 XZ 位置和初始朝向；实际 Y 会根据起落架脚底自动计算。")]
        private Transform spawnPoint;

        [SerializeField, InspectorName("场景等待相机")]
        [Tooltip("无人机镜头启用后会关闭此相机及其 AudioListener。")]
        private Camera sceneCamera;

        [Header("启动模式")]
        [SerializeField, InspectorName("模式")]
        [Tooltip("手动模式开放玩家输入；自动巡航模式禁用玩家和装备输入。")]
        private DroneStandaloneMode mode = DroneStandaloneMode.Manual;

        [SerializeField, InspectorName("巡航路线")]
        [Tooltip("自动巡航模式必填；手动模式可以为空。")]
        private DroneCruiseRoute cruiseRoute;

        [SerializeField, InspectorName("自动驾驶配置")]
        [Tooltip("自动巡航的位置跟随、偏航和到点容差配置。")]
        private DroneAutopilotConfig autopilotConfig;

        [SerializeField, InspectorName("巡航前自动起飞")]
        [Tooltip("启用后先执行现有自动起飞，稳定进入 Flying 后再开始路线。")]
        private bool automaticTakeoff = true;

        [SerializeField, InspectorName("巡航完成行为")]
        [Tooltip("仅对单次路线生效；循环和往返路线不会自然完成。")]
        private DroneCruiseCompletionBehavior completionBehavior = DroneCruiseCompletionBehavior.Hover;

        private GameObject currentDrone;
        private DroneFlightVehicleRuntime runtime;
        private DroneStandaloneControlSession controlSession;
        private DroneCruiseRunner cruiseRunner;

        /// 当前由独立启动器装配出的运行时，仅供同模块验证与诊断。
        internal DroneFlightVehicleRuntime Runtime => runtime;

        /// <summary>由测试或自定义场景构建器写入最小手动启动参数。</summary>
        internal void Configure(GameObject prefab, Transform spawn, Camera waitingCamera)
        {
            dronePrefab = prefab;
            spawnPoint = spawn;
            sceneCamera = waitingCamera;
            mode = DroneStandaloneMode.Manual;
        }

        private IEnumerator Start()
        {
            if (dronePrefab == null || spawnPoint == null || sceneCamera == null)
            {
                Debug.LogError("[DroneFlight] StandaloneBootstrap 缺少无人机 Prefab、出生点或场景相机。", this);
                yield break;
            }

            if (mode == DroneStandaloneMode.AutomaticCruise && cruiseRoute == null)
            {
                Debug.LogError("[DroneFlight] 自动巡航缺少路线。", this);
                yield break;
            }

            if (mode == DroneStandaloneMode.AutomaticCruise && !cruiseRoute.IsValid(out var routeError))
            {
                Debug.LogError($"[DroneFlight] {routeError}", this);
                yield break;
            }

            var stagingRoot = new GameObject("DroneStandaloneStaging");
            stagingRoot.SetActive(false);
            currentDrone = Instantiate(dronePrefab, stagingRoot.transform);
            currentDrone.name = dronePrefab.name;

            var controller = currentDrone.GetComponent<DroneFlightController>();
            var input = currentDrone.GetComponent<DronePlayerInput>();
            var equipmentInput = currentDrone.GetComponent<DroneEquipmentInput>();
            var cameraRig = currentDrone.GetComponentInChildren<DroneCameraRig>(true);
            DisablePrefabHostSessions(currentDrone);
            controlSession = new DroneStandaloneControlSession(
                sceneCamera,
                cameraRig,
                input,
                controller,
                equipmentInput);

            if (!DroneFlightVehicleAssembler.TryPrepare(
                    currentDrone,
                    DetectVehicleKind(currentDrone),
                    spawnPoint,
                    controlSession,
                    out runtime,
                    out var error))
            {
                Debug.LogError($"[DroneFlight] Standalone 装配失败：{error}", this);
                Destroy(stagingRoot);
                yield break;
            }

            runtime.Activate();
            Destroy(stagingRoot);
            yield return new WaitForFixedUpdate();
            runtime.FinalizeAfterFirstPhysicsStep();

            if (mode == DroneStandaloneMode.Manual)
            {
                yield break;
            }

            input.enabled = false;
            if (equipmentInput != null)
            {
                equipmentInput.enabled = false;
            }

            cruiseRunner = currentDrone.GetComponent<DroneCruiseRunner>()
                           ?? currentDrone.AddComponent<DroneCruiseRunner>();
            cruiseRunner.Configure(controller, runtime.Body, cruiseRoute, autopilotConfig);
            cruiseRunner.Completed += HandleCruiseCompleted;

            if (automaticTakeoff)
            {
                controller.BeginAutomaticTakeoff();
                while (controller.OperationState == DroneFlightOperationState.TakingOff)
                {
                    yield return new WaitForFixedUpdate();
                }

                if (controller.OperationState == DroneFlightOperationState.Fault)
                {
                    Debug.LogError("[DroneFlight] 自动起飞进入 Fault，已取消巡航。", this);
                    yield break;
                }
            }
            else
            {
                controller.SetArmed(true);
            }

            cruiseRunner.StartCruise();
        }

        private void OnDestroy()
        {
            if (cruiseRunner != null)
            {
                cruiseRunner.Completed -= HandleCruiseCompleted;
                cruiseRunner.Stop();
            }

            controlSession?.ReturnToWaiting();
            if (currentDrone != null)
            {
                Destroy(currentDrone);
            }
        }

        private void HandleCruiseCompleted()
        {
            if (completionBehavior == DroneCruiseCompletionBehavior.AutomaticLanding)
            {
                runtime.Controller.BeginAutomaticLanding();
            }
        }

        private static DroneVehicleKind DetectVehicleKind(GameObject drone)
        {
            if (drone.GetComponentInChildren<DroneGrappleModule>(true) != null)
            {
                return DroneVehicleKind.Grapple;
            }

            return drone.GetComponentInChildren<DroneHarpoonModule>(true) != null
                ? DroneVehicleKind.Harpoon
                : DroneVehicleKind.Plain;
        }

        private static void DisablePrefabHostSessions(GameObject drone)
        {
            foreach (var behaviour in drone.GetComponents<MonoBehaviour>())
            {
                if (behaviour is IDroneControlSession)
                {
                    behaviour.enabled = false;
                }
            }
        }
    }

    /// <summary>Standalone 场景的相机与输入切换，不接触 SleepyDemos UI 根节点。</summary>
    internal sealed class DroneStandaloneControlSession : IDroneControlSession
    {
        private readonly Camera sceneCamera;
        private readonly DroneCameraRig cameraRig;
        private readonly DronePlayerInput flightInput;
        private readonly DroneFlightController controller;
        private readonly DroneEquipmentInput equipmentInput;

        internal DroneStandaloneControlSession(
            Camera waitingCamera,
            DroneCameraRig rig,
            DronePlayerInput input,
            DroneFlightController flightController,
            DroneEquipmentInput equipment)
        {
            sceneCamera = waitingCamera;
            cameraRig = rig;
            flightInput = input;
            controller = flightController;
            equipmentInput = equipment;
        }

        public bool IsActive { get; private set; }

        public void Activate()
        {
            IsActive = true;
            SetCameraState(sceneCamera, false);
            SetCameraState(cameraRig != null ? cameraRig.OutputCamera : null, true);
            if (flightInput != null)
            {
                flightInput.enabled = true;
            }
            if (equipmentInput != null)
            {
                equipmentInput.enabled = true;
            }
        }

        public void ReturnToWaiting()
        {
            IsActive = false;
            controller?.SetArmed(false);
            flightInput?.ResetBufferedInput();
            if (flightInput != null)
            {
                flightInput.enabled = false;
            }
            if (equipmentInput != null)
            {
                equipmentInput.ResetTransientState();
                equipmentInput.enabled = false;
            }
            SetCameraState(cameraRig != null ? cameraRig.OutputCamera : null, false);
            SetCameraState(sceneCamera, true);
        }

        private static void SetCameraState(Camera camera, bool enabled)
        {
            if (camera == null)
            {
                return;
            }

            camera.targetTexture = null;
            camera.enabled = enabled;
            var listener = camera.GetComponent<AudioListener>();
            if (listener != null)
            {
                listener.enabled = enabled;
            }
        }
    }
}
