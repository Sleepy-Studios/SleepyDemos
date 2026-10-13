using Core.Runtime;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Tests.Module
{
    public sealed class CoreUIComponentMigrationTests
    {
        [Test]
        public void GlobalExtensions_KeepActiveComponentAndRectSemanticsExplicit()
        {
            GameObject target = new GameObject("ExtensionTarget", typeof(RectTransform));
            try
            {
                target.Hide();
                Assert.That(target.activeSelf, Is.False);
                target.Show();
                Assert.That(target.activeSelf, Is.True);

                CanvasGroup first = target.GetOrAddComponent<CanvasGroup>();
                CanvasGroup second = target.transform.GetOrAddComponent<CanvasGroup>();
                Assert.That(second, Is.SameAs(first));
                first.SetCanvasGroupVisible(false);
                Assert.That(first.alpha, Is.Zero);
                Assert.That(first.interactable, Is.False);
                Assert.That(first.blocksRaycasts, Is.False);

                RectTransform rect = target.GetComponent<RectTransform>();
                rect.SetSize(new Vector2(240f, 80f));
                rect.SetAnchoredPositionX(12f);
                rect.SetAnchoredPositionY(-8f);
                Assert.That(rect.rect.width, Is.EqualTo(240f).Within(0.01f));
                Assert.That(rect.rect.height, Is.EqualTo(80f).Within(0.01f));
                Assert.That(rect.anchoredPosition, Is.EqualTo(new Vector2(12f, -8f)));
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void ImageExtend_HorizontalFlipMirrorsVerticesAroundRectCenter()
        {
            GameObject target = new GameObject("FlipTarget", typeof(RectTransform), typeof(Image), typeof(ImageExtend));
            try
            {
                RectTransform rect = target.GetComponent<RectTransform>();
                rect.SetSize(new Vector2(100f, 40f));
                ImageExtend effect = target.GetComponent<ImageExtend>();
                effect.FlipHorizontal = true;
                using VertexHelper helper = CreateQuad(-50f, -20f, 50f, 20f);
                effect.ModifyMesh(helper);

                UIVertex vertex = default;
                helper.PopulateUIVertex(ref vertex, 0);
                Assert.That(vertex.position.x, Is.EqualTo(50f).Within(0.001f));
                helper.PopulateUIVertex(ref vertex, 2);
                Assert.That(vertex.position.x, Is.EqualTo(-50f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void PyramidLayoutGroup_StaggerModePlacesRemainderAboveFullRow()
        {
            GameObject root = new GameObject("Pyramid", typeof(RectTransform), typeof(PyramidLayoutGroup));
            try
            {
                RectTransform rootRect = root.GetComponent<RectTransform>();
                rootRect.SetSize(new Vector2(300f, 200f));
                PyramidLayoutGroup layout = root.GetComponent<PyramidLayoutGroup>();
                layout.Columns = 3;
                layout.CellSize = new Vector2(50f, 20f);
                layout.Spacing = new Vector2(10f, 5f);
                for (int i = 0; i < 5; i++)
                {
                    new GameObject($"Child{i}", typeof(RectTransform)).transform.SetParent(root.transform, false);
                }

                LayoutRebuilder.ForceRebuildLayoutImmediate(rootRect);
                RectTransform first = root.transform.GetChild(0) as RectTransform;
                RectTransform third = root.transform.GetChild(2) as RectTransform;
                Assert.That(first, Is.Not.Null);
                Assert.That(third, Is.Not.Null);
                Assert.That(first.anchoredPosition.y, Is.GreaterThan(third.anchoredPosition.y));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TMPAutoScroll_InitializeDoesNotAddPerItemMask()
        {
            GameObject viewport = new GameObject("Viewport", typeof(RectTransform));
            GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            try
            {
                textObject.transform.SetParent(viewport.transform, false);
                TMPAutoScrollEnableBehaviour scroll = viewport.AddComponent<TMPAutoScrollEnableBehaviour>();
                scroll.Initialize(viewport.GetComponent<RectTransform>(), textObject.GetComponent<TextMeshProUGUI>());
                Assert.That(viewport.GetComponent<RectMask2D>(), Is.Null, "文字网格裁剪不需要独立遮罩");
            }
            finally
            {
                Object.DestroyImmediate(viewport);
            }
        }

        [Test]
        public void ImageExtend_UsesPublicShaderAndCreatesModifiedMaterial()
        {
            Shader shader = Shader.Find("UI/ImageExtend");
            Assert.That(shader, Is.Not.Null, "公共 UIImageExtend.shader 必须已被 Unity 导入。");
            Assert.That(shader.isSupported, Is.True, "图片效果 Shader 必须能在当前图形 API 下完成编译。");

            GameObject target = new GameObject("Rounded", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(ImageExtend));
            Material baseMaterial = new Material(Shader.Find("UI/Default"));
            try
            {
                ImageExtend rounded = target.GetComponent<ImageExtend>();
                rounded.EnableRoundedCorners = true;
                Assert.That(target.GetComponent<Mask>(), Is.Null, "图片圆角不得自动改变子节点的遮罩契约。");
                Material modified = rounded.GetModifiedMaterial(baseMaterial);
                Assert.That(modified, Is.Not.Null);
                Assert.That(modified.shader, Is.EqualTo(shader));
                Assert.That(modified.GetVector("_Radius"), Is.EqualTo(new Vector4(10f, 10f, 10f, 10f)));
            }
            finally
            {
                Object.DestroyImmediate(baseMaterial);
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void ImageExtend_GradientPreservesTintAndOpacityWithoutChangingLayout()
        {
            GameObject target = new GameObject("TintedImage", typeof(RectTransform), typeof(Image), typeof(ImageExtend));
            try
            {
                var effect = target.GetComponent<ImageExtend>();
                target.GetComponent<RectTransform>().sizeDelta = new Vector2(100f, 40f);
                effect.EnableGradient = true;
                effect.Gradient = new Gradient
                {
                    colorKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    alphaKeys = new[] { new GradientAlphaKey(0.5f, 0f), new GradientAlphaKey(0.5f, 1f) }
                };
                using VertexHelper helper = CreateQuad(-50f, -20f, 50f, 20f);
                UIVertex vertex = default;
                for (int i = 0; i < helper.currentVertCount; i++)
                {
                    helper.PopulateUIVertex(ref vertex, i);
                    vertex.color = new Color(0.4f, 0.6f, 0.8f, 0.5f);
                    helper.SetUIVertex(vertex, i);
                }
                effect.ModifyMesh(helper);
                helper.PopulateUIVertex(ref vertex, 0);
                Assert.That(((Color)vertex.color).a, Is.EqualTo(0.25f).Within(0.01f));
                Assert.That(((Color)vertex.color).r, Is.EqualTo(0.4f).Within(0.01f));
                Assert.That(vertex.position, Is.EqualTo(new Vector3(-50f, -20f, 0f)));
                Assert.That(effect.GetModifiedMaterial(Graphic.defaultGraphicMaterial), Is.SameAs(Graphic.defaultGraphicMaterial));
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void ImageExtend_SharedMaterialsKeepMaskAndFilledContracts()
        {
            var first = new GameObject("First", typeof(RectTransform), typeof(Image), typeof(ImageExtend));
            var second = new GameObject("Second", typeof(RectTransform), typeof(Image), typeof(ImageExtend));
            var baseMaterial = new Material(Graphic.defaultGraphicMaterial);
            try
            {
                var a = first.GetComponent<ImageExtend>();
                var b = second.GetComponent<ImageExtend>();
                a.EnableRoundedCorners = b.EnableRoundedCorners = true;
                baseMaterial.SetInt("_Stencil", 7);
                Material shared = a.GetModifiedMaterial(baseMaterial);
                Assert.That(b.GetModifiedMaterial(baseMaterial), Is.SameAs(shared));
                Assert.That(shared.GetInt("_Stencil"), Is.EqualTo(7));
                Object.DestroyImmediate(first);
                Assert.That(shared != null, Is.True, "仍有使用者时不能销毁共享材质。");
                b.EnableShadow = true;
                using (var helper = CreateQuad(-50f, -20f, 50f, 20f))
                {
                    b.ModifyMesh(helper);
                    var mesh = new Mesh();
                    try
                    {
                        helper.FillMesh(mesh);
                        Assert.That(mesh.bounds.min.y, Is.LessThan(-20f), "偏移阴影须保留原图范围之外的绘制区域。");
                        Assert.That(mesh.bounds.max.x, Is.GreaterThanOrEqualTo(50f), "不能截去原图的右侧范围。");
                    }
                    finally
                    {
                        Object.DestroyImmediate(mesh);
                    }
                }
                second.GetComponent<Image>().type = Image.Type.Filled;
                Assert.That(b.GetSupportIssue(), Is.Not.Null);
                Assert.That(b.GetModifiedMaterial(baseMaterial), Is.SameAs(baseMaterial));
            }
            finally
            {
                if (first != null) Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
                Object.DestroyImmediate(baseMaterial);
            }
        }

        [Test]
        public void TMPUGUIExtend_AppliesGradientAndSkewToUploadedMesh()
        {
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/LoadResources/Fonts/TMP_FontAssets/CN/HarmonyOS_CN.asset");
            Assert.That(font, Is.Not.Null);

            GameObject canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler));
            GameObject textObject = new GameObject(
                "TMPEffect",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI),
                typeof(TMP_UGUI_Extend));
            try
            {
                textObject.transform.SetParent(canvasObject.transform, false);
                TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
                text.font = font;
                text.text = "渐变测试";
                text.fontSize = 48f;
                text.rectTransform.SetSize(new Vector2(400f, 100f));

                TMP_UGUI_Extend effect = textObject.GetComponent<TMP_UGUI_Extend>();
                SerializedObject serializedEffect = new SerializedObject(effect);
                serializedEffect.FindProperty("gradientType").enumValueIndex =
                    (int)TMP_UGUI_Extend.GradientType.FourCorner;
                serializedEffect.FindProperty("leftTopColor").colorValue = Color.red;
                serializedEffect.FindProperty("leftBottomColor").colorValue = Color.blue;
                serializedEffect.FindProperty("rightTopColor").colorValue = Color.green;
                serializedEffect.FindProperty("rightBottomColor").colorValue = Color.yellow;
                serializedEffect.FindProperty("enableSkew").boolValue = true;
                serializedEffect.FindProperty("skewAngleDegrees").floatValue = 20f;
                serializedEffect.ApplyModifiedPropertiesWithoutUndo();

                effect.RefreshAllEffects();
                Mesh mesh = text.mesh;
                Assert.That(mesh, Is.Not.Null);
                Assert.That(mesh.vertexCount, Is.GreaterThan(0));

                Color32[] colors = mesh.colors32;
                Assert.That(colors.Length, Is.EqualTo(mesh.vertexCount));
                bool hasDifferentVertexColors = false;
                for (int i = 1; i < colors.Length; i++)
                {
                    if (!colors[i].Equals(colors[0]))
                    {
                        hasDifferentVertexColors = true;
                        break;
                    }
                }

                Assert.That(hasDifferentVertexColors, Is.True, "渐变必须写入最终上传网格的顶点色。");

                Vector3[] vertices = mesh.vertices;
                Assert.That(vertices[1].x, Is.Not.EqualTo(vertices[0].x).Within(0.001f),
                    "启用倾斜后，同一字符上下顶点的 X 坐标应产生偏移。");
            }
            finally
            {
                Object.DestroyImmediate(canvasObject);
            }
        }

        [Test]
        public void FlowLayoutGroup_HorizontalFlowWrapsVariablePreferredSizes()
        {
            GameObject root = new GameObject("Flow", typeof(RectTransform), typeof(FlowLayoutGroup));
            try
            {
                RectTransform rootRect = root.GetComponent<RectTransform>();
                rootRect.SetSize(new Vector2(210f, 120f));
                FlowLayoutGroup layout = root.GetComponent<FlowLayoutGroup>();
                layout.Spacing = new Vector2(10f, 5f);

                RectTransform first = CreateLayoutChild(root.transform, "First", 120f, 30f);
                RectTransform second = CreateLayoutChild(root.transform, "Second", 80f, 20f);
                RectTransform third = CreateLayoutChild(root.transform, "Third", 90f, 25f);

                LayoutRebuilder.ForceRebuildLayoutImmediate(rootRect);

                Assert.That(GetLocalTop(first), Is.EqualTo(GetLocalTop(second)).Within(0.01f));
                Assert.That(GetLocalTop(third), Is.LessThan(GetLocalTop(first)));
                Assert.That(first.rect.width, Is.EqualTo(120f).Within(0.01f));
                Assert.That(second.rect.width, Is.EqualTo(80f).Within(0.01f));
                Assert.That(LayoutUtility.GetPreferredHeight(rootRect), Is.EqualTo(60f).Within(0.01f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void FlowLayoutGroup_VerticalFlowWrapsIntoColumns()
        {
            GameObject root = new GameObject("VerticalFlow", typeof(RectTransform), typeof(FlowLayoutGroup));
            try
            {
                RectTransform rootRect = root.GetComponent<RectTransform>();
                rootRect.SetSize(new Vector2(180f, 100f));
                FlowLayoutGroup layout = root.GetComponent<FlowLayoutGroup>();
                layout.StartAxis = FlowLayoutAxis.Vertical;
                layout.Spacing = new Vector2(6f, 10f);

                RectTransform first = CreateLayoutChild(root.transform, "First", 40f, 55f);
                RectTransform second = CreateLayoutChild(root.transform, "Second", 50f, 35f);
                RectTransform third = CreateLayoutChild(root.transform, "Third", 60f, 45f);

                LayoutRebuilder.ForceRebuildLayoutImmediate(rootRect);

                Assert.That(GetLocalLeft(first), Is.EqualTo(GetLocalLeft(second)).Within(0.01f));
                Assert.That(GetLocalLeft(third), Is.GreaterThan(GetLocalLeft(first)));
                Assert.That(LayoutUtility.GetPreferredWidth(rootRect), Is.EqualTo(116f).Within(0.01f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static RectTransform CreateLayoutChild(Transform parent, string name, float width, float height)
        {
            GameObject child = new GameObject(name, typeof(RectTransform), typeof(LayoutElement));
            child.transform.SetParent(parent, false);
            LayoutElement element = child.GetComponent<LayoutElement>();
            element.preferredWidth = width;
            element.preferredHeight = height;
            return child.GetComponent<RectTransform>();
        }

        private static float GetLocalLeft(RectTransform rect) => rect.localPosition.x + rect.rect.xMin;
        private static float GetLocalTop(RectTransform rect) => rect.localPosition.y + rect.rect.yMax;

        private static VertexHelper CreateQuad(float minX, float minY, float maxX, float maxY)
        {
            VertexHelper helper = new VertexHelper();
            Color32 color = Color.white;
            helper.AddVert(new Vector3(minX, minY), color, Vector2.zero);
            helper.AddVert(new Vector3(minX, maxY), color, Vector2.up);
            helper.AddVert(new Vector3(maxX, maxY), color, Vector2.one);
            helper.AddVert(new Vector3(maxX, minY), color, Vector2.right);
            helper.AddTriangle(0, 1, 2);
            helper.AddTriangle(2, 3, 0);
            return helper;
        }
    }
}
