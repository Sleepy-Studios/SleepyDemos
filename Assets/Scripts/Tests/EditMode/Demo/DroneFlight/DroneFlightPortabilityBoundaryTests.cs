using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Tests.Demo
{
    /*
     * 测试说明：验证 DroneFlight 可迁移核心不会反向依赖 SleepyDemos 的 UI、资源和场景导航适配层。
     */
    public sealed class DroneFlightPortabilityBoundaryTests
    {
        private const string ModuleRoot = "Assets/Scripts/Hotfix/Module/DroneFlight";

        private static readonly string[] PortableDirectories =
        {
            "Control", "Physics", "Equipment", "Payload", "Input", "Camera", "Telemetry", "Mission"
        };

        private static readonly string[] PortableVehicleFiles =
        {
            "Vehicle/DroneFlightModelContract.cs",
            "Vehicle/DroneFlightVehicleAssembler.cs",
            "Vehicle/DroneSpawnPlacement.cs"
        };

        private static readonly string[] ForbiddenDependencies =
        {
            "using Core.Runtime", "using Hotfix.SceneManagement", "UIManager.",
            "ResourceServices.", "GameSceneNavigator."
        };

        [Test]
        public void PortableCore_DoesNotReferenceSleepyDemosHostServices()
        {
            foreach (var path in EnumeratePortableFiles())
            {
                var source = File.ReadAllText(Path.GetFullPath(path));
                foreach (var dependency in ForbiddenDependencies)
                {
                    StringAssert.DoesNotContain(dependency, source, $"{path} 不得依赖宿主能力 {dependency}");
                }
            }
        }

        [Test]
        public void HostDependencies_AreConfinedToDocumentedAdapters()
        {
            var allowed = new HashSet<string>
            {
                "Runtime/DroneRemoteControllerExperience.cs",
                "UI/DroneFlightUIController.cs",
                "Vehicle/DroneFlightSceneCoordinator.cs",
                "DroneFlightVehicleSelectView/View/DroneFlightVehicleSelectView.cs",
                "DroneFlightVehicleSelectView/View/DroneFlightVehicleSelectViewComponent.cs",
                "DroneFlightHudView/View/DroneFlightHudView.cs",
                "DroneFlightHudView/View/DroneFlightHudViewComponent.cs",
                "DroneFlightDebugView/View/DroneFlightDebugView.cs",
                "DroneFlightDebugView/View/DroneFlightDebugViewComponent.cs"
            };
            var violations = Directory.GetFiles(Path.GetFullPath(ModuleRoot), "*.cs", SearchOption.AllDirectories)
                .Where(path => ForbiddenDependencies.Any(dependency => File.ReadAllText(path).Contains(dependency)))
                .Select(path => Path.GetRelativePath(Path.GetFullPath(ModuleRoot), path).Replace('\\', '/'))
                .Where(path => !allowed.Contains(path))
                .ToArray();
            Assert.That(violations, Is.Empty);
        }

        private static IEnumerable<string> EnumeratePortableFiles()
        {
            foreach (var directory in PortableDirectories)
            {
                foreach (var file in Directory.GetFiles(
                             Path.GetFullPath($"{ModuleRoot}/{directory}"),
                             "*.cs",
                             SearchOption.AllDirectories))
                {
                    yield return file;
                }
            }

            foreach (var file in PortableVehicleFiles)
            {
                yield return Path.GetFullPath($"{ModuleRoot}/{file}");
            }
        }
    }
}
