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

        public void Configure(GameplayFactService factService, GameplayEventHub eventHub)
        {
            facts = factService;
            events = eventHub;
        }

        public void React(in FlashlightHit hit)
        {
            facts?.SetGlareActive(true);
            events?.EmitStimulus(KillerStimulusType.Glare, "mirror");
        }
    }
}
