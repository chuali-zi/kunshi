using ConGame.Domain.Facts;
using ConGame.Domain.Interaction;
using ConGame.Runtime.Events;
using ConGame.Runtime.Facts;
using UnityEngine;

namespace ConGame.Runtime.Interaction
{
    [DisallowMultipleComponent]
    public sealed class FlashlightUseHandler : MonoBehaviour, IUseHandler
    {
        [SerializeField] private GameplayFactService facts;
        [SerializeField] private GameplayEventHub events;
        [SerializeField, Min(0.5f)] private float beamRange = 8f;

        public void Configure(GameplayFactService factService, GameplayEventHub eventHub, float range = 8f)
        {
            facts = factService;
            events = eventHub;
            beamRange = Mathf.Max(0.5f, range);
        }

        public bool CanUse(in InteractionContext context)
        {
            return facts != null && context.Player != null;
        }

        public InteractionResult Use(in InteractionContext context)
        {
            if (!CanUse(context))
            {
                return InteractionResult.Rejected;
            }

            Transform origin = context.Player.transform;
            Vector3 position = origin.position + Vector3.up * 1.4f;
            Vector3 forward = origin.forward;
            facts.SetGlareActive(true);
            events?.EmitStimulus(KillerStimulusType.Glare, "flashlight");

            if (Physics.Raycast(position, forward, out RaycastHit hit, beamRange, ~0, QueryTriggerInteraction.Collide))
            {
                IFlashlightReactive[] reactives = hit.collider.GetComponentsInParent<IFlashlightReactive>(true);
                FlashlightHit beam = new FlashlightHit("flashlight", position, forward, 0.25f);
                for (int i = 0; i < reactives.Length; i++)
                {
                    reactives[i].React(beam);
                }
            }

            return InteractionResult.Accepted;
        }
    }
}
