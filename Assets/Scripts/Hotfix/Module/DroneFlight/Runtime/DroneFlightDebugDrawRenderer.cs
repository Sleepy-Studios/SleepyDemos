using System;
using TMPro;
using UnityEngine;

namespace Hotfix.DroneFlight
{
    /// <summary>以无物理组件的世界空间箭头显示飞行动力数据，Game 与 Scene 视图共用。</summary>
    public sealed class DroneFlightDebugDrawRenderer : MonoBehaviour
    {
        private const int RotorCount = 4;
        private const int VectorCount = 9;
        private const float VisualSmoothingSharpness = 18f;
        private static readonly string[] RotorNames = { "左前 (FL)", "右前 (FR)", "左后 (RL)", "右后 (RR)" };
        private readonly Vector3[] rotorOrigins = new Vector3[RotorCount];
        private readonly DroneDebugVectorSmoother[] rotorForces = new DroneDebugVectorSmoother[RotorCount];
        private readonly WorldVectorVisual[] visuals = new WorldVectorVisual[VectorCount];
        private DroneFlightController flightController;
        private DroneCameraRig cameraRig;
        private DroneDebugVectorSmoother totalThrust;
        private DroneDebugVectorSmoother gravity;
        private DroneDebugVectorSmoother actualVelocity;
        private DroneDebugVectorSmoother desiredVelocity;
        private DroneDebugVectorSmoother desiredAcceleration;
        private Material lineMaterial;
        private Material labelMaterial;
        private Transform visualRoot;
        private Vector3 bodyCenter;

        internal void Configure(DroneFlightSceneContext context)
        {
            flightController = context != null ? context.FlightController : null;
            cameraRig = context != null ? context.CameraRig : null;
            ResetSnapshot();
            enabled = false;
        }

        private void OnEnable()
        {
            ResetSnapshot();
            EnsureVisuals();
            SetVisualsActive(true);
        }

        private void OnDisable()
        {
            ResetSnapshot();
            SetVisualsActive(false);
        }

        private void OnDestroy()
        {
            if (visualRoot != null) Destroy(visualRoot.gameObject);
            if (lineMaterial != null) Destroy(lineMaterial);
            if (labelMaterial != null) Destroy(labelMaterial);
        }

        private void LateUpdate()
        {
            if (flightController == null || flightController.Body == null)
            {
                SetVisualsActive(false);
                ResetSnapshot();
                return;
            }

            EnsureVisuals();
            SetVisualsActive(true);
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
            bodyCenter = DroneDebugVectorSmoother.Sanitize(body.transform.TransformPoint(body.centerOfMass));
            totalThrust.Step(flightController.CurrentTotalThrustVector, VisualSmoothingSharpness, deltaTime);
            gravity.Step(Physics.gravity * flightController.CurrentSupportedMassKilograms, VisualSmoothingSharpness, deltaTime);
            actualVelocity.Step(body.linearVelocity, VisualSmoothingSharpness, deltaTime);
            desiredVelocity.Step(flightController.LastDesiredWorldVelocity, VisualSmoothingSharpness, deltaTime);
            desiredAcceleration.Step(flightController.LastDesiredWorldAcceleration, VisualSmoothingSharpness, deltaTime);

            var camera = cameraRig != null ? cameraRig.OutputCamera : Camera.main;
            for (var index = 0; index < RotorCount; index++)
            {
                var thrust = rotorForces[index].Current;
                visuals[index].Update(rotorOrigins[index], thrust, 10f,
                    $"{RotorNames[index]} {thrust.magnitude:F1} N", camera, GetRotorLabelOffset(index));
            }
            visuals[4].Update(bodyCenter, totalThrust.Current, 10f,
                $"总升力 {totalThrust.Current.magnitude:F1} N", camera, new Vector2(-34f, 20f));
            visuals[5].Update(bodyCenter, gravity.Current, 10f,
                $"重力 {gravity.Current.magnitude:F1} N", camera, new Vector2(28f, -20f));
            visuals[6].Update(bodyCenter, actualVelocity.Current, 4f,
                $"实际速度 {actualVelocity.Current.magnitude:F1} m/s", camera, new Vector2(24f, 22f));
            visuals[7].Update(bodyCenter, desiredVelocity.Current, 4f,
                $"目标速度 {desiredVelocity.Current.magnitude:F1} m/s", camera, new Vector2(24f, 46f));
            visuals[8].Update(bodyCenter, desiredAcceleration.Current, 4f,
                $"目标加速度 {desiredAcceleration.Current.magnitude:F1} m/s²", camera, new Vector2(-36f, 46f));
        }

