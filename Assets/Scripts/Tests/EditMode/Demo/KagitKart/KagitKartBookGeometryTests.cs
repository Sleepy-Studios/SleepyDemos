using System;
using System.IO;
using Hotfix.KagitKart;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Demo
{
    public sealed class KagitKartBookGeometryTests
    {
        [Serializable]
        private sealed class Reference
        {
            public string sourceCommit;
            public CoverSample[] cover;
            public SheetSample[] sheet;
            public FrameSample[] frames;
        }

        [Serializable]
        private sealed class CoverSample
        {
            public float[] point;
            public float closed;
            public float[] expected;
        }

        [Serializable]
        private sealed class SheetSample
        {
            public float[] point;
            public float progress;
            public bool forward;
            public float[] expected;
        }

        [Serializable]
        private sealed class FrameSample
        {
            public float[] eye, subject, screen, expected;
            public float aspect, fov;
        }

        [Test]
        public void DeformationAndFramingMatchOriginalJavascript()
        {
            var reference = JsonUtility.FromJson<Reference>(File.ReadAllText("Assets/Scripts/Tests/EditMode/Demo/KagitKart/Fixtures/OriginalBook.json"));
            Assert.That(reference.sourceCommit, Is.EqualTo(KagitKartCatalog.SourceCommit));
            foreach (var sample in reference.cover)
            {
                var actual = KagitKartBookGeometry.CoverPoint(Vector(sample.point), sample.closed);
                Assert.That(Vector3.Distance(actual, Vector(sample.expected)), Is.LessThan(0.0001f), "封面 " + sample.closed);
            }
            foreach (var sample in reference.sheet)
            {
                var actual = KagitKartBookGeometry.SheetPoint(Vector(sample.point), sample.progress, sample.forward);
                Assert.That(Vector3.Distance(actual, Vector(sample.expected)), Is.LessThan(0.0001f), "卷页 " + sample.progress);
            }
            foreach (var sample in reference.frames)
            {
                float fov = KagitKartBookGeometry.FitFov(sample.aspect, 32, 44);
                Assert.That(fov, Is.EqualTo(sample.fov).Within(0.0001f));
                var actual = KagitKartBookGeometry.FrameAt(Vector(sample.eye), Vector(sample.subject),
                    new Vector2(sample.screen[0], sample.screen[1]), fov, sample.aspect);
                Assert.That(Vector3.Distance(actual, Vector(sample.expected)), Is.LessThan(0.0001f), "镜头 " + sample.aspect);
            }
        }

        private static Vector3 Vector(float[] values) => new(values[0], values[1], values[2]);
    }
}
