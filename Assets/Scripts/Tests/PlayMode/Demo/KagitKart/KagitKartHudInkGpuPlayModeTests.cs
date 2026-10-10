using System;
using System.Collections;
using System.Reflection;
using Hotfix.KagitKart;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Tests.Demo
{
    public sealed class KagitKartHudInkGpuPlayModeTests
    {
        [Serializable]
        private sealed class Fixtures
        {
            public string sourceCommit;
            public Case[] cases;
        }

        [Serializable]
        private sealed class Case
        {
            public uint seed;
            public float duration;
            public Pixel[] pixelSamples;
        }

        [Serializable]
        private sealed class Pixel
        {
            public string kind;
            public int x, y;
            public int[] rgba;
        }

        [UnityTest]
        public IEnumerator OriginalOverlapAndHighlightAreCompositedBeforeWholeLayerFade()
        {
#if UNITY_EDITOR
            var fixture = JsonUtility.FromJson<Fixtures>(UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/Scripts/Tests/EditMode/Demo/KagitKart/Fixtures/OriginalHudInk.json").text);
            var material = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/LoadResources/Demos/kagit_kart/Art/Materials/HudInk.mat");
            Assert.That(fixture.sourceCommit, Is.EqualTo(KagitKartCatalog.SourceCommit));
            var output = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            var readback = new Texture2D(1600, 900, TextureFormat.RGBAFloat, false, true);
            var cameraObject = new GameObject("墨水GPU相机");
            var camera = cameraObject.AddComponent<Camera>();
            camera.targetTexture = output;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.cullingMask = 1 << 31;
            camera.orthographic = true;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100;
            var canvasObject = new GameObject("墨水GPU Canvas", typeof(RectTransform), typeof(Canvas));
            canvasObject.layer = 31;
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 10;
            var graphicObject = new GameObject("原动态墨水", typeof(RectTransform), typeof(CanvasRenderer));
            graphicObject.layer = 31;
            graphicObject.transform.SetParent(canvas.transform, false);
            var ink = graphicObject.AddComponent<KagitKartHudInk>();
            ink.material = material;
            ink.rectTransform.anchorMin = Vector2.zero;
            ink.rectTransform.anchorMax = Vector2.one;
            ink.rectTransform.offsetMin = ink.rectTransform.offsetMax = Vector2.zero;
            try
            {
                Canvas.ForceUpdateCanvases();
                foreach (var value in fixture.cases)
                {
                    ink.Splat(value.duration, value.seed);
                    ink.Advance(0, value.duration, true);
                    Canvas.ForceUpdateCanvases();
                    yield return new WaitForEndOfFrame();
                    AssertSamples(Read(output, readback), value, 1);
                    var composition = ink.mainTexture as RenderTexture;
                    Assert.That(composition, Is.Not.Null);
                    Assert.That(composition.sRGB, Is.False, "RT保存源SVG sRGB数值，不进行硬件色彩转换");
                    ink.Advance(0, value.duration * 0.35f * 0.5f, true);
                    Canvas.ForceUpdateCanvases();
                    yield return new WaitForEndOfFrame();
                    AssertSamples(Read(output, readback), value, 0.5f);
                    Assert.That(ink.mainTexture, Is.SameAs(composition), "渐隐不重新分配合成RT");
                    ink.Advance(0, 0, true);
                    Assert.That(ink.mainTexture, Is.Not.InstanceOf<RenderTexture>(), "结束释放本层RT");
                }
                ink.Splat(3.5f, fixture.cases[0].seed);
                ink.enabled = false;
                Assert.That(ink.mainTexture, Is.Not.InstanceOf<RenderTexture>(), "禁用释放本层RT");
                Assert.That(typeof(KagitKartHudInk).GetField("target", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(ink), Is.Null);
            }
            finally
            {
                camera.targetTexture = null;
                Object.DestroyImmediate(canvasObject);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(output);
                Object.DestroyImmediate(readback);
            }
#else
            throw new InvalidOperationException("本fixture测试仅在Editor执行，不伪报Player GPU已验证。");
#endif
        }

        private static Color[] Read(RenderTexture target, Texture2D readback)
        {
            var previous = RenderTexture.active;
            try
            {
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
                readback.Apply();
                return readback.GetPixels();
            }
            finally
            {
                RenderTexture.active = previous;
            }
        }

        private static void AssertSamples(Color[] pixels, Case value, float opacity)
        {
            foreach (var p in value.pixelSamples)
            {
                var source = new Color(p.rgba[0] / 255f, p.rgba[1] / 255f, p.rgba[2] / 255f, 1);
                if (QualitySettings.activeColorSpace == ColorSpace.Linear)
                {
                    source = source.linear;
                }
                float alpha = p.rgba[3] / 255f * opacity;
                Color expected = source * alpha;
                Color actual = pixels[(899 - p.y) * 1600 + p.x];
                Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.004f), p.kind + " 红色/整层opacity");
                Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.004f), p.kind + " 绿色/色彩空间");
                Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.004f), p.kind + " 蓝色/UV方向");
            }
        }
    }
}
