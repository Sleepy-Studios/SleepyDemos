using UnityEngine;

namespace Hotfix.DroneFlight
{
    /// <summary>Editor Game 视图中的飞行动力向量绘制；不属于 UI Prefab。</summary>
    public sealed class DroneFlightDebugDrawRenderer : MonoBehaviour
    {
        private DroneFlightController flightController;
        private DroneCameraRig cameraRig;

        internal void Configure(DroneFlightSceneContext context)
        {
            flightController = context != null ? context.FlightController : null;
            cameraRig = context != null ? context.CameraRig : null;
            enabled = false;
        }

#if UNITY_EDITOR
        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || flightController == null || flightController.Body == null)
            {
                return;
            }

            var outputCamera = cameraRig != null ? cameraRig.OutputCamera : Camera.main;
            if (outputCamera == null)
            {
                return;
            }

            var names = new[] { "左前 (FL)", "右前 (FR)", "左后 (RL)", "右后 (RR)" };
            for (var index = 0; index < 4; index++)
            {
                if (flightController.TryGetRotorDebugVector(index, out var origin, out var thrust))
                {
                    DrawWorldVector(outputCamera, origin, thrust * 0.08f, Color.cyan,
                        $"{names[index]} {thrust.magnitude:F1} N");
                }
            }

            var center = flightController.Body.worldCenterOfMass;
            DrawWorldVector(outputCamera, center, flightController.CurrentTotalThrustVector * 0.06f,
                Color.yellow, $"总升力 {flightController.CurrentTotalThrustVector.magnitude:F1} N");
            DrawWorldVector(outputCamera, center,
                Physics.gravity * flightController.CurrentSupportedMassKilograms * 0.06f,
                new Color(1f, 0.25f, 0.2f), "重力");
            DrawWorldVector(outputCamera, center, flightController.Body.linearVelocity * 0.35f,
                Color.green, $"实际速度 {flightController.Body.linearVelocity.magnitude:F1} m/s");
            DrawWorldVector(outputCamera, center, flightController.LastDesiredWorldVelocity * 0.35f,
                new Color(0.2f, 0.65f, 1f), "目标速度");
            DrawWorldVector(outputCamera, center, flightController.LastDesiredWorldAcceleration * 0.25f,
                new Color(1f, 0.35f, 1f), "目标加速度");

            GUI.Label(new Rect(12f, Screen.height - 90f, 760f, 72f),
                "F3 动力矢量：青=单旋翼升力  黄=总升力  红=重力\n绿=实际速度  蓝=目标速度  紫=目标加速度");
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
    }
}
