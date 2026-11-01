using System.Collections.Generic;
using System.Linq;
using ConGame.Domain.Choreography;
using NUnit.Framework;

namespace ConGame.Tests.EditMode
{
    public sealed class PairedPerformancePhaseMachineTests
    {
        private const int Epoch = 7;

        [Test]
        public void StartEmitsStartedThenFirstStageEnterEvents()
        {
            PairedPerformancePhaseMachine machine = CreateD21Machine();
            var events = new List<PairedPerformanceSemanticEvent>();

            machine.Start(Epoch, events);

            Assert.That(machine.IsRunning, Is.True);
            Assert.That(machine.CurrentStageId, Is.EqualTo("fall"));
            Assert.That(events.Select(e => e.EventId),
                Is.EqualTo(new[] { "D21.Started", "D21.Fall.Begin" }));
            Assert.That(events.All(e => e.LoopEpoch == Epoch), Is.True);
        }

        [Test]
        public void StageBoundaryEmitsExitBeforeNextEnter()
        {
            PairedPerformancePhaseMachine machine = CreateD21Machine();
            var events = new List<PairedPerformanceSemanticEvent>();
            machine.Start(Epoch, events);
            events.Clear();

            machine.Advance(1.2f, false, Epoch, events);

            Assert.That(machine.CurrentStageId, Is.EqualTo("item-drop"));
            Assert.That(events.Select(e => e.EventId), Is.EqualTo(new[]
            {
                "D21.Fall.Impact", "D21.Pistol.Dropped", "D21.Flashlight.Dropped"
            }));
        }

        [Test]
        public void ContactAndDetachMarkersFollowStageContactState()
        {
            PairedPerformancePhaseMachine machine = CreateD21Machine();
            var events = new List<PairedPerformanceSemanticEvent>();
            machine.Start(Epoch, events);
            machine.Advance(2.0f, false, Epoch, events); // fall + item-drop done
            events.Clear();

            machine.Advance(0.1f, false, Epoch, events); // enter dark-grapple

            Assert.That(machine.CurrentContactState, Is.EqualTo(PairedContactState.Contact));
            Assert.That(events.Select(e => e.EventId),
                Is.EqualTo(new[] { "D21.Contact", "D21.Pair.Begin" }));

            events.Clear();
            machine.Advance(3.0f, false, Epoch, events); // finish grapple -> detach-escape

            Assert.That(machine.CurrentContactState, Is.EqualTo(PairedContactState.Detached));
            Assert.That(events.Select(e => e.EventId), Does.Contain("D21.Pair.End"));
            Assert.That(events.Select(e => e.EventId), Does.Contain("D21.Detach"));
            Assert.That(events.Select(e => e.EventId), Does.Contain("D21.Player.Escape"));
        }

        [Test]
        public void QteSuccessAndFailureEmitDifferentEvents()
        {
            PairedPerformancePhaseMachine success = CreateD21Machine();
            var successEvents = new List<PairedPerformanceSemanticEvent>();
            success.Start(Epoch, successEvents);
            success.Advance(2.1f, false, Epoch, successEvents);
            successEvents.Clear();
            // 3 windows x 4 presses; rising edges only (press at window boundary
            // counts toward the window that is about to complete).
            for (int i = 0; i < 12; i++)
            {
                success.Advance(0.25f, true, Epoch, successEvents);
                success.Advance(0f, false, Epoch, successEvents);
            }

            success.Advance(0.2f, false, Epoch, successEvents);
            Assert.That(successEvents.Select(e => e.EventId), Does.Contain("D21.Pair.Dominated"));
            Assert.That(successEvents.Select(e => e.EventId), Does.Not.Contain("D21.Pair.Struggled"));

            PairedPerformancePhaseMachine failure = CreateD21Machine();
            var failureEvents = new List<PairedPerformanceSemanticEvent>();
            failure.Start(Epoch, failureEvents);
            failure.Advance(5.1f, false, Epoch, failureEvents); // no presses at all

            Assert.That(failureEvents.Select(e => e.EventId), Does.Contain("D21.Pair.Struggled"));
        }

        [Test]
        public void QteSnapshotIsPublishedWhileWindowsRun()
        {
            PairedPerformancePhaseMachine machine = CreateD21Machine();
            var events = new List<PairedPerformanceSemanticEvent>();
            machine.Start(Epoch, events);
            machine.Advance(2.1f, false, Epoch, events);

            machine.Advance(0.2f, true, Epoch, events);

            Assert.That(machine.HasQteSnapshot, Is.True);
            Assert.That(machine.LastQteSnapshot.CurrentPressCount, Is.EqualTo(1));
        }

