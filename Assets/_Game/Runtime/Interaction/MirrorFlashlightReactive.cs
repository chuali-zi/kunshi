using ConGame.Domain.Facts;
using ConGame.Domain.Interaction;
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
        [SerializeField] private StatefulWorldItem mirrorItem;

        private bool _mirrorBroken;

        public void Configure(
            GameplayFactService factService,
            GameplayEventHub eventHub,
            float duration = 1f,
            StatefulWorldItem breakableMirror = null)
        {
            facts = factService;
            events = eventHub;
            glareDuration = Mathf.Max(0.05f, duration);
            BindMirror(breakableMirror);
        }

        private void Awake()
        {
            mirrorItem ??= GetComponentInParent<StatefulWorldItem>();
            BindMirror(mirrorItem);
        }

        private void OnDestroy()
        {
            if (mirrorItem != null)
            {
                mirrorItem.StateChanged -= OnMirrorStateChanged;
            }
        }

        public void React(in FlashlightHit hit)
        {
            // D17 reflection is only judged while the mirror is intact; a
            // broken mirror loses the glare counter for the rest of the loop.
            if (_mirrorBroken)
            {
                return;
            }

            GlareLifetime.Activate(facts, Mathf.Max(glareDuration, hit.ExposureSeconds));
            events?.EmitStimulus(KillerStimulusType.Glare, "mirror");
        }

        private void BindMirror(StatefulWorldItem breakableMirror)
        {
            if (mirrorItem != null)
            {
                mirrorItem.StateChanged -= OnMirrorStateChanged;
            }

            mirrorItem = breakableMirror;
            if (mirrorItem != null)
            {
                mirrorItem.StateChanged += OnMirrorStateChanged;
                _mirrorBroken = mirrorItem.CurrentState == ItemPhysicalState.Broken;
            }
            else
            {
                _mirrorBroken = false;
            }
        }

        private void OnMirrorStateChanged(ItemPhysicalState state)
        {
            _mirrorBroken = state == ItemPhysicalState.Broken;
        }
    }
}
