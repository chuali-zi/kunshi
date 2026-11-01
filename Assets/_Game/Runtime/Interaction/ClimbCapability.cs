using ConGame.Domain.Interaction;
using UnityEngine;

namespace ConGame.Runtime.Interaction
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(InteractableEntity))]
    public sealed class ClimbCapability : InteractionCapability
    {
        [SerializeField] private Transform standAnchor;
        [SerializeField, Min(0.01f)] private float duration = 0.4f;
        [SerializeField] private bool requirePlacedState = true;

        public override InteractionVerb Verb => InteractionVerb.Use;
        public override int Priority => 130;
        public Transform StandAnchor => standAnchor;
        public float Duration => duration;
        public bool RequirePlacedState => requirePlacedState;

        public void Configure(Transform anchor, float climbDuration = 0.4f, bool placedStateOnly = true)
        {
            standAnchor = anchor;
            duration = Mathf.Max(0.01f, climbDuration);
            requirePlacedState = placedStateOnly;
        }

        public override bool CanExecute(in InteractionContext context)
        {
            if (context.Player == null || context.Player.HeldEntity != null || standAnchor == null)
            {
                return false;
            }

            if (standAnchor.position.y <= context.Player.transform.position.y + 0.1f)
            {
                return false;
            }

            StatefulWorldItem stateful = GetComponent<StatefulWorldItem>();
            return !requirePlacedState ||
                   (stateful != null && stateful.CurrentState == ItemPhysicalState.Placed);
        }

        public override InteractionResult Execute(in InteractionContext context)
        {
            return context.Player.TryClimb(standAnchor, duration)
                ? InteractionResult.Accepted
                : InteractionResult.Rejected;
        }
    }
}
