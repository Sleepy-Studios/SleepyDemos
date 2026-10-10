using System;
using Hotfix.KagitKart;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.Demo
{
    public sealed class KagitKartCinematicPathTests
    {
        [Serializable] private sealed class Fixture { public string sourceCommit; public Frame[] frames; }
        [Serializable] private sealed class Frame { public string track; public int entry, segment; public float time, fov; public float[] position, look; }

        [Test]
        public void IntroCurvesMatchOriginalJavaScriptForAllGridSlots()
        {
            var source = JsonUtility.FromJson<KagitKartRaceCinematics.Data>(AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/LoadResources/Demos/kagit_kart/Data/IntroPaths.json").text);
            var fixture = JsonUtility.FromJson<Fixture>(AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/Scripts/Tests/EditMode/Demo/KagitKart/Fixtures/OriginalIntro.json").text);
            Assert.That(source.sourceCommit, Is.EqualTo(KagitKartCatalog.SourceCommit));
            Assert.That(fixture.sourceCommit, Is.EqualTo(source.sourceCommit));
            foreach (var frame in fixture.frames)
            {
                var track = Array.Find(source.tracks, t => t.id == frame.track);
                var path = new KagitKartRaceCinematics.Path(track.entries[frame.entry].segments[frame.segment].keys);
                float fov = path.Sample(frame.time, out Vector3 position, out Vector3 look);
                Assert.That(Vector3.Distance(position, KagitKartRaceCinematics.Point(frame.position)), Is.LessThan(0.003f), frame.track);
                Assert.That(Vector3.Distance(look, KagitKartRaceCinematics.Point(frame.look)), Is.LessThan(0.003f), frame.track);
                Assert.That(fov, Is.EqualTo(frame.fov).Within(0.001f), frame.track);
            }
        }
    }
}
