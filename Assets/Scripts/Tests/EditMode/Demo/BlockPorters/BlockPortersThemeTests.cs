using System;
using System.Collections;
using System.Collections.Generic;
using Core.Runtime;
using Cysharp.Threading.Tasks;
using Hotfix.BlockPorters;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tests.Demo
{
    public sealed class BlockPortersThemeTests
    {
        private readonly List<Object> assets = new();

        [Test]
        public void SelectionExcludesLastAppliedAndUsesIndependentSeed()
        {
            var catalog = ScriptableObject.CreateInstance<BlockPortersThemeCatalog>(); assets.Add(catalog);
            catalog.Themes = new[] { Theme("a"), Theme("b"), Theme("c"), Theme("d") };
            var first = new System.Random(17); var second = new System.Random(17);
            string previous = "a";
            for (int i = 0; i < 100; i++)
            {
                var choice = catalog.Choose(previous, first);
                Assert.That(choice.Id, Is.Not.EqualTo(previous));
                Assert.That(choice.Id, Is.EqualTo(catalog.Choose(previous, second).Id));
                previous = choice.Id;
            }
        }

        [Test]
        public void EmptyAndSingleCatalogHaveDefinedFallbacks()
        {
            var catalog = ScriptableObject.CreateInstance<BlockPortersThemeCatalog>(); assets.Add(catalog);
            Assert.That(catalog.Choose(null, new System.Random(1)), Is.Null);
            catalog.Themes = new[] { Theme("only") };
            Assert.That(catalog.Choose("only", new System.Random(1)).Id, Is.EqualTo("only"));
        }

        [UnityTest]
        public IEnumerator StaleCompletionCannotReplaceNewerTextureAndReleasesItsLoader()
        {
            var old = new PendingLoader(); var fresh = new PendingLoader();
            var requests = new Queue<PendingLoader>(new[] { old, fresh });
            using var loader = new BlockPortersThemeLoader(() => requests.Dequeue());
            Texture2D shown = null;
            var oldTask = loader.ApplyAsync(Theme("old"), t => shown = t).AsTask();
            var newTask = loader.ApplyAsync(Theme("new"), t => shown = t).AsTask();
            var texture = Texture(); fresh.Complete(texture);
            yield return new WaitUntil(() => newTask.IsCompleted);
            Assert.That(newTask.Result, Is.True); Assert.That(shown, Is.SameAs(texture));
            old.Complete(Texture()); yield return new WaitUntil(() => oldTask.IsCompleted);
            Assert.That(oldTask.Result, Is.False); Assert.That(loader.AppliedId, Is.EqualTo("new"));
            Assert.That(shown, Is.SameAs(texture)); Assert.That(old.DisposeCount, Is.EqualTo(1));
            Assert.That(fresh.DisposeCount, Is.Zero);
            loader.Dispose(); Assert.That(fresh.DisposeCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator FailedLoadKeepsCurrentTextureAndSuccessfulSwitchReleasesPrevious()
        {
            var first = new PendingLoader(); var failed = new PendingLoader(); var next = new PendingLoader();
            var requests = new Queue<PendingLoader>(new[] { first, failed, next });
            using var loader = new BlockPortersThemeLoader(() => requests.Dequeue());
            Texture2D shown = null; var original = Texture();
            var task = loader.ApplyAsync(Theme("first"), t => shown = t).AsTask(); first.Complete(original);
            yield return new WaitUntil(() => task.IsCompleted);
            var failure = loader.ApplyAsync(Theme("failed"), t => shown = t).AsTask(); failed.Complete(null);
            yield return new WaitUntil(() => failure.IsCompleted);
            Assert.That(failure.Result, Is.False); Assert.That(shown, Is.SameAs(original));
            Assert.That(loader.AppliedId, Is.EqualTo("first")); Assert.That(first.DisposeCount, Is.Zero); Assert.That(failed.DisposeCount, Is.EqualTo(1));
            var replacement = loader.ApplyAsync(Theme("next"), t => shown = t).AsTask(); next.Complete(Texture());
            yield return new WaitUntil(() => replacement.IsCompleted);
            Assert.That(replacement.Result, Is.True); Assert.That(first.DisposeCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator InvalidationAndExitIsolatePendingResultsWithoutDisposingActiveOperation()
        {
            var pending = new PendingLoader(); using var loader = new BlockPortersThemeLoader(() => pending);
            int applications = 0;
            var task = loader.ApplyAsync(Theme("pending"), _ => applications++).AsTask();
            loader.Invalidate(); loader.Dispose();
            Assert.That(pending.DisposeCount, Is.Zero, "加载操作完成前不能释放其句柄");
            pending.Complete(Texture()); yield return new WaitUntil(() => task.IsCompleted);
            Assert.That(task.Result, Is.False); Assert.That(applications, Is.Zero); Assert.That(pending.DisposeCount, Is.EqualTo(1));
            Assert.That(loader.ApplyAsync(Theme("later"), _ => applications++).GetAwaiter().GetResult(), Is.False);
        }

        [Test]
        public void TileSpecSharesOuterAndInnerSizesAndContrastingInk()
        {
            var style = ScriptableObject.CreateInstance<BlockPortersUiStyle>(); assets.Add(style);
            Assert.That(style.TileDimensions, Is.EqualTo(new Vector2(80, 80)));
            Assert.That(style.FaceDimensions, Is.EqualTo(new Vector2(64, 64)));
            Assert.That(style.ColumnX(1) - style.ColumnX(0), Is.EqualTo(96));
            Assert.That(style.Ink(new Color(.08f, .12f, .22f)), Is.EqualTo(style.LightInk));
            Assert.That(style.Ink(Color.yellow), Is.EqualTo(style.DarkInk));
        }

        [TearDown]
        public void Cleanup() { foreach (var asset in assets) Object.DestroyImmediate(asset); assets.Clear(); }
        private Texture2D Texture() { var texture = new Texture2D(2, 2); assets.Add(texture); return texture; }
        private static BlockPortersThemeCatalog.Theme Theme(string id) => new() { Id = id, BackgroundAddress = id };
        private sealed class PendingLoader : IResourceLoader
        {
            private readonly UniTaskCompletionSource<Texture2D> completion = new();
            public int DisposeCount { get; private set; }
            public void Complete(Texture2D texture) => completion.TrySetResult(texture);
            public async UniTask<T> LoadAssetAsync<T>(string address) where T : Object => await completion.Task as T;
            public void Dispose() => DisposeCount++;
            public void ReleaseAsset(Object asset) { }
            public T LoadAsset<T>(string address) where T : Object => throw new NotSupportedException();
            public GameObject Instantiate(string address, Transform parent) => throw new NotSupportedException();
            public GameObject Instantiate(string address, Transform parent, bool stays) => throw new NotSupportedException();
            public UniTask<GameObject> InstantiateAsync(string address, Transform parent) => throw new NotSupportedException();
            public UniTask<GameObject> InstantiateAsync(string address, Transform parent, bool stays) => throw new NotSupportedException();
            public void ReleaseInstance(GameObject instance) => throw new NotSupportedException();
        }
    }
}
