using Hotfix.DroneFlight;
using NUnit.Framework;

namespace Hotfix.Tests
{
    public sealed class DroneRemoteControlSequenceTests
    {
        [Test]
        public void ActivateAndReturn_UseOnlyWaitingAndActiveStates()
        {
            var session = new DroneControlSession();

            Assert.That(session.State, Is.EqualTo(DroneControlSessionState.Waiting));
            Assert.That(session.Activate(), Is.True);
            Assert.That(session.State, Is.EqualTo(DroneControlSessionState.Active));
            Assert.That(session.Activate(), Is.False);
            Assert.That(session.ReturnToWaiting(), Is.True);
            Assert.That(session.State, Is.EqualTo(DroneControlSessionState.Waiting));
        }
    }
}
