using ConGame.Domain.Counterplay;
using NUnit.Framework;

namespace ConGame.Tests.EditMode
{
    public sealed class QteSequenceCounterTests
    {
        [Test]
        public void HeldButtonCountsOnlyOneNewPress()
        {
            QteSequenceCounter counter = CreateCounter(1f, 2);

            QteSequenceSnapshot snapshot = counter.Advance(0.2f, true);
            snapshot = counter.Advance(0.2f, true);

            Assert.That(snapshot.CurrentPressCount, Is.EqualTo(1));
            counter.Advance(0f, false);
            snapshot = counter.Advance(0.2f, true);
            Assert.That(snapshot.CurrentPressCount, Is.EqualTo(2));
        }

        [Test]
        public void AccessibilityScaleLowersMechanicalThreshold()
        {
            QteSequenceCounter counter = new QteSequenceCounter(
                new[] { new QteWindowDefinition(1f, 4) },
                0.5f);

            counter.Advance(0.1f, true);
            counter.Advance(0f, false);
            counter.Advance(0.1f, true);
            QteSequenceSnapshot result = counter.Advance(0.8f, false);

            Assert.That(result.IsComplete, Is.True);
            Assert.That(result.AllWindowsSucceeded, Is.True);
            Assert.That(counter.Results[0].RequiredPresses, Is.EqualTo(2));
        }

        [Test]
        public void ThreeSuccessfulWindowsStillHaveScriptedFailureNarrative()
        {
            QteSequenceCounter counter = new QteSequenceCounter(new[]
            {
                new QteWindowDefinition(0.1f, 1),
                new QteWindowDefinition(0.1f, 1),
                new QteWindowDefinition(0.1f, 1)
            });

            for (int i = 0; i < 3; i++)
            {
                counter.Advance(0f, false);
                counter.Advance(0.1f, true);
            }

            Assert.That(counter.IsComplete, Is.True);
            Assert.That(counter.AllWindowsSucceeded, Is.True);
            Assert.That(counter.NarrativeOutcome, Is.EqualTo(CounterplayNarrativeOutcome.ScriptedFailure));
        }

        private static QteSequenceCounter CreateCounter(float duration, int presses)
        {
            return new QteSequenceCounter(new[] { new QteWindowDefinition(duration, presses) });
        }
    }
}
