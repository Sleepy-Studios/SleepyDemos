using Core.Editor.Hotfix;
using NUnit.Framework;

namespace Core.Tests.Hotfix
{
    public sealed class HotfixAssemblyDefinitionFilterTests
    {
        [Test]
        public void TryGetDllNameFromJson_RuntimeAssembly_ReturnsDllName()
        {
            const string json = "{\"name\":\"Hotfix\",\"references\":[\"Core.Runtime\"]}";

            var accepted = HotfixAssemblyDefinitionFilter.TryGetDllNameFromJson(json, out var dllName);

            Assert.That(accepted, Is.True);
            Assert.That(dllName, Is.EqualTo("Hotfix.dll"));
        }

        [Test]
        public void TryGetDllNameFromFile_HotfixAssembly_ReturnsDllName()
        {
            var accepted = HotfixAssemblyDefinitionFilter.TryGetDllNameFromFile(
                "Assets/Scripts/Hotfix/Hotfix.asmdef",
                out var dllName);

            Assert.That(accepted, Is.True);
            Assert.That(dllName, Is.EqualTo("Hotfix.dll"));
        }

        [Test]
        public void TryGetDllNameFromJson_TestAssemblyReference_ReturnsFalse()
        {
            const string json =
                "{\"name\":\"Core.Tests\",\"optionalUnityReferences\":[\"TestAssemblies\"]}";

            AssertRejected(json);
        }

        [Test]
        public void TryGetDllNameFromJson_TestsSuffix_ReturnsFalse()
        {
            AssertRejected("{\"name\":\"Hotfix.Tests\"}");
        }

        [Test]
        public void TryGetDllNameFromJson_EditorOnlyAssembly_ReturnsFalse()
        {
            const string json = "{\"name\":\"Tools\",\"includePlatforms\":[\"Editor\"]}";

            AssertRejected(json);
        }

        [TestCase("not json")]
        [TestCase("{}")]
        [TestCase("{\"name\":\"\"}")]
        public void TryGetDllNameFromJson_InvalidOrMissingName_ReturnsFalse(string json)
        {
            AssertRejected(json);
        }

        private static void AssertRejected(string json)
        {
            var accepted = HotfixAssemblyDefinitionFilter.TryGetDllNameFromJson(json, out var dllName);

            Assert.That(accepted, Is.False);
            Assert.That(dllName, Is.Empty);
        }
    }
}
