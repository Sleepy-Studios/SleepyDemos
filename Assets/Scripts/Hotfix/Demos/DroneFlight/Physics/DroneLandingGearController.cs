using System;
using UnityEngine;

namespace Hotfix.DroneFlight
{
    /// <summary>手动起落架的稳定状态与过渡状态。</summary>
    internal enum DroneLandingGearState
    {
        Deployed,
        Deploying,
        Retracted,
        Retracting
    }

    /// <summary>不依赖场景对象的起落架迟滞状态机。</summary>
    internal sealed class DroneLandingGearStateMachine
    {
        internal DroneLandingGearState State { get; private set; } = DroneLandingGearState.Deployed;

        internal float NormalizedPosition { get; private set; }

        internal void Step(bool shouldDeploy, float transitionSeconds, float deltaTime)
        {
            if (!float.IsFinite(deltaTime) || deltaTime <= 0f)
            {
                return;
            }

            var duration = Mathf.Max(0.01f, transitionSeconds);
            var target = shouldDeploy ? 0f : 1f;
            NormalizedPosition = Mathf.MoveTowards(NormalizedPosition, target, deltaTime / duration);
            if (Mathf.Approximately(NormalizedPosition, 0f))
            {
                State = DroneLandingGearState.Deployed;
            }
            else if (Mathf.Approximately(NormalizedPosition, 1f))
            {
                State = DroneLandingGearState.Retracted;
            }
            else
            {
                State = shouldDeploy
                    ? DroneLandingGearState.Deploying
                    : DroneLandingGearState.Retracting;
            }
        }

        internal void ResetDeployed()
        {
            NormalizedPosition = 0f;
            State = DroneLandingGearState.Deployed;
        }
    }

    /// <summary>只响应显式指令的四支腿手动起落架。</summary>
    public sealed class DroneLandingGearController : MonoBehaviour
    {
        [SerializeField] private DroneFlightController flightController;
        [SerializeField] private Rigidbody droneBody;
        [SerializeField] private Transform[] legRoots = Array.Empty<Transform>();
        [SerializeField] private Vector3[] retractedEulerOffsets = Array.Empty<Vector3>();

        private readonly DroneLandingGearStateMachine stateMachine = new();
        private Quaternion[] deployedRotations = Array.Empty<Quaternion>();
        private bool deploymentRequested = true;

        /// 当前起落架状态。
        internal DroneLandingGearState State => stateMachine.State;

        /// 0 表示完全放下，1 表示完全收起。
        internal float NormalizedPosition => stateMachine.NormalizedPosition;

        /// 当前手动目标是否为放下。
        internal bool IsDeploymentRequested => deploymentRequested;

        private void Awake()
        {
            CaptureDeployedRotations();
            ApplyPose();
        }

        private void Update()
        {
            if (flightController == null)
            {
                return;
            }

            var config = flightController.Config;
            stateMachine.Step(deploymentRequested, config.LandingGearTransitionSeconds, Time.unscaledDeltaTime);
            ApplyPose();
        }

        /// 在收起与放下目标之间切换一次。
        internal void Toggle()
        {
            SetDeployed(!deploymentRequested);
        }

        /// <summary>
        /// 设置手动起落架目标，不读取高度或飞控状态。
        /// </summary>
        /// <param name="deployed">是否放下。</param>
        internal void SetDeployed(bool deployed)
        {
            deploymentRequested = deployed;
        }

        /// <summary>
        /// 由 Prefab 装配或测试夹具绑定飞控、机体与四个支腿。
        /// </summary>
        /// <param name="controller">提供运行状态和配置的飞控。</param>
        /// <param name="body">用于计算离地高度的无人机刚体。</param>
        /// <param name="roots">支腿旋转根，预期恰好四个。</param>
        /// <param name="retractedOffsets">每个支腿从放下姿态到收起姿态的局部欧拉偏移。</param>
        internal void Configure(
            DroneFlightController controller,
            Rigidbody body,
            Transform[] roots,
            Vector3[] retractedOffsets)
        {
            flightController = controller;
            droneBody = body;
            legRoots = roots ?? Array.Empty<Transform>();
            retractedEulerOffsets = retractedOffsets ?? Array.Empty<Vector3>();
            CaptureDeployedRotations();
            ResetToDeployed();
        }

        /// 将起落架立即恢复为放下状态。
        internal void ResetToDeployed()
        {
            deploymentRequested = true;
            stateMachine.ResetDeployed();
            ApplyPose();
        }

        private void CaptureDeployedRotations()
        {
            deployedRotations = new Quaternion[legRoots.Length];
            for (var index = 0; index < legRoots.Length; index++)
            {
                deployedRotations[index] = legRoots[index] != null
                    ? legRoots[index].localRotation
                    : Quaternion.identity;
            }
        }

        private void ApplyPose()
        {
            for (var index = 0; index < legRoots.Length; index++)
            {
                if (legRoots[index] == null)
                {
                    continue;
                }

                var offset = index < retractedEulerOffsets.Length
                    ? retractedEulerOffsets[index]
                    : Vector3.zero;
                var retracted = deployedRotations[index] * Quaternion.Euler(offset);
                legRoots[index].localRotation = Quaternion.Slerp(
                    deployedRotations[index],
                    retracted,
                    stateMachine.NormalizedPosition);
            }
        }
    }
}
