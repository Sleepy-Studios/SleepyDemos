using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Core.Runtime
{
    /// 统一图片变换、渐变、圆角、细描边与阴影；保持原生 Image 的布局、填充和点击语义。
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Image))]
    [AddComponentMenu("UI/Effects/Image Extend")]
    public sealed class ImageExtend : BaseMeshEffect, IMaterialModifier
    {
        /// 渐变在变换后的图片局部坐标中采样。
        public enum GradientDirection
        {
            Vertical,
            Horizontal
        }

        private const string ShaderName = "UI/ImageExtend";
        private const string ShaderAddress = "LoadResources/Art/Shaders/UIImageExtend";
        /// 仅限制闲置材质；活动材质受引用计数保护，不计入此容量。
        public const int IdleMaterialCapacity = 64;

        private static readonly Dictionary<(Material, Shader, Vector4, float, Color, Vector4, Color, Vector4), SharedMaterial> Materials = new();
        private static readonly LinkedList<SharedMaterial> IdleMaterials = new();

        [SerializeField] private bool flipHorizontal;
        [SerializeField] private bool flipVertical;
        [SerializeField] private float rotationAngle;
        [SerializeField] private bool enableGradient;
        [SerializeField] private GradientDirection direction;
        [SerializeField, Range(-1f, 1f)] private float offset;
        [SerializeField] private Gradient gradient = new Gradient();
        [SerializeField] private bool enableRoundedCorners;
        [SerializeField] private bool useUnifiedRadius = true;
        [SerializeField, Min(0f)] private float unifiedRadius = 10f;
        [SerializeField] private Vector4 separateRadius = new(10f, 10f, 10f, 10f);
        [SerializeField, Min(0f)] private float edgeFeather;
        [SerializeField] private bool enableOutline;
        [SerializeField] private bool useSpriteAlpha;
        [SerializeField, Range(0f, 8f)] private float outlineWidth = 2f;
        [SerializeField] private Color outlineColor = Color.black;
        [SerializeField] private bool enableShadow;
        [SerializeField] private Color shadowColor = new(0f, 0f, 0f, 0.15f);
        [SerializeField] private Vector2 shadowOffset = new(0f, -4f);
        [SerializeField, Range(0f, 16f)] private float shadowSoftness = 4f;

        private readonly List<UIVertex> sourceVertices = new();
        private readonly List<BaseMeshEffect> meshEffects = new();
        private (Material, Shader, Vector4, float, Color, Vector4, Color, Vector4) materialKey;
        private SharedMaterial sharedMaterial;
        private Shader effectShader;
        private ShaderReference shaderReference;
        private bool shaderLoading;
        private bool shaderReleasePending;
        private bool shaderLoadFailed;
        private bool destroyed;

        private sealed class SharedMaterial
        {
            public Material Material;
            public int References;
            public (Material, Shader, Vector4, float, Color, Vector4, Color, Vector4) Key;
            public LinkedListNode<SharedMaterial> IdleNode;
            public ShaderReference ShaderReference;
        }

        // 缓存材质与组件共同持有加载器，避免组件销毁后闲置材质失去 Shader 依赖。
        private sealed class ShaderReference
        {
            public IResourceService Service;
            public IResourceLoader Loader;
            public int References = 1;

            public void Release()
            {
                if (--References == 0)
                {
                    var loader = Loader;
                    Loader = null;
                    try
                    {
                        loader.Dispose();
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception);
                    }
                }
            }
        }

        /// 是否水平翻转图片网格。
        public bool FlipHorizontal
        {
            get => flipHorizontal;
            set
            {
                flipHorizontal = value;
                Refresh();
            }
        }

        /// 是否垂直翻转图片网格。
        public bool FlipVertical
        {
            get => flipVertical;
            set
            {
                flipVertical = value;
                Refresh();
            }
        }

        /// 图片绕 Rect 中心旋转的角度；不旋转点击矩形。
        public float RotationAngle
        {
            get => rotationAngle;
            set
            {
                rotationAngle = value;
                Refresh();
            }
        }

        /// 是否启用与 Image 染色相乘的渐变。
        public bool EnableGradient
        {
            get => enableGradient;
            set
            {
                enableGradient = value;
                Refresh();
            }
        }

        /// 渐变方向。
        public GradientDirection Direction
        {
            get => direction;
            set
            {
                direction = value;
                Refresh();
            }
        }

        /// 渐变采样偏移，范围 -1 到 1。
        public float Offset
        {
            get => offset;
            set
            {
                offset = Mathf.Clamp(value, -1f, 1f);
                Refresh();
            }
        }

        /// 渐变颜色与透明度。
        public Gradient Gradient
        {
            get => gradient;
            set
            {
                gradient = value;
                Refresh();
            }
        }

        /// 是否启用圆角裁切。
        public bool EnableRoundedCorners
        {
            get => enableRoundedCorners;
            set
            {
                enableRoundedCorners = value;
                Refresh();
            }
        }

        /// 统一四角半径；左上、右上、右下、左下。
        public Vector4 RadiusVector => enableRoundedCorners
            ? (useUnifiedRadius ? Vector4.one * Mathf.Max(0f, unifiedRadius) : MaxZero(separateRadius))
            : Vector4.zero;

        /// 圆角轮廓向内羽化的宽度，单位同半径；0 保留基础抗锯齿，关闭圆角时不生效。
        public float EdgeFeather
        {
            get => edgeFeather;
            set
            {
                edgeFeather = Mathf.Max(0f, value);
                Refresh();
            }
        }

        /// 是否使用图片透明轮廓；关闭时描边和阴影按面板矩形计算。
        public bool UseSpriteAlpha
        {
            get => useSpriteAlpha;
            set
            {
                useSpriteAlpha = value;
                Refresh();
            }
        }

        /// 是否启用描边。
        public bool EnableOutline
        {
            get => enableOutline;
            set
            {
                enableOutline = value;
                Refresh();
            }
        }

        /// 细描边宽度，范围 0 到 8 个 UI 本地单位。
        public float OutlineWidth
        {
            get => outlineWidth;
            set
            {
                outlineWidth = Mathf.Clamp(value, 0f, 8f);
                Refresh();
            }
        }

        /// 独立于图片 RGB 染色的描边颜色。
        public Color OutlineColor
        {
            get => outlineColor;
            set
            {
                outlineColor = value;
                Refresh();
            }
        }

        /// 是否启用阴影。
        public bool EnableShadow
        {
            get => enableShadow;
            set
            {
                enableShadow = value;
                Refresh();
            }
        }

        /// 阴影颜色，透明度仍跟随图片和 CanvasGroup。
        public Color ShadowColor
        {
            get => shadowColor;
            set
            {
                shadowColor = value;
                Refresh();
            }
        }

        /// 图片局部坐标中的阴影偏移。
        public Vector2 ShadowOffset
        {
            get => shadowOffset;
            set
            {
                shadowOffset = value;
                Refresh();
            }
        }

        /// 阴影柔化范围，0 到 16 个 UI 本地单位。
        public float ShadowSoftness
        {
            get => shadowSoftness;
            set
            {
                shadowSoftness = Mathf.Clamp(value, 0f, 16f);
                Refresh();
            }
        }

        private bool HasOutline => enableOutline && outlineWidth > 0f && outlineColor.a > 0f;
        private bool HasShadow => enableShadow && shadowColor.a > 0f;
        private bool NeedsShader => enableRoundedCorners || HasOutline || HasShadow;

        protected override void OnEnable()
        {
            base.OnEnable();
            shaderLoadFailed = false;
            EnsureShader();
            Refresh();
        }

        protected override void OnDisable()
        {
            ReleaseMaterial();
            base.OnDisable();
            Refresh();
        }

        protected override void OnDestroy()
        {
            destroyed = true;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall -= RefreshAfterValidate;
#endif
            ReleaseMaterial();
            if (!shaderLoading)
            {
                ReleaseShaderReference();
            }
            base.OnDestroy();
        }

        protected override void OnCanvasHierarchyChanged()
        {
            base.OnCanvasHierarchyChanged();
            Refresh();
        }

        protected override void OnDidApplyAnimationProperties()
        {
            base.OnDidApplyAnimationProperties();
            Refresh();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            outlineWidth = Mathf.Clamp(outlineWidth, 0f, 8f);
            shadowSoftness = Mathf.Clamp(shadowSoftness, 0f, 16f);
            unifiedRadius = Mathf.Max(0f, unifiedRadius);
            separateRadius = MaxZero(separateRadius);
            edgeFeather = Mathf.Max(0f, edgeFeather);
            offset = Mathf.Clamp(offset, -1f, 1f);
            // 本组件的 Shader 与 Canvas 刷新延后到 Editor 主线程。
            UnityEditor.EditorApplication.delayCall -= RefreshAfterValidate;
            UnityEditor.EditorApplication.delayCall += RefreshAfterValidate;
        }

        private void RefreshAfterValidate()
        {
            if (this != null)
            {
                Refresh();
            }
        }
