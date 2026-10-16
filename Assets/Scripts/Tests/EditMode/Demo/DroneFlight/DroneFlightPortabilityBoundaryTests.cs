using System.Linq;
using Hotfix.DroneFlight;
using NUnit.Framework;

namespace Tests.Demo
{
    /*
     * 测试说明：无人机独立发布，不依赖其它 Demo 的程序集。
     */
    public sealed class DroneFlightPortabilityBoundaryTests
    {
        [Test]
        public void DroneFlightAssembly_DoesNotReferenceOtherDemos()
        {
            var assembly = typeof(DroneFlightData).Assembly;
            Assert.That(assembly.GetName().Name, Is.EqualTo("Hotfix.DroneFlight"));
            Assert.That(assembly.GetReferencedAssemblies().Select(item => item.Name),
                Does.Not.Contain("Hotfix.KagitKart").And.Not.Contain("Hotfix.WallSqueeze"));
        }
    }
}
