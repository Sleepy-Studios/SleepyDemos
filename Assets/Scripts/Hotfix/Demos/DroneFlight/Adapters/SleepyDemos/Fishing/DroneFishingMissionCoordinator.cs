using System.Collections;
using Hotfix.DroneFlight;
using UnityEngine;
using UnityEngine.UI;

namespace Hotfix.DroneFlight.Adapters.SleepyDemos
{
    /// <summary>捕鱼演出阶段；失败只允许重播，不伪造后续成功。</summary>
    internal enum DroneFishingMissionState
    {
        WaitingForQte,
        Preparing,
        Entering,
        Orbiting,
        Diving,
        Aiming,
        Firing,
        Returning,
        Completed,
        Failed
    }

    /// <summary>独立 MVP 场景的 QTE、自动飞行、渔叉命中、载荷返航和重播协调器。</summary>
    public sealed class DroneFishingMissionCoordinator : MonoBehaviour
    {
        [SerializeField, InspectorName("捕鱼任务配置")]
        [Tooltip("集中管理捕鱼区域、路线速度、超时和固定机位参数。")]
        private DroneFishingMissionConfig config;

        [Header("场景结构引用")]
        [Header("Scene References")]
        [SerializeField] private Camera sceneCamera;
        [SerializeField] private DroneCinematicCameraTracker cameraTracker;
        [SerializeField] private DroneBezierMissionPath missionPath;
        [SerializeField] private GameObject harpoonDronePrefab;
        [SerializeField] private Transform endHoverPoint;
        [SerializeField] private Rigidbody fishBody;
        [SerializeField] private Collider fishCollider;

        [Header("QTE UI")]
        [SerializeField] private GameObject qtePanel;
        [SerializeField] private Button launchButton;
        [SerializeField] private GameObject completedPanel;
        [SerializeField] private Button completedReplayButton;
        [SerializeField] private GameObject failedPanel;
        [SerializeField] private Button failedReplayButton;

        [Header("Mission Tuning")]

        private DroneFishingMissionState state;
        private DroneFlightVehicleRuntime runtime;
        private DroneMissionAutopilot autopilot;
        private DroneEquipmentHost equipmentHost;
        private Coroutine preparationCoroutine;
        private float sectionProgress;
        private float phaseElapsed;
        private int completedOrbitLoops;
        private bool fishReleased;

        /// 当前捕鱼任务阶段。
        internal DroneFishingMissionState State => state;

        /// 当前阶段名称，供场景验收和正式项目 UI 只读展示。
        public string CurrentState => state.ToString();

        private void Awake()
        {
            launchButton?.onClick.AddListener(BeginMission);
            completedReplayButton?.onClick.AddListener(Replay);
            failedReplayButton?.onClick.AddListener(Replay);
            ResetExperience();
        }

        private void Update()
        {
            if (runtime.Root == null || cameraTracker == null || cameraTracker.IsTracking || sceneCamera == null)
            {
                return;
            }

            var viewport = sceneCamera.WorldToViewportPoint(runtime.Body.position);
            if (viewport.z > 0f
                && viewport.x is >= -0.05f and <= 1.05f
                && viewport.y is >= -0.05f and <= 1.05f)
            {
                cameraTracker.BeginTracking(runtime.Root.transform);
            }
        }

        private void FixedUpdate()
        {
            if (autopilot == null)
            {
                return;
            }

            phaseElapsed += Time.fixedDeltaTime;
            switch (state)
            {
                case DroneFishingMissionState.Entering:
                    StepPathSection(DroneMissionPathSection.Entry, DroneFishingMissionState.Orbiting);
                    break;
                case DroneFishingMissionState.Orbiting:
                    StepOrbit();
                    break;
                case DroneFishingMissionState.Diving:
                    StepPathSection(DroneMissionPathSection.Dive, DroneFishingMissionState.Aiming);
                    break;
                case DroneFishingMissionState.Aiming:
                    StepAiming();
                    break;
                case DroneFishingMissionState.Firing:
                    StepFiring();
                    break;
                case DroneFishingMissionState.Returning:
                    StepReturning();
                    break;
            }
        }

