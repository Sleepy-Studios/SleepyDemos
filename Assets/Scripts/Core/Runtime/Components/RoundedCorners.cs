using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Core.Runtime
{
    /// 使用共享圆角材质和顶点尺寸数据，为 UGUI Graphic 提供四角独立半径与可选 Mask。
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(Graphic))]
    [DisallowMultipleComponent]
    public sealed class RoundedCorners : BaseMeshEffect, IMaterialModifier
    {
        private const string ShaderName = "UI/RoundedCorners";
        private const string ShaderAddress = "LoadResources/Art/Shaders/UIRoundedCorners";
        private const int MaxCacheSize = 100;

        private static readonly int RadiusProperty = Shader.PropertyToID("_Radius");
        private static readonly int UseUiAlphaClipProperty = Shader.PropertyToID("_UseUIAlphaClip");
        private static readonly Dictionary<MaterialCacheKey, Material> MaterialCache = new Dictionary<MaterialCacheKey, Material>();
        private static readonly List<MaterialCacheKey> MaterialLruKeys = new List<MaterialCacheKey>();
        private static readonly List<RoundedCorners> PendingShaderRefresh = new List<RoundedCorners>();
        private static Shader roundedShader;
        private static IResourceLoader shaderLoader;
        private static bool shaderLoading;

        [SerializeField] private bool useAsMask;
        [SerializeField] private bool showMaskGraphic = true;
        [SerializeField] private bool useUnifiedRadius = true;
        [SerializeField, Min(0f)] private float unifiedRadius = 10f;
        [SerializeField] private Vector4 separateRadius = new Vector4(10f, 10f, 10f, 10f);

        private RectTransform rectTransform;
        private Canvas cachedCanvas;

        private readonly struct MaterialCacheKey : System.IEquatable<MaterialCacheKey>
        {
            private readonly int baseMaterialId;
            private readonly Vector4 radius;

            public MaterialCacheKey(Material baseMaterial, Vector4 radius)
            {
                baseMaterialId = baseMaterial != null ? baseMaterial.GetInstanceID() : 0;
                this.radius = radius;
            }

            public bool Equals(MaterialCacheKey other) => baseMaterialId == other.baseMaterialId && radius == other.radius;
            public override bool Equals(object obj) => obj is MaterialCacheKey other && Equals(other);
            public override int GetHashCode()
            {
                unchecked { return (baseMaterialId * 397) ^ radius.GetHashCode(); }
            }
        }

        /// 是否自动补 Mask 组件。
        public bool UseAsMask
        {
            get => useAsMask;
            set
            {
                if (useAsMask == value) return;
                useAsMask = value;
                EnsureMaskState();
                SetMaterialDirty();
            }
        }

        /// 作为 Mask 时是否显示自身 Graphic。
        public bool ShowMaskGraphic
        {
            get => showMaskGraphic;
            set
            {
                if (showMaskGraphic == value) return;
                showMaskGraphic = value;
                EnsureMaskState();
            }
        }

        /// 是否四角使用同一个半径。
        public bool UseUnifiedRadius
        {
            get => useUnifiedRadius;
            set
            {
                if (useUnifiedRadius == value) return;
                useUnifiedRadius = value;
                SetMaterialDirty();
            }
        }

        /// 当前四角半径，顺序为左上、右上、右下、左下。
        public Vector4 RadiusVector
        {
            get
            {
                Vector4 radius = useUnifiedRadius
                    ? new Vector4(unifiedRadius, unifiedRadius, unifiedRadius, unifiedRadius)
                    : separateRadius;
                return new Vector4(
                    Mathf.Max(0f, radius.x),
                    Mathf.Max(0f, radius.y),
                    Mathf.Max(0f, radius.z),
                    Mathf.Max(0f, radius.w));
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            ValidateCanvasChannels();
            EnsureMaskState();
            EnsureShaderLoaded();
            SetVerticesDirty();
            SetMaterialDirty();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            unifiedRadius = Mathf.Max(0f, unifiedRadius);
            separateRadius = new Vector4(
                Mathf.Max(0f, separateRadius.x),
                Mathf.Max(0f, separateRadius.y),
                Mathf.Max(0f, separateRadius.z),
                Mathf.Max(0f, separateRadius.w));
            ValidateCanvasChannels();
            EnsureMaskState();
            EnsureShaderLoaded();
            SetVerticesDirty();
            SetMaterialDirty();
        }
#endif

        /// <summary>把 Rect 尺寸和归一化坐标写入 UV1、UV2。</summary>
        /// <param name="helper">当前 Graphic 的顶点辅助对象。</param>
        public override void ModifyMesh(VertexHelper helper)
        {
            if (!IsActive()) return;
            Rect rect = GetRectTransform().rect;
            Vector2 size = new Vector2(rect.width, rect.height);
            Vector2 inverseSize = new Vector2(rect.width > 0f ? 1f / rect.width : 0f, rect.height > 0f ? 1f / rect.height : 0f);
            Vector2 rectMin = rect.min;
            UIVertex vertex = default;
            for (int i = 0; i < helper.currentVertCount; i++)
            {
                helper.PopulateUIVertex(ref vertex, i);
                vertex.uv1 = size;
                vertex.uv2 = new Vector2(
                    (vertex.position.x - rectMin.x) * inverseSize.x,
                    (vertex.position.y - rectMin.y) * inverseSize.y);
                helper.SetUIVertex(vertex, i);
            }
        }

        /// <summary>按基础材质和圆角半径取得共享派生材质。</summary>
        /// <param name="baseMaterial">Graphic 或 Mask 已处理的基础材质。</param>
        /// <returns>应用圆角 Shader 的材质；组件未激活或 Shader 不可用时返回基础材质。</returns>
        public Material GetModifiedMaterial(Material baseMaterial)
        {
            if (!IsActive() || roundedShader == null) return baseMaterial;
            Vector4 radius = RadiusVector;
            MaterialCacheKey key = new MaterialCacheKey(baseMaterial, radius);
            if (MaterialCache.TryGetValue(key, out Material cachedMaterial) && cachedMaterial != null)
            {
                TouchCacheKey(key);
                return cachedMaterial;
            }

            MaterialCache.Remove(key);
            MaterialLruKeys.Remove(key);
            Material material = baseMaterial != null ? new Material(baseMaterial) : new Material(roundedShader);
            material.shader = roundedShader;
            material.SetVector(RadiusProperty, radius);
            material.hideFlags = HideFlags.HideAndDontSave;
            if (baseMaterial != null && baseMaterial.HasProperty(UseUiAlphaClipProperty))
            {
                bool alphaClip = baseMaterial.GetFloat(UseUiAlphaClipProperty) > 0.5f;
                material.SetFloat(UseUiAlphaClipProperty, alphaClip ? 1f : 0f);
                if (alphaClip) material.EnableKeyword("UNITY_UI_ALPHACLIP");
                else material.DisableKeyword("UNITY_UI_ALPHACLIP");
            }

            MaterialCache[key] = material;
            MaterialLruKeys.Add(key);
            return material;
        }

        /// 清理超出缓存上限的最久未使用圆角材质。
        public static void TrimCache()
        {
            while (MaterialCache.Count > MaxCacheSize && MaterialLruKeys.Count > 0)
            {
                MaterialCacheKey key = MaterialLruKeys[0];
                MaterialLruKeys.RemoveAt(0);
                if (!MaterialCache.TryGetValue(key, out Material material)) continue;
                if (material != null)
                {
                    if (Application.isPlaying) Destroy(material);
                    else DestroyImmediate(material);
                }

                MaterialCache.Remove(key);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void InitializeGlobalState()
        {
            SceneManager.activeSceneChanged -= OnSceneChanged;
            SceneManager.activeSceneChanged += OnSceneChanged;
            PendingShaderRefresh.Clear();
            shaderLoading = false;
            roundedShader = null;
            shaderLoader?.Dispose();
            shaderLoader = null;
        }

        private static void OnSceneChanged(Scene previous, Scene next) => TrimCache();

        private void EnsureMaskState()
        {
            if (!useAsMask) return;
            Mask mask = gameObject.GetOrAddComponent<Mask>();
            mask.showMaskGraphic = showMaskGraphic;
        }

        private void ValidateCanvasChannels()
        {
            cachedCanvas ??= GetComponentInParent<Canvas>();
            if (cachedCanvas == null) return;
            AdditionalCanvasShaderChannels needed = AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.TexCoord2;
            cachedCanvas.additionalShaderChannels |= needed;
        }

        private void EnsureShaderLoaded()
        {
            if (roundedShader == null) roundedShader = Shader.Find(ShaderName);
            if (roundedShader != null)
            {
                SetMaterialDirty();
                return;
            }

            if (!Application.isPlaying || !ResourceServices.Default.IsInitialized) return;
            if (!PendingShaderRefresh.Contains(this)) PendingShaderRefresh.Add(this);
            if (shaderLoading) return;
            shaderLoading = true;
            LoadShaderAsync().Forget();
        }

        private static async UniTaskVoid LoadShaderAsync()
        {
            shaderLoader ??= ResourceServices.CreateLoader();
            roundedShader = await shaderLoader.LoadAssetAsync<Shader>(ShaderAddress);
            shaderLoading = false;
            for (int i = 0; i < PendingShaderRefresh.Count; i++)
            {
                RoundedCorners target = PendingShaderRefresh[i];
                if (target != null && target.isActiveAndEnabled) target.SetMaterialDirty();
            }

            PendingShaderRefresh.Clear();
        }

        private RectTransform GetRectTransform()
        {
            return rectTransform != null ? rectTransform : rectTransform = GetComponent<RectTransform>();
        }

        private void SetVerticesDirty()
        {
            if (graphic != null) graphic.SetVerticesDirty();
        }

        private void SetMaterialDirty()
        {
            if (graphic != null) graphic.SetMaterialDirty();
        }

        private static void TouchCacheKey(MaterialCacheKey key)
        {
            if (MaterialLruKeys.Count > 0 && MaterialLruKeys[MaterialLruKeys.Count - 1].Equals(key)) return;
            MaterialLruKeys.Remove(key);
            MaterialLruKeys.Add(key);
        }
    }
}
