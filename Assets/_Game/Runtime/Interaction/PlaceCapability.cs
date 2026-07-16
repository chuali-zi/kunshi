using ConGame.Domain.Interaction;
using UnityEngine;

namespace ConGame.Runtime.Interaction
{
    [RequireComponent(typeof(PlacementSocket))]
    public sealed class PlaceCapability : InteractionCapability
    {
        [SerializeField] private PlacementSocket socket;

        public override InteractionVerb Verb => InteractionVerb.Place;
        public override int Priority => 120;

        public void Configure(PlacementSocket targetSocket)
        {
            socket = targetSocket;
        }

        private void Awake()
        {
            socket ??= GetComponent<PlacementSocket>();
        }

        public override bool CanExecute(in InteractionContext context)
        {
            return context.Player != null && context.Player.HeldEntity != null &&
                   socket != null && socket.CanAccept(context.Player.HeldEntity);
        }

        public override InteractionResult Execute(in InteractionContext context)
        {
            return context.Player.TryPlaceHeld(socket)
                ? InteractionResult.Accepted
                : InteractionResult.Rejected;
        }
    }
}