        [Test]
        public void LargeDeltaAdvancesMultipleStagesAndCompletes()
        {
            PairedPerformancePhaseMachine machine = CreateD21Machine();
            var events = new List<PairedPerformanceSemanticEvent>();
            machine.Start(Epoch, events);
            events.Clear();

            machine.Advance(50f, false, Epoch, events);

            Assert.That(machine.IsComplete, Is.True);
            Assert.That(machine.IsRunning, Is.False);
            Assert.That(events.Select(e => e.EventId), Does.Contain("D21.BlockLight.Begin"));
            Assert.That(events.Select(e => e.EventId), Does.Contain("D21.BackupPistol.Draw"));
            Assert.That(events.Select(e => e.EventId), Does.Contain("D21.BackupPistol.Fire"));
            Assert.That(events.Select(e => e.EventId), Does.Contain("D21.Completed"));
            // Completion detaches the grapple contact exactly once.
            Assert.That(events.Count(e => e.EventId == "D21.Detach"), Is.EqualTo(1));
        }

        [Test]
        public void GameplayCancelIsRefusedOutsideCancelBoundary()
        {
            PairedPerformancePhaseMachine machine = CreateD21Machine();
            var events = new List<PairedPerformanceSemanticEvent>();
            machine.Start(Epoch, events); // D21 "fall" forbids gameplay cancel
            events.Clear();

            PairedCleanupAction actions =
                machine.Cancel(PairedCancelReason.GameplayInterrupt, Epoch, events);

            Assert.That(actions, Is.EqualTo(PairedCleanupAction.None));
            Assert.That(machine.IsRunning, Is.True);
            Assert.That(events, Is.Empty);
        }

        [Test]
        public void LoopResetCancelEmitsAtomicCleanupAndStopsMachine()
        {
            PairedPerformancePhaseMachine machine = CreateD21Machine();
            var events = new List<PairedPerformanceSemanticEvent>();
            machine.Start(Epoch, events);
            machine.Advance(2.1f, false, Epoch, events); // inside dark-grapple (Contact)
            events.Clear();

            PairedCleanupAction actions =
                machine.Cancel(PairedCancelReason.LoopReset, Epoch, events);

            Assert.That(actions, Is.EqualTo(PairedCleanupAction.All));
            Assert.That(machine.IsCancelled, Is.True);
            Assert.That(machine.IsRunning, Is.False);
            // Exit events, Detach marker and Cancelled arrive together, in one call.
            Assert.That(events.Select(e => e.EventId), Does.Contain("D21.Pair.End"));
            Assert.That(events.Select(e => e.EventId), Does.Contain("D21.Detach"));
            Assert.That(events.Select(e => e.EventId), Does.Contain("D21.Cancelled"));

            events.Clear();
            machine.Advance(1f, true, Epoch, events);
            Assert.That(events, Is.Empty);
            Assert.That(machine.Cancel(PairedCancelReason.LoopReset, Epoch, events),
                Is.EqualTo(PairedCleanupAction.None));
        }

        [Test]
        public void StaleEpochInputIsDiscarded()
        {
            PairedPerformancePhaseMachine machine = CreateD21Machine();
            var events = new List<PairedPerformanceSemanticEvent>();
            machine.Start(Epoch, events);
            events.Clear();

            machine.Advance(10f, false, Epoch + 1, events);
            Assert.That(events, Is.Empty);
            Assert.That(machine.CurrentStageId, Is.EqualTo("fall"));
            Assert.That(machine.Cancel(PairedCancelReason.LoopReset, Epoch + 1, events),
                Is.EqualTo(PairedCleanupAction.None));
            Assert.That(machine.IsRunning, Is.True);
        }

        [Test]
        public void D08GrappleSuccessDropsMainPistolAndOpensLootWindow()
        {
            PairedPerformancePhaseMachine machine =
                new PairedPerformancePhaseMachine(PairedPerformanceCatalog.CreateD08());
            var events = new List<PairedPerformanceSemanticEvent>();
            machine.Start(Epoch, events);
            machine.Advance(0.5f, false, Epoch, events); // strike done, grapple starts
            for (int i = 0; i < 12; i++)
            {
                machine.Advance(0.25f, true, Epoch, events);
                machine.Advance(0f, false, Epoch, events);
            }

            machine.Advance(0.2f, false, Epoch, events);

            Assert.That(events.Select(e => e.EventId), Does.Contain("D08.MainPistol.Dropped"));
            Assert.That(events.Select(e => e.EventId), Does.Contain("D08.LootWindow.Open"));
        }

        private static PairedPerformancePhaseMachine CreateD21Machine()
        {
            return new PairedPerformancePhaseMachine(PairedPerformanceCatalog.CreateD21());
        }
    }
}
