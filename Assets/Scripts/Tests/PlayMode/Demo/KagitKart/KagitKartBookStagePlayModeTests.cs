#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using Hotfix.KagitKart;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tests.Demo
{
    public sealed class KagitKartBookStagePlayModeTests
    {
        [UnityTest]
        public IEnumerator OpenSwapQueueReverseAndDisposePreserveSharedMeshes()
        {
            var template = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/LoadResources/Demos/kagit_kart/Prefabs/BookStage.prefab");
            var original = template.GetComponentsInChildren<MeshFilter>(true).Single(f => f.name == "book:frontCover").sharedMesh;
            var saved = original.vertices;
            var root = Object.Instantiate(template);
            var cameraObject = new GameObject("BookTestCamera", typeof(Camera), typeof(Skybox));
            var lightObject = new GameObject("BookTestSun", typeof(Light));
            try
            {
                var stage = root.GetComponent<KagitKartBookStage>();
                stage.Initialize(cameraObject.GetComponent<Camera>(), lightObject.GetComponent<Light>());
                stage.SetPage(KagitKartCommand.Title, "tilki", "meadow");
                stage.Advance(0);
                Assert.That(stage.ClosedFraction, Is.EqualTo(1));
                var cover = root.GetComponentsInChildren<MeshFilter>(true).Single(f => f.name == "book:frontCover");
                Assert.That(cover.sharedMesh, Is.Not.SameAs(original));
                stage.SetPage(KagitKartCommand.Modes, "tilki", "meadow");
                Advance(stage, 1.3f);
                Assert.That(stage.ClosedFraction, Is.Zero);
                Assert.That(Vector3.Distance(cover.sharedMesh.vertices[0], saved[0]), Is.LessThan(0.0001f));
                stage.SetPage(KagitKartCommand.Characters, "tilki", "meadow");
                stage.PreviewCharacter("kurbaga");
                Advance(stage, 1.5f);
                Assert.That(stage.CurrentCharacterId, Is.EqualTo("kurbaga"));
                var cameraPose = cameraObject.transform.position;
                stage.Advance(0);
                Assert.That(cameraObject.transform.position, Is.EqualTo(cameraPose));
                stage.SetPage(KagitKartCommand.Tracks, "kurbaga", "meadow");
                stage.ShowChapter("bosphorus");
                Advance(stage, 0.5f);
                Assert.That(stage.Turning, Is.True);
                var sheet = root.GetComponentsInChildren<MeshFilter>(true).Single(f => f.name == "sheet:front");
                Assert.That(sheet.gameObject.activeInHierarchy, Is.True);
                Assert.That(sheet.sharedMesh.vertices.Any(p => p.y > 3), Is.True, "卷页的自由边应实际抬起。");
                stage.ShowChapter("glacier");
                stage.ShowChapter("desk");
                Advance(stage, 3.5f);
                Assert.That(stage.Turning, Is.False);
                Assert.That(stage.ChapterId, Is.EqualTo("desk"), "连续焦点请求应保留最后章节。");
                stage.ShowChapter("meadow");
                Advance(stage, 1.5f);
                Assert.That(stage.ChapterId, Is.EqualTo("meadow"));
                stage.SetPodium(false);
                Assert.That(stage.ChapterId, Is.EqualTo("desk"));
                Assert.That(stage.Turning, Is.False);
                Assert.That(stage.CurrentCharacterId, Is.Null);
                var podiumCamera = cameraObject.transform.position;
                Advance(stage, 1.5f);
                Assert.That(cameraObject.transform.position, Is.EqualTo(podiumCamera), "领奖镜头由颁奖舞台接管。");
                stage.SetPage(KagitKartCommand.Title, "kurbaga", "meadow");
                Advance(stage, 1.5f);
                Assert.That(stage.ClosedFraction, Is.EqualTo(1));
                CollectionAssert.AreEqual(saved, original.vertices);
            }
            finally
            {
                Object.Destroy(root);
                Object.Destroy(cameraObject);
                Object.Destroy(lightObject);
            }
            yield return null;
        }

        private static void Advance(KagitKartBookStage stage, float seconds)
        {
            for (int i = 0; i < Mathf.CeilToInt(seconds * 120); i++)
            {
                stage.Advance(1f / 120);
            }
        }
    }
}
#endif
