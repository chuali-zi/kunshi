using ConGame.Domain.Facts;
using ConGame.Domain.Interaction;
using ConGame.Runtime.Events;
using ConGame.Runtime.Facts;
using UnityEngine;

namespace ConGame.Runtime.Interaction
{
    [DisallowMultipleComponent]
    public sealed class BlanketTrapUseHandler : MonoBehaviour, IUseHandler
    {
        [SerializeField] private GameplayFactService facts;
        [SerializeField] private GameplayEventHub events;

        public void Configure(GameplayFactService factService, GameplayEventHub eventHub = null)
        {
            facts = factService;
            events = eventHub;
        }

        public bool CanUse(in InteractionContext context)
        {
            if (facts == null)
            {
                return false;
            }

            FactSnapshot snapshot = facts.CaptureSnapshot();
            StatefulWorldItem item = GetComponent<StatefulWorldItem>();
            if (item == null || item.CurrentSocket == null ||
                snapshot.Trap != TrapState.BlanketArmed || snapshot.TrapPulled)
            {
                return false;
            }

            TripTrapController contact = item.CurrentSocket.GetComponent<TripTrapController>() ??
                                         item.CurrentSocket.GetComponentInParent<TripTrapController>();
            return contact != null && contact.CanUse(context);
        }

        public InteractionResult Use(in InteractionContext context)
        {
            if (!CanUse(context))
            {
                return InteractionResult.Rejected;
            }

            TripTrapController contact = GetComponent<StatefulWorldItem>()?.CurrentSocket?.GetComponent<TripTrapController>() ??
                                         GetComponent<StatefulWorldItem>()?.CurrentSocket?.GetComponentInParent<TripTrapController>();
            return contact == null ? InteractionResult.Rejected : contact.Use(context);
        }
    }
}
