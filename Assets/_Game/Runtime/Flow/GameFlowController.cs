using System;
using ConGame.Domain.Flow;
using UnityEngine;

namespace ConGame.Runtime.Flow
{

    [DefaultExecutionOrder(-500)]
    [DisallowMultipleComponent]
    public sealed class GameFlowController : MonoBehaviour
    {
        private readonly GameFlowMachine _machine = new GameFlowMachine(GamePhase.Boot);

        public event Action<GamePhase, GamePhase, string> PhaseChanged;

        public GamePhase CurrentPhase => _machine.CurrentPhase;

        public void Initialize(GamePhase phase)
        {
            _machine.SetInitialPhase(phase);
        }

        public bool Request(GamePhase target, TransitionPriority priority, string reason)
        {
            FlowTransitionRequest request = new FlowTransitionRequest(target, priority, reason);
            return _machine.Request(request);
        }

        public bool CommitPending()
        {
            GamePhase previous = _machine.CurrentPhase;
            if (!_machine.Commit(out FlowTransitionRequest committed))
            {
                return false;
            }

            PhaseChanged?.Invoke(previous, committed.Target, committed.Reason);
            return true;
        }

        private void LateUpdate()
        {
            CommitPending();
        }
    }
}
