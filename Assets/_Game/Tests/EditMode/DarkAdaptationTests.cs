using ConGame.Domain.Loop;
using NUnit.Framework;

namespace ConGame.Tests.EditMode
{

    public sealed class DarkAdaptationTests
    {
        [Test]
        public void AwardsOneExperiencePerFiveContinuousBlackSeconds()
        {
            DarkAdaptationTracker tracker = new DarkAdaptationTracker();

            Assert.That(tracker.Advance(true, 4.9f).ExperienceGained, Is.Zero);
            Assert.That(tracker.Advance(true, 0.1f).ExperienceGained, Is.EqualTo(1));
            Assert.That(tracker.Experience, Is.EqualTo(1));
        }

        [Test]
        public void LeavingPersistentBlackClearsPartialProgress()
        {
            DarkAdaptationTracker tracker = new DarkAdaptationTracker();
            tracker.Advance(true, 4f);
            tracker.Advance(false, 0.1f);

            Assert.That(tracker.PartialSeconds, Is.Zero);
            Assert.That(tracker.Advance(true, 1f).ExperienceGained, Is.Zero);
        }

        [Test]
        public void ExperienceClampsAtFive()
        {
            DarkAdaptationTracker tracker = new DarkAdaptationTracker(4);
            DarkAdaptationResult result = tracker.Advance(true, 20f);

            Assert.That(result.ExperienceGained, Is.EqualTo(1));
            Assert.That(tracker.Experience, Is.EqualTo(5));
            Assert.That(tracker.PartialSeconds, Is.Zero);
        }
    }
}
