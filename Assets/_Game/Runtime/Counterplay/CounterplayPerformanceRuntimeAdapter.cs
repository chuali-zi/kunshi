using System;
using ConGame.Domain.Counterplay;
using ConGame.Runtime.Input;
using ConGame.Runtime.Loop;
using ConGame.Runtime.Settings;
using UnityEngine;

namespace ConGame.Runtime.Counterplay
{
    [DisallowMultipleComponent]
    public sealed class CounterplayPerformanceRuntimeAdapter : MonoBehaviour,
        ICounterplayPerformanceInput,
        ICounterplayPerformanceProgress
    {
        [SerializeField] private PlayerInputRouter input;
        [SerializeField] private LoopDirector loop;
        [SerializeField] private MonoBehaviour performancePlayerBehaviour;

        private ICounterplayPerformanceHookTarget _performancePlayer;
        private GameSettingsStore _settingsStore;
        private GameSettingsV1 _settings;
        private bool _interactPulse;
        private bool _subscribed;

        public event Action<string, int, QteSequenceSnapshot> QteProgressed;
        public event Action<string, int, PerformanceBranchDefinition, CounterplayNarrativeOutcome> BranchResolved;
        public event Action<string, int, string> NextOutcomeRequested;
        public event Action<string, int> PerformanceCancelled;
        public event Action<string, int, PerformanceHeldItemPolicy> PerformanceStarted;

        public float AccessibilityThresholdScale => _settings == null ? 1f : _settings.qteThresholdScale;

        public bool InteractIsPressed
        {
            get
            {
                bool pulse = _interactPulse;
                _interactPulse = false;
                return pulse;
            }
        }

        public void Configure(
            PlayerInputRouter inputRouter,
            LoopDirector loopDirector,
            MonoBehaviour performancePlayer,
            GameSettingsStore settingsStore = null)
        {
            Unsubscribe();
            input = inputRouter;
            loop = loopDirector;
            performancePlayerBehaviour = performancePlayer;
            _settingsStore = settingsStore ?? new GameSettingsStore();
            ReloadSettings();
            ResolveAndInjectPlayer();
            if (isActiveAndEnabled)
            {
                Subscribe();
            }
        }

        public void ReloadSettings()
        {
            _settingsStore ??= new GameSettingsStore();
            _settings = _settingsStore.Load();
        }

        private void Awake()
        {
            ReloadSettings();
            ResolveAndInjectPlayer();
        }

        private void OnEnable()
        {
            Subscribe();
            ResolveAndInjectPlayer();
        }

        private void OnDisable()
        {
            Unsubscribe();
            _interactPulse = false;
        }

        public void OnPerformanceStarted(
            string outcomeId,
            int loopEpoch,
            PerformanceHeldItemPolicy heldItemPolicy)
        {
            _interactPulse = false;
            PerformanceStarted?.Invoke(outcomeId, loopEpoch, heldItemPolicy);
        }

        public void OnQteProgress(string outcomeId, int loopEpoch, in QteSequenceSnapshot snapshot)
        {
            QteProgressed?.Invoke(outcomeId, loopEpoch, snapshot);
        }

        public void OnBranchResolved(
            string outcomeId,
            int loopEpoch,
            in PerformanceBranchDefinition branch,
            CounterplayNarrativeOutcome narrativeOutcome)
        {
            if (string.Equals(outcomeId, "D21", StringComparison.Ordinal) &&
                branch.RequiresAllQteWindows)
            {
                loop?.MarkD21Seen();
            }

            BranchResolved?.Invoke(outcomeId, loopEpoch, branch, narrativeOutcome);
            if (!string.IsNullOrWhiteSpace(branch.NextOutcomeId))
            {
                NextOutcomeRequested?.Invoke(outcomeId, loopEpoch, branch.NextOutcomeId);
            }
        }

        public void OnPerformanceCancelled(string outcomeId, int loopEpoch)
        {
            _interactPulse = false;
            PerformanceCancelled?.Invoke(outcomeId, loopEpoch);
        }

        private void OnInteractPressed()
        {
            _interactPulse = true;
        }

        private void OnLoopResetCompleted(int loopIndex, string outcomeId)
        {
            _interactPulse = false;
            _performancePlayer?.PrepareLoopEpoch(loop == null ? loopIndex : loop.LoopEpoch);
        }

        private void ResolveAndInjectPlayer()
        {
            _performancePlayer = performancePlayerBehaviour as ICounterplayPerformanceHookTarget;
            if (_performancePlayer == null)
            {
                return;
            }

            _performancePlayer.ConfigureHooks(this, this);
            _performancePlayer.PrepareLoopEpoch(loop == null ? 0 : loop.LoopEpoch);
        }

        private void Subscribe()
        {
            if (_subscribed)
            {
                return;
            }

            if (input != null)
            {
                input.InteractPressed += OnInteractPressed;
            }

            if (loop != null)
            {
                loop.LoopResetCompleted += OnLoopResetCompleted;
            }

            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            if (input != null)
            {
                input.InteractPressed -= OnInteractPressed;
            }

            if (loop != null)
            {
                loop.LoopResetCompleted -= OnLoopResetCompleted;
            }

            _subscribed = false;
        }
    }
}
