using UnityEngine;

namespace Hotfix.DroneFlight
{
    /// <summary>已完成同步装配、等待首个物理步收口的无人机运行时引用。</summary>
    internal readonly struct DroneFlightVehicleRuntime
    {
        internal DroneFlightVehicleRuntime(
            GameObject root,
            Rigidbody body,
            DroneFlightController controller,
            DronePlayerInput input,
            IDroneControlSession controlSession,
            DroneFlightUiTelemetrySource telemetry,
            DroneFlightDebugDrawRenderer debugRenderer)
        {
            Root = root;
            Body = body;
            Controller = controller;
            Input = input;
            ControlSession = controlSession;
            Telemetry = telemetry;
            DebugRenderer = debugRenderer;
        }

        /// 已装配的无人机根对象。
        internal GameObject Root { get; }

        /// 无人机主刚体。
        internal Rigidbody Body { get; }

        /// 无人机飞控。
        internal DroneFlightController Controller { get; }

        /// 玩家飞行输入。
        internal DronePlayerInput Input { get; }

        /// 当前宿主或独立场景提供的控制会话。
        internal IDroneControlSession ControlSession { get; }

        /// HUD/F3 遥测源。
        internal DroneFlightUiTelemetrySource Telemetry { get; }

        /// Game View 调试矢量绘制器。
        internal DroneFlightDebugDrawRenderer DebugRenderer { get; }

        /// 激活机体前清空刚体状态并脱离临时父节点。
        internal void Activate()
        {
            Root.SetActive(false);
            Root.transform.SetParent(null, true);
            Root.SetActive(true);
            ResetBodyMotion();
            Physics.SyncTransforms();
        }

        /// 首个物理步后直接进入未解锁的第三人称控制并开放输入。
        internal void FinalizeAfterFirstPhysicsStep()
        {
            Controller.SetArmed(false);
            ResetBodyMotion();
            if (ControlSession != null)
            {
                ControlSession.Activate();
            }
            else if (Input != null)
            {
                Input.enabled = true;
            }
        }

        /// 首个物理步后保持玩家输入关闭，交由任务自动驾驶接管。
        internal void FinalizeForAutomation()
        {
            ControlSession?.ReturnToWaiting();
            Controller.SetArmed(false);
            ResetBodyMotion();
            if (Input != null)
            {
                Input.enabled = false;
            }
        }

        // 禁止出生阶段残留资源加载或选择界面的输入速度。
        private void ResetBodyMotion()
        {
            Body.linearVelocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
        }
    }

    /// <summary>只依赖 Unity 与 DroneFlight 组件的机体同步装配器。</summary>
    internal static class DroneFlightVehicleAssembler
    {
        /// <summary>
        /// 校验并装配一个已经实例化的成品机体，不负责资源加载、UI 或场景导航。
        /// </summary>
        /// <param name="drone">待装配的基础或装备机体。</param>
        /// <param name="selection">选择的机型，用于校验装备模块并准备收纳碰撞。</param>
        /// <param name="spawnPoint">只提供地面 XZ 和朝向的出生标记。</param>
        /// <param name="controlSession">宿主或独立场景提供的控制会话；自动任务可为空。</param>
        /// <param name="runtime">成功时返回首物理步所需的运行时引用。</param>
        /// <param name="error">失败时返回可直接记录的中文诊断。</param>
        internal static bool TryPrepare(
            GameObject drone,
            DroneVehicleKind selection,
            Transform spawnPoint,
            IDroneControlSession controlSession,
            out DroneFlightVehicleRuntime runtime,
            out string error)
        {
            runtime = default;
            error = string.Empty;
            if (drone == null)
            {
                error = "实例化结果为空。";
                return false;
            }

            drone.transform.localScale = Vector3.one;
            if (!DroneSpawnPlacement.TryPlaceOnGround(
                    drone,
                    spawnPoint,
                    DroneSpawnPlacement.DefaultGroundClearanceMeters,
                    out _))
            {
                error = "无法从四个起落架脚部计算安全出生高度。";
                return false;
            }

            var controller = drone.GetComponent<DroneFlightController>();
            var body = drone.GetComponent<Rigidbody>();
            var cameraRig = drone.GetComponentInChildren<DroneCameraRig>(true);
            var input = drone.GetComponent<DronePlayerInput>();
            var landingGear = drone.GetComponent<DroneLandingGearController>();
            var equipmentHost = drone.GetComponent<DroneEquipmentHost>();
            var equipmentInput = drone.GetComponent<DroneEquipmentInput>();
            var context = drone.GetComponent<DroneFlightSceneContext>();
            var module = FindEquipmentModule(drone);
            if (context == null || controller == null || body == null || input == null || equipmentHost == null
                || selection != DroneVehicleKind.Plain && module == null)
            {
                error = "所选机型缺少 Context、飞控、刚体、玩家输入或装备宿主。";
                return false;
            }

            var telemetry = drone.GetComponent<DroneFlightUiTelemetrySource>()
                            ?? drone.AddComponent<DroneFlightUiTelemetrySource>();
            var debugRenderer = drone.GetComponent<DroneFlightDebugDrawRenderer>()
                                ?? drone.AddComponent<DroneFlightDebugDrawRenderer>();
            equipmentHost.Configure(controller, body, cameraRig != null ? cameraRig.OutputCamera : null, module);
            equipmentInput?.Configure(equipmentHost, landingGear, controlSession);

            // 生成首帧先屏蔽玩法 Update，避免消费进入 Play 或点击机型前残留的按键边沿。
            if (input != null)
            {
                input.enabled = false;
            }
            if (equipmentInput != null)
            {
                equipmentInput.enabled = false;
            }

            context.Configure(
                controller,
                input,
                cameraRig,
                controlSession,
                equipmentHost,
                landingGear,
                telemetry);
            PrepareDockedEquipment(drone, selection);
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            controller.SetArmed(false);
            telemetry.Configure(context, drone.GetComponent<DroneTelemetryRecorder>()?.Config);
            debugRenderer.Configure(context);
            runtime = new DroneFlightVehicleRuntime(
                drone,
                body,
                controller,
                input,
                controlSession,
                telemetry,
                debugRenderer);
            return true;
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
                // 抓斗 Prefab 已以 Kinematic 完成装配，模块会在 Joint 与锚点就绪后统一开放物理。
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
                body.interpolation = RigidbodyInterpolation.None;
            }
            if (colliderComponent != null)
            {
                colliderComponent.enabled = false;
            }
        }
    }
}
