using ConGame.Runtime.Events;
using ConGame.Runtime.Loop;
using UnityEngine;

namespace ConGame.Presentation.Interaction
{
    [DisallowMultipleComponent]
    public sealed class ImpactDamageVisualPresenter : MonoBehaviour, ILoopResettable
    {
        [SerializeField] private GameplayEventHub events;
        [SerializeField] private MonoBehaviour impactReceiver;
        [SerializeField] private GameObject[] intactVisuals;
        [SerializeField] private GameObject[] brokenVisuals;
        [SerializeField] private GameObject[] damageVisuals;
        [SerializeField] private int[] damageThresholds;
        [SerializeField, Min(1)] private int maximumDamage = 1;

        private int _damage;
        private int _initialDamage;

        public int ResetOrder => 95;
        public int Damage => _damage;

        public void Configure(
            GameplayEventHub gameplayEvents,
            MonoBehaviour receiver,
            int maxDamage,
            GameObject[] intact = null,
            GameObject[] broken = null,
            GameObject[] stagedDamage = null,
            int[] stagedThresholds = null)
        {
            Unsubscribe();
            events = gameplayEvents;
            impactReceiver = receiver;
            maximumDamage = Mathf.Max(1, maxDamage);
            intactVisuals = intact;
            brokenVisuals = broken;
            damageVisuals = stagedDamage;
            damageThresholds = stagedThresholds;
            _damage = 0;
            _initialDamage = 0;
            Subscribe();
            ApplyVisuals();
        }

        public void CaptureInitialState()
        {
            _initialDamage = _damage;
            ApplyVisuals();
        }

        public void Quiesce(in LoopResetContext context)
        {
        }

        public void Restore(in LoopResetContext context)
        {
            _damage = _initialDamage;
            ApplyVisuals();
        }

        public void Resume(in LoopResetContext context)
        {
            ApplyVisuals();
        }

        private void OnEnable()
        {
            Subscribe();
            ApplyVisuals();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (isActiveAndEnabled && events != null)
            {
                events.ImpactResolved -= OnImpactResolved;
                events.ImpactResolved += OnImpactResolved;
            }
        }

        private void Unsubscribe()
        {
            if (events != null)
            {
                events.ImpactResolved -= OnImpactResolved;
            }
        }

        private void OnImpactResolved(ImpactEvent impact)
        {
            if (impact.Receiver != impactReceiver || _damage >= maximumDamage)
            {
                return;
            }

            _damage++;
            ApplyVisuals();
        }

        private void ApplyVisuals()
        {
            int pairCount = Mathf.Max(intactVisuals == null ? 0 : intactVisuals.Length,
                                      brokenVisuals == null ? 0 : brokenVisuals.Length);
            for (int i = 0; i < pairCount; i++)
            {
                SetActive(intactVisuals, i, i >= _damage);
                SetActive(brokenVisuals, i, i < _damage);
            }

            if (damageVisuals == null)
            {
                return;
            }

            for (int i = 0; i < damageVisuals.Length; i++)
            {
                int threshold = damageThresholds != null && i < damageThresholds.Length
                    ? Mathf.Max(1, damageThresholds[i])
                    : i + 1;
                SetActive(damageVisuals, i, _damage >= threshold);
            }
        }

        private static void SetActive(GameObject[] visuals, int index, bool active)
        {
            if (visuals != null && index < visuals.Length && visuals[index] != null)
            {
                visuals[index].SetActive(active);
            }
        }
    }
}
