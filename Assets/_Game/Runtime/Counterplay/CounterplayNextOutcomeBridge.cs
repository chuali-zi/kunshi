using System;
using ConGame.Runtime.AI;
using ConGame.Runtime.Counterplay;
using UnityEngine;

namespace ConGame.Runtime.Counterplay
{
    [DisallowMultipleComponent]
    public sealed class CounterplayNextOutcomeBridge : MonoBehaviour
    {
        [SerializeField] private CounterplayPerformanceRuntimeAdapter adapter;
        [SerializeField] private KillerDirector killer;

        private bool _subscribed;

        public void Configure(CounterplayPerformanceRuntimeAdapter performanceAdapter, KillerDirector killerDirector)
        {
            Unsubscribe();
            adapter = performanceAdapter;
            killer = killerDirector;
            if (isActiveAndEnabled)
            {
                Subscribe();
            }
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnNextOutcomeRequested(string outcomeId, int loopEpoch, string nextOutcomeId)
        {
            if (killer == null || string.IsNullOrWhiteSpace(nextOutcomeId))
            {
                return;
            }

            killer.TryStartOutcome(nextOutcomeId);
        }

        private void OnOutcomeCompletionRequested(string outcomeId, int loopEpoch)
        {
            killer?.CompleteCurrentOutcome(outcomeId);
        }

        private void Subscribe()
        {
            if (_subscribed || adapter == null)
            {
                return;
            }

            adapter.NextOutcomeRequested += OnNextOutcomeRequested;
            adapter.OutcomeCompletionRequested += OnOutcomeCompletionRequested;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed || adapter == null)
            {
                return;
            }

            adapter.NextOutcomeRequested -= OnNextOutcomeRequested;
            adapter.OutcomeCompletionRequested -= OnOutcomeCompletionRequested;
            _subscribed = false;
        }
    }
}
