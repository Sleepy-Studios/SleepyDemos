using System;
using System.Linq;
using System.Reflection;
using Hotfix.KagitKart;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Tests.Demo
{
    public sealed class KagitKartHudInkTests
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
            public Blob[] blobs;
            public Drip[] drips;
            public Fade[] opacity;
        }

        [Serializable]
        private sealed class Blob
        {
            public Vector2[] samples;
        }

        [Serializable]
        private sealed class Drip
        {
            public float circleX, circleY, radius;
        }

        [Serializable]
        private sealed class Fade
        {
            public float left, opacity;
            public bool hidden;
        }

        [Test]
        public void CurvesDripsFadeAndPauseMatchOriginalJavaScriptFixtures()
        {
            var fixture = JsonUtility.FromJson<Fixtures>(AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/Scripts/Tests/EditMode/Demo/KagitKart/Fixtures/OriginalHudInk.json").text);
            Assert.That(fixture.sourceCommit, Is.EqualTo(KagitKartCatalog.SourceCommit));
            var root = new GameObject("原作屏幕墨水fixture", typeof(RectTransform), typeof(CanvasRenderer));
            var graphic = root.AddComponent<KagitKartHudInk>();
            graphic.material = AssetDatabase.LoadAssetAtPath<Material>("Assets/LoadResources/Demos/kagit_kart/Art/Materials/HudInk.mat");
            graphic.rectTransform.sizeDelta = new Vector2(1600, 900);
            try
            {
                foreach (var value in fixture.cases)
                {
                    graphic.Splat(value.duration, value.seed);
                    graphic.Advance(0, value.duration, false);
                    var initial = Vertices(graphic);
                    foreach (var blob in value.blobs)
                    {
                        foreach (var p in blob.samples)
                        {
                            AssertPoint(initial, p, "原16段二次Bezier曲线边界");
                        }
                    }
                    Assert.That(graphic.raycastTarget, Is.False);
                    // 暂停只冻结规则墨水剩余时间；原CSS滴落仍继续，Opacity不随墙钟缩短。
                    graphic.Advance(4, value.duration, false);
                    Assert.That(graphic.Opacity, Is.EqualTo(1));
                    var grown = Vertices(graphic);
                    foreach (var drip in value.drips)
                    {
                        var bottom = new Vector2(drip.circleX, drip.circleY + drip.radius);
                        AssertPoint(grown, bottom, "原六条滴落完整终点");
                        Assert.That(initial.Any(v => Vector2.Distance(v, bottom) < 0.001f), Is.False);
                    }
                    graphic.Splat(value.duration, value.seed);
                    graphic.Advance(0, value.duration, true);
                    var reduced = Vertices(graphic);
                    foreach (var drip in value.drips)
                    {
                        AssertPoint(reduced, new Vector2(drip.circleX, drip.circleY + drip.radius), "低动态取消动画，立即全长");
                    }
                    graphic.Advance(0, value.duration, false);
                    var restarted = Vertices(graphic);
                    var firstBottom = new Vector2(value.drips[0].circleX, value.drips[0].circleY + value.drips[0].radius);
                    Assert.That(restarted.Any(v => Vector2.Distance(v, firstBottom) < 0.001f), Is.False,
                        "退出低动态重新启动CSS滴落，而不是沿用旧时间直接全长");
                    foreach (var fade in value.opacity)
                    {
                        graphic.Advance(0, fade.left, true);
                        Assert.That(graphic.Opacity, Is.EqualTo(fade.opacity).Within(0.00001f));
                        if (fade.hidden)
                        {
                            Assert.That(Vertices(graphic), Is.Empty);
                        }
                    }
                    graphic.Splat(value.duration, value.seed);
                    graphic.Clear();
                    Assert.That(Vertices(graphic), Is.Empty);
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static Vector2[] Vertices(KagitKartHudInk graphic)
        {
            using (var helper = new VertexHelper())
            {
                typeof(KagitKartHudInk).GetMethod("PopulateInkMesh", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(graphic, new object[] { helper });
                var mesh = new Mesh();
                try
                {
                    helper.FillMesh(mesh);
                    return mesh.vertices.Select(v => new Vector2(v.x + 800, 450 - v.y)).ToArray();
                }
                finally
                {
                    Object.DestroyImmediate(mesh);
                }
            }
        }

        private static void AssertPoint(Vector2[] vertices, Vector2 expected, string meaning)
        {
            Assert.That(vertices.Min(v => Vector2.Distance(v, expected)), Is.LessThan(0.001f), meaning + " " + expected);
        }
    }
}
