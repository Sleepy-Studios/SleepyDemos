using System;
using UnityEngine;

namespace Hotfix.DroneFlight
{
    /// <summary>通用航点巡航的运行状态。</summary>
    public enum DroneCruiseState
    {
        Idle,
        Running,
        Waiting,
        Paused,
        Completed,
        Faulted
    }

    /// <summary>按路线驱动自动驾驶输入，不直接修改 Transform 或 Rigidbody 速度。</summary>
    public sealed class DroneCruiseRunner : MonoBehaviour
    {
        [SerializeField, InspectorName("巡航路线")]
        [Tooltip("包含有序航点、默认速度和循环方式的场景路线。")]
        private DroneCruiseRoute route;

        [SerializeField, InspectorName("自动驾驶配置")]
        [Tooltip("控制位置误差、最大指令速度和到点容差。")]
        private DroneAutopilotConfig autopilotConfig;

        private DroneFlightController controller;
        private Rigidbody body;
        private DroneMissionAutopilot autopilot;
        private readonly DroneCruiseProgression progression = new();
        private float remainingWaitSeconds;
        private Vector3 retainedForward;

        /// 巡航完成时触发一次。
        public event Action Completed;

        /// 当前巡航状态。
        public DroneCruiseState State { get; private set; } = DroneCruiseState.Idle;

        /// 当前目标航点索引；尚未开始时为 -1。
        public int CurrentWaypointIndex { get; private set; } = -1;

        private void FixedUpdate()
        {
            if (State is DroneCruiseState.Idle or DroneCruiseState.Paused
                or DroneCruiseState.Completed or DroneCruiseState.Faulted)
            {
                return;
            }

            if (!route.TryGetWaypoint(CurrentWaypointIndex, out var waypoint))
            {
                Fail($"巡航过程中第 {CurrentWaypointIndex + 1} 个航点失效。");
                return;
            }

            if (State == DroneCruiseState.Waiting)
            {
                remainingWaitSeconds -= Time.fixedDeltaTime;
                if (remainingWaitSeconds <= 0f)
                {
                    Advance();
                }
                return;
            }

            if (!autopilot.HasArrived)
            {
                return;
            }

            remainingWaitSeconds = waypoint.WaitSeconds;
            if (remainingWaitSeconds > 0f)
            {
                State = DroneCruiseState.Waiting;
                autopilot.StopAtCurrentPosition();
            }
            else
            {
                Advance();
            }
        }

        /// <summary>显式注入当前无人机和路线；不会自动开始巡航。</summary>
        /// <param name="flightController">接收自动驾驶输入的现有飞控。</param>
        /// <param name="droneBody">无人机主刚体。</param>
        /// <param name="cruiseRoute">需要执行的场景航点路线。</param>
        /// <param name="config">可选自动驾驶配置；为空时使用安全默认值。</param>
        public void Configure(
            DroneFlightController flightController,
            Rigidbody droneBody,
            DroneCruiseRoute cruiseRoute,
            DroneAutopilotConfig config = null)
        {
            controller = flightController;
            body = droneBody;
            route = cruiseRoute;
            autopilotConfig = config;
            autopilot = GetComponent<DroneMissionAutopilot>() ?? gameObject.AddComponent<DroneMissionAutopilot>();
            autopilot.Configure(controller, body, autopilotConfig);
        }

        /// 从第一个航点开始巡航。
        public void StartCruise()
        {
            if (!EnsureReady())
            {
                return;
            }

            progression.Reset(route.WaypointCount, route.Mode);
            CurrentWaypointIndex = progression.CurrentIndex;
            retainedForward = body.transform.forward;
            State = DroneCruiseState.Running;
            ApplyCurrentWaypoint();
        }

        /// 暂停并保持当前位置。
        public void Pause()
        {
            if (State is not DroneCruiseState.Running and not DroneCruiseState.Waiting)
            {
                return;
            }

            State = DroneCruiseState.Paused;
            autopilot?.StopAtCurrentPosition();
        }

        /// 从当前航点继续巡航。
        public void Resume()
        {
            if (State != DroneCruiseState.Paused || !EnsureReady())
            {
                return;
            }

            State = DroneCruiseState.Running;
            ApplyCurrentWaypoint();
        }

        /// 停止路线并保持当前位置。
        public void Stop()
        {
            autopilot?.StopAtCurrentPosition();
            State = DroneCruiseState.Idle;
            CurrentWaypointIndex = -1;
            remainingWaitSeconds = 0f;
        }

        private void OnDisable()
        {
            if (State is DroneCruiseState.Running or DroneCruiseState.Waiting or DroneCruiseState.Paused)
            {
                Stop();
            }
        }

        private bool EnsureReady()
        {
            if (controller == null || body == null || autopilot == null)
            {
                Fail("巡航未绑定飞控、主刚体或自动驾驶组件。");
                return false;
            }

            if (route == null)
            {
                Fail("巡航路线为空。");
                return false;
            }

            if (!route.IsValid(out var error))
            {
                Fail(error);
                return false;
            }

            return true;
        }

        private void Advance()
        {
            if (!progression.TryAdvance(out var completed) && completed)
            {
                autopilot.StopAtCurrentPosition();
                State = DroneCruiseState.Completed;
                Completed?.Invoke();
                return;
            }

            CurrentWaypointIndex = progression.CurrentIndex;

            State = DroneCruiseState.Running;
            ApplyCurrentWaypoint();
        }

        private void ApplyCurrentWaypoint()
        {
            if (!route.TryGetWaypoint(CurrentWaypointIndex, out var waypoint))
            {
                Fail($"无法读取第 {CurrentWaypointIndex + 1} 个巡航航点。");
                return;
            }

            var forward = waypoint.HeadingMode switch
            {
                DroneCruiseHeadingMode.KeepCurrent => retainedForward,
                DroneCruiseHeadingMode.UseWaypointForward => waypoint.Target.forward,
                _ => ResolveRouteForward(waypoint.Target.position)
            };
            var speed = waypoint.SpeedOverride > 0f
                ? waypoint.SpeedOverride
                : route.DefaultSpeedMetersPerSecond;
            autopilot.SetTarget(waypoint.Target.position, forward, speed);
        }

        private Vector3 ResolveRouteForward(Vector3 target)
        {
            var delta = Vector3.ProjectOnPlane(target - body.position, Vector3.up);
            return delta.sqrMagnitude > 0.0001f ? delta.normalized : body.transform.forward;
        }

        private void Fail(string error)
        {
            autopilot?.StopAtCurrentPosition();
            State = DroneCruiseState.Faulted;
            Debug.LogError($"[DroneFlight] {error}", this);
        }
    }
}