        private void EnsureVisuals()
        {
            if (visualRoot != null) return;
            var rootObject = new GameObject("F2WorldVectors");
            visualRoot = rootObject.transform;
            visualRoot.SetParent(transform, false);
            lineMaterial = new Material(Shader.Find("Sprites/Default")) { name = "F2 World Vector Material" };
            var defaultFont = TMP_Settings.defaultFontAsset;
            if (defaultFont != null)
            {
                labelMaterial = new Material(defaultFont.material) { name = "F2 World Vector Label Material" };
                labelMaterial.SetColor(ShaderUtilities.ID_OutlineColor, new Color32(8, 10, 12, 255));
                labelMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.28f);
            }
            var colors = new[] { Color.cyan, Color.cyan, Color.cyan, Color.cyan, Color.yellow,
                new Color(1f, 0.25f, 0.2f), Color.green, new Color(0.2f, 0.65f, 1f), new Color(1f, 0.35f, 1f) };
            for (var index = 0; index < visuals.Length; index++)
                visuals[index] = WorldVectorVisual.Create(
                    visualRoot,
                    $"Vector_{index + 1}",
                    colors[index],
                    lineMaterial,
                    labelMaterial);
        }

        private void SetVisualsActive(bool value)
        {
            if (visualRoot != null && visualRoot.gameObject.activeSelf != value) visualRoot.gameObject.SetActive(value);
        }

        private void ResetSnapshot()
        {
            bodyCenter = Vector3.zero;
            Array.Clear(rotorOrigins, 0, rotorOrigins.Length);
            Array.Clear(rotorForces, 0, rotorForces.Length);
            totalThrust.Reset(); gravity.Reset(); actualVelocity.Reset(); desiredVelocity.Reset(); desiredAcceleration.Reset();
        }

        private static Vector2 GetRotorLabelOffset(int index) => index switch
        {
            0 => new Vector2(-46f, 26f),
            1 => new Vector2(18f, 26f),
            2 => new Vector2(-46f, -24f),
            _ => new Vector2(18f, -24f)
        };

        private sealed class WorldVectorVisual
        {
            private readonly GameObject root;
            private readonly LineRenderer shaft;
            private readonly LineRenderer leftHead;
            private readonly LineRenderer rightHead;
            private readonly TextMeshPro label;

            private WorldVectorVisual(GameObject owner, LineRenderer line, LineRenderer left, LineRenderer right, TextMeshPro text)
            { root = owner; shaft = line; leftHead = left; rightHead = right; label = text; }

            internal static WorldVectorVisual Create(
                Transform parent,
                string name,
                Color color,
                Material material,
                Material textMaterial)
            {
                var owner = new GameObject(name); owner.transform.SetParent(parent, false);
                var shaft = CreateLine(owner.transform, "Shaft", color, material);
                var left = CreateLine(owner.transform, "ArrowHeadLeft", color, material);
                var right = CreateLine(owner.transform, "ArrowHeadRight", color, material);
                var labelObject = new GameObject("ValueLabel", typeof(TextMeshPro)); labelObject.transform.SetParent(owner.transform, false);
                var text = labelObject.GetComponent<TextMeshPro>();
                text.alignment = TextAlignmentOptions.MidlineLeft;
                text.fontSize = 3f;
                text.color = color;
                if (textMaterial != null)
                {
                    text.fontSharedMaterial = textMaterial;
                }
                return new WorldVectorVisual(owner, shaft, left, right, text);
            }

            internal void Update(
                Vector3 origin,
                Vector3 physicalVector,
                float referenceMagnitude,
                string text,
                Camera camera,
                Vector2 labelPixelOffset)
            {
                var visible = physicalVector.sqrMagnitude >= 0.000001f && camera != null;
                root.SetActive(visible);
                if (!visible) return;
                var direction = physicalVector.normalized;
                var unitsPerPixel = CalculateWorldUnitsPerPixel(camera, origin);
                var maximumPixels = Mathf.Min(camera.pixelWidth, camera.pixelHeight) * 0.22f;
                var maximumWorldLength = unitsPerPixel * maximumPixels;
                var desiredLength = DroneDebugVectorPresentationMath.CalculateSaturatedLength(
                    physicalVector.magnitude,
                    referenceMagnitude,
                    maximumWorldLength);
                var displayedVector = DroneDebugVectorPresentationMath.ClampVectorToViewport(
                    camera,
                    origin,
                    direction * desiredLength,
                    new Rect(0.06f, 0.08f, 0.88f, 0.84f));
                var end = origin + displayedVector;
                shaft.SetPosition(0, origin); shaft.SetPosition(1, end);
                var viewNormal = camera != null ? camera.transform.forward : Vector3.forward;
                var side = Vector3.Cross(direction, viewNormal).normalized;
                if (side.sqrMagnitude < 0.001f) side = Vector3.Cross(direction, Vector3.up).normalized;
                var headLength = Mathf.Min(displayedVector.magnitude * 0.4f, unitsPerPixel * 12f);
                leftHead.SetPosition(0, end); leftHead.SetPosition(1, end - direction * headLength + side * headLength * 0.45f);
                rightHead.SetPosition(0, end); rightHead.SetPosition(1, end - direction * headLength - side * headLength * 0.45f);
                var width = unitsPerPixel * 3f;
                shaft.widthMultiplier = leftHead.widthMultiplier = rightHead.widthMultiplier = width;
                label.text = text;
                var screen = camera.WorldToScreenPoint(end);
                screen.x = Mathf.Clamp(screen.x + labelPixelOffset.x, camera.pixelWidth * 0.06f, camera.pixelWidth * 0.94f);
                screen.y = Mathf.Clamp(screen.y + labelPixelOffset.y, camera.pixelHeight * 0.08f, camera.pixelHeight * 0.92f);
                label.transform.position = camera.ScreenToWorldPoint(screen);
                label.transform.rotation = Quaternion.LookRotation(
                    label.transform.position - camera.transform.position,
                    camera.transform.up);
                label.transform.localScale = Vector3.one * unitsPerPixel * 6f;
            }