#endif

        /// 更新图片网格和材质；运行期修改 Gradient 的键后显式调用。
        public void Refresh()
        {
            SetDirty();
        }

        private void SetDirty()
        {
            if (graphic == null)
            {
                return;
            }
            if (NeedsShader && graphic.canvas != null)
            {
                graphic.canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.TexCoord2;
            }
            graphic.SetVerticesDirty();
            graphic.SetMaterialDirty();
        }

        /// <summary>返回不能安全组合的配置原因；null 表示当前组合可用。</summary>
        /// <param name="english">为编辑器英文提示返回英文；默认保留既有中文诊断。</param>
        public string GetSupportIssue(bool english = false)
        {
            var image = graphic as Image;
            if (image == null)
            {
                return english ? "Only native Image is supported." : "仅支持原生 Image。";
            }
            GetComponents(meshEffects);
            foreach (var effect in meshEffects)
            {
                if (effect != this && effect.isActiveAndEnabled)
                {
                    return english ? "Migrate other mesh effects to ImageExtend; do not stack Outline, Shadow or legacy effects." : "请将其他网格效果迁移到 ImageExtend，不要叠加 Outline、Shadow 或旧效果组件。";
                }
            }
            if (!NeedsShader)
            {
                return null;
            }
            if (image.overrideSprite != null && image.overrideSprite.associatedAlphaSplitTexture != null)
            {
                return english ? "External alpha textures retain native rendering; rounded corners, outlines and shadows are unsupported." : "外置 Alpha 纹理保留原生图片绘制，不支持圆角／描边／阴影材质。";
            }
            if (image.material.shader != Graphic.defaultGraphicMaterial.shader)
            {
                return english ? "Rounded corners, outlines and shadows require the default UI material." : "圆角／描边／阴影仅支持默认 UI 材质。";
            }
            if ((HasOutline || HasShadow) && (image.type == Image.Type.Filled || image.type == Image.Type.Tiled))
            {
                return english ? "Filled / Tiled retain native rendering; expanded outlines and shadows are unsupported." : "Filled／Tiled 保留原生绘制，不支持外扩描边和阴影。";
            }
            if ((HasOutline || HasShadow) && TryGetComponent<Mask>(out var mask) && mask.isActiveAndEnabled)
            {
                return english ? "A Mask writer cannot expand outlines or shadows; use a separate decorative Image." : "Mask 写入节点不支持外扩描边／阴影；请放在独立装饰图片上。";
            }
            if (useSpriteAlpha && (HasOutline || HasShadow))
            {
                if (image.type != Image.Type.Simple || image.useSpriteMesh)
                {
                    return english ? "Sprite alpha effects require Simple Image with Use Sprite Mesh disabled." : "透明轮廓效果需要 Simple Image，并关闭 Use Sprite Mesh。";
                }
                Sprite sprite = image.overrideSprite;
                if (sprite == null)
                {
                    return english ? "Sprite alpha effects require a Sprite with a transparent background." : "透明轮廓效果需要带透明背景的 Sprite。";
                }
                if (sprite.packed && (sprite.packingRotation != SpritePackingRotation.None || sprite.packingMode == SpritePackingMode.Tight))
                {
                    return english ? "Disable Rotation and Tight Packing for sprite alpha atlases." : "透明轮廓图集请关闭 Rotation 与 Tight Packing。";
                }
                if (sprite.texture == null)
                {
                    return english ? "The Sprite texture is not ready; native Image rendering is retained." : "Sprite 纹理尚未就绪，保留原生 Image 绘制。";
                }
                if (sprite.texture.mipmapCount > 1)
                {
                    return english ? "Disable mipmaps on sprite alpha textures / atlases to prevent neighbouring sprite bleed." : "透明轮廓图片或图集请关闭 Mipmap，避免相邻图片串色。";
                }
            }
            return null;
        }

        /// <summary>统一变换与渐变，并为 Shader 填入图片局部坐标和图集边界。</summary>
        /// <param name="helper">Image 生成的网格，不修改 RectTransform 或射线矩形。</param>
        public override void ModifyMesh(VertexHelper helper)
        {
            if (!IsActive() || helper.currentVertCount == 0 || GetSupportIssue() != null)
            {
                return;
            }
            EnsureShader();
            Rect bounds = GetMeshBounds(helper);
            if (bounds.width <= 0f || bounds.height <= 0f)
            {
                return;
            }
            bool shaderReady = NeedsShader && effectShader != null;
            bool expanded = shaderReady && (HasOutline || HasShadow);
            UIVertex first = default, third = default;
            helper.PopulateUIVertex(ref first, 0);
            helper.PopulateUIVertex(ref third, Mathf.Min(2, helper.currentVertCount - 1));
            Vector4 uvBounds = new(first.uv0.x, first.uv0.y, third.uv0.x, third.uv0.y);
            if (expanded)
            {
                sourceVertices.Clear();
                helper.GetUIVertexStream(sourceVertices);
                helper.Clear();
                float padding = (HasOutline ? outlineWidth : 0f) + (HasShadow ? shadowSoftness + Mathf.Max(Mathf.Abs(shadowOffset.x), Mathf.Abs(shadowOffset.y)) : 0f) + 1f;
                AddEffectQuad(helper, bounds, uvBounds, padding, useSpriteAlpha ? 2f : 1f, first.color);
                if (!useSpriteAlpha)
                {
                    int start = helper.currentVertCount;
                    foreach (UIVertex source in sourceVertices)
                    {
                        helper.AddVert(source);
                    }
                    for (int i = 0; i < sourceVertices.Count; i += 3)
                    {
                        helper.AddTriangle(start + i, start + i + 1, start + i + 2);
                    }
                }
            }
            Vector2 pivot = graphic.rectTransform.rect.center;
            float radians = Mathf.Repeat(rotationAngle, 360f) * Mathf.Deg2Rad;
            float sin = Mathf.Sin(radians), cos = Mathf.Cos(radians);
            Vector2 axis = direction == GradientDirection.Vertical ? Vector2.up : Vector2.right;
            float min = float.MaxValue, max = float.MinValue;
            for (int corner = 0; corner < 4; corner++)
            {
                Vector2 local = new Vector2(corner < 2 ? bounds.xMin : bounds.xMax, corner == 0 || corner == 3 ? bounds.yMin : bounds.yMax) - pivot;
                if (flipHorizontal)
                {
                    local.x = -local.x;
                }
                if (flipVertical)
                {
                    local.y = -local.y;
                }
                float coordinate = Vector2.Dot(pivot + new Vector2(local.x * cos - local.y * sin, local.x * sin + local.y * cos), axis);
                min = Mathf.Min(min, coordinate);
                max = Mathf.Max(max, coordinate);
            }
            UIVertex vertex = default;
            for (int i = 0; i < helper.currentVertCount; i++)
            {
                helper.PopulateUIVertex(ref vertex, i);
                if (shaderReady)
                {
                    vertex.uv1 = new Vector4(vertex.position.x - bounds.center.x, vertex.position.y - bounds.center.y, bounds.width, bounds.height);
                    vertex.uv2 = uvBounds;
                }
                Vector2 local = (Vector2)vertex.position - pivot;
                if (flipHorizontal)
                {
                    local.x = -local.x;
                }
                if (flipVertical)
                {
                    local.y = -local.y;
                }
                vertex.position = new Vector3(pivot.x + local.x * cos - local.y * sin, pivot.y + local.x * sin + local.y * cos, vertex.position.z);
                helper.SetUIVertex(vertex, i);
            }
            if (!enableGradient || gradient == null || max - min <= Mathf.Epsilon)
            {
                return;
            }
            for (int i = 0; i < helper.currentVertCount; i++)
            {
                helper.PopulateUIVertex(ref vertex, i);
                float t = Mathf.Clamp01((Vector2.Dot(vertex.position, axis) - min) / (max - min) - offset);
                vertex.color = (Color)vertex.color * gradient.Evaluate(t);
                helper.SetUIVertex(vertex, i);
            }
        }

        /// <summary>按上游材质及效果参数共享派生材质；不支持的组合保留上游材质。</summary>
        /// <param name="baseMaterial">原生 Image 或 Mask 已处理的基础材质。</param>
        /// <returns>共享效果材质，或未修改的基础材质。</returns>
        public Material GetModifiedMaterial(Material baseMaterial)
        {
            EnsureShader();
            if (!IsActive() || !NeedsShader || effectShader == null || baseMaterial == null || GetSupportIssue() != null)
            {
                ReleaseMaterial();
                return baseMaterial;
            }
            Vector4 radius = RadiusVector;
            float feather = enableRoundedCorners ? edgeFeather : 0f;
            // 透明轮廓模式由顶点传递，不拆分材质；关闭的效果参数也不影响共享。
            Vector4 outline = new(HasOutline ? outlineWidth : 0f, 0f, 0f, 0f);
            Color activeOutlineColor = HasOutline ? outlineColor : Color.clear;
            Vector4 shadow = HasShadow ? new Vector4(shadowOffset.x, shadowOffset.y, shadowSoftness, 1f) : Vector4.zero;
            Color activeShadowColor = HasShadow ? shadowColor : Color.clear;
            var key = (baseMaterial, effectShader, radius, feather, activeOutlineColor, outline, activeShadowColor, shadow);
            if (sharedMaterial != null && materialKey.Equals(key) && sharedMaterial.Material != null)
            {
                return sharedMaterial.Material;
            }
            PruneIdleMaterials();
            Materials.TryGetValue(key, out var next);
            if (next != null && (next.Material == null || next.Material.shader == null))
            {
                RemoveCachedMaterial(next);
                next = null;
            }
            if (next == null)
            {
                var material = new Material(baseMaterial)
                {
                    shader = effectShader,
                    name = "ImageExtend (Shared)",
                    hideFlags = HideFlags.HideAndDontSave
                };
                material.SetVector("_Radius", radius);
                material.SetFloat("_EdgeFeather", feather);
                material.SetColor("_OutlineColor", activeOutlineColor);
                material.SetVector("_Outline", outline);
                material.SetColor("_ShadowColor", activeShadowColor);
                material.SetVector("_Shadow", shadow);
                next = new SharedMaterial { Material = material, Key = key, ShaderReference = shaderReference };
                if (shaderReference != null)
                {
                    shaderReference.References++;
                }
                Materials.Add(key, next);
            }
            if (next.IdleNode != null)
            {
                IdleMaterials.Remove(next.IdleNode);
                next.IdleNode = null;
            }
            // 先保护命中项，再释放旧参数，避免同一次切换将目标闲置材质淘汰。
            next.References++;
            ReleaseMaterial();
            sharedMaterial = next;
            materialKey = key;
            return sharedMaterial.Material;
        }

        private void ReleaseMaterial()
        {
            if (sharedMaterial == null)
            {
                return;
            }
            var released = sharedMaterial;
            sharedMaterial = null;
            if (Materials.TryGetValue(released.Key, out var cached) && ReferenceEquals(cached, released) && --released.References == 0)
            {
                released.IdleNode = IdleMaterials.AddLast(released);
                PruneIdleMaterials();
            }
        }

        /// 清理所有闲置派生材质及其 Shader 资源引用，保留正在使用的材质。
        public static void ClearUnusedMaterials()
        {
            while (IdleMaterials.First != null)
            {
                RemoveCachedMaterial(IdleMaterials.First.Value);
            }
        }

        private static void PruneIdleMaterials()
        {
            var node = IdleMaterials.First;
            while (node != null)
            {
                var next = node.Next;
                var entry = node.Value;
                if (entry.Key.Item1 == null || entry.Material == null || entry.Material.shader == null ||
                    (entry.ShaderReference != null && (entry.ShaderReference.Service != ResourceServices.Default || !entry.ShaderReference.Service.IsInitialized)))
                {
                    RemoveCachedMaterial(entry);
                }
                node = next;
            }
            while (IdleMaterials.Count > IdleMaterialCapacity)
            {
                RemoveCachedMaterial(IdleMaterials.First.Value);
            }
        }

        private static void RemoveCachedMaterial(SharedMaterial entry)
        {
            Materials.Remove(entry.Key);
            if (entry.IdleNode != null)
            {
                IdleMaterials.Remove(entry.IdleNode);
                entry.IdleNode = null;
            }
            var material = entry.Material;
            entry.Material = null;
            if (material != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(material);
                }
                else
                {
                    DestroyImmediate(material);
                }
            }
            var reference = entry.ShaderReference;
            entry.ShaderReference = null;
            reference?.Release();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetMaterialCache()
        {
            foreach (var component in Resources.FindObjectsOfTypeAll<ImageExtend>())
            {
                if (!component.gameObject.scene.IsValid())
                {
                    continue;
                }
                component.ReleaseMaterial();
                component.effectShader = null;
                component.shaderLoadFailed = false;
                if (component.shaderLoading)
                {
                    component.shaderReleasePending = true;
                }
                else
                {
                    component.ReleaseShaderReference();
                }
                component.Refresh();
            }
            foreach (var entry in new List<SharedMaterial>(Materials.Values))
            {
                RemoveCachedMaterial(entry);
            }
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void RegisterEditorCleanup()
        {
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += ResetMaterialCache;
            UnityEditor.EditorApplication.quitting += ResetMaterialCache;
            UnityEditor.EditorApplication.playModeStateChanged += state =>
            {
                if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode || state == UnityEditor.PlayModeStateChange.ExitingEditMode)
                {
                    ResetMaterialCache();
                }
            };
        }
