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
        [SerializeField] private PlayerPose hidePose = PlayerPose.Hidden;
        [SerializeField] private bool requiresPlacedItem;

        public override InteractionVerb Verb => InteractionVerb.Hide;

        public override int Priority => 100;
        public PlayerPose HidePose => hidePose;
        public bool RequiresPlacedItem => requiresPlacedItem;

        public void Configure(Transform hideAnchor, PlayerZone playerZone,
                              PlayerPose playerPose = PlayerPose.Hidden, bool placedItemOnly = false)
        {
            anchor = hideAnchor;
            zone = playerZone;
            hidePose = playerPose;
            requiresPlacedItem = placedItemOnly;
        }

        public override bool CanExecute(in InteractionContext context)
        {
            if (context.Player == null || context.Player.HeldEntity != null ||
                context.Player.IsHiding || anchor == null)
            {
                return false;
            }

            // The bookcase creates its hiding space only after it has fallen.
            // Keeping this prerequisite here also prevents the scanner from
            // presenting a Hide action that cannot exist in the upright mesh.
            TipBookcaseUseHandler bookcase = GetComponent<TipBookcaseUseHandler>();
            if (bookcase != null && !bookcase.IsTipped)
            {
                return false;
            }

            StatefulWorldItem item = GetComponent<StatefulWorldItem>();
            return !requiresPlacedItem ||
                   (item != null && item.CurrentState == ItemPhysicalState.Placed && item.CurrentSocket == null);
        }

        public override InteractionResult Execute(in InteractionContext context)
        {
            context.Player.EnterHide(anchor, zone, hidePose);
            return InteractionResult.Accepted;
        }
    }
}
