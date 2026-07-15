using ConGame.Domain.Loop;
using NUnit.Framework;

namespace ConGame.Tests.EditMode
{

    public sealed class EntryClockTests
    {
        [Test]
        public void IdlePlayerWarnsAtFortyFourPointFiveAndEntersAtFortyFive()
        {
            EntryClock clock = new EntryClock(EntryClockSettings.Default);

            Assert.That(clock.Advance(44.49f), Is.EqualTo(EntryClockSignal.None));
            Assert.That(clock.Advance(0.01f), Is.EqualTo(EntryClockSignal.Warning));
            Assert.That(clock.Advance(0.5f), Is.EqualTo(EntryClockSignal.Enter));
            Assert.That(clock.EntryIssued, Is.True);
        }

        [Test]
        public void EarlyMeaningfulActionMovesDeadlineToThirty()
        {
            EntryClock clock = new EntryClock(EntryClockSettings.Default);
            clock.Advance(5f);

            Assert.That(clock.MarkMeaningfulAction(), Is.EqualTo(EntryClockSignal.None));
            Assert.That(clock.Deadline, Is.EqualTo(30f));
            Assert.That(clock.Advance(24.5f), Is.EqualTo(EntryClockSignal.Warning));
            Assert.That(clock.Advance(0.5f), Is.EqualTo(EntryClockSignal.Enter));
        }

        [Test]
        public void LateFirstActionWarnsImmediatelyThenEntersHalfSecondLater()
        {
            EntryClock clock = new EntryClock(EntryClockSettings.Default);
            clock.Advance(35f);

            Assert.That(clock.MarkMeaningfulAction(), Is.EqualTo(EntryClockSignal.Warning));
            Assert.That(clock.Deadline, Is.EqualTo(35.5f));
            Assert.That(clock.Advance(0.5f), Is.EqualTo(EntryClockSignal.Enter));
        }

        [Test]
        public void LoudNoiseAdvancesButNeverDelaysDeadline()
        {
            EntryClock clock = new EntryClock(EntryClockSettings.Default);
            clock.Advance(5f);
            clock.MarkLoudNoise();
            Assert.That(clock.Deadline, Is.EqualTo(13f));

            clock.Advance(6f);
            clock.MarkLoudNoise();
            Assert.That(clock.Deadline, Is.EqualTo(13f));
        }
    }
}
