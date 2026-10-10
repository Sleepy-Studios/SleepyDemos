using System;
using System.IO;
using System.Reflection;
using Hotfix.KagitKart;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;

namespace Tests.Demo
{
    public sealed class KagitKartHudTests
    {
        [Serializable]
        private sealed class Fixture
        {
            public string commit;
            public TimeSample[] times;
            public ThreatSample[] threats;
        }

        [Serializable]
        private sealed class TimeSample
        {
            public double seconds;
            public string chinese;
        }

        [Serializable]
        private sealed class ThreatSample
        {
            public string name;
            public float width, height, heading, localX, localY;
            public Relative relative;
        }

        [Serializable]
        private sealed class Relative
        {
            public float x, z;
        }

        [Test]
        public void TimeAndThreatDirectionMatchOriginalHud()
        {
            var fixture = JsonUtility.FromJson<Fixture>(File.ReadAllText(
                "Assets/Scripts/Tests/EditMode/Demo/KagitKart/Fixtures/notice-fixture.json"));
            Assert.That(fixture.commit, Is.EqualTo(KagitKartCatalog.SourceCommit));
            foreach (var sample in fixture.times)
            {
                Assert.That(KagitKartHudMotion.TimeText(sample.seconds), Is.EqualTo(sample.chinese));
            }
            var method = typeof(KagitKartHudNotice).GetMethod("ThreatPoint", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            foreach (var sample in fixture.threats)
            {
                Vector3 relative = sample.name == "fallback-behind" ? Vector3.back : new Vector3(sample.relative.x, 0, sample.relative.z);
                var point = (Vector2)method.Invoke(null, new object[] {new Vector2(sample.width, sample.height), relative, sample.heading, 70f});
                Assert.That(point.x, Is.EqualTo(sample.localX).Within(0.11f), sample.name);
                Assert.That(point.y, Is.EqualTo(sample.localY).Within(0.11f), sample.name);
            }
        }

        [Test]
        public void MinimapRealTrackPointsAndPlayerHeadingMatchOriginalSvgCoordinates()
        {
            var fixtures = JsonUtility.FromJson<MinimapFixtures>(AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/Scripts/Tests/EditMode/Demo/KagitKart/Fixtures/OriginalMinimap.json").text);
            Assert.That(fixtures.sourceCommit, Is.EqualTo(KagitKartCatalog.SourceCommit));
            var root = new GameObject("原作地图方向", typeof(RectTransform), typeof(CanvasRenderer));
            var graphic = root.AddComponent<KagitKartMinimap>();
            graphic.rectTransform.sizeDelta = new Vector2(100, 100);
            try
            {
                foreach (var route in fixtures.cases)
                {
                    var data = ReadTrack(route.track);
                    using var session = new KagitKartSession(ReadSource(), data, ReadPlan(route.track, data),
                        KagitKartRaceMode.TimeTrial, "tilki", 120, 12345);
                    graphic.SetData(session);
                    foreach (var expected in route.points)
                    {
                        var samples = data.data.samples;
                        Vector2 point = graphic.Project(samples.px[expected.index], samples.pz[expected.index]);
                        Assert.That(point.x, Is.EqualTo(expected.x).Within(0.011), route.track + " SVG X");
                        Assert.That(point.y, Is.EqualTo(expected.y).Within(0.011), route.track + " SVG Y");
                        Vector2 tip = KagitKartMinimap.RotateArrow(new Vector2(4.6f, 0), samples.heading[expected.index]);
                        Assert.That(tip.x, Is.EqualTo(expected.tipX).Within(0.00001), route.track + " 箭头 X");
                        Assert.That(tip.y, Is.EqualTo(expected.tipY).Within(0.00001), route.track + " 箭头 Y");
                        var position = new Vector3(samples.px[expected.index], samples.py[expected.index], samples.pz[expected.index]);
                        session.Player.PlaceAt(position, samples.heading[expected.index]);
                        Vector2 placed = graphic.Project(position.x, position.z);
                        using var mesh = new UnityEngine.UI.VertexHelper();
                        typeof(KagitKartMinimap).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                            .Invoke(graphic, new object[] { mesh });
                        var vertex = new UIVertex();
                        mesh.PopulateUIVertex(ref vertex, 0);
                        Vector2 actualTip = KagitKartMinimap.RotateArrow(new Vector2(4.6f, 0), session.Player.Heading);
                        Assert.That(vertex.position.x, Is.EqualTo(placed.x + actualTip.x - 50).Within(0.0001));
                        Assert.That(vertex.position.y, Is.EqualTo(50 - placed.y - actualTip.y).Within(0.0001), "UGUI 必须翻转源 Y");
                        Assert.That(graphic.mainTexture, Is.SameAs(Texture2D.whiteTexture), "标记不得采样透明路线 Sprite");
                    }
                }
                Assert.That(KagitKartMinimap.SourceFixed(2.675, 2), Is.EqualTo(2.67));
                Assert.That(KagitKartMinimap.SourceFixed(1.005, 2), Is.EqualTo(1));
                Assert.That(KagitKartMinimap.SourceFixed(-2.675, 2), Is.EqualTo(-2.67));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Serializable]
        private sealed class MinimapFixtures
        {
            public string sourceCommit;
            public MinimapRoute[] cases;
        }

        [Serializable]
        private sealed class MinimapRoute
        {
            public string track;
            public MinimapPoint[] points;
        }

        [Serializable]
        private sealed class MinimapPoint
        {
            public int index;
            public float x, y, tipX, tipY;
        }

        private const string Root = "Assets/LoadResources/Demos/kagit_kart/Data/";
        private static KagitKartSourceCatalog ReadSource() => KagitKartSourceCatalog.Parse(AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "SourceCatalog.json").text);
        private static KagitKartTrackData ReadTrack(string id) => KagitKartTrackData.Parse(AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "Tracks/" + KagitKartCatalog.TrackAssetName(id) + ".json").text);
        private static KagitKartAiPlan ReadPlan(string id, KagitKartTrackData data) => KagitKartAiPlan.Parse(AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "AiPlans/" + KagitKartCatalog.TrackAssetName(id) + ".json").text, data);
    }
}
