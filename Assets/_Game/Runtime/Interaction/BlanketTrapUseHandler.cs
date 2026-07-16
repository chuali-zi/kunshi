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
            return facts != null;
        }

        public InteractionResult Use(in InteractionContext context)
        {
            if (!CanUse(context))
            {
                return InteractionResult.Rejected;
            }

            FactSnapshot snapshot = facts.CaptureSnapshot();
            if (snapshot.Trap != TrapState.BlanketArmed)
            {
                facts.SetTrap(TrapState.BlanketArmed);
                facts.SetTrapPulled(false);
                return InteractionResult.Accepted;
            }

            facts.SetTrapPulled(true);
            events?.EmitStimulus(KillerStimulusType.TrapPull, "blanket");
            return InteractionResult.Accepted;
        }
    }
}
