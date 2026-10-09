using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Tests.Demo
{
    /*
     * 测试说明：验证 DroneFlight 继续编入 Hotfix.dll，同时以源码目录和接口形成可迁移边界。
     */
    public sealed class DroneFlightPortabilityBoundaryTests
    {
        private const string ModuleRoot = "Assets/Scripts/Hotfix/Demos/DroneFlight";
        private static readonly string[] PresentationDirectories = { "Scene", "UI", "Fishing", "Experience" };

        private static readonly string[] CoreDirectories =
        {
            "Control", "Physics", "Equipment", "Payload", "Input", "Camera",
            "Telemetry", "Vehicle", "Cruise", "Runtime"
        };

        private static readonly string[] ForbiddenHostDependencies =
        {
            // 公共输入按当前项目约定依赖 Core.Runtime.Inputs；禁止的是 UI、资源与宿主导航接入。
            "using Core.Runtime;", "using Hotfix.SceneManagement", "using Cysharp.Threading.Tasks",
            "using UnityEngine.UI", "UIManager", "UIRootManager", "ResourceServices",
            "GameSceneNavigator", "DemoIslandEditorBootstrap", "GameSceneId.Hub", "ComponentItemIndex"
        };

        private static readonly string[] PresentationConcreteTypes =
        {
            "DroneRemoteControllerExperience", "DroneFlightSceneCoordinator",
            "DroneFlightDemoExit", "DroneFishingMissionCoordinator", "DroneBezierMissionPath",
            "DroneCinematicCameraTracker", "DroneFlightViewData", "DroneFlightHudView",
            "DroneFlightDebugView", "DroneFlightVehicleSelectView"
        };

        [Test]
        public void CoreDirectories_DoNotReferenceSceneOrPresentationServices()
        {
            foreach (var path in EnumerateCoreFiles())
            {
                var source = File.ReadAllText(path);
                foreach (var dependency in ForbiddenHostDependencies.Concat(PresentationConcreteTypes))
                {
                    StringAssert.DoesNotContain(
                        dependency,
                        source,
                        $"{Path.GetRelativePath(Path.GetFullPath(ModuleRoot), path)} 不得依赖 {dependency}");
                }
            }
        }

        [Test]
        public void HostDependencies_AreConfinedToSceneAndPresentation()
        {
            var modulePath = Path.GetFullPath(ModuleRoot);
            var presentationPaths = PresentationDirectories
                .Select(directory => Path.GetFullPath(ModuleRoot + "/" + directory) + Path.DirectorySeparatorChar)
                .ToArray();
            var violations = Directory.GetFiles(modulePath, "*.cs", SearchOption.AllDirectories)
                .Where(path => ForbiddenHostDependencies.Any(dependency => File.ReadAllText(path).Contains(dependency)))
                .Where(path => !presentationPaths.Any(directory =>
                    path.StartsWith(directory, System.StringComparison.OrdinalIgnoreCase)))
                .Select(path => Path.GetRelativePath(modulePath, path).Replace('\\', '/'))
                .ToArray();

            Assert.That(violations, Is.Empty);
        }

        [Test]
        public void DroneFlightAsmReference_StillTargetsHotfixAssembly()
        {
            var hotfixMeta = File.ReadAllLines(Path.GetFullPath("Assets/Scripts/Hotfix/Hotfix.asmdef.meta"));
            var hotfixGuid = hotfixMeta.Single(line => line.StartsWith("guid: ")).Substring("guid: ".Length);
            var asmref = File.ReadAllText(Path.GetFullPath(ModuleRoot + "/DroneFlight.asmref"));

            StringAssert.Contains($"GUID:{hotfixGuid}", asmref);
            Assert.That(Directory.GetFiles(Path.GetFullPath(ModuleRoot), "*.asmdef", SearchOption.AllDirectories),
                Is.Empty,
                "DroneFlight 不得新增独立运行时或编辑器程序集。");
        }

        private static string[] EnumerateCoreFiles()
        {
            return CoreDirectories
                .SelectMany(directory => Directory.GetFiles(
                    Path.GetFullPath($"{ModuleRoot}/{directory}"),
                    "*.cs",
                    SearchOption.AllDirectories))
                .ToArray();
        }
    }
}
