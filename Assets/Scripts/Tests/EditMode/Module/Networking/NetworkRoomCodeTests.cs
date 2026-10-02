using Core.Runtime.Networking;
using NUnit.Framework;

namespace Tests.Module
{
    public sealed class NetworkRoomCodeTests
    {
        [Test]
        public void CanonicalCodePreservesProtocolRegionAndToken()
        {
            var expected = NetworkRoomCodeCodec.Encode(12, "ASIA", "abcdefgh");
            Assert.That(NetworkRoomCodeCodec.TryParse("  v12-ASIA-abcdefgh  ", out var parsed), Is.True);
            Assert.That(parsed, Is.EqualTo(expected));
            Assert.That(parsed.ProtocolVersion, Is.EqualTo(12));
            Assert.That(parsed.Region, Is.EqualTo("asia"));
            Assert.That(parsed.SessionName, Is.EqualTo("V12-asia-ABCDEFGH"));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("V0-asia-ABCDEFGH")]
        [TestCase("V01-asia-ABCDEFGH")]
        [TestCase("V65536-asia-ABCDEFGH")]
        [TestCase("V+1-asia-ABCDEFGH")]
        [TestCase("V1-a-ABCDEFGH")]
        [TestCase("V1-1asia-ABCDEFGH")]
        [TestCase("V1-中文-ABCDEFGH")]
        [TestCase("V1-asia-ABCDEFGI")]
        [TestCase("V1-asia-ABCDEF01")]
        [TestCase("V1-asia-ABCDE")]
        [TestCase("V1-asia-ABCDEFGH-android")]
        public void InvalidCodesNeverProduceValidValue(string value)
        {
            Assert.That(NetworkRoomCodeCodec.TryParse(value, out var code), Is.False);
            Assert.That(code.IsValid, Is.False);
        }

        [Test]
        public void GeneratedCodeRoundTripsWithoutPlatformIdentity()
        {
            var generated = NetworkRoomCodeCodec.Generate(1, "eu");
            Assert.That(generated.Token.Length, Is.EqualTo(8));
            Assert.That(NetworkRoomCodeCodec.TryParse(generated.ToString(), out var parsed), Is.True);
            Assert.That(parsed, Is.EqualTo(generated));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => NetworkRoomCodeCodec.Encode(0, "eu", "ABCDEFGH"));
        }
    }
}
