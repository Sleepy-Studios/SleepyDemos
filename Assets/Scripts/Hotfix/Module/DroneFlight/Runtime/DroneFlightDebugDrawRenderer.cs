using System;
using UnityEngine;

namespace Hotfix.DroneFlight
{
    /// <summary>Editor Game 视图中的飞行动力向量绘制；不属于 UI Prefab。</summary>
    public sealed class DroneFlightDebugDrawRenderer : MonoBehaviour
    {
        private const int RotorCount = 4;
        private const float VisualSmoothingSharpness = 18f;

        private DroneFlightController flightController;
        private DroneCameraRig cameraRig;
        private readonly Vector3[] rotorOrigins = new Vector3[RotorCount];
        private readonly DroneDebugVectorSmoother[] rotorForces = new DroneDebugVectorSmoother[RotorCount];
        private DroneDebugVectorSmoother totalThrust;
        private DroneDebugVectorSmoother gravity;
        private DroneDebugVectorSmoother actualVelocity;
        private DroneDebugVectorSmoother desiredVelocity;
        private DroneDebugVectorSmoother desiredAcceleration;
        private Vector3 bodyCenter;
        private bool hasSnapshot;

        internal void Configure(DroneFlightSceneContext context)
        {
            flightController = context != null ? context.FlightController : null;
            cameraRig = context != null ? context.CameraRig : null;
            ResetSnapshot();
            enabled = false;
        }

#if UNITY_EDITOR
        private void OnEnable()
        {
            ResetSnapshot();
        }

        private void OnDisable()
        {
            ResetSnapshot();
        }

        private void LateUpdate()
        {
            if (flightController == null || flightController.Body == null)
            {
                ResetSnapshot();
                return;
            }

            var deltaTime = Mathf.Max(0f, Time.unscaledDeltaTime);
            for (var index = 0; index < RotorCount; index++)
            {
                if (flightController.TryGetRotorDebugVector(index, out var origin, out var thrust))
                {
                    rotorOrigins[index] = DroneDebugVectorSmoother.Sanitize(origin);
                    rotorForces[index].Step(thrust, VisualSmoothingSharpness, deltaTime);
                }
                else
                {
                    rotorOrigins[index] = Vector3.zero;
                    rotorForces[index].Step(Vector3.zero, VisualSmoothingSharpness, deltaTime);
                }
            }

            var body = flightController.Body;
            bodyCenter = DroneDebugVectorSmoother.Sanitize(
                body.transform.TransformPoint(body.centerOfMass));
            totalThrust.Step(flightController.CurrentTotalThrustVector, VisualSmoothingSharpness, deltaTime);
            gravity.Step(
                Physics.gravity * flightController.CurrentSupportedMassKilograms,
                VisualSmoothingSharpness,
                deltaTime);
            actualVelocity.Step(body.linearVelocity, VisualSmoothingSharpness, deltaTime);
            desiredVelocity.Step(
                flightController.LastDesiredWorldVelocity,
                VisualSmoothingSharpness,
                deltaTime);
            desiredAcceleration.Step(
                flightController.LastDesiredWorldAcceleration,
                VisualSmoothingSharpness,
                deltaTime);
            hasSnapshot = true;
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || !hasSnapshot)
            {
                return;
            }

            var outputCamera = cameraRig != null ? cameraRig.OutputCamera : Camera.main;
            if (outputCamera == null)
            {
                return;
            }

            var names = new[] { "左前 (FL)", "右前 (FR)", "左后 (RL)", "右后 (RR)" };
            for (var index = 0; index < RotorCount; index++)
            {
                var thrust = rotorForces[index].Current;
                DrawWorldVector(outputCamera, rotorOrigins[index], thrust * 0.08f, Color.cyan,
                    $"{names[index]} {thrust.magnitude:F1} N");
            }

            DrawWorldVector(outputCamera, bodyCenter, totalThrust.Current * 0.06f,
                Color.yellow, $"总升力 {totalThrust.Current.magnitude:F1} N");
            DrawWorldVector(outputCamera, bodyCenter, gravity.Current * 0.06f,
                new Color(1f, 0.25f, 0.2f), "重力");
            DrawWorldVector(outputCamera, bodyCenter, actualVelocity.Current * 0.35f,
                Color.green, $"实际速度 {actualVelocity.Current.magnitude:F1} m/s");
            DrawWorldVector(outputCamera, bodyCenter, desiredVelocity.Current * 0.35f,
                new Color(0.2f, 0.65f, 1f), "目标速度");
            DrawWorldVector(outputCamera, bodyCenter, desiredAcceleration.Current * 0.25f,
                new Color(1f, 0.35f, 1f), "目标加速度");

