using System;
using System.Collections.Generic;
using ConGame.Domain.Flow;
using ConGame.Runtime.Ending;
using NUnit.Framework;

namespace ConGame.Tests.EditMode
{

    public sealed class EndingSequenceTimelineTests
    {
        [Test]
        public void CatalogBuildsSpecOrderedE2Stages()
        {
            EndingStageDefinition[] stages =
                EndingStageCatalog.BuildE2Stages(EndingSequenceSettings.Default);

            Assert.That(stages.Length, Is.EqualTo(5));
            Assert.That(stages[0].Id, Is.EqualTo(EndingStageId.E2FirstPersonTakeover));
            Assert.That(stages[1].Id, Is.EqualTo(EndingStageId.E2FreeControl));
            Assert.That(stages[2].Id, Is.EqualTo(EndingStageId.E2HuntToDeath));
            Assert.That(stages[3].Id, Is.EqualTo(EndingStageId.E2Fall));
            Assert.That(stages[4].Id, Is.EqualTo(EndingStageId.E2Blackout));
            Assert.That(stages[1].DurationSeconds, Is.EqualTo(4f).Within(0.001f));
            Assert.That(stages[3].DurationSeconds, Is.EqualTo(1.2f).Within(0.001f));
            Assert.That(stages[2].WaitForExternalCompletion, Is.True);
            Assert.That(stages[2].TimeoutSeconds, Is.EqualTo(8f).Within(0.001f));
        }

        [Test]
        public void CatalogBuildsSpecOrderedE3Stages()
        {
            EndingStageDefinition[] stages =
                EndingStageCatalog.BuildE3Stages(EndingSequenceSettings.Default);

            Assert.That(stages.Length, Is.EqualTo(5));
            Assert.That(stages[0].Id, Is.EqualTo(EndingStageId.E3SitChair));
            Assert.That(stages[1].Id, Is.EqualTo(EndingStageId.E3KillerApproach));
            Assert.That(stages[2].Id, Is.EqualTo(EndingStageId.E3ClockTick));
            Assert.That(stages[3].Id, Is.EqualTo(EndingStageId.E3BackupShot));
            Assert.That(stages[4].Id, Is.EqualTo(EndingStageId.E3Title));
            Assert.That(stages[3].DurationSeconds, Is.EqualTo(5f).Within(0.001f));
        }

        [Test]
        public void BeginIsIdempotent()
        {
            EndingSequenceTimeline timeline = CreateE2();
            List<EndingStageEvent> events = new List<EndingStageEvent>();

            Assert.That(timeline.Begin(), Is.True);
            Assert.That(timeline.Begin(), Is.False);

            timeline.DrainEvents(events);
            Assert.That(events.Count, Is.EqualTo(1));
            Assert.That(events[0].Kind, Is.EqualTo(EndingStageEventKind.Enter));
            Assert.That(events[0].Stage, Is.EqualTo(EndingStageId.E2FirstPersonTakeover));
        }

        [Test]
        public void TimedStagesAdvanceInOrder()
        {
            EndingSequenceTimeline timeline = CreateE2();
            timeline.Begin();
            List<EndingStageEvent> events = new List<EndingStageEvent>();
            timeline.DrainEvents(events);
            events.Clear();

            timeline.Advance(0.2f); // takeover 0.1s -> free control
            timeline.DrainEvents(events);

            Assert.That(timeline.CurrentStage, Is.EqualTo(EndingStageId.E2FreeControl));
            Assert.That(events.Count, Is.EqualTo(2));
            Assert.That(events[0].Stage, Is.EqualTo(EndingStageId.E2FirstPersonTakeover));
            Assert.That(events[0].Kind, Is.EqualTo(EndingStageEventKind.Exit));
            Assert.That(events[0].ExitReason, Is.EqualTo(EndingStageExitReason.Completed));
            Assert.That(events[1].Stage, Is.EqualTo(EndingStageId.E2FreeControl));
            Assert.That(events[1].Kind, Is.EqualTo(EndingStageEventKind.Enter));
        }

        [Test]
        public void GatedStageWaitsForExternalCompletion()
        {
            EndingSequenceTimeline timeline = CreateE2();
            timeline.Begin();
            AdvanceToHunt(timeline);
            List<EndingStageEvent> events = new List<EndingStageEvent>();

            timeline.Advance(1f);
            timeline.DrainEvents(events);
            Assert.That(timeline.CurrentStage, Is.EqualTo(EndingStageId.E2HuntToDeath));
            Assert.That(events.Count, Is.EqualTo(0));

            Assert.That(timeline.CompleteCurrentStage(), Is.True);
            timeline.DrainEvents(events);
            Assert.That(timeline.CurrentStage, Is.EqualTo(EndingStageId.E2Fall));
            Assert.That(events.Count, Is.EqualTo(2));
            Assert.That(events[0].ExitReason, Is.EqualTo(EndingStageExitReason.Completed));
        }

        [Test]
        public void GatedStageTimesOut()
        {
            EndingSequenceTimeline timeline = CreateE2();
            timeline.Begin();
            AdvanceToHunt(timeline);
            List<EndingStageEvent> events = new List<EndingStageEvent>();

            timeline.Advance(9f); // timeout = 8s
            timeline.DrainEvents(events);

            Assert.That(timeline.CurrentStage, Is.EqualTo(EndingStageId.E2Fall));
            Assert.That(events.Count, Is.EqualTo(2));
            Assert.That(events[0].Stage, Is.EqualTo(EndingStageId.E2HuntToDeath));
            Assert.That(events[0].ExitReason, Is.EqualTo(EndingStageExitReason.Timeout));
        }