            private static float CalculateWorldUnitsPerPixel(Camera camera, Vector3 worldPosition)
            {
                var screen = camera.WorldToScreenPoint(worldPosition);
                if (screen.z <= camera.nearClipPlane)
                {
                    return 0.001f;
                }
                var right = camera.ScreenToWorldPoint(screen + Vector3.right) - camera.ScreenToWorldPoint(screen);
                return Mathf.Max(0.00001f, right.magnitude);
            }

            private static LineRenderer CreateLine(Transform parent, string name, Color color, Material material)
            {
                var child = new GameObject(name, typeof(LineRenderer)); child.transform.SetParent(parent, false);
                var line = child.GetComponent<LineRenderer>(); line.useWorldSpace = true; line.positionCount = 2;
                line.widthMultiplier = 0.008f; line.sharedMaterial = material; line.startColor = line.endColor = color; line.numCapVertices = 2;
                return line;
            }
        }
    }

    /// <summary>F2 世界矢量的屏幕安全显示数学，不改变原始遥测数值。</summary>
    internal static class DroneDebugVectorPresentationMath
    {
        internal static float CalculateSaturatedLength(float magnitude, float referenceMagnitude, float maximumLength)
        {
            if (!float.IsFinite(magnitude) || !float.IsFinite(referenceMagnitude)
                || !float.IsFinite(maximumLength) || magnitude <= 0f || referenceMagnitude <= 0f
                || maximumLength <= 0f)
            {
                return 0f;
            }

            return maximumLength * (1f - Mathf.Exp(-magnitude / referenceMagnitude));
        }

        internal static Vector3 ClampVectorToViewport(
            Camera camera,
            Vector3 origin,
            Vector3 desiredVector,
            Rect safeViewport)
        {
            if (camera == null || desiredVector.sqrMagnitude <= 0.000001f)
            {
                return Vector3.zero;
            }

            if (IsInside(camera.WorldToViewportPoint(origin + desiredVector), safeViewport))
            {
                return desiredVector;
            }

            var lower = 0f;
            var upper = 1f;
            for (var iteration = 0; iteration < 18; iteration++)
            {
                var value = (lower + upper) * 0.5f;
                if (IsInside(camera.WorldToViewportPoint(origin + desiredVector * value), safeViewport))
                {
                    lower = value;
                }
                else
                {
                    upper = value;
                }
            }

            return desiredVector * lower;
        }

        private static bool IsInside(Vector3 viewport, Rect safeViewport) =>
            viewport.z > 0f
            && viewport.x >= safeViewport.xMin && viewport.x <= safeViewport.xMax
            && viewport.y >= safeViewport.yMin && viewport.y <= safeViewport.yMax;
    }

    /// <summary>用于调试矢量显示的帧率无关指数平滑状态。</summary>
    internal struct DroneDebugVectorSmoother
    {
        /// 当前可直接绘制的有限向量。
        internal Vector3 Current { get; private set; }
        /// 是否已经捕获过首帧目标。
        internal bool HasValue { get; private set; }
        internal Vector3 Step(Vector3 target, float sharpness, float deltaTime)
        {
            if (!IsFinite(target)) { Current = Vector3.zero; HasValue = true; return Current; }
            if (!HasValue || !float.IsFinite(deltaTime) || deltaTime <= 0f) { Current = target; HasValue = true; return Current; }
            var blend = 1f - Mathf.Exp(-Mathf.Max(0f, sharpness) * deltaTime);
            Current = Vector3.LerpUnclamped(Current, target, blend); return Current;
        }
        internal void Reset() { Current = Vector3.zero; HasValue = false; }
        internal static Vector3 Sanitize(Vector3 value) => IsFinite(value) ? value : Vector3.zero;
        private static bool IsFinite(Vector3 value) => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
    }
}
