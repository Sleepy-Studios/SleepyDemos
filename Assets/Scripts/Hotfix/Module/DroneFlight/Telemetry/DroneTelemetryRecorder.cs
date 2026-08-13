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
            QuadrotorMotorOutput motors)
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

        internal bool IsFinite => float.IsFinite(Time)
                                  && float.IsFinite(Height)
                                  && float.IsFinite(TargetHeight)
                                  && float.IsFinite(HorizontalSpeed)
                                  && float.IsFinite(VerticalSpeed)
                                  && float.IsFinite(TiltDegrees);
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
                invalidSamples += sample.IsFinite ? 0 : 1;
            }

            var builder = new StringBuilder(256);
            builder.AppendLine("DroneFlight telemetry summary");
            builder.AppendLine($"samples={Count}, duration={Math.Max(0f, last.Time - first.Time):F2}s");
            builder.AppendLine($"height.mae={heightAbsoluteErrorSum / Count:F3}m, height.maxError={maximumHeightAbsoluteError:F3}m");
            builder.AppendLine($"tilt.max={maximumTilt:F2}deg, horizontalSpeed.max={maximumHorizontalSpeed:F2}m/s");
            builder.AppendLine($"motor.saturated={saturatedSamples}/{Count}, invalid={invalidSamples}");
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
        [SerializeField] private int sampleCapacity = 500;

        private DroneFlightController controller;
        private DroneTelemetryBuffer buffer;

        private void Awake()
        {
            controller = GetComponent<DroneFlightController>();
            buffer = new DroneTelemetryBuffer(sampleCapacity);
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
                controller.LastMotorOutput));
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
