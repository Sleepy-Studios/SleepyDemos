using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Core.Editor.Hotfix;
using NUnit.Framework;

namespace Tests.Module
{
    public sealed class GitHubPagesDeploymentTests
    {
        [Test]
        public void ClearAsync_UnsafeDirectories_PreserveExistingFiles()
        {
            string testRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "SleepyDemos-PagesGuard-" + Guid.NewGuid().ToString("N")));
            string pcDirectory = Path.Combine(testRoot, "unity", "SleepyDemos", "hotupdate", "PC");
            string androidDirectory = Path.Combine(testRoot, "unity", "SleepyDemos", "hotupdate", "Android");
            string otherProjectDirectory = Path.Combine(testRoot, "unity", "OtherGame", "hotupdate", "PC");
            string[] protectedFiles =
            {
                Path.Combine(pcDirectory, "keep.bundle"),
                Path.Combine(androidDirectory, "keep.bundle"),
                Path.Combine(otherProjectDirectory, "keep.bundle")
            };

            try
            {
                foreach (string file in protectedFiles)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(file));
                    File.WriteAllText(file, "keep");
                }

                var deploymentType = typeof(HotfixBuildWindow).Assembly.GetType("Core.Editor.Hotfix.GitHubPagesDeployment", true);
                MethodInfo clear = deploymentType.GetMethod("ClearAsync", BindingFlags.Static | BindingFlags.NonPublic);
                string[] invalidPaths =
                {
                    "", "/", "unity/SleepyDemos/hotupdate", "unity/../hotupdate/PC",
                    "unity/SleepyDemos/hotupdate/../Android", "unity/SleepyDemos/hotupdate/PC/.."
                };

                foreach (string invalidPath in invalidPaths)
                {
                    Assert.ThrowsAsync<InvalidOperationException>(() => (Task<string>)clear.Invoke(null, new object[] { testRoot, invalidPath }));
                }

                string nestedDirectory = Path.Combine(pcDirectory, "unexpected");
                Directory.CreateDirectory(nestedDirectory);
                string nestedFile = Path.Combine(nestedDirectory, "keep.bundle");
                File.WriteAllText(nestedFile, "keep");
                Assert.ThrowsAsync<InvalidOperationException>(() => (Task<string>)clear.Invoke(null, new object[] { testRoot, "unity/SleepyDemos/hotupdate/PC" }));
                Assert.That(File.ReadAllText(nestedFile), Is.EqualTo("keep"));
                foreach (string file in protectedFiles)
                {
                    Assert.That(File.ReadAllText(file), Is.EqualTo("keep"));
                }
            }
            finally
            {
                if (Directory.Exists(testRoot))
                {
                    Directory.Delete(testRoot, true);
                }
            }
        }
    }
}
