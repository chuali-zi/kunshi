using ConGame.Domain.Interaction;
using ConGame.Runtime.Player;
using UnityEngine;

namespace ConGame.Runtime.Interaction
{

    public enum InteractionResult
    {
        Rejected = 0,
        Accepted = 1
    }

    public readonly struct InteractionContext
    {
        public InteractionContext(PlayerInteractor player, InteractableEntity entity)
        {
            Player = player;
            Entity = entity;
        }

        public PlayerInteractor Player { get; }
        public InteractableEntity Entity { get; }
    }

    public interface IInteractionCapability
    {
        InteractionVerb Verb { get; }
        int Priority { get; }
        bool CanExecute(in InteractionContext context);
        InteractionResult Execute(in InteractionContext context);
    }

    public readonly struct HitInfo
    {
        public HitInfo(ImpactKind kind, string sourceId, Vector3 point, Vector3 direction, float force)
        {
            Kind = kind;
            SourceId = sourceId ?? string.Empty;
            Point = point;
            Direction = direction;
            Force = force;
        }

        public ImpactKind Kind { get; }
        public string SourceId { get; }
        public Vector3 Point { get; }
        public Vector3 Direction { get; }
        public float Force { get; }
    }

    public enum HitResult
    {
        Ignored = 0,
        Accepted = 1,
        Broke = 2,
        EndingTriggered = 3
    }

    public interface IHitReceiver
    {
        HitResult ReceiveHit(in HitInfo hit);
    }

    public abstract class InteractionCapability : MonoBehaviour, IInteractionCapability
    {
        public abstract InteractionVerb Verb { get; }

        public virtual int Priority => 0;

        public abstract bool CanExecute(in InteractionContext context);

        public abstract InteractionResult Execute(in InteractionContext context);
    }
}
