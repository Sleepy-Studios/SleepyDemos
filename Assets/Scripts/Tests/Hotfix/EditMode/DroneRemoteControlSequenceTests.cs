using Hotfix.DroneFlight;
using NUnit.Framework;

namespace Hotfix.Tests
{
    public sealed class DroneRemoteControlSequenceTests
    {
        [Test]
        public void EnterPreviewExpandAndExit_FollowsExplicitStateOrder()
        {
            var sequence = new DroneRemoteControlSequence();

            sequence.BeginEnter();
            Assert.That(sequence.State, Is.EqualTo(DroneRemoteControlState.PickingUp));
            sequence.Step(0.7f);
            Assert.That(sequence.State, Is.EqualTo(DroneRemoteControlState.PoweringOn));
            sequence.Step(0.5f);
            Assert.That(sequence.State, Is.EqualTo(DroneRemoteControlState.Connecting));
            sequence.Step(0.8f);
            Assert.That(sequence.State, Is.EqualTo(DroneRemoteControlState.Preview));

            sequence.ExpandToFullscreen();
            sequence.Step(0.45f);
            Assert.That(sequence.State, Is.EqualTo(DroneRemoteControlState.Fullscreen));

            sequence.BeginExit();
            sequence.Step(0.5f);
            Assert.That(sequence.State, Is.EqualTo(DroneRemoteControlState.GroundIdle));
        }

        [Test]
        public void InvalidDeltaTime_DoesNotAdvanceOrCorruptState()
        {
            var sequence = new DroneRemoteControlSequence();
            sequence.BeginEnter();

            sequence.Step(float.NaN);
            sequence.Step(float.PositiveInfinity);
            sequence.Step(0f);

            Assert.That(sequence.State, Is.EqualTo(DroneRemoteControlState.PickingUp));
            Assert.That(sequence.NormalizedProgress, Is.Zero);
        }
    }
}
