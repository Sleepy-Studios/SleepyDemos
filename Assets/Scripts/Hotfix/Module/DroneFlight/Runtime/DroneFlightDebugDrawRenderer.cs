using System;
using System.Collections.Generic;
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
        private Renderer[] bodyRenderers = Array.Empty<Renderer>();

        private static readonly int[] LabelLayoutPriority = { 4, 5, 8, 7, 6, 0, 1, 2, 3 };

        internal void Configure(DroneFlightSceneContext context)
        {
            flightController = context != null ? context.FlightController : null;
            cameraRig = context != null ? context.CameraRig : null;
            bodyRenderers = flightController != null
                ? flightController.GetComponentsInChildren<Renderer>(true)
                : Array.Empty<Renderer>();
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
                visuals[index].UpdateGeometry(rotorOrigins[index], thrust, 10f,
                    $"{RotorNames[index]} {thrust.magnitude:F1} N", camera, GetRotorLabelOffset(index));
            }
            visuals[4].UpdateGeometry(bodyCenter, totalThrust.Current, 10f,
                $"总升力 {totalThrust.Current.magnitude:F1} N", camera, new Vector2(-34f, 20f));
            visuals[5].UpdateGeometry(bodyCenter, gravity.Current, 10f,
                $"重力 {gravity.Current.magnitude:F1} N", camera, new Vector2(28f, -20f));
            visuals[6].UpdateGeometry(bodyCenter, actualVelocity.Current, 4f,
                $"实际速度 {actualVelocity.Current.magnitude:F1} m/s", camera, new Vector2(24f, 22f));
            visuals[7].UpdateGeometry(bodyCenter, desiredVelocity.Current, 4f,
                $"目标速度 {desiredVelocity.Current.magnitude:F1} m/s", camera, new Vector2(24f, 46f));
            visuals[8].UpdateGeometry(bodyCenter, desiredAcceleration.Current, 4f,
                $"目标加速度 {desiredAcceleration.Current.magnitude:F1} m/s²", camera, new Vector2(-36f, 46f));

            LayoutLabels(camera);
        }

        private void LayoutLabels(Camera camera)
        {
            if (camera == null)
            {
                return;
            }

            var pixelRect = camera.pixelRect;
            var safeRect = new Rect(
                pixelRect.xMin + pixelRect.width * 0.06f,
                pixelRect.yMin + pixelRect.height * 0.08f,
                pixelRect.width * 0.88f,
                pixelRect.height * 0.84f);
            var reservedRect = CalculateBodyScreenRect(camera);
            var occupied = new List<Rect>(VectorCount);
            foreach (var index in LabelLayoutPriority)
            {
                if (!visuals[index].IsVisible)
                {
                    continue;
                }

                var placed = DroneDebugLabelLayoutMath.PlaceLabelRect(
                    visuals[index].DesiredLabelRect,
                    safeRect,
                    reservedRect,
                    occupied);
                visuals[index].PlaceLabel(camera, placed.center);
                occupied.Add(placed);
            }
        }

        private Rect CalculateBodyScreenRect(Camera camera)
        {
            var initialized = false;
            var minimum = Vector2.zero;
            var maximum = Vector2.zero;
            foreach (var renderer in bodyRenderers)
            {
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                var bounds = renderer.bounds;
                for (var corner = 0; corner < 8; corner++)
                {
                    var world = bounds.center + Vector3.Scale(
                        bounds.extents,
                        new Vector3(
                            (corner & 1) == 0 ? -1f : 1f,
                            (corner & 2) == 0 ? -1f : 1f,
                            (corner & 4) == 0 ? -1f : 1f));
                    var screen = camera.WorldToScreenPoint(world);
                    if (screen.z <= 0f)
                    {
                        continue;
                    }

                    var point = new Vector2(screen.x, screen.y);
                    if (!initialized)
                    {
                        minimum = maximum = point;
                        initialized = true;
                    }
                    else
                    {
                        minimum = Vector2.Min(minimum, point);
                        maximum = Vector2.Max(maximum, point);
                    }
                }
            }

            if (!initialized)
            {
                var center = camera.WorldToScreenPoint(bodyCenter);
                return new Rect(center.x - 60f, center.y - 40f, 120f, 80f);
            }

            const float padding = 12f;
            return Rect.MinMaxRect(
                minimum.x - padding,
                minimum.y - padding,
                maximum.x + padding,
                maximum.y + padding);
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
                labelMaterial.EnableKeyword(ShaderUtilities.Keyword_Underlay);
                labelMaterial.SetColor(ShaderUtilities.ID_UnderlayColor, new Color32(0, 0, 0, 170));
                labelMaterial.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.18f);
                labelMaterial.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.18f);
                labelMaterial.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.08f);
                labelMaterial.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.12f);
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
            private float labelScreenDepth;

            private WorldVectorVisual(GameObject owner, LineRenderer line, LineRenderer left, LineRenderer right, TextMeshPro text)
            { root = owner; shaft = line; leftHead = left; rightHead = right; label = text; }

            internal bool IsVisible => root.activeSelf;
            internal Rect DesiredLabelRect { get; private set; }

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
                text.alignment = TextAlignmentOptions.Center;
                text.fontSize = 36f;
                text.enableAutoSizing = false;
                text.fontStyle = FontStyles.Bold;
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.overflowMode = TextOverflowModes.Overflow;
                text.color = color;
                if (textMaterial != null)
                {
                    text.fontSharedMaterial = textMaterial;
                }
                return new WorldVectorVisual(owner, shaft, left, right, text);
            }

            internal void UpdateGeometry(
                Vector3 origin,
                Vector3 physicalVector,
                float referenceMagnitude,
                string text,
                Camera camera,
                Vector2 labelPixelOffset)
            {
                var visible = physicalVector.sqrMagnitude >= 0.000001f && camera != null;
                root.SetActive(visible);
                if (!visible)
                {
                    DesiredLabelRect = default;
                    return;
                }
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
                label.ForceMeshUpdate();
                var screen = camera.WorldToScreenPoint(end);
                labelScreenDepth = screen.z;
                var targetPixelHeight = DroneDebugLabelLayoutMath.CalculateTargetPixelHeight(
                    Mathf.Min(camera.pixelWidth, camera.pixelHeight));
                var renderedSize = label.textBounds.size;
                var localHeight = Mathf.Max(0.001f, renderedSize.y);
                var labelUnitsPerPixel = CalculateWorldUnitsPerPixel(camera, end);
                var worldScale = labelUnitsPerPixel * targetPixelHeight / localHeight;
                label.transform.localScale = Vector3.one * worldScale;
                var pixelWidth = Mathf.Max(
                    targetPixelHeight,
                    renderedSize.x * worldScale / labelUnitsPerPixel);
                var center = new Vector2(screen.x + labelPixelOffset.x, screen.y + labelPixelOffset.y);
                DesiredLabelRect = new Rect(
                    center.x - pixelWidth * 0.5f - 4f,
                    center.y - targetPixelHeight * 0.5f - 3f,
                    pixelWidth + 8f,
                    targetPixelHeight + 6f);
            }

            internal void PlaceLabel(Camera camera, Vector2 screenCenter)
            {
                var screen = new Vector3(screenCenter.x, screenCenter.y, labelScreenDepth);
                label.transform.position = camera.ScreenToWorldPoint(screen);
                label.transform.rotation = Quaternion.LookRotation(
                    label.transform.position - camera.transform.position,
                    camera.transform.up);
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

    /// <summary>F2 标签的恒定像素字号与确定性屏幕避让算法。</summary>
    internal static class DroneDebugLabelLayoutMath
    {
        private static readonly Vector2[] SearchDirections =
        {
            Vector2.up,
            Vector2.right,
            Vector2.left,
            Vector2.down,
            new Vector2(1f, 1f).normalized,
            new Vector2(-1f, 1f).normalized,
            new Vector2(1f, -1f).normalized,
            new Vector2(-1f, -1f).normalized
        };

        internal static float CalculateTargetPixelHeight(float screenShortSide) =>
            Mathf.Clamp(screenShortSide * 0.02037f, 18f, 26f);

        internal static Rect PlaceLabelRect(
            Rect desired,
            Rect safeRect,
            Rect reservedRect,
            IReadOnlyList<Rect> occupied)
        {
            var origin = ClampInside(desired, safeRect);
            if (!HasOverlap(origin, reservedRect, occupied))
            {
                return origin;
            }

            var best = origin;
            var bestScore = CalculateOverlapScore(origin, reservedRect, occupied);
            var step = Mathf.Max(12f, desired.height * 0.8f);
            for (var ring = 1; ring <= 12; ring++)
            {
                foreach (var direction in SearchDirections)
                {
                    var candidate = origin;
                    candidate.center += direction * (ring * step);
                    candidate = ClampInside(candidate, safeRect);
                    if (!HasOverlap(candidate, reservedRect, occupied))
                    {
                        return candidate;
                    }

                    var score = CalculateOverlapScore(candidate, reservedRect, occupied)
                                + Vector2.Distance(origin.center, candidate.center) * 0.001f;
                    if (score < bestScore)
                    {
                        best = candidate;
                        bestScore = score;
                    }
                }
            }

            return best;
        }

        private static Rect ClampInside(Rect value, Rect bounds)
        {
            var width = Mathf.Min(value.width, bounds.width);
            var height = Mathf.Min(value.height, bounds.height);
            var x = Mathf.Clamp(value.x, bounds.xMin, bounds.xMax - width);
            var y = Mathf.Clamp(value.y, bounds.yMin, bounds.yMax - height);
            return new Rect(x, y, width, height);
        }

        private static bool HasOverlap(Rect value, Rect reserved, IReadOnlyList<Rect> occupied)
        {
            if (value.Overlaps(reserved))
            {
                return true;
            }

            for (var index = 0; index < occupied.Count; index++)
            {
                if (value.Overlaps(occupied[index]))
                {
                    return true;
                }
            }

            return false;
        }

        private static float CalculateOverlapScore(Rect value, Rect reserved, IReadOnlyList<Rect> occupied)
        {
            var result = IntersectionArea(value, reserved);
            for (var index = 0; index < occupied.Count; index++)
            {
                result += IntersectionArea(value, occupied[index]);
            }
            return result;
        }

        private static float IntersectionArea(Rect left, Rect right)
        {
            var width = Mathf.Max(0f, Mathf.Min(left.xMax, right.xMax) - Mathf.Max(left.xMin, right.xMin));
            var height = Mathf.Max(0f, Mathf.Min(left.yMax, right.yMax) - Mathf.Max(left.yMin, right.yMin));
            return width * height;
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