            GUI.Label(new Rect(12f, Screen.height - 90f, 760f, 72f),
                "F2 动力矢量：青=单旋翼升力  黄=总升力  红=重力\n绿=实际速度  蓝=目标速度  紫=目标加速度");
        }

        private static void DrawWorldVector(Camera camera, Vector3 origin, Vector3 vector, Color color, string label)
        {
            if (vector.sqrMagnitude < 0.000001f)
            {
                return;
            }

            var startWorld = camera.WorldToScreenPoint(origin);
            var endWorld = camera.WorldToScreenPoint(origin + vector);
            if (startWorld.z <= 0f || endWorld.z <= 0f)
            {
                return;
            }

            var start = new Vector2(startWorld.x, Screen.height - startWorld.y);
            var end = new Vector2(endWorld.x, Screen.height - endWorld.y);
            DrawLine(start, end, color, 3f);
            var direction = (end - start).normalized;
            var perpendicular = new Vector2(-direction.y, direction.x);
            DrawLine(end, end - direction * 10f + perpendicular * 5f, color, 3f);
            DrawLine(end, end - direction * 10f - perpendicular * 5f, color, 3f);
            var style = new GUIStyle(GUI.skin.label) { fontSize = 11 };
            style.normal.textColor = color;
            GUI.Label(new Rect(end.x + 5f, end.y - 10f, 220f, 22f), label, style);
        }

        private static void DrawLine(Vector2 start, Vector2 end, Color color, float width)
        {
            var delta = end - start;
            if (delta.sqrMagnitude < 0.01f)
            {
                return;
            }

            var matrix = GUI.matrix;
            var previous = GUI.color;
            GUI.color = color;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, start);
            GUI.DrawTexture(new Rect(start.x, start.y - width * 0.5f, delta.magnitude, width), Texture2D.whiteTexture);
            GUI.matrix = matrix;
            GUI.color = previous;
        }
#endif

        private void ResetSnapshot()
        {
            hasSnapshot = false;
            bodyCenter = Vector3.zero;
            Array.Clear(rotorOrigins, 0, rotorOrigins.Length);
            Array.Clear(rotorForces, 0, rotorForces.Length);
            totalThrust.Reset();
            gravity.Reset();
            actualVelocity.Reset();
            desiredVelocity.Reset();
            desiredAcceleration.Reset();
        }
    }

    /// <summary>用于调试矢量显示的帧率无关指数平滑状态。</summary>
    internal struct DroneDebugVectorSmoother
    {
        /// 当前可直接绘制的有限向量。
        internal Vector3 Current { get; private set; }

        /// 是否已经捕获过首帧目标。
        internal bool HasValue { get; private set; }

        /// <summary>
        /// 捕获并平滑目标；首帧直接采用目标，非有限目标立即归零。
        /// </summary>
        /// <param name="target">本渲染帧读取的物理目标向量。</param>
        /// <param name="sharpness">指数平滑锐度；值越大跟随越快。</param>
        /// <param name="deltaTime">当前非缩放渲染帧时长。</param>
        /// <returns>本帧可用于绘制的有限向量。</returns>
        internal Vector3 Step(Vector3 target, float sharpness, float deltaTime)
        {
            if (!IsFinite(target))
            {
                Current = Vector3.zero;
                HasValue = true;
                return Current;
            }

            if (!HasValue || !float.IsFinite(deltaTime) || deltaTime <= 0f)
            {
                Current = target;
                HasValue = true;
                return Current;
            }

            var blend = 1f - Mathf.Exp(-Mathf.Max(0f, sharpness) * deltaTime);
            Current = Vector3.LerpUnclamped(Current, target, blend);
            return Current;
        }

        /// 清空平滑历史，使下一帧直接捕获目标。
        internal void Reset()
        {
            Current = Vector3.zero;
            HasValue = false;
        }

        /// <summary>
        /// 把非有限向量转换为零，保证世界坐标投影安全。
        /// </summary>
        /// <param name="value">待检查的世界空间向量或坐标。</param>
        /// <returns>有限原值或零向量。</returns>
        internal static Vector3 Sanitize(Vector3 value) => IsFinite(value) ? value : Vector3.zero;

        private static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
    }
}
