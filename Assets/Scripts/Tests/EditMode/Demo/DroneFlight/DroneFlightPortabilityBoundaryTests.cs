using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Tests.Demo
{
    /*
     * 测试说明：验证 DroneFlight 继续编入 Hotfix.dll，不引入独立 Demo 程序集。
     */
    public sealed class DroneFlightPortabilityBoundaryTests
    {
        private const string ModuleRoot = "Assets/Scripts/Hotfix/Demos/DroneFlight";

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
    }
}
