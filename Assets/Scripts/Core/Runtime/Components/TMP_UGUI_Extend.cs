using TMPro;
using UnityEngine;

namespace Core.Runtime
{
    /// 为 TextMeshProUGUI 提供渐变、描边、阴影和倾斜效果，并维护独立材质实例。
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TextMeshProUGUI))]
    public sealed class TMP_UGUI_Extend : MonoBehaviour
    {
        private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");
        private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
        private static readonly int UnderlayColorId = Shader.PropertyToID("_UnderlayColor");
        private static readonly int UnderlayOffsetXId = Shader.PropertyToID("_UnderlayOffsetX");
        private static readonly int UnderlayOffsetYId = Shader.PropertyToID("_UnderlayOffsetY");
        private static readonly int UnderlayDilateId = Shader.PropertyToID("_UnderlayDilate");
        private static readonly int UnderlaySoftnessId = Shader.PropertyToID("_UnderlaySoftness");
        private static readonly int UnderlayTypeId = Shader.PropertyToID("_UnderlayType");

        public enum GradientType
        {
            None,
            FourCorner,
            CenterToEdge,
            ThreePoint,
            UnityGradient
        }

        [Header("渐变")]
        [SerializeField] private GradientType gradientType;
        [SerializeField] private Color leftTopColor = Color.white;
        [SerializeField] private Color leftBottomColor = Color.white;
        [SerializeField] private Color rightTopColor = Color.white;
        [SerializeField] private Color rightBottomColor = Color.white;
        [SerializeField, Range(-1f, 1f)] private float gradientOffsetX;
        [SerializeField, Range(-1f, 1f)] private float gradientOffsetY;
        [SerializeField, Range(-180f, 180f)] private float gradientAngleOffset;
        [SerializeField] private Color centerColor = Color.white;
        [SerializeField] private Color leftEdgeColor = Color.gray;
        [SerializeField] private Color rightEdgeColor = Color.gray;
        [SerializeField, Range(0f, 1f)] private float gradientIntensity = 1f;
        [SerializeField, Range(-1f, 1f)] private float centerOffset;
        [SerializeField, Range(0.1f, 2f)] private float gradientWidth = 1f;
        [SerializeField] private Color leftColor = Color.red;
        [SerializeField] private Color middleColor = Color.green;
        [SerializeField] private Color rightColor = Color.blue;
        [SerializeField, Range(0f, 1f)] private float middlePosition = 0.5f;
        [SerializeField, Range(-1f, 1f)] private float threePointOffsetX;
        [SerializeField] private Gradient unityGradient = new Gradient();
        [SerializeField, Range(0f, 360f)] private float unityGradientAngle;

        [Header("描边")]
        [SerializeField] private bool enableOutline;
        [SerializeField] private Color outlineColor = Color.black;
        [SerializeField, Range(0f, 3f)] private float outlineWidth = 0.2f;

        [Header("阴影")]
        [SerializeField] private bool enableShadow;
        [SerializeField] private Color shadowColor = Color.black;
        [SerializeField, Range(-1f, 1f)] private float shadowOffsetX = 0.2f;
        [SerializeField, Range(-1f, 1f)] private float shadowOffsetY = -0.2f;
        [SerializeField, Range(-1f, 1f)] private float shadowDilate;
        [SerializeField, Range(0f, 1f)] private float shadowSoftness = 0.2f;

        [Header("倾斜")]
        [SerializeField] private bool enableSkew;
        [SerializeField, Range(-90f, 90f)] private float skewAngleDegrees = 12f;
        [SerializeField, Range(0f, 1f)] private float skewPivotY = 0.5f;

        private TMP_Text tmpText;
        private Material materialInstance;
        private Material sourceMaterial;
        private TMP_FontAsset boundFontAsset;
        private int boundSourceMaterialId;
        private bool ownsMaterialInstance;
        private bool refreshScheduled;
        private readonly Vector2[] cornerCache = new Vector2[4];

        /// 当前渐变类型。
        public GradientType CurrentGradientType
        {
            get => gradientType;
            set { gradientType = value; RefreshGradient(); }
        }

        /// 是否启用描边。
        public bool EnableOutline
        {
            get => enableOutline;
            set { enableOutline = value; RefreshOutline(); }
        }

        /// 是否启用阴影。
        public bool EnableShadow
        {
            get => enableShadow;
            set { enableShadow = value; RefreshShadow(); }
        }

        /// 是否启用倾斜。
        public bool EnableSkew
        {
            get => enableSkew;
            set { enableSkew = value; RefreshSkew(); }
        }

        private void Awake() => tmpText = GetComponent<TMP_Text>();

        private void OnEnable()
        {
            tmpText ??= GetComponent<TMP_Text>();
            EnsureRenderingCallback();
            ScheduleRefresh();
        }

        private void OnDisable()
        {
            if (tmpText != null) tmpText.OnPreRenderText -= OnTextRendering;
            CancelScheduledRefresh();
        }

        private void OnDestroy()
        {
            if (tmpText != null) tmpText.OnPreRenderText -= OnTextRendering;
            CancelScheduledRefresh();
            CleanupMaterialInstance();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            tmpText ??= GetComponent<TMP_Text>();
            if (!Application.isPlaying && isActiveAndEnabled)
            {
                EnsureRenderingCallback();
                ScheduleRefresh();
            }
        }
#endif

        /// 重新应用全部材质与顶点效果。
        public void RefreshAllEffects()
        {
            tmpText ??= GetComponent<TMP_Text>();
            EnsureRenderingCallback();
            if (tmpText == null || !EnsureMaterialInstance()) return;
            RefreshOutline();
            RefreshShadow();
            tmpText.UpdateMeshPadding();
            tmpText.ForceMeshUpdate(true, true);
        }

        /// 重新应用描边材质参数。
        public void RefreshOutline()
        {
            if (tmpText == null || !EnsureMaterialInstance()) return;
            if (enableOutline)
            {
                materialInstance.EnableKeyword("OUTLINE_ON");
                if (materialInstance.HasProperty(OutlineWidthId))
                    materialInstance.SetFloat(OutlineWidthId, Mathf.Clamp(outlineWidth / 6f, 0f, 0.5f));
                if (materialInstance.HasProperty(OutlineColorId)) materialInstance.SetColor(OutlineColorId, outlineColor);
            }
            else
            {
                materialInstance.DisableKeyword("OUTLINE_ON");
                if (materialInstance.HasProperty(OutlineWidthId)) materialInstance.SetFloat(OutlineWidthId, 0f);
            }
        }

        /// 重新应用阴影材质参数。
        public void RefreshShadow()
        {
            if (tmpText == null || !EnsureMaterialInstance()) return;
            if (enableShadow)
            {
                materialInstance.EnableKeyword("UNDERLAY_ON");
                materialInstance.DisableKeyword("UNDERLAY_INNER");
                SetMaterialFloat(UnderlayTypeId, 1f);
                SetMaterialColor(UnderlayColorId, shadowColor);
                SetMaterialFloat(UnderlayOffsetXId, shadowOffsetX);
                SetMaterialFloat(UnderlayOffsetYId, shadowOffsetY);
                SetMaterialFloat(UnderlayDilateId, shadowDilate);
                SetMaterialFloat(UnderlaySoftnessId, Mathf.Max(shadowSoftness, 0.2f));
            }
            else
            {
                materialInstance.DisableKeyword("UNDERLAY_ON");
                materialInstance.DisableKeyword("UNDERLAY_INNER");
                SetMaterialColor(UnderlayColorId, Color.clear);
                SetMaterialFloat(UnderlayOffsetXId, 0f);
                SetMaterialFloat(UnderlayOffsetYId, 0f);
                SetMaterialFloat(UnderlayDilateId, 0f);
                SetMaterialFloat(UnderlaySoftnessId, 0f);
            }
        }

        /// 重新生成倾斜顶点。
        public void RefreshSkew()
        {
            EnsureRenderingCallback();
            if (tmpText != null) tmpText.ForceMeshUpdate(true, true);
        }

        /// 重新生成渐变顶点色。
        public void RefreshGradient()
        {
            EnsureRenderingCallback();
            if (tmpText != null) tmpText.ForceMeshUpdate(true, true);
        }

        private void EnsureRenderingCallback()
        {
            tmpText ??= GetComponent<TMP_Text>();
            if (tmpText == null || !isActiveAndEnabled) return;
            // 编辑模式下 Awake/OnEnable 不一定在 Inspector 刷新前完成，必须幂等补订阅。
            tmpText.OnPreRenderText -= OnTextRendering;
            tmpText.OnPreRenderText += OnTextRendering;
        }

        private void ScheduleRefresh()
        {
            if (refreshScheduled) return;
            refreshScheduled = true;
            Canvas.willRenderCanvases += RefreshBeforeCanvasRender;
        }

        private void CancelScheduledRefresh()
        {
            if (!refreshScheduled) return;
            refreshScheduled = false;
            Canvas.willRenderCanvases -= RefreshBeforeCanvasRender;
        }

        private void RefreshBeforeCanvasRender()
        {
            CancelScheduledRefresh();
            // 编辑器测试或快速销毁对象时，Canvas 可能仍派发一次已排队的回调。
            if (this == null) return;
            if (isActiveAndEnabled) RefreshAllEffects();
        }

        private bool EnsureMaterialInstance()
        {
            if (tmpText == null || tmpText.font == null)
            {
                CleanupMaterialInstance();
                return false;
            }

            TextMeshProUGUI tmpUgui = tmpText as TextMeshProUGUI;
            if (tmpUgui == null) return false;
            bool fontChanged = !ReferenceEquals(boundFontAsset, tmpUgui.font);
            bool usingOwned = materialInstance != null && ReferenceEquals(tmpUgui.fontSharedMaterial, materialInstance);
            Material nextSource = usingOwned ? (fontChanged ? tmpUgui.font.material : sourceMaterial) : tmpUgui.fontSharedMaterial;
            nextSource ??= tmpUgui.font.material;
            if (nextSource == null) return false;

            int sourceId = nextSource.GetInstanceID();
            if (materialInstance != null && !fontChanged && boundSourceMaterialId == sourceId && usingOwned) return true;

            CleanupMaterialInstance();
            sourceMaterial = nextSource;
            materialInstance = new Material(sourceMaterial)
            {
                name = tmpUgui.font.name + " (TMP Effect Instance)",
                hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild
            };
            tmpUgui.fontSharedMaterial = materialInstance;
            boundFontAsset = tmpUgui.font;
            boundSourceMaterialId = sourceId;
            ownsMaterialInstance = true;
            return true;
        }

        private void CleanupMaterialInstance()
        {
            if (ownsMaterialInstance && materialInstance != null)
            {
                TextMeshProUGUI tmpUgui = tmpText as TextMeshProUGUI;
                if (tmpUgui != null && ReferenceEquals(tmpUgui.fontSharedMaterial, materialInstance))
                    tmpUgui.fontSharedMaterial = sourceMaterial;
                if (Application.isPlaying) Destroy(materialInstance);
                else DestroyImmediate(materialInstance);
            }

            materialInstance = null;
            sourceMaterial = null;
            boundFontAsset = null;
            boundSourceMaterialId = 0;
            ownsMaterialInstance = false;
        }

        private void OnTextRendering(TMP_TextInfo textInfo)
        {
            TextMeshProUGUI tmpUgui = tmpText as TextMeshProUGUI;
            if (tmpUgui != null && (tmpUgui.font != boundFontAsset || !ReferenceEquals(tmpUgui.fontSharedMaterial, materialInstance)))
                ScheduleRefresh();
            ApplyGradient(textInfo);
            if (enableSkew) ApplySkew(textInfo);
        }

        private void ApplyGradient(TMP_TextInfo textInfo)
        {
            if (gradientType == GradientType.None) return;
            for (int i = 0; i < textInfo.meshInfo.Length; i++)
            {
                Vector3[] vertices = textInfo.meshInfo[i].vertices;
                Color32[] colors = textInfo.meshInfo[i].colors32;
                int length = textInfo.meshInfo[i].vertexCount;
                if (length <= 0) continue;
                switch (gradientType)
                {
                    case GradientType.FourCorner: ApplyFourCornerGradient(vertices, colors, length); break;
                    case GradientType.CenterToEdge: ApplyCenterGradient(vertices, colors, length); break;
                    case GradientType.ThreePoint: ApplyThreePointGradient(vertices, colors, length); break;
                    case GradientType.UnityGradient: ApplyUnityGradient(vertices, colors, length); break;
                }
            }
        }

        private void ApplyFourCornerGradient(Vector3[] vertices, Color32[] colors, int length)
        {
            CalculateBounds(vertices, length, out Vector2 min, out Vector2 max);
            float width = max.x - min.x;
            float height = max.y - min.y;
            if (width < 0.001f || height < 0.001f) return;
            float radians = gradientAngleOffset * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);
            for (int i = 0; i < length; i++)
            {
                float x = (vertices[i].x - min.x) / width + gradientOffsetX;
                float y = (vertices[i].y - min.y) / height + gradientOffsetY;
                if (!Mathf.Approximately(gradientAngleOffset, 0f)) (x, y) = (cos * x - sin * y, sin * x + cos * y);
                Color left = Color.Lerp(leftBottomColor, leftTopColor, Mathf.Clamp01(y));
                Color right = Color.Lerp(rightBottomColor, rightTopColor, Mathf.Clamp01(y));
                colors[i] = Color.Lerp(left, right, Mathf.Clamp01(x));
            }
        }

        private void ApplyCenterGradient(Vector3[] vertices, Color32[] colors, int length)
        {
            CalculateHorizontalBounds(vertices, length, out float left, out float right);
            float width = right - left;
            if (width < 0.001f) return;
            float center = (left + right) * 0.5f + centerOffset * width * 0.5f;
            for (int i = 0; i < length; i++)
            {
                float distance = Mathf.Abs(vertices[i].x - center) / (width * 0.5f);
                Color edge = vertices[i].x < center ? leftEdgeColor : rightEdgeColor;
                colors[i] = Color.Lerp(centerColor, edge, Mathf.Clamp01(distance * gradientWidth) * gradientIntensity);
            }
        }

        private void ApplyThreePointGradient(Vector3[] vertices, Color32[] colors, int length)
        {
            CalculateHorizontalBounds(vertices, length, out float left, out float right);
            float width = right - left;
            if (width < 0.001f) return;
            for (int i = 0; i < length; i++)
            {
                float x = (vertices[i].x - left) / width + threePointOffsetX;
                if (x <= middlePosition)
                {
                    float t = middlePosition > 0.001f ? Mathf.Clamp01(x / middlePosition) : 0f;
                    colors[i] = Color.Lerp(leftColor, middleColor, t);
                }
                else
                {
                    float range = 1f - middlePosition;
                    float t = range > 0.001f ? Mathf.Clamp01((x - middlePosition) / range) : 1f;
                    colors[i] = Color.Lerp(middleColor, rightColor, t);
                }
            }
        }

        private void ApplyUnityGradient(Vector3[] vertices, Color32[] colors, int length)
        {
            if (unityGradient == null) return;
            CalculateBounds(vertices, length, out Vector2 min, out Vector2 max);
            float radians = unityGradientAngle * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
            cornerCache[0] = min;
            cornerCache[1] = new Vector2(max.x, min.y);
            cornerCache[2] = new Vector2(min.x, max.y);
            cornerCache[3] = max;
            float minProjection = float.MaxValue;
            float maxProjection = float.MinValue;
            for (int i = 0; i < cornerCache.Length; i++)
            {
                float projection = Vector2.Dot(cornerCache[i], direction);
                minProjection = Mathf.Min(minProjection, projection);
                maxProjection = Mathf.Max(maxProjection, projection);
            }

            float range = maxProjection - minProjection;
            if (range < 0.001f) return;
            for (int i = 0; i < length; i++)
            {
                colors[i] = unityGradient.Evaluate(Mathf.Clamp01((Vector2.Dot(vertices[i], direction) - minProjection) / range));
            }
        }

        private void ApplySkew(TMP_TextInfo textInfo)
        {
            float skew = Mathf.Tan(skewAngleDegrees * Mathf.Deg2Rad);
            for (int i = 0; i < textInfo.meshInfo.Length; i++)
            {
                Vector3[] vertices = textInfo.meshInfo[i].vertices;
                int length = textInfo.meshInfo[i].vertexCount;
                if (length == 0) continue;
                float minY = vertices[0].y;
                float maxY = vertices[0].y;
                for (int j = 1; j < length; j++)
                {
                    minY = Mathf.Min(minY, vertices[j].y);
                    maxY = Mathf.Max(maxY, vertices[j].y);
                }

                float pivot = Mathf.Lerp(minY, maxY, skewPivotY);
                for (int j = 0; j < length; j++) vertices[j].x += skew * (vertices[j].y - pivot);
            }
        }

        private static void CalculateHorizontalBounds(Vector3[] vertices, int length, out float left, out float right)
        {
            left = right = vertices[0].x;
            for (int i = 1; i < length; i++)
            {
                left = Mathf.Min(left, vertices[i].x);
                right = Mathf.Max(right, vertices[i].x);
            }
        }

        private static void CalculateBounds(Vector3[] vertices, int length, out Vector2 min, out Vector2 max)
        {
            min = max = vertices[0];
            for (int i = 1; i < length; i++)
            {
                min.x = Mathf.Min(min.x, vertices[i].x);
                min.y = Mathf.Min(min.y, vertices[i].y);
                max.x = Mathf.Max(max.x, vertices[i].x);
                max.y = Mathf.Max(max.y, vertices[i].y);
            }
        }

        private void SetMaterialFloat(int propertyId, float value)
        {
            if (materialInstance.HasProperty(propertyId)) materialInstance.SetFloat(propertyId, value);
        }

        private void SetMaterialColor(int propertyId, Color value)
        {
            if (materialInstance.HasProperty(propertyId)) materialInstance.SetColor(propertyId, value);
        }
    }
}
