using System;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hotfix.DroneFlight
{
    /// <summary>定长飞行遥测单样本。</summary>
    internal readonly struct DroneTelemetrySample
    {
        internal DroneTelemetrySample(
            float time,
            float height,
            float targetHeight,
            float horizontalSpeed,
            float verticalSpeed,
            float tiltDegrees,
            Vector3 targetLocalRate,
            Vector3 actualLocalRate,
            DronePidTelemetry roll,
            DronePidTelemetry pitch,
            DronePidTelemetry yaw,
            QuadrotorMotorOutput motors,
            Vector3 targetWorldVelocity = default,
            Vector3 actualWorldVelocity = default,
            float attitudeTrackingErrorDegrees = 0f,
            DroneControlSaturation saturation = default,
            float yawScale = 1f)
        {
            Time = time;
            Height = height;
            TargetHeight = targetHeight;
            HorizontalSpeed = horizontalSpeed;
            VerticalSpeed = verticalSpeed;
            TiltDegrees = tiltDegrees;
            TargetLocalRate = targetLocalRate;
            ActualLocalRate = actualLocalRate;
            Roll = roll;
            Pitch = pitch;
            Yaw = yaw;
            Motors = motors;
            TargetWorldVelocity = targetWorldVelocity;
            ActualWorldVelocity = actualWorldVelocity;
            AttitudeTrackingErrorDegrees = attitudeTrackingErrorDegrees;
            Saturation = saturation;
            YawScale = yawScale;
        }

        internal float Time { get; }
        internal float Height { get; }
        internal float TargetHeight { get; }
        internal float HorizontalSpeed { get; }
        internal float VerticalSpeed { get; }
        internal float TiltDegrees { get; }
        internal Vector3 TargetLocalRate { get; }
        internal Vector3 ActualLocalRate { get; }
        internal DronePidTelemetry Roll { get; }
        internal DronePidTelemetry Pitch { get; }
        internal DronePidTelemetry Yaw { get; }
        internal QuadrotorMotorOutput Motors { get; }
        internal Vector3 TargetWorldVelocity { get; }
        internal Vector3 ActualWorldVelocity { get; }
        internal float AttitudeTrackingErrorDegrees { get; }
        internal DroneControlSaturation Saturation { get; }
        internal float YawScale { get; }

        internal bool IsFinite => float.IsFinite(Time)
                                  && float.IsFinite(Height)
                                  && float.IsFinite(TargetHeight)
                                  && float.IsFinite(HorizontalSpeed)
                                  && float.IsFinite(VerticalSpeed)
                                  && float.IsFinite(TiltDegrees)
                                  && IsFiniteVector(TargetWorldVelocity)
                                  && IsFiniteVector(ActualWorldVelocity)
                                  && float.IsFinite(AttitudeTrackingErrorDegrees)
                                  && float.IsFinite(YawScale);

        private static bool IsFiniteVector(Vector3 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        }
    }

    /// <summary>固定容量环形缓冲，生成可复制的调参摘要。</summary>
    internal sealed class DroneTelemetryBuffer
    {
        private readonly DroneTelemetrySample[] samples;
        private int nextIndex;

        internal DroneTelemetryBuffer(int capacity)
        {
            samples = new DroneTelemetrySample[Math.Max(2, capacity)];
        }

        internal int Count { get; private set; }

        internal void Add(DroneTelemetrySample sample)
        {
            samples[nextIndex] = sample;
            nextIndex = (nextIndex + 1) % samples.Length;
            Count = Math.Min(Count + 1, samples.Length);
        }

        internal void Clear()
        {
            Array.Clear(samples, 0, samples.Length);
            nextIndex = 0;
            Count = 0;
        }

        internal string BuildSummary()
        {
            if (Count == 0)
            {
                return "DroneFlight telemetry: no samples";
            }

            var heightAbsoluteErrorSum = 0f;
            var maximumHeightAbsoluteError = 0f;
            var maximumTilt = 0f;
            var maximumHorizontalSpeed = 0f;
            var saturatedSamples = 0;
            var thrustSaturatedSamples = 0;
            var pitchSaturatedSamples = 0;
            var yawSaturatedSamples = 0;
            var rollSaturatedSamples = 0;
            var yawReducedSamples = 0;
            var velocityErrorSum = 0f;
            var maximumVelocityError = 0f;
            var attitudeErrorSum = 0f;
            var maximumAttitudeError = 0f;
            var commandStartTime = float.NaN;
            var responseStartTime = float.NaN;
            var invalidSamples = 0;
            var first = GetChronological(0);
            var last = first;
            for (var index = 0; index < Count; index++)
            {
                var sample = GetChronological(index);
                last = sample;
                var heightError = Math.Abs(sample.TargetHeight - sample.Height);
                heightAbsoluteErrorSum += heightError;
                maximumHeightAbsoluteError = Math.Max(maximumHeightAbsoluteError, heightError);
                maximumTilt = Math.Max(maximumTilt, sample.TiltDegrees);
                maximumHorizontalSpeed = Math.Max(maximumHorizontalSpeed, sample.HorizontalSpeed);
                saturatedSamples += sample.Motors.IsSaturated ? 1 : 0;
                thrustSaturatedSamples += sample.Saturation.Thrust != DroneSaturationDirection.None ? 1 : 0;
                pitchSaturatedSamples += sample.Saturation.Pitch != DroneSaturationDirection.None ? 1 : 0;
                yawSaturatedSamples += sample.Saturation.Yaw != DroneSaturationDirection.None ? 1 : 0;
                rollSaturatedSamples += sample.Saturation.Roll != DroneSaturationDirection.None ? 1 : 0;
                yawReducedSamples += sample.YawScale < 0.999f ? 1 : 0;
                var velocityError = (sample.TargetWorldVelocity - sample.ActualWorldVelocity).magnitude;
                velocityErrorSum += velocityError;
                maximumVelocityError = Math.Max(maximumVelocityError, velocityError);
                attitudeErrorSum += sample.AttitudeTrackingErrorDegrees;
                maximumAttitudeError = Math.Max(maximumAttitudeError, sample.AttitudeTrackingErrorDegrees);
                var horizontalTarget = Vector3.ProjectOnPlane(sample.TargetWorldVelocity, Vector3.up);
                var horizontalActual = Vector3.ProjectOnPlane(sample.ActualWorldVelocity, Vector3.up);
                if (float.IsNaN(commandStartTime) && horizontalTarget.magnitude > 0.1f)
                {
                    commandStartTime = sample.Time;
                }

                if (!float.IsNaN(commandStartTime) && float.IsNaN(responseStartTime)
                    && horizontalTarget.sqrMagnitude > 0.01f
                    && Vector3.Dot(horizontalActual, horizontalTarget.normalized) >= horizontalTarget.magnitude * 0.1f)
                {
                    responseStartTime = sample.Time;
                }
                invalidSamples += sample.IsFinite ? 0 : 1;
            }

            var builder = new StringBuilder(256);
            builder.AppendLine("DroneFlight telemetry summary");
            builder.AppendLine($"samples={Count}, duration={Math.Max(0f, last.Time - first.Time):F2}s");
            builder.AppendLine($"height.mae={heightAbsoluteErrorSum / Count:F3}m, height.maxError={maximumHeightAbsoluteError:F3}m");
            builder.AppendLine($"tilt.max={maximumTilt:F2}deg, horizontalSpeed.max={maximumHorizontalSpeed:F2}m/s");
            builder.AppendLine($"velocity.mae={velocityErrorSum / Count:F3}m/s, velocity.maxError={maximumVelocityError:F3}m/s");
            builder.AppendLine($"attitude.mae={attitudeErrorSum / Count:F2}deg, attitude.maxError={maximumAttitudeError:F2}deg");
            builder.AppendLine($"motor.saturated={saturatedSamples}/{Count}, axis.thrust/pitch/yaw/roll={thrustSaturatedSamples}/{pitchSaturatedSamples}/{yawSaturatedSamples}/{rollSaturatedSamples}, yawReduced={yawReducedSamples}");
            builder.AppendLine($"input.responseDelay={(float.IsNaN(responseStartTime) ? -1f : Math.Max(0f, responseStartTime - commandStartTime)):F3}s");
            builder.AppendLine($"invalid={invalidSamples}");
            builder.Append($"last.rateTarget={last.TargetLocalRate:F3}, last.rateActual={last.ActualLocalRate:F3}");
            return builder.ToString();
        }

        private DroneTelemetrySample GetChronological(int chronologicalIndex)
        {
            var start = Count == samples.Length ? nextIndex : 0;
            return samples[(start + chronologicalIndex) % samples.Length];
        }
    }

    /// <summary>采集飞控固定步遥测；F4 将最近窗口摘要复制到剪贴板。</summary>
    [RequireComponent(typeof(DroneFlightController))]
    public sealed class DroneTelemetryRecorder : MonoBehaviour
    {
        [SerializeField, InspectorName("诊断配置")]
        [Tooltip("集中管理遥测样本容量和界面刷新频率。")]
        private DroneDiagnosticsConfig config;

        private DroneFlightController controller;
        private DroneTelemetryBuffer buffer;

        /// 当前遥测与调试显示共用的配置资产。
        internal DroneDiagnosticsConfig Config => config;

        private void Awake()
        {
            controller = GetComponent<DroneFlightController>();
            buffer = new DroneTelemetryBuffer(config != null ? config.SampleCapacity : 500);
        }

        private void FixedUpdate()
        {
            if (controller == null || controller.Body == null)
            {
                return;
            }

            var body = controller.Body;
            var horizontalVelocity = new Vector2(body.linearVelocity.x, body.linearVelocity.z);
            buffer.Add(new DroneTelemetrySample(
                Time.fixedTime,
                body.position.y,
                controller.TargetHeightMeters,
                horizontalVelocity.magnitude,
                body.linearVelocity.y,
                Vector3.Angle(controller.transform.up, Vector3.up),
                controller.LastTargetLocalRate,
                controller.LastActualLocalRate,
                controller.RollRateTelemetry,
                controller.PitchRateTelemetry,
                controller.YawRateTelemetry,
                controller.LastMotorOutput,
                controller.LastDesiredWorldVelocity,
                body.linearVelocity,
                CalculateAttitudeError(controller),
                controller.LastAllocation.Saturation,
                controller.LastAllocation.YawScale));
        }

        private static float CalculateAttitudeError(DroneFlightController controller)
        {
            var force = controller.LastDesiredWorldForce;
            return force.sqrMagnitude > 0.000001f
                ? Vector3.Angle(controller.transform.up, force.normalized)
                : 0f;
        }

        private void Update()
        {
            if (Keyboard.current == null || !Keyboard.current.f4Key.wasPressedThisFrame)
            {
                return;
            }

            GUIUtility.systemCopyBuffer = buffer.BuildSummary();
            Debug.Log("[DroneFlight] 已复制最近遥测摘要。", this);
        }
    }
}
