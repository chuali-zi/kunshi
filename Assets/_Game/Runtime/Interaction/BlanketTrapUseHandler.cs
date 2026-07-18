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
            return item != null && item.CurrentSocket != null &&
                   snapshot.Trap == TrapState.BlanketArmed && !snapshot.TrapPulled;
        }

        public InteractionResult Use(in InteractionContext context)
        {
            if (!CanUse(context))
            {
                return InteractionResult.Rejected;
            }

            FactSnapshot snapshot = facts.CaptureSnapshot();
            facts.SetTrapPulled(true);
            events?.EmitStimulus(KillerStimulusType.TrapPull, "blanket");
            return InteractionResult.Accepted;
        }
    }
}
