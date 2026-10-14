using System;
using System.Collections.Generic;
using Core.Runtime;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Module
{
    public sealed class ResourceServiceTests
    {
        [Test]
        public void Default_ReturnsResourceService()
        {
            Assert.That(ResourceServices.Default, Is.Not.Null);
        }

        [Test]
        public void CreateLoader_ReturnsDisposableResourceLoader()
        {
            var loader = ResourceServices.Default.CreateLoader();
            try
            {
                Assert.That(loader, Is.InstanceOf<IResourceLoader>());
            }
            finally
            {
                loader?.Dispose();
            }
        }

        [Test]
        public void CreateSceneLoader_ReturnsResourceSceneLoader()
        {
            var loader = ResourceServices.CreateSceneLoader();

            Assert.That(loader, Is.InstanceOf<IResourceSceneLoader>());
        }

        [Test]
        public void NormalizeAddress_ReplacesBackslashes()
        {
            var normalized = ResourceServices.Default.NormalizeAddress("LoadResources/UI\\Views\\SampleView");

            Assert.That(normalized, Is.EqualTo("LoadResources/UI/Views/SampleView"));
        }

        [Test]
        public void DownloadFailure_StopsStartupBeforeHotfix()
        {
            IResourceService previous = ResourceServices.Default;
            var resourceState = new ResourceStartupState();
            var nextState = new AfterDownloadState();
            var machine = new StartupStateMachine(new StartupContext(null, null, null), 2);
            machine.AddState(resourceState);
            machine.AddState(nextState);
            try
            {
                ResourceServices.RegisterDefault(new FailedDownloadService(previous));
                var error = Assert.ThrowsAsync<InvalidOperationException>(() =>
                    machine.RunAsync((int)StartupStateId.Resource).AsTask());
                Assert.That(error.Message, Does.Contain("test-download-failed"));
                Assert.That(machine.CurrentState, Is.SameAs(resourceState));
                Assert.That(resourceState.Description, Does.Contain("test-download-failed"));
                Assert.That(resourceState.Progress, Is.LessThan(1f));
                Assert.That(nextState.Entered, Is.False);
            }
            finally
            {
                ResourceServices.RegisterDefault(previous);
            }
        }

        private sealed class AfterDownloadState : StartupStateBase
        {
            internal bool Entered { get; private set; }
            public override int StateId => (int)StartupStateId.BeforeHotfix;
            public override int NextStateId => -1;
            public override string Title => "测试后续阶段";

            protected override void CreateSystems(List<StartupSystemBase> systems)
            {
                Entered = true;
            }
        }

        private sealed class FailedDownloadService : IResourceService
        {
            private readonly IResourceService inner;

            internal FailedDownloadService(IResourceService inner)
            {
                this.inner = inner;
            }

            bool IResourceService.IsInitialized => true;
            string IResourceService.NormalizeAddress(string address) => inner.NormalizeAddress(address);
            UniTask IResourceService.InitializeAsync(ResourceInitializeOptions options) => UniTask.CompletedTask;
            UniTask<DownloadReport> IResourceService.DownloadPackageAsync(int maximum, int retry, Action<DownloadProgress> progress)
                => UniTask.FromResult(new DownloadReport(false, 1, 1, "test-download-failed"));
            IResourceLoader IResourceService.CreateLoader() => inner.CreateLoader();
            IResourceSceneLoader IResourceService.CreateSceneLoader() => inner.CreateSceneLoader();
            ResourceLoadResult<T> IResourceService.LoadAsset<T>(string address) => inner.LoadAsset<T>(address);
            UniTask<ResourceLoadResult<T>> IResourceService.LoadAssetAsync<T>(string address) => inner.LoadAssetAsync<T>(address);
            UniTask<ResourceLoadResult<TextAsset>> IResourceService.LoadTextAssetAsync(string address) => inner.LoadTextAssetAsync(address);
            void IResourceService.ReleaseAsset(UnityEngine.Object asset) => inner.ReleaseAsset(asset);
        }
    }
}
