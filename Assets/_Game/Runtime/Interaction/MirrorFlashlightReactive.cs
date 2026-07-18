using ConGame.Domain.Facts;
using ConGame.Runtime.Events;
using ConGame.Runtime.Facts;
using UnityEngine;

namespace ConGame.Runtime.Interaction
{
    [DisallowMultipleComponent]
    public sealed class MirrorFlashlightReactive : MonoBehaviour, IFlashlightReactive
    {
        [SerializeField] private GameplayFactService facts;
        [SerializeField] private GameplayEventHub events;
        [SerializeField, Min(0.05f)] private float glareDuration = 1f;

        public void Configure(
            GameplayFactService factService,
            GameplayEventHub eventHub,
            float duration = 1f)
        {
            facts = factService;
            events = eventHub;
            glareDuration = Mathf.Max(0.05f, duration);
        }

        public void React(in FlashlightHit hit)
        {
            GlareLifetime.Activate(facts, Mathf.Max(glareDuration, hit.ExposureSeconds));
            events?.EmitStimulus(KillerStimulusType.Glare, "mirror");
        }
    }
}
