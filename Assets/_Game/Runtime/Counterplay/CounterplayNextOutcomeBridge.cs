using System;
using ConGame.Runtime.AI;
using ConGame.Runtime.Counterplay;
using ConGame.Runtime.Loop;
using UnityEngine;

namespace ConGame.Runtime.Counterplay
{
    [DisallowMultipleComponent]
    public sealed class CounterplayNextOutcomeBridge : MonoBehaviour
    {
        [SerializeField] private CounterplayPerformanceRuntimeAdapter adapter;
        [SerializeField] private KillerDirector killer;
        [SerializeField] private LoopDirector loop;

        private bool _subscribed;

        private void Awake()
        {
            loop ??= UnityEngine.Object.FindAnyObjectByType<LoopDirector>();
        }

        public void Configure(CounterplayPerformanceRuntimeAdapter performanceAdapter, KillerDirector killerDirector)
        {
            Unsubscribe();
            adapter = performanceAdapter;
            killer = killerDirector;
            loop ??= UnityEngine.Object.FindAnyObjectByType<LoopDirector>();
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
            if (killer == null || string.IsNullOrWhiteSpace(nextOutcomeId) ||
                !string.Equals(killer.SelectedOutcomeId, outcomeId, StringComparison.Ordinal) ||
                !IsCurrentEpoch(loopEpoch))
            {
                return;
            }

            killer.TryStartOutcome(nextOutcomeId);
        }

        private void OnOutcomeCompletionRequested(string outcomeId, int loopEpoch)
        {
            if (IsCurrentEpoch(loopEpoch))
            {
                killer?.CompleteCurrentOutcome(outcomeId);
            }
        }

        private bool IsCurrentEpoch(int loopEpoch)
        {
            return loop == null || loop.LoopEpoch == loopEpoch;
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
