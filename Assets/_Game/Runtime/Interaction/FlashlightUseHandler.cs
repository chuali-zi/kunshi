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
        [SerializeField, Min(0.05f)] private float glareDuration = 0.75f;

        public void Configure(
            GameplayFactService factService,
            GameplayEventHub eventHub,
            float range = 8f,
            float duration = 0.75f)
        {
            facts = factService;
            events = eventHub;
            beamRange = Mathf.Max(0.5f, range);
            glareDuration = Mathf.Max(0.05f, duration);
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
            Vector3 forward = context.Player.AimForward;
            if (Physics.Raycast(position, forward, out RaycastHit hit, beamRange, ~0, QueryTriggerInteraction.Collide))
            {
                IFlashlightReactive[] reactives = hit.collider.GetComponentsInParent<IFlashlightReactive>(true);
                if (reactives.Length == 0)
                {
                    return InteractionResult.Accepted;
                }

                GlareLifetime.Activate(facts, glareDuration);
                events?.EmitStimulus(KillerStimulusType.Glare, "flashlight");
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
