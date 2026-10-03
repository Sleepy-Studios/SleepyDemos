#if UNITY_EDITOR
using System.Collections;
using Core.Runtime;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Tests.Module
{
    public sealed class TMPAutoScrollPlayModeTests
    {
        private GameObject canvas;
        private TMPAutoScrollEnableBehaviour scroll;
        private TextMeshProUGUI text;
        private RectTransform viewport;
        private const string LongText = "ABCDEFGHIJKLMN 中文文本滚动验证 ABCDEFGHIJKLMN";

        [UnitySetUp] public IEnumerator SetUp()
        {
            canvas = new GameObject("AutoScrollTest", typeof(Canvas));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/LoadResources/UI/Common/TMPAutoScroll.prefab");
            Assert.That(prefab, Is.Not.Null);
            var instance = Object.Instantiate(prefab, canvas.transform);
            viewport = (RectTransform)instance.transform;
            viewport.sizeDelta = new Vector2(120, 42);
            scroll = instance.GetComponent<TMPAutoScrollEnableBehaviour>();
            text = instance.GetComponentInChildren<TextMeshProUGUI>();
            var options = TMPAutoScrollOptions.Default;
            options.StartDelay = .03f; options.EndStayTime = .03f;
            options.PixelsPerSecond = 1200; options.Loop = false; options.UseUnscaledTime = true;
            scroll.SetOptions(options);
            yield return null;
        }

        [UnityTearDown] public IEnumerator TearDown()
        {
            Time.timeScale = 1;
            Object.Destroy(canvas);
            yield return null;
        }

        [UnityTest] public IEnumerator DirectTextAssignmentClipsAndRestoresShortTextWithoutMask()
        {
            Assert.That(viewport.GetComponent<RectMask2D>(), Is.Null);
            text.text = LongText;
            yield return new WaitForSecondsRealtime(.12f);
            Assert.That(text.rectTransform.anchoredPosition.x, Is.LessThan(0));
            AssertClipped();
            text.text = "短";
            yield return null; yield return null;
            Assert.That(text.rectTransform.anchoredPosition.x, Is.EqualTo(0).Within(.01f));
            Assert.That(scroll.IsReading, Is.False);
            Assert.That(text.textInfo.characterCount, Is.EqualTo(1));
        }

        [UnityTest] public IEnumerator ResizeAndSameTextFontRebuildRecalculateClipping()
        {
            text.text = LongText;
            yield return new WaitForSecondsRealtime(.1f);
            viewport.sizeDelta = new Vector2(190, 42);
            text.fontSize = 36;
            text.SetVerticesDirty();
            yield return null; yield return null;
            Assert.That(scroll.IsReading, Is.True);
            AssertClipped();
            yield return WaitForCompletion();
            // 完成后仍保留裁剪网格，外部字体重建也必须使缓存失效。
            text.fontSize = 40;
            yield return null; yield return null;
            Assert.That(scroll.IsReading, Is.True);
            AssertClipped();
            viewport.sizeDelta = new Vector2(3000, 42);
            yield return null; yield return null;
            Assert.That(scroll.IsReading, Is.False);
            Assert.That(text.rectTransform.anchoredPosition.x, Is.EqualTo(0).Within(.01f));
        }

        [UnityTest] public IEnumerator DisableResetsAndCompletionFiresOncePerRoundDuringPause()
        {
            int completed = 0;
            scroll.ScrollCompleted += () => completed++;
            text.text = LongText;
            Time.timeScale = 0;
            yield return new WaitForSecondsRealtime(.12f);
            Assert.That(text.rectTransform.anchoredPosition.x, Is.LessThan(0));
            scroll.enabled = false;
            Assert.That(text.rectTransform.anchoredPosition.x, Is.EqualTo(0).Within(.01f));
            Assert.That(completed, Is.Zero);
            scroll.enabled = true;
            yield return WaitForCompletion();
            Assert.That(completed, Is.EqualTo(1));
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(completed, Is.EqualTo(1));
            scroll.gameObject.SetActive(false);
            scroll.gameObject.SetActive(true);
            yield return WaitForCompletion();
            Assert.That(completed, Is.EqualTo(2));
        }

        private IEnumerator WaitForCompletion()
        {
            float deadline = Time.realtimeSinceStartup + 4;
            while (scroll.IsReading && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(scroll.IsReading, Is.False, "非循环滚动应有界完成");
        }

        private void AssertClipped()
        {
            int visibleQuads = 0;
            foreach (var mesh in text.textInfo.meshInfo)
            {
                for (int i = 0; i + 3 < mesh.vertexCount; i += 4)
                {
                    if (mesh.vertices[i] == mesh.vertices[i + 2]) continue;
                    visibleQuads++;
                    for (int j = 0; j < 4; j++)
                    {
                        var point = viewport.InverseTransformPoint(text.transform.TransformPoint(mesh.vertices[i + j]));
                        Assert.That(point.x, Is.InRange(viewport.rect.xMin - .1f, viewport.rect.xMax + .1f));
                        Assert.That(float.IsNaN(mesh.uvs0[i + j].x), Is.False);
                    }
                }
            }
            Assert.That(visibleQuads, Is.GreaterThan(0), "不能用全部隐藏来通过裁剪检查");
        }
    }
}
#endif
