using System.Collections.Generic;
using System.Linq;
using ConGame.Domain.Choreography;
using NUnit.Framework;

namespace ConGame.Tests.EditMode
{
    public sealed class PairedPerformanceD21TimingTests
    {
        [Test]
        public void D21FallMustFinishBeforeLootDropSemanticEvents()
        {
            PairedPerformancePhaseMachine machine =
                new PairedPerformancePhaseMachine(PairedPerformanceCatalog.CreateD21());
            List<PairedPerformanceSemanticEvent> events =
                new List<PairedPerformanceSemanticEvent>();

            machine.Start(7, events);
            Assert.That(HasEvent(events, "D21.Fall.Begin"), Is.True);
            Assert.That(HasEvent(events, "D21.Pistol.Dropped"), Is.False);

            machine.Advance(1.19f, false, 7, events);
            Assert.That(HasEvent(events, "D21.Pistol.Dropped"), Is.False);

            machine.Advance(0.02f, false, 7, events);
            int impact = EventIndex(events, "D21.Fall.Impact");
            int pistolDrop = EventIndex(events, "D21.Pistol.Dropped");
            int flashlightDrop = EventIndex(events, "D21.Flashlight.Dropped");
            Assert.That(impact, Is.GreaterThanOrEqualTo(0));
            Assert.That(pistolDrop, Is.GreaterThan(impact));
            Assert.That(flashlightDrop, Is.GreaterThan(impact));
            Assert.That(machine.CurrentStageId, Is.EqualTo("item-drop"));
        }

        [Test]
        public void D21GameplayCancelIsRefusedDuringFallButLoopResetCleansUp()
        {
            PairedPerformancePhaseMachine machine =
                new PairedPerformancePhaseMachine(PairedPerformanceCatalog.CreateD21());
            List<PairedPerformanceSemanticEvent> events =
                new List<PairedPerformanceSemanticEvent>();
            machine.Start(3, events);

            Assert.That(
                machine.Cancel(PairedCancelReason.GameplayInterrupt, 3, events),
                Is.EqualTo(PairedCleanupAction.None));
            Assert.That(machine.IsRunning, Is.True);
            Assert.That(HasEvent(events, "D21.Pistol.Dropped"), Is.False);

            Assert.That(
                machine.Cancel(PairedCancelReason.LoopReset, 3, events),
                Is.EqualTo(PairedCleanupAction.All));
            Assert.That(machine.IsCancelled, Is.True);
            Assert.That(HasEvent(events, "D21.Cancelled"), Is.True);
            Assert.That(HasEvent(events, "D21.Pistol.Dropped"), Is.False);
        }

        [Test]
        public void StaleLoopEpochCannotAdvanceOrEmitD21Events()
        {
            PairedPerformancePhaseMachine machine =
                new PairedPerformancePhaseMachine(PairedPerformanceCatalog.CreateD21());
            List<PairedPerformanceSemanticEvent> events =
                new List<PairedPerformanceSemanticEvent>();
            machine.Start(11, events);
            events.Clear();

            machine.Advance(2f, false, 10, events);
            Assert.That(events, Is.Empty);
            Assert.That(machine.CurrentStageId, Is.EqualTo("fall"));

            machine.Advance(1.2f, false, 11, events);
            Assert.That(HasEvent(events, "D21.Pistol.Dropped"), Is.True);
            Assert.That(machine.CurrentStageId, Is.EqualTo("item-drop"));
        }

        [Test]
        public void D21FlashlightPickupEnablesGrappleQteThenWaitsForSecondIlluminationChoice()
        {
            const int epoch = 14;
            PairedPerformancePhaseMachine machine =
                new PairedPerformancePhaseMachine(PairedPerformanceCatalog.CreateD21());
            List<PairedPerformanceSemanticEvent> events =
                new List<PairedPerformanceSemanticEvent>();

            machine.Start(epoch, events);
            machine.Advance(1.2f, false, epoch, events);
            Assert.That(machine.CurrentStageId, Is.EqualTo("item-drop"));

            // Production pauses here for the player's explicit loot choice.
            machine.SetPaused(true);
            Assert.That(machine.TryJumpToStage("dark-grapple", epoch, events), Is.True);
            Assert.That(machine.IsPaused, Is.False);
            Assert.That(machine.CurrentStageId, Is.EqualTo("dark-grapple"));
            Assert.That(HasEvent(events, "D21.Pair.Begin"), Is.True);

            events.Clear();
            // The three authored QTE windows consume the live Interact pulses
            // while the paired grapple stage is active.
            for (int i = 0; i < 12; i++)
            {
                machine.Advance(0.25f, true, epoch, events);
                machine.Advance(0f, false, epoch, events);
            }
            machine.Advance(0.1f, false, epoch, events);
            Assert.That(machine.CurrentStageId, Is.EqualTo("detach-escape"));
            Assert.That(HasEvent(events, "D21.Pair.End"), Is.True);
            Assert.That(HasEvent(events, "D21.Pair.Dominated"), Is.True);
            Assert.That(HasEvent(events, "D21.Flashlight.Dislodged"), Is.True);

            events.Clear();
            machine.Advance(1f, false, epoch, events);
            Assert.That(machine.CurrentStageId, Is.EqualTo("block-light"));
            Assert.That(HasEvent(events, "D21.BlockLight.Begin"), Is.True);

            machine.SetPaused(true);
            events.Clear();
            machine.Advance(20f, false, epoch, events);
            Assert.That(machine.CurrentStageId, Is.EqualTo("block-light"));
            Assert.That(events, Is.Empty);

            // The loot controller resumes this same stage only after the player
            // picks up and deliberately aims the dislodged flashlight.
            machine.SetPaused(false);
            machine.Advance(3f, false, epoch, events);
            Assert.That(machine.CurrentStageId, Is.EqualTo("backup-draw"));
        }

        private static bool HasEvent(
            IEnumerable<PairedPerformanceSemanticEvent> events,
            string eventId)
        {
            return events.Any(e => e.EventId == eventId);
        }

        private static int EventIndex(
            IList<PairedPerformanceSemanticEvent> events,
            string eventId)
        {
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].EventId == eventId)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