        private void OnDestroy()
        {
            launchButton?.onClick.RemoveListener(BeginMission);
            completedReplayButton?.onClick.RemoveListener(Replay);
            failedReplayButton?.onClick.RemoveListener(Replay);
            TeardownDrone();
        }

        /// 将单按钮点击视为三次 QTE 全部成功并启动任务。
        public void BeginMission()
        {
            if (state == DroneFishingMissionState.Preparing
                || state is >= DroneFishingMissionState.Entering and <= DroneFishingMissionState.Returning)
            {
                return;
            }

            if (preparationCoroutine != null)
            {
                StopCoroutine(preparationCoroutine);
            }

            TeardownDrone();
            ResetFish();
            cameraTracker?.ResetTracking();
            SetPanels(false, false, false);
            state = DroneFishingMissionState.Preparing;
            preparationCoroutine = StartCoroutine(PrepareMission());
        }

        /// 清理当前会话并回到可再次触发的 QTE Pop。
        public void Replay()
        {
            if (preparationCoroutine != null)
            {
                StopCoroutine(preparationCoroutine);
                preparationCoroutine = null;
            }

            ResetExperience();
        }

        internal void Configure(
            Camera camera,
            DroneCinematicCameraTracker tracker,
            DroneBezierMissionPath path,
            GameObject dronePrefab,
            Transform returnPoint,
            Rigidbody targetBody,
            Collider targetCollider,
            GameObject gatePanel,
            Button gateButton,
            GameObject successPanel,
            Button successReplay,
            GameObject errorPanel,
            Button errorReplay)
        {
            sceneCamera = camera;
            cameraTracker = tracker;
            missionPath = path;
            harpoonDronePrefab = dronePrefab;
            endHoverPoint = returnPoint;
            fishBody = targetBody;
            fishCollider = targetCollider;
            qtePanel = gatePanel;
            launchButton = gateButton;
            completedPanel = successPanel;
            completedReplayButton = successReplay;
            failedPanel = errorPanel;
            failedReplayButton = errorReplay;
        }

        /// 由场景构建器统一写入捕鱼演出配置。
        internal void Configure(DroneFishingMissionConfig missionConfig)
        {
            config = missionConfig;
        }

        private IEnumerator PrepareMission()
        {
            if (missionPath == null || harpoonDronePrefab == null || sceneCamera == null || fishBody == null)
            {
                FailMission("场景缺少路径、无人机、相机或鱼目标引用");
                yield break;
            }

            missionPath.transform.position = new Vector3(fishBody.position.x, 0f, fishBody.position.z);
            missionPath.RecalculateLengths();
            var stagingRoot = new GameObject("FishingMissionSpawnStaging");
            stagingRoot.SetActive(false);
            var drone = Instantiate(harpoonDronePrefab, stagingRoot.transform);
            drone.name = "FishingMissionHarpoonDrone";
            var spawnMarker = new GameObject("FishingMissionSpawnMarker");
            spawnMarker.transform.SetPositionAndRotation(
                missionPath.EntryStart,
                Quaternion.LookRotation(
                    Vector3.ProjectOnPlane(
                        missionPath.EvaluateTangent(DroneMissionPathSection.Entry, 0f),
                        Vector3.up).normalized,
                    Vector3.up));
            if (!DroneFlightVehicleAssembler.TryPrepare(
                drone,
                DroneVehicleKind.Harpoon,
                spawnMarker.transform,
                null,
                out runtime,
                    out var error))
            {
                Destroy(spawnMarker);
                Destroy(stagingRoot);
                FailMission(error);
                yield break;
            }

            Destroy(spawnMarker);
            runtime.Activate();
            Destroy(stagingRoot);
            yield return new WaitForFixedUpdate();
            runtime.FinalizeForAutomation();

            equipmentHost = runtime.Root.GetComponent<DroneEquipmentHost>();
            autopilot = runtime.Root.AddComponent<DroneMissionAutopilot>();
            autopilot.Configure(runtime.Controller, runtime.Body);
            runtime.Controller.SetResponseProfile(DroneResponseProfile.Sport);
            runtime.Controller.SetTargetHeight(runtime.Body.position.y);
            runtime.Controller.SetArmed(true);
            preparationCoroutine = null;
            BeginPhase(DroneFishingMissionState.Entering);
        }

