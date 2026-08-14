using System;
using System.IO;
using System.Linq;
using Core.Editor.Config;
using NUnit.Framework;

namespace Tests.Module
{
    public sealed class LubanTemplateClassGeneratorTests
    {
        private string temporaryDirectory;

        [SetUp]
        public void SetUp()
        {
            temporaryDirectory = Path.Combine(
                Path.GetTempPath(),
                "SleepyDemosLubanGeneratorTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(temporaryDirectory))
            {
                Directory.Delete(temporaryDirectory, true);
            }
        }

        [Test]
        public void DiscoverTables_FindsPropertiesAndSortsDeterministically()
        {
            var managerPath = WriteManager(@"
namespace Cfg
{
    public class GeneratedTables
    {
        public Zebra Zebra {get; }
        public ExampleInfo ExampleInfo {get; }
        public GeneratedTables(System.Func<string, object> loader)
        {
            Zebra = new Zebra(loader(""zebra""));
            ExampleInfo = new ExampleInfo(loader(""example_info""));
        }
    }
}");

            var tables = LubanTemplateClassGenerator.DiscoverTables(managerPath);

            CollectionAssert.AreEqual(
                new[] { "ExampleInfo", "Zebra" },
                tables.Select(table => table.PropertyName).ToArray());
            CollectionAssert.AreEqual(
                new[] { "example_info", "zebra" },
                tables.Select(table => table.DataFileName).ToArray());
        }

        [Test]
        public void GenerateSource_IsStableAndExposesExampleInfo()
        {
            var tables = new[]
            {
                new LubanTableDescriptor("Zebra", "Zebra", "zebra"),
                new LubanTableDescriptor("ExampleInfo", "ExampleInfo", "example_info")
            };

            var first = LubanTemplateClassGenerator.GenerateSource(tables);
            var second = LubanTemplateClassGenerator.GenerateSource(tables.Reverse().ToArray());

            Assert.AreEqual(first, second);
            StringAssert.Contains("public static ExampleInfo ExampleInfo => Instance.ExampleInfo;", first);
            Assert.Less(first.IndexOf("ExampleInfo", StringComparison.Ordinal),
                first.IndexOf("Zebra", StringComparison.Ordinal));
        }

        [Test]
        public void DiscoverTables_EmptyManagerThrowsClearError()
        {
            var managerPath = WriteManager("namespace Cfg { public class GeneratedTables { } }");

            var exception = Assert.Throws<InvalidDataException>(
                () => LubanTemplateClassGenerator.DiscoverTables(managerPath));

            StringAssert.Contains("未发现任何表", exception.Message);
        }

        [Test]
        public void DiscoverTables_PropertyWithoutLoaderThrowsClearError()
        {
            var managerPath = WriteManager(
                "namespace Cfg { public class GeneratedTables { public ExampleInfo ExampleInfo {get; } } }");

            var exception = Assert.Throws<InvalidDataException>(
                () => LubanTemplateClassGenerator.DiscoverTables(managerPath));

            StringAssert.Contains("缺少对应 loader", exception.Message);
        }

        private string WriteManager(string content)
        {
            var path = Path.Combine(temporaryDirectory, "GeneratedTables.cs");
            File.WriteAllText(path, content);
            return path;
        }
    }
}
