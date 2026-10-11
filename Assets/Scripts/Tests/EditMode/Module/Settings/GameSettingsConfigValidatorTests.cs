using System;
using Core.Editor.Settings;
using NUnit.Framework;

namespace Tests.Module
{
    public sealed class GameSettingsConfigValidatorTests
    {
        [TestCase("No arguments {{literal}}", new int[0])]
        [TestCase("{{literal}} {1} {0, -8:N2} {{{0}}}", new[] { 0, 1 })]
        [TestCase("{1} seconds, {0} items, {1} again", new[] { 0, 1 })]
        public void CompositeArgumentsRespectEscapedBracesAndLanguageOrder(string text, int[] expected)
        {
            Assert.That(GameSettingsConfigValidator.GetFormatIndices(text), Is.EqualTo(expected));
        }

        [TestCase("{name}")]
        [TestCase("{0")]
        [TestCase("{0,}")]
        [TestCase("text }")]
        [TestCase("{999999999999999999}")]
        public void MalformedCompositeArgumentsAreReported(string text)
        {
            Assert.Throws<FormatException>(() => GameSettingsConfigValidator.GetFormatIndices(text));
        }
    }
}
