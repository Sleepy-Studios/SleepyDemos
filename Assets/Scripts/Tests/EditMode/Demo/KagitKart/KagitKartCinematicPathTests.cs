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
        [Test]
        public void SwitchingReducedIntroKeepsProgressAndDoesNotRestartRaceClock()
        {
            const string root = "Assets/LoadResources/Demos/kagit_kart/Data/";
            var source = KagitKartSourceCatalog.Parse(AssetDatabase.LoadAssetAtPath<TextAsset>(root + "SourceCatalog.json").text);
            string name = KagitKartCatalog.TrackAssetName("meadow");
            var track = KagitKartTrackData.Parse(AssetDatabase.LoadAssetAtPath<TextAsset>(root + "Tracks/" + name + ".json").text);
            var plan = KagitKartAiPlan.Parse(AssetDatabase.LoadAssetAtPath<TextAsset>(root + "AiPlans/" + name + ".json").text, track);
            string json = AssetDatabase.LoadAssetAtPath<TextAsset>(root + "IntroPaths.json").text;
            var data = JsonUtility.FromJson<KagitKartRaceCinematics.Data>(json);
            using var session = new KagitKartSession(source, track, plan, KagitKartRaceMode.TimeTrial, "tilki", 120, 1);
            var entries = Array.Find(data.tracks, value => value.id == "meadow").entries;
            var normal = Array.Find(entries, value => !value.reducedMotion
                && Vector3.Distance(KagitKartRaceCinematics.Point(value.position), session.Player.Position) < 0.01f);
            var reduced = Array.Find(entries, value => value.reducedMotion
                && Vector3.Distance(KagitKartRaceCinematics.Point(value.position), session.Player.Position) < 0.01f);
            var cameraObject = new GameObject("ReducedIntroTestCamera", typeof(Camera));
            try
            {
                var shot = new KagitKartRaceCinematics(json, cameraObject.GetComponent<Camera>(), session, source.config.camera);
                for (int i = 0; i < 10; i++)
                {
                    shot.AdvanceIntro(0.1f);
                }
                shot.SetReducedMotion(true);
                float remaining = reduced.duration * (1 - 1 / normal.duration);
                float elapsed = 0;
                while (elapsed + 0.1f < remaining - 0.02f)
                {
                    shot.AdvanceIntro(0.1f);
                    elapsed += 0.1f;
                }
                Assert.That(shot.IntroPlaying, Is.True);
                while (elapsed < remaining + 0.05f)
                {
                    shot.AdvanceIntro(0.05f);
                    elapsed += 0.05f;
                }
                Assert.That(shot.IntroPlaying, Is.False, "切换应接续原进度，而非从低动态起点重播。");
                Assert.That(session.RaceTime, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }



    }
}