        private void StepPathSection(DroneMissionPathSection section, DroneFishingMissionState nextState)
        {
            if (phaseElapsed >= RoutePhaseTimeoutSeconds)
            {
                FailMission($"{section} 路径阶段超时");
                return;
            }

            var length = Mathf.Max(0.1f, missionPath.GetApproximateLength(section));
            if (autopilot.TrackingError <= MaximumTrackingErrorMeters || sectionProgress <= 0f)
            {
                sectionProgress = Mathf.Min(
                    1f,
                    sectionProgress + RouteSpeedMetersPerSecond * Time.fixedDeltaTime / length);
            }

            autopilot.SetTarget(
                missionPath.Evaluate(section, sectionProgress),
                missionPath.EvaluateTangent(section, sectionProgress),
                RouteSpeedMetersPerSecond);
            if (sectionProgress >= 1f && autopilot.HasArrived)
            {
                BeginPhase(nextState);
            }
        }

        private void StepOrbit()
        {
            if (phaseElapsed >= RoutePhaseTimeoutSeconds)
            {
                FailMission("环绕阶段超时");
                return;
            }

            var length = Mathf.Max(0.1f, missionPath.GetApproximateLength(DroneMissionPathSection.Orbit));
            if (autopilot.TrackingError <= MaximumTrackingErrorMeters || sectionProgress <= 0f)
            {
                sectionProgress = Mathf.Min(
                    1f,
                    sectionProgress + RouteSpeedMetersPerSecond * Time.fixedDeltaTime / length);
            }

            autopilot.SetTarget(
                missionPath.Evaluate(DroneMissionPathSection.Orbit, sectionProgress),
                missionPath.EvaluateTangent(DroneMissionPathSection.Orbit, sectionProgress),
                RouteSpeedMetersPerSecond);
            if (sectionProgress < 1f || !autopilot.HasArrived)
            {
                return;
            }

            completedOrbitLoops++;
            if (completedOrbitLoops >= Mathf.Clamp(OrbitLoops, 1, 2))
            {
                BeginPhase(DroneFishingMissionState.Diving);
            }
            else
            {
                sectionProgress = 0f;
            }
        }

        private void StepAiming()
        {
            if (phaseElapsed >= RoutePhaseTimeoutSeconds)
            {
                FailMission("自动瞄准阶段超时");
                return;
            }

            var targetPoint = fishCollider != null ? fishCollider.bounds.center : fishBody.worldCenterOfMass;
            autopilot.SetTarget(missionPath.DiveEnd, targetPoint - runtime.Body.position, 1f);
            if (!autopilot.HasArrived || equipmentHost == null
                || !equipmentHost.TrySetAutomatedAimTarget(targetPoint)
                || !equipmentHost.Snapshot.CanUsePrimary)
            {
                return;
            }

            equipmentHost.PrimaryAction();
            BeginPhase(DroneFishingMissionState.Firing);
        }

        private void StepFiring()
        {
            autopilot.SetTarget(missionPath.DiveEnd, Vector3.forward, 0.5f);
            if (equipmentHost != null && equipmentHost.State == DroneEquipmentState.Attached)
            {
                ReleaseFishAsPayload();
                BeginPhase(DroneFishingMissionState.Returning);
                return;
            }

            if (phaseElapsed >= FiringTimeoutSeconds)
            {
                FailMission("渔叉未在限定时间命中鱼目标");
            }
        }

        private void StepReturning()
        {
            if (endHoverPoint == null)
            {
                FailMission("场景缺少返航悬停点");
                return;
            }

            if (phaseElapsed >= RoutePhaseTimeoutSeconds)
            {
                FailMission("负载返航阶段超时");
                return;
            }

            var forward = endHoverPoint.position - runtime.Body.position;
            autopilot.SetTarget(endHoverPoint.position, forward, ReturnSpeedMetersPerSecond);
            if (!autopilot.HasArrived)
            {
                return;
            }

            autopilot.StopAtCurrentPosition();
            state = DroneFishingMissionState.Completed;
            SetPanels(false, true, false);
        }

