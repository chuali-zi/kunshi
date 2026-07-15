using ConGame.Domain.Flow;
using NUnit.Framework;

namespace ConGame.Tests.EditMode
{

    public sealed class GameFlowMachineTests
    {
        [Test]
        public void EndingRequestWinsSameFrameDeathRace()
        {
            GameFlowMachine machine = new GameFlowMachine(GamePhase.Counterplay);
            machine.Request(new FlowTransitionRequest(GamePhase.KillImpact, TransitionPriority.Death, "shot"));
            machine.Request(
                new FlowTransitionRequest(GamePhase.EndingE1, TransitionPriority.EndingCommitted, "second hit"));

            Assert.That(machine.Commit(out FlowTransitionRequest committed), Is.True);
            Assert.That(committed.Target, Is.EqualTo(GamePhase.EndingE1));
            Assert.That(machine.CurrentPhase, Is.EqualTo(GamePhase.EndingE1));
        }

        [Test]
        public void SamePriorityKeepsFirstRequest()
        {
            GameFlowMachine machine = new GameFlowMachine(GamePhase.Awakening);
            machine.Request(new FlowTransitionRequest(GamePhase.FreeRoam, TransitionPriority.Normal, "first"));
            machine.Request(new FlowTransitionRequest(GamePhase.Breach, TransitionPriority.Normal, "second"));

            machine.Commit(out FlowTransitionRequest committed);
            Assert.That(committed.Target, Is.EqualTo(GamePhase.FreeRoam));
        }

        [Test]
        public void M1LoopCanAdvanceThroughTheCompletePhaseChain()
        {
            GameFlowMachine machine = new GameFlowMachine(GamePhase.Awakening);

            AssertTransition(machine, GamePhase.FreeRoam, TransitionPriority.Normal);
            AssertTransition(machine, GamePhase.Breach, TransitionPriority.Normal);
            AssertTransition(machine, GamePhase.Counterplay, TransitionPriority.Normal);
            AssertTransition(machine, GamePhase.KillImpact, TransitionPriority.Death);
            AssertTransition(machine, GamePhase.LoopReset, TransitionPriority.Normal);
            AssertTransition(machine, GamePhase.Awakening, TransitionPriority.Normal);
        }

        [TestCase(GamePhase.FreeRoam)]
        [TestCase(GamePhase.Breach)]
        public void DeathCannotBypassCounterplay(GamePhase phase)
        {
            GameFlowMachine machine = new GameFlowMachine(phase);
            FlowTransitionRequest request =
                new FlowTransitionRequest(GamePhase.KillImpact, TransitionPriority.Death, "invalid direct death");

            Assert.That(machine.Request(request), Is.False);
            Assert.That(machine.HasPendingTransition, Is.False);
            Assert.That(machine.Commit(out _), Is.False);
            Assert.That(machine.CurrentPhase, Is.EqualTo(phase));
        }

        private static void AssertTransition(GameFlowMachine machine, GamePhase target, TransitionPriority priority)
        {
            FlowTransitionRequest request = new FlowTransitionRequest(target, priority, "M1 phase chain");
            Assert.That(machine.Request(request), Is.True);
            Assert.That(machine.Commit(out FlowTransitionRequest committed), Is.True);
            Assert.That(committed.Target, Is.EqualTo(target));
            Assert.That(machine.CurrentPhase, Is.EqualTo(target));
        }
    }
}
