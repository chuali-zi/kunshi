using ConGame.Domain.Flow;
using NUnit.Framework;

namespace ConGame.Tests.EditMode
{

    public sealed class GameFlowMachineTests
    {
        [Test]
        public void EndingRequestWinsSameFrameDeathRace()
        {
            GameFlowMachine machine = new GameFlowMachine(GamePhase.FreeRoam);
            machine.Request(new FlowTransitionRequest(GamePhase.KillImpact, TransitionPriority.Death, "shot"));
            machine.Request(new FlowTransitionRequest(GamePhase.EndingE1, TransitionPriority.EndingCommitted, "second hit"));

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
    }
}
