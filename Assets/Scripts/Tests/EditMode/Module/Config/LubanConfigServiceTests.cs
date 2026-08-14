using System;
using System.Threading.Tasks;
using Cfg;
using Core.Runtime;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.Module
{
    public sealed class LubanConfigServiceTests
    {
        private const string ExampleAssetPath = "Assets/LoadResources/Config/Luban/example_info.bytes";

        private IResourceService originalResourceService;

        [SetUp]
        public void SetUp()
        {
            originalResourceService = ResourceServices.Default;
            LubanConfigService.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            LubanConfigService.ResetForTests();
            ResourceServices.RegisterDefault(originalResourceService);
        }

        [Test]
        public async Task InitializeAsync_LoadsExampleBytesAndIsIdempotent()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(ExampleAssetPath);
            Assert.IsNotNull(asset, $"测试 bytes 未导入：{ExampleAssetPath}");
            var service = new FakeResourceService(asset);
            ResourceServices.RegisterDefault(service);

            await LubanConfigService.InitializeAsync();
            await LubanConfigService.InitializeAsync();

            Assert.IsTrue(Tables.IsInitialized);
            Assert.AreEqual(1, Tables.ExampleInfo.Get(1).Id);
            Assert.AreEqual("示例配置", Tables.ExampleInfo.Get(1).Name);
            Assert.AreEqual(2, Tables.ExampleInfo.DataList.Count);
            Assert.IsNull(Tables.ExampleInfo.GetOrDefault(999));
            Assert.AreEqual(1, service.LoadCount, "重复初始化不应重复加载同一张表。");
        }

        [Test]
        public void TablesAccess_BeforeInitializationThrowsClearError()
        {
            var exception = Assert.Throws<InvalidOperationException>(() => _ = Tables.ExampleInfo);

            StringAssert.Contains("尚未初始化", exception.Message);
            StringAssert.Contains(LubanConfigService.ResourceBaseAddress, exception.Message);
        }

        [Test]
        public void InitializeAsync_MissingAssetReportsTableAndAddress()
        {
            ResourceServices.RegisterDefault(new FakeResourceService(null));

            var exception = Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await LubanConfigService.InitializeAsync().AsTask());

            StringAssert.Contains("example_info", exception.Message);
            StringAssert.Contains(
                "LoadResources/Config/Luban/example_info",
                exception.Message);
            Assert.IsFalse(Tables.IsInitialized);
        }

        private sealed class FakeResourceService : IResourceService
        {
            private readonly TextAsset textAsset;

            public FakeResourceService(TextAsset textAsset)
            {
                this.textAsset = textAsset;
            }

            public int LoadCount { get; private set; }
            public bool IsInitialized => true;

            public string NormalizeAddress(string address)
            {
                return address?.Replace('\\', '/');
            }

            public UniTask InitializeAsync(ResourceInitializeOptions options)
            {
                return UniTask.CompletedTask;
            }

            public UniTask<DownloadReport> DownloadPackageAsync(
                int downloadingMaxNum,
                int failedTryAgain,
                Action<DownloadProgress> onProgress = null)
            {
                return UniTask.FromResult(new DownloadReport(true, 0, 0, string.Empty));
            }

            public IResourceLoader CreateLoader()
            {
                return new FakeResourceLoader(this, textAsset);
            }

            public IResourceSceneLoader CreateSceneLoader()
            {
                throw new NotSupportedException("当前配置测试不使用场景资源加载器。");
            }

            public ResourceLoadResult<T> LoadAsset<T>(string address) where T : Object
            {
                return ResourceLoadResult<T>.Failure(address, "测试未实现同步加载。");
            }

            public UniTask<ResourceLoadResult<T>> LoadAssetAsync<T>(string address) where T : Object
            {
                return UniTask.FromResult(ResourceLoadResult<T>.Failure(address, "测试未实现服务异步加载。"));
            }

            public UniTask<ResourceLoadResult<TextAsset>> LoadTextAssetAsync(string address)
            {
                var result = textAsset != null
                    ? ResourceLoadResult<TextAsset>.SuccessResult(textAsset, address)
                    : ResourceLoadResult<TextAsset>.Failure(address, "测试资源不存在。");
                return UniTask.FromResult(result);
            }

            public void ReleaseAsset(Object asset)
            {
            }

            public void IncrementLoadCount()
            {
                LoadCount++;
            }
        }

        private sealed class FakeResourceLoader : IResourceLoader
        {
            private readonly FakeResourceService service;
            private readonly TextAsset textAsset;

            public FakeResourceLoader(FakeResourceService service, TextAsset textAsset)
            {
                this.service = service;
                this.textAsset = textAsset;
            }

            public GameObject Instantiate(string address, Transform parent)
            {
                return null;
            }

            public GameObject Instantiate(string address, Transform parent, bool worldPositionStays)
            {
                return null;
            }

            public UniTask<GameObject> InstantiateAsync(string address, Transform parent)
            {
                return UniTask.FromResult<GameObject>(null);
            }

            public UniTask<GameObject> InstantiateAsync(
                string address,
                Transform parent,
                bool worldPositionStays)
            {
                return UniTask.FromResult<GameObject>(null);
            }

            public T LoadAsset<T>(string address) where T : Object
            {
                return null;
            }

            public UniTask<T> LoadAssetAsync<T>(string address) where T : Object
            {
                service.IncrementLoadCount();
                return UniTask.FromResult(textAsset as T);
            }

            public void ReleaseAsset(Object asset)
            {
            }

            public void ReleaseInstance(GameObject instance)
            {
            }

            public void Dispose()
            {
            }
        }
    }
}
