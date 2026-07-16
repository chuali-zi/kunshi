using System;
using ConGame.Domain.Counterplay;
using ConGame.Runtime.Counterplay;
using ConGame.Runtime.Loop;
using UnityEngine;

namespace ConGame.Runtime.Interaction
{
    [DisallowMultipleComponent]
    public sealed class StolenLootDropController : MonoBehaviour, ILoopResettable
    {
        [SerializeField] private GameObject pistolDrop;
        [SerializeField] private GameObject flashlightDrop;
        [SerializeField] private GameObject backupPistolDrop;
        [SerializeField] private CounterplayPerformanceRuntimeAdapter adapter;

        private bool _initialPistolActive;
        private bool _initialFlashlightActive;
        private bool _initialBackupActive;
        private bool _subscribed;

        public int ResetOrder => 95;

        public void Configure(
            GameObject pistol,
            GameObject flashlight,
            CounterplayPerformanceRuntimeAdapter performanceAdapter,
            GameObject backupPistol = null)
        {
            Unsubscribe();
            pistolDrop = pistol;
            flashlightDrop = flashlight;
            backupPistolDrop = backupPistol;
            adapter = performanceAdapter;
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

        public void CaptureInitialState()
        {
            _initialPistolActive = pistolDrop != null && pistolDrop.activeSelf;
            _initialFlashlightActive = flashlightDrop != null && flashlightDrop.activeSelf;
            _initialBackupActive = backupPistolDrop != null && backupPistolDrop.activeSelf;
        }

        public void Quiesce(in LoopResetContext context)
        {
        }

        public void Restore(in LoopResetContext context)
        {
            if (pistolDrop != null)
            {
                pistolDrop.SetActive(_initialPistolActive);
            }

            if (flashlightDrop != null)
            {
                flashlightDrop.SetActive(_initialFlashlightActive);
            }

            if (backupPistolDrop != null)
            {
                backupPistolDrop.SetActive(_initialBackupActive);
            }
        }

        public void Resume(in LoopResetContext context)
        {
        }

        private void OnPerformanceStarted(string outcomeId, int loopEpoch, PerformanceHeldItemPolicy policy)
        {
            if (string.Equals(outcomeId, "D08", StringComparison.Ordinal) ||
                string.Equals(outcomeId, "D21", StringComparison.Ordinal) ||
                string.Equals(outcomeId, "D22", StringComparison.Ordinal))
            {
                EnableLoot(outcomeId);
            }
        }

        private void OnBranchResolved(
            string outcomeId,
            int loopEpoch,
            PerformanceBranchDefinition branch,
            CounterplayNarrativeOutcome narrative)
        {
            if (string.Equals(outcomeId, "D08", StringComparison.Ordinal) &&
                string.Equals(branch.NextOutcomeId, "D22", StringComparison.Ordinal))
            {
                EnableLoot("D22");
            }

            if (string.Equals(outcomeId, "D21", StringComparison.Ordinal) && branch.RequiresAllQteWindows)
            {
                EnableLoot("D21");
            }
        }

        private void EnableLoot(string outcomeId)
        {
            if (pistolDrop != null &&
                (string.Equals(outcomeId, "D08", StringComparison.Ordinal) ||
                 string.Equals(outcomeId, "D21", StringComparison.Ordinal) ||
                 string.Equals(outcomeId, "D22", StringComparison.Ordinal)))
            {
                pistolDrop.SetActive(true);
            }

            if (flashlightDrop != null &&
                (string.Equals(outcomeId, "D08", StringComparison.Ordinal) ||
                 string.Equals(outcomeId, "D21", StringComparison.Ordinal)))
            {
                flashlightDrop.SetActive(true);
            }

            if (backupPistolDrop != null &&
                (string.Equals(outcomeId, "D21", StringComparison.Ordinal) ||
                 string.Equals(outcomeId, "D22", StringComparison.Ordinal)))
            {
                backupPistolDrop.SetActive(true);
            }
        }

        private void Subscribe()
        {
            if (_subscribed || adapter == null)
            {
                return;
            }

            adapter.PerformanceStarted += OnPerformanceStarted;
            adapter.BranchResolved += OnBranchResolved;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed || adapter == null)
            {
                return;
            }

            adapter.PerformanceStarted -= OnPerformanceStarted;
            adapter.BranchResolved -= OnBranchResolved;
            _subscribed = false;
        }
    }
}
