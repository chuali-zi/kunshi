using ConGame.Domain.Facts;
using ConGame.Domain.Interaction;
using UnityEngine;

namespace ConGame.Runtime.Interaction
{

    [DisallowMultipleComponent]
    public sealed class HideCapability : InteractionCapability
    {
        [SerializeField] private Transform anchor;
        [SerializeField] private PlayerZone zone = PlayerZone.SofaRear;

        public override InteractionVerb Verb => InteractionVerb.Hide;

        public override int Priority => 100;

        public void Configure(Transform hideAnchor, PlayerZone playerZone)
        {
            anchor = hideAnchor;
            zone = playerZone;
        }

        public override bool CanExecute(in InteractionContext context)
        {
            return context.Player != null && anchor != null;
        }

        public override InteractionResult Execute(in InteractionContext context)
        {
            context.Player.EnterHide(anchor, zone);
            return InteractionResult.Accepted;
        }
    }
}
