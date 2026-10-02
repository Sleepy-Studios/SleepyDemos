using Hotfix.JinxCasino.Presentation;
using NUnit.Framework;

namespace Tests.Demo
{
    /// 暂停与恢复的时间语义，防止后台长dt使效果提前结束或同帧重复位移。
    public sealed class JinxCasinoPresentationClockTests
    {
        [Test]
        public void PausedTimeAndFirstResumeFrameNeverConsumeWallClock()
        {
            var clock = new JinxCasinoPresentationClock();
            clock.Advance(30, 1);
            Assert.That(clock.TimeSeconds, Is.Zero, "初始首帧不消费初始化耗时。");
            clock.Advance(.2f, 2);
            clock.SetPaused(true); clock.Advance(60, 3);
            Assert.That(clock.TimeSeconds, Is.EqualTo(.2f));
            Assert.That(clock.GetDeltaSeconds(3), Is.Zero);
            clock.SetPaused(false); clock.Advance(60, 4);
            Assert.That(clock.TimeSeconds, Is.EqualTo(.2f), "恢复首帧不能补算后台60秒。");
            clock.Advance(.1f, 5);
            Assert.That(clock.TimeSeconds, Is.EqualTo(.3f).Within(.00001f));
        }

        [Test]
        public void ComponentsBeforeHostCannotConsumePreviousFrameAndPauseIsImmediate()
        {
            bool paused = false;
            var clock = new JinxCasinoPresentationClock(); clock.BindPauseSource(() => paused);
            clock.Advance(0, 1); clock.Advance(.1f, 2);
            Assert.That(clock.GetDeltaSeconds(3), Is.Zero, "Update先于宿主不重复使用上一帧dt。");
            paused = true;
            Assert.That(clock.IsPaused, Is.True);
            Assert.That(clock.GetDeltaSeconds(2), Is.Zero, "当帧已经tick也不能在暂停后继续位移。");
            paused = false; clock.Advance(20, 3);
            Assert.That(clock.TimeSeconds, Is.EqualTo(.1f));
        }

        [Test]
        public void DuplicateTickAndRepeatedPausedSignalsDoNotRestartTime()
        {
            var clock = new JinxCasinoPresentationClock();
            clock.Advance(0, 1); clock.Advance(.1f, 2); clock.Advance(1, 2);
            Assert.That(clock.TimeSeconds, Is.EqualTo(.1f));
            clock.SetPaused(false); clock.Advance(.2f, 3);
            Assert.That(clock.TimeSeconds, Is.EqualTo(.3f).Within(.00001f));
            Assert.That(clock.GetDeltaSeconds(3), Is.EqualTo(.2f));
            Assert.That(clock.GetDeltaSeconds(4), Is.Zero);
        }
    }
}