#endif

        private void ReleaseShaderReference()
        {
            var reference = shaderReference;
            shaderReference = null;
            reference?.Release();
        }

        private void EnsureShader()
        {
            if (!shaderLoading && shaderReference != null && (shaderReference.Service != ResourceServices.Default || !shaderReference.Service.IsInitialized))
            {
                ReleaseMaterial();
                ReleaseShaderReference();
                effectShader = null;
                shaderLoadFailed = false;
            }
            if (!IsActive() || !NeedsShader || effectShader != null || destroyed)
            {
                return;
            }
            if (!Application.isPlaying)
            {
                effectShader = Shader.Find(ShaderName);
            }
            else if (!shaderLoading && !shaderLoadFailed && ResourceServices.Default.IsInitialized)
            {
                LoadShaderAsync().Forget();
            }
        }

        private async UniTaskVoid LoadShaderAsync()
        {
            shaderLoading = true;
            try
            {
                shaderReference ??= new ShaderReference { Service = ResourceServices.Default, Loader = ResourceServices.CreateLoader() };
                Shader shader = await shaderReference.Loader.LoadAssetAsync<Shader>(ShaderAddress);
                if (!destroyed && !shaderReleasePending)
                {
                    effectShader = shader;
                    if (shader == null)
                    {
                        // 已确定失败后保留原生显示；重新启用才重试，避免刷新网格时循环请求。
                        shaderLoadFailed = true;
                        Debug.LogError($"[ImageExtend] Shader 加载失败：{ShaderAddress}", this);
                    }
                    // 缓存命中可能在 ModifyMesh 内同步完成，避开当前 Canvas 重建再标脏。
                    if (CanvasUpdateRegistry.IsRebuildingGraphics())
                    {
                        await UniTask.Yield(PlayerLoopTiming.Update);
                    }
                    if (!destroyed)
                    {
                        Refresh();
                    }
                }
            }
            catch (Exception exception)
            {
                if (!destroyed)
                {
                    shaderLoadFailed = true;
                    Debug.LogException(exception, this);
                }
            }
            finally
            {
                shaderLoading = false;
                bool reloadAfterReset = shaderReleasePending && !destroyed;
                if (destroyed || shaderReleasePending)
                {
                    ReleaseShaderReference();
                    shaderReleasePending = false;
                }
                if (reloadAfterReset)
                {
                    Refresh();
                }
            }
        }

        private static Vector4 MaxZero(Vector4 value) => new(Mathf.Max(0f, value.x), Mathf.Max(0f, value.y), Mathf.Max(0f, value.z), Mathf.Max(0f, value.w));

        private static Rect GetMeshBounds(VertexHelper helper)
        {
            Vector2 min = new(float.MaxValue, float.MaxValue), max = new(float.MinValue, float.MinValue);
            UIVertex vertex = default;
            for (int i = 0; i < helper.currentVertCount; i++)
            {
                helper.PopulateUIVertex(ref vertex, i);
                min = Vector2.Min(min, vertex.position);
                max = Vector2.Max(max, vertex.position);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static void AddEffectQuad(VertexHelper helper, Rect bounds, Vector4 uvBounds, float padding, float mode, Color color)
        {
            Vector2[] corners =
            {
                new(bounds.xMin - padding, bounds.yMin - padding),
                new(bounds.xMin - padding, bounds.yMax + padding),
                new(bounds.xMax + padding, bounds.yMax + padding),
                new(bounds.xMax + padding, bounds.yMin - padding)
            };
            foreach (Vector2 position in corners)
            {
                UIVertex vertex = UIVertex.simpleVert;
                vertex.position = position;
                vertex.color = color;
                Vector2 t = new((position.x - bounds.xMin) / bounds.width, (position.y - bounds.yMin) / bounds.height);
                vertex.uv0 = new Vector4(Mathf.LerpUnclamped(uvBounds.x, uvBounds.z, t.x), Mathf.LerpUnclamped(uvBounds.y, uvBounds.w, t.y), mode, 0f);
                helper.AddVert(vertex);
            }
            helper.AddTriangle(0, 1, 2);
            helper.AddTriangle(2, 3, 0);
        }
    }
}
