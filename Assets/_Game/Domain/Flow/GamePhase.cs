using System;

namespace ConGame.Domain.Flow
{

    public enum GamePhase
    {
        Boot = 0,
        Menu = 1,
        Awakening = 2,
        FreeRoam = 3,
        Breach = 4,
        Counterplay = 5,
        KillImpact = 6,
        LoopReset = 7,
        EndingE1 = 8,
        EndingE2 = 9,
        EndingE3 = 10,
        Finished = 11
    }

    public enum TransitionPriority
    {
        Normal = 0,
        Death = 100,
        EndingCommitted = 200
    }

    public readonly struct FlowTransitionRequest
    {
        public FlowTransitionRequest(GamePhase target, TransitionPriority priority, string reason)
        {
            Target = target;
            Priority = priority;
            Reason = reason ?? string.Empty;
        }

        public GamePhase Target { get; }

        public TransitionPriority Priority { get; }

        public string Reason { get; }
    }

    public sealed class GameFlowMachine
    {
        private FlowTransitionRequest? _pending;

        public GameFlowMachine(GamePhase initialPhase = GamePhase.Boot)
        {
            CurrentPhase = initialPhase;
        }

        public GamePhase CurrentPhase { get; private set; }

        public bool HasPendingTransition => _pending.HasValue;

        public bool Request(in FlowTransitionRequest request)
        {
            if (!IsAllowed(CurrentPhase, request.Target))
            {
                return false;
            }

            if (!_pending.HasValue || request.Priority > _pending.Value.Priority)
            {
                _pending = request;
                return true;
            }

            return false;
        }

        public bool Commit(out FlowTransitionRequest committed)
        {
            if (!_pending.HasValue)
            {
                committed = default;
                return false;
            }

            committed = _pending.Value;
            _pending = null;
            CurrentPhase = committed.Target;
            return true;
        }

        public void SetInitialPhase(GamePhase phase)
        {
            CurrentPhase = phase;
            _pending = null;
        }

        public static bool IsAllowed(GamePhase from, GamePhase to)
        {
            if (from == to)
            {
                return false;
            }

            switch (from)
            {
                case GamePhase.Boot:
                    return to == GamePhase.Menu || to == GamePhase.Awakening;
                case GamePhase.Menu:
                    return to == GamePhase.Awakening;
                case GamePhase.Awakening:
                    return to == GamePhase.FreeRoam || to == GamePhase.Breach ||
                           to == GamePhase.KillImpact || to == GamePhase.EndingE1;
                case GamePhase.FreeRoam:
                    return to == GamePhase.Breach || to == GamePhase.KillImpact ||
                           to == GamePhase.EndingE1;
                case GamePhase.Breach:
                    return to == GamePhase.Counterplay || to == GamePhase.KillImpact ||
                           to == GamePhase.EndingE1;
                case GamePhase.Counterplay:
                    return to == GamePhase.KillImpact || to == GamePhase.EndingE1;
                case GamePhase.KillImpact:
                    return to == GamePhase.LoopReset;
                case GamePhase.LoopReset:
                    return to == GamePhase.Awakening;
                case GamePhase.EndingE1:
                    return to == GamePhase.EndingE2;
                case GamePhase.EndingE2:
                    return to == GamePhase.EndingE3;
                case GamePhase.EndingE3:
                    return to == GamePhase.Finished;
                case GamePhase.Finished:
                    return to == GamePhase.Menu;
                default:
                    return false;
            }
        }
    }
}