        [Test]
        public void CompleteCurrentStageRejectedOnTimedStage()
        {
            EndingSequenceTimeline timeline = CreateE2();
            timeline.Begin();
            timeline.Advance(0.2f);

            Assert.That(timeline.CompleteCurrentStage(), Is.False);
            Assert.That(timeline.CurrentStage, Is.EqualTo(EndingStageId.E2FreeControl));
        }

        [Test]
        public void EndingCommittedCancelsRunningSequence()
        {
            EndingSequenceTimeline timeline = CreateE2();
            timeline.Begin();
            AdvanceToHunt(timeline);
            List<EndingStageEvent> events = new List<EndingStageEvent>();

            Assert.That(timeline.TryCancel(TransitionPriority.EndingCommitted, "second hit re-committed"), Is.True);
            timeline.DrainEvents(events);

            Assert.That(timeline.State, Is.EqualTo(EndingSequenceState.Cancelled));
            Assert.That(timeline.CancelReason, Is.EqualTo("second hit re-committed"));
            Assert.That(timeline.CurrentStage, Is.EqualTo(EndingStageId.None));
            Assert.That(events.Count, Is.EqualTo(1));
            Assert.That(events[0].Kind, Is.EqualTo(EndingStageEventKind.Exit));
            Assert.That(events[0].Stage, Is.EqualTo(EndingStageId.E2HuntToDeath));
            Assert.That(events[0].ExitReason, Is.EqualTo(EndingStageExitReason.Cancelled));
        }

        [TestCase(TransitionPriority.Normal)]
        [TestCase(TransitionPriority.Death)]
        public void LowerPriorityCannotCancel(TransitionPriority priority)
        {
            EndingSequenceTimeline timeline = CreateE2();
            timeline.Begin();
            AdvanceToHunt(timeline);

            Assert.That(timeline.TryCancel(priority, "ordinary death"), Is.False);
            Assert.That(timeline.State, Is.EqualTo(EndingSequenceState.Running));
            Assert.That(timeline.CurrentStage, Is.EqualTo(EndingStageId.E2HuntToDeath));
        }

        [Test]
        public void CancelIsIdempotent()
        {
            EndingSequenceTimeline timeline = CreateE2();
            timeline.Begin();

            Assert.That(timeline.TryCancel(TransitionPriority.EndingCommitted, "first"), Is.True);
            Assert.That(timeline.TryCancel(TransitionPriority.EndingCommitted, "second"), Is.False);

            List<EndingStageEvent> events = new List<EndingStageEvent>();
            timeline.DrainEvents(events);
            Assert.That(events.Count, Is.EqualTo(2)); // Enter + Exit(Cancelled)
        }

        [Test]
        public void AdvanceAfterCancelIsNoOp()
        {
            EndingSequenceTimeline timeline = CreateE2();
            timeline.Begin();
            timeline.TryCancel(TransitionPriority.EndingCommitted, "cancelled");
            List<EndingStageEvent> events = new List<EndingStageEvent>();
            timeline.DrainEvents(events);

            timeline.Advance(100f);
            events.Clear();
            timeline.DrainEvents(events);

            Assert.That(timeline.State, Is.EqualTo(EndingSequenceState.Cancelled));
            Assert.That(events.Count, Is.EqualTo(0));
        }

        [Test]
        public void FullE2RunCompletes()
        {
            EndingSequenceTimeline timeline = CreateE2();
            timeline.Begin();
            AdvanceToHunt(timeline);
            timeline.CompleteCurrentStage();
            timeline.Advance(1.3f); // fall 1.2s
            Assert.That(timeline.CurrentStage, Is.EqualTo(EndingStageId.E2Blackout));
            timeline.Advance(3.1f); // blackout 3s

            Assert.That(timeline.State, Is.EqualTo(EndingSequenceState.Completed));
            Assert.That(timeline.CurrentStage, Is.EqualTo(EndingStageId.None));
        }

        [Test]
        public void RejectsInvalidStageLists()
        {
            Assert.Throws<ArgumentException>(() => new EndingSequenceTimeline(null));
            Assert.Throws<ArgumentException>(
                () => new EndingSequenceTimeline(new EndingStageDefinition[0]));
            Assert.Throws<ArgumentException>(
                () => new EndingSequenceTimeline(new[]
                {
                    new EndingStageDefinition(EndingStageId.E2Fall, 1f),
                    new EndingStageDefinition(EndingStageId.E2Fall, 1f)
                }));
        }

        [Test]
        public void SettingsNormalizeClampsInvalidValues()
        {
            EndingSequenceSettings settings = new EndingSequenceSettings
            {
                E2FreeControlSeconds = -1f,
                FallPitchDegrees = 180f,
                FallRollDegrees = 45f,
                E3ClockTickCount = -2
            };

            settings.Normalize();

            Assert.That(settings.E2FreeControlSeconds, Is.EqualTo(0f));
            Assert.That(settings.FallPitchDegrees, Is.EqualTo(85f));
            Assert.That(settings.FallRollDegrees, Is.EqualTo(15f));
            Assert.That(settings.E3ClockTickCount, Is.EqualTo(0));
        }

        private static EndingSequenceTimeline CreateE2()
        {
            return new EndingSequenceTimeline(
                EndingStageCatalog.BuildE2Stages(EndingSequenceSettings.Default));
        }

        private static void AdvanceToHunt(EndingSequenceTimeline timeline)
        {
            timeline.Advance(0.2f); // takeover
            timeline.Advance(4.1f); // free control
            Assert.That(timeline.CurrentStage, Is.EqualTo(EndingStageId.E2HuntToDeath));
            timeline.DrainEvents(new List<EndingStageEvent>());
        }
    }
}
