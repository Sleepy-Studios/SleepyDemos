using System.Linq;
using Core.Editor.AssetNaming;
using NUnit.Framework;

namespace Core.Tests
{
    public sealed class LubanAssetNamingTests
    {
        [Test]
        public void GeneratedBytes_AllowsLubanFileNameAndKeepsConfigLabel()
        {
            const string path = "Assets/LoadResources/Config/Luban/example_info.bytes";

            var issues = LoadResourcesAssetNamingRules.Validate(path);
            var labels = LoadResourcesAssetNamingRules.GetLabels(path);

            Assert.IsEmpty(issues);
            CollectionAssert.Contains(labels.ToArray(), "config");
        }

        [Test]
        public void LubanFolder_RejectsUnexpectedExtension()
        {
            const string path = "Assets/LoadResources/Config/Luban/example_info.asset";

            var issues = LoadResourcesAssetNamingRules.Validate(path);

            Assert.IsTrue(issues.Any(issue =>
                issue.Severity == NamingSeverity.Error && issue.Message.Contains("扩展名")));
        }

        [Test]
        public void ConfigOutsideLuban_StillWarnsForNonPascalName()
        {
            const string path = "Assets/LoadResources/Config/example_info.bytes";

            var issues = LoadResourcesAssetNamingRules.Validate(path);

            Assert.IsTrue(issues.Any(issue => issue.Severity == NamingSeverity.Warning));
        }
    }
}
