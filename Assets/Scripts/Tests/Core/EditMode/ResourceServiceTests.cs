using Core.Runtime;
using NUnit.Framework;

namespace Core.Tests.Resource
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
        public void NormalizeAddress_ReplacesBackslashes()
        {
            var normalized = ResourceServices.Default.NormalizeAddress("LoadResources/UI\\Views\\TestView");

            Assert.That(normalized, Is.EqualTo("LoadResources/UI/Views/TestView"));
        }
    }
}
