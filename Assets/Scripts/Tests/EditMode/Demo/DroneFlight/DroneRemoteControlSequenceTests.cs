using Hotfix.DroneFlight;
using NUnit.Framework;

namespace Tests.Demo
{
    /*
     * 测试说明：验证遥控体验只在 Waiting 与 Active 两个公开状态间切换，并可可靠返回等待状态。
     */
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