        private void BeginPhase(DroneFishingMissionState nextState)
        {
            state = nextState;
            sectionProgress = 0f;
            phaseElapsed = 0f;
            if (nextState == DroneFishingMissionState.Orbiting)
            {
                completedOrbitLoops = 0;
            }
        }

        private void ReleaseFishAsPayload()
        {
            if (fishBody == null || fishReleased)
            {
                return;
            }

            fishReleased = true;
            fishBody.constraints = RigidbodyConstraints.None;
            fishBody.isKinematic = false;
            fishBody.useGravity = true;
            fishBody.WakeUp();
        }

        private void ResetExperience()
        {
            TeardownDrone();
            ResetFish();
            cameraTracker?.ResetTracking();
            SetPanels(true, false, false);
            state = DroneFishingMissionState.WaitingForQte;
            sectionProgress = 0f;
            phaseElapsed = 0f;
            completedOrbitLoops = 0;
        }

        private void ResetFish()
        {
            if (fishBody == null)
            {
                return;
            }

            fishReleased = false;
            fishBody.isKinematic = true;
            fishBody.useGravity = false;
            fishBody.constraints = RigidbodyConstraints.None;
            var targetPosition = new Vector3(
                Random.Range(-Mathf.Abs(FishAreaHalfExtents.x), Mathf.Abs(FishAreaHalfExtents.x)),
                FishDepthMeters,
                Random.Range(-Mathf.Abs(FishAreaHalfExtents.y), Mathf.Abs(FishAreaHalfExtents.y)));
            fishBody.transform.SetPositionAndRotation(targetPosition, Quaternion.identity);
            fishBody.position = targetPosition;
            fishBody.rotation = Quaternion.identity;
            fishBody.linearVelocity = Vector3.zero;
            fishBody.angularVelocity = Vector3.zero;
            fishBody.constraints = RigidbodyConstraints.FreezeAll;
            Physics.SyncTransforms();
        }

        private void FailMission(string reason)
        {
            Debug.LogError($"[DroneFishingMvp] {reason}", this);
            state = DroneFishingMissionState.Failed;
            autopilot?.StopAtCurrentPosition();
            equipmentHost?.ClearAutomatedAimTarget();
            SetPanels(false, false, true);
            preparationCoroutine = null;
        }

        private void TeardownDrone()
        {
            if (runtime.Root != null)
            {
                equipmentHost?.ReleaseAndCleanup();
                Destroy(runtime.Root);
            }

            runtime = default;
            autopilot = null;
            equipmentHost = null;
        }

        private void SetPanels(bool showQte, bool showCompleted, bool showFailed)
        {
            qtePanel?.SetActive(showQte);
            completedPanel?.SetActive(showCompleted);
            failedPanel?.SetActive(showFailed);
        }

        // 配置资产缺失时保留旧 Demo 数值，避免历史场景在迁移期间直接失效。
        private Vector2 FishAreaHalfExtents => config != null ? config.FishAreaHalfExtents : new Vector2(3f, 3f);
        private float FishDepthMeters => config != null ? config.FishDepthMeters : -5f;
        private int OrbitLoops => config != null ? config.OrbitLoops : 2;
        private float RouteSpeedMetersPerSecond => config != null ? config.RouteSpeedMetersPerSecond : 4f;
        private float ReturnSpeedMetersPerSecond => config != null ? config.ReturnSpeedMetersPerSecond : 3f;
        private float MaximumTrackingErrorMeters => config != null ? config.MaximumTrackingErrorMeters : 2.5f;
        private float RoutePhaseTimeoutSeconds => config != null ? config.RoutePhaseTimeoutSeconds : 60f;
        private float FiringTimeoutSeconds => config != null ? config.FiringTimeoutSeconds : 10f;
    }
}
