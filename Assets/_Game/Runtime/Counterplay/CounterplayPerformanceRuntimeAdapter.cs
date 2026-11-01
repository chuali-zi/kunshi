using System;
using ConGame.Domain.Counterplay;
using ConGame.Runtime.Input;
using ConGame.Runtime.Loop;
using ConGame.Runtime.Player;
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
        private bool _explicitHooksConfigured;
        private int _lootWindowControllerCount;

        public event Action<string, int, QteSequenceSnapshot> QteProgressed;
        public event Action<string, int, PerformanceBranchDefinition, CounterplayNarrativeOutcome> BranchResolved;
        public event Action<string, int, string> NextOutcomeRequested;
        public event Action<string, int> OutcomeCompletionRequested;
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

        public void ConfigurePerformanceHooks(ICounterplayPerformanceHookTarget target)
        {
            _explicitHooksConfigured = target != null;
            _performancePlayer = target;
            target?.ConfigureHooks(this, this);
            target?.PrepareLoopEpoch(loop == null ? 0 : loop.LoopEpoch);
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
            loop?.BeginConfirmedPerformance();
            loop?.SetCounterplayInputWindows(ResolvePerformanceWindows(outcomeId));
            PerformanceStarted?.Invoke(outcomeId, loopEpoch, heldItemPolicy);
        }

        public void OnQteProgress(string outcomeId, int loopEpoch, in QteSequenceSnapshot snapshot)
        {
            QteProgressed?.Invoke(outcomeId, loopEpoch, snapshot);
        }

        public bool OnBranchResolved(
            string outcomeId,
            int loopEpoch,
            in PerformanceBranchDefinition branch,
            CounterplayNarrativeOutcome narrativeOutcome)
        {
            if (string.Equals(outcomeId, "D21", StringComparison.Ordinal) &&
                branch.MarksD21Seen)
            {
                loop?.MarkD21Seen();
            }

            BranchResolved?.Invoke(outcomeId, loopEpoch, branch, narrativeOutcome);
            if (!string.IsNullOrWhiteSpace(branch.NextOutcomeId) &&
                !string.Equals(outcomeId, branch.NextOutcomeId, StringComparison.Ordinal))
            {
                NextOutcomeRequested?.Invoke(outcomeId, loopEpoch, branch.NextOutcomeId);
            }

            if (_lootWindowControllerCount <= 0 || !branch.RequiresAllQteWindows)
            {
                return false;
            }

            bool d08LootWindow = string.Equals(outcomeId, "D08", StringComparison.Ordinal) &&
                                 string.Equals(branch.BranchId, "disarm_window", StringComparison.Ordinal);
            bool d22LootWindow = string.Equals(outcomeId, "D22", StringComparison.Ordinal) &&
                                 string.Equals(branch.BranchId, "single_round_dodged", StringComparison.Ordinal);
            return d08LootWindow || d22LootWindow;
        }

        public void RegisterLootWindowController()
        {
            _lootWindowControllerCount++;
        }

        public void UnregisterLootWindowController()
        {
            _lootWindowControllerCount = Math.Max(0, _lootWindowControllerCount - 1);
        }

        public void RequestNextOutcome(string outcomeId, int loopEpoch, string nextOutcomeId)
        {
            if (string.IsNullOrWhiteSpace(nextOutcomeId) ||
                string.Equals(outcomeId, nextOutcomeId, StringComparison.Ordinal))
            {
                return;
            }

            NextOutcomeRequested?.Invoke(outcomeId, loopEpoch, nextOutcomeId);
        }

        public void RequestOutcomeCompletion(string outcomeId, int loopEpoch, bool markD21Seen = false)
        {
            if (string.IsNullOrWhiteSpace(outcomeId))
            {
                return;
            }

            if (markD21Seen && string.Equals(outcomeId, "D21", StringComparison.Ordinal))
            {
                loop?.MarkD21Seen();
            }

            OutcomeCompletionRequested?.Invoke(outcomeId, loopEpoch);
        }

        public void SetCounterplayInputWindows(CounterplayInputWindow windows)
        {
            loop?.SetCounterplayInputWindows(windows);
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
            if (_explicitHooksConfigured)
            {
                return;
            }

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

        private CounterplayInputWindow ResolvePerformanceWindows(string outcomeId)
        {
            if (string.Equals(outcomeId, "D08", StringComparison.Ordinal))
            {
                return CounterplayInputWindow.Qte;
            }

            if (string.Equals(outcomeId, "D21", StringComparison.Ordinal))
            {
                // Direct legacy callers still receive the historical grab window.
                // Production paired playback explicitly owns the timing and starts
                // with no input window until its drop semantic event.
                return _performancePlayer == null
                    ? CounterplayInputWindow.Grab
                    : CounterplayInputWindow.None;
            }

            if (string.Equals(outcomeId, "D22", StringComparison.Ordinal))
            {
                return CounterplayInputWindow.Use;
            }

            return CounterplayInputWindow.None;
        }
    }
}
