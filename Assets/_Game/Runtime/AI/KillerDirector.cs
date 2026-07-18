using System;
using ConGame.Domain.Counterplay;
using ConGame.Domain.Facts;
using ConGame.Domain.Interaction;
using ConGame.Runtime.Config;
using ConGame.Runtime.Counterplay;
using ConGame.Runtime.Facts;
using ConGame.Runtime.Loop;
using UnityEngine;
using UnityEngine.AI;
namespace ConGame.Runtime.AI
{
    public enum KillerState
    {
        Dormant = 0,
        Perceiving = 1,
        Navigating = 2,
        PerformanceLocked = 3
    }

    [DisallowMultipleComponent]
    public sealed class KillerDirector : MonoBehaviour, ILoopResettable
    {
        private const float ContinuousEvaluationInterval = 0.1f;
        private const float EntryReactionWindowSeconds = 2f;
        [SerializeField]
        private NavMeshAgent agent;
        [SerializeField]
        private Transform playerTarget;
        [SerializeField]
        private GameplayFactService facts;
        [SerializeField]
        private CounterplayCatalogAsset catalog;
        [SerializeField]
        private GameConfigAsset config;
        [SerializeField]
        private MonoBehaviour performancePlayerBehaviour;

        private Action<string> _killCallback;
        private IPerformancePlayer _performancePlayer;
        private Vector3 _initialPosition;
        private Quaternion _initialRotation;
        private bool _initialActive;
        private float _remainingDelay;
        private float _continuousEvaluationRemaining;
        private bool _running;
        private int _loopEpoch;
        private bool _endingMode;
        private bool _sprintInterruptUsed;
        private int _performanceGeneration;
        private bool _entrySelectionPending;
        private float _entryReactionRemaining;

        public event Action<KillerState> StateChanged;
        public event Action<string> CounterplaySelected;

        public int ResetOrder => 30;

        public KillerState State { get; private set; } = KillerState.Dormant;
        public string SelectedOutcomeId { get; private set; }
        public bool EndingMode => _endingMode;

        public void Configure(NavMeshAgent navMeshAgent, Transform target, GameplayFactService factService,
                              CounterplayCatalogAsset counterplayCatalog, GameConfigAsset gameConfig,
                              MonoBehaviour performancePlayer = null)
        {
            agent = navMeshAgent;
            playerTarget = target;
            facts = factService;
            catalog = counterplayCatalog;
            config = gameConfig;
            performancePlayerBehaviour = performancePlayer;
            _performancePlayer = performancePlayerBehaviour as IPerformancePlayer;
        }

        public void SetEndingMode(bool enabled)
        {
            _endingMode = enabled;
        }

        public void ForceImmediateKill(string outcomeId = null)
        {
            if (State == KillerState.Dormant)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(outcomeId))
            {
                SelectedOutcomeId = outcomeId;
            }

            CompleteCurrentOutcome(SelectedOutcomeId);
        }

        public bool CompleteCurrentOutcome(string expectedOutcomeId = null)
        {
            if (State == KillerState.Dormant ||
                (!string.IsNullOrWhiteSpace(expectedOutcomeId) &&
                 !string.Equals(expectedOutcomeId, SelectedOutcomeId, StringComparison.Ordinal)))
            {
                return false;
            }

            int generation = ++_performanceGeneration;
            _running = false;
            _entrySelectionPending = false;
            string completedOutcomeId = string.IsNullOrEmpty(SelectedOutcomeId) ? "D01" : SelectedOutcomeId;
            Action<string> callback = _killCallback;
            _performancePlayer?.CancelAllForLoop(_loopEpoch);
            if (generation != _performanceGeneration)
            {
                return false;
            }

            _killCallback = null;
            callback?.Invoke(completedOutcomeId);
            return true;
        }

        public void SuspendPerformanceForInteractiveWindow()
        {
            if (State != KillerState.PerformanceLocked)
            {
                return;
            }

            _performanceGeneration++;
            _performancePlayer?.CancelAllForLoop(_loopEpoch);
            _running = false;
        }

        public bool TryStartOutcome(string outcomeId)
        {
            if (catalog == null || string.IsNullOrWhiteSpace(outcomeId) || State == KillerState.Dormant)
            {
                return false;
            }

            CounterplayDefinitionAsset definition = FindDefinition(outcomeId);
            if (definition == null)
            {
                return false;
            }

            StartCounterplay(definition);
            return true;
        }

        public void RequestSprintInterrupt()
        {
            if (State == KillerState.Dormant || _sprintInterruptUsed)
            {
                return;
            }

            _sprintInterruptUsed = true;
            if (State == KillerState.PerformanceLocked)
            {
                int generation = ++_performanceGeneration;
                _performancePlayer?.CancelAllForLoop(_loopEpoch);
                if (generation != _performanceGeneration)
                {
                    return;
                }

                SetState(KillerState.Navigating);
                if (generation != _performanceGeneration)
                {
                    return;
                }

                _remainingDelay = 0.6f;
                _running = true;
            }

            if (agent != null)
            {
                agent.enabled = true;
                agent.speed = Mathf.Max(agent.speed, 5.5f);
                if (agent.isOnNavMesh && playerTarget != null)
                {
                    agent.SetDestination(playerTarget.position);
                }
            }

            if (_running && State != KillerState.PerformanceLocked)
            {
                _remainingDelay = Mathf.Min(_remainingDelay, 0.6f);
            }
        }

        private void Awake()
        {
            agent ??= GetComponent<NavMeshAgent>();
            _performancePlayer = performancePlayerBehaviour as IPerformancePlayer;
        }

        private void Update()
        {
            if (State == KillerState.Dormant)
            {
                return;
            }

            if (_entrySelectionPending)
            {
                _entryReactionRemaining -= Time.deltaTime;
                if (_entryReactionRemaining <= 0f)
                {
                    _entrySelectionPending = false;
                    StartCounterplay(SelectInitialCounterplay());
                }

                return;
            }

            if (_running && agent != null && agent.enabled && agent.isOnNavMesh && playerTarget != null)
            {
                agent.SetDestination(playerTarget.position);
            }

            _continuousEvaluationRemaining -= Time.deltaTime;
            if (_continuousEvaluationRemaining <= 0f)
            {
                _continuousEvaluationRemaining = ContinuousEvaluationInterval;
                EvaluateContinuousCounterplay();
            }

            if (!_running)
            {
                return;
            }

            _remainingDelay -= Time.deltaTime;
            if (_remainingDelay <= 0f)
            {
                CompleteCurrentOutcome(SelectedOutcomeId);
            }
        }

        public void BeginEntry(int loopEpoch, Action<string> killCallback)
        {
            _loopEpoch = loopEpoch;
            _killCallback = killCallback;
            _continuousEvaluationRemaining = 0f;
            gameObject.SetActive(true);
            SetState(KillerState.Perceiving);

            if (agent != null)
            {
                agent.enabled = true;
                if (agent.isOnNavMesh && playerTarget != null)
                {
                    agent.SetDestination(playerTarget.position);
                }
            }

            _entrySelectionPending = true;
            _entryReactionRemaining = EntryReactionWindowSeconds;
        }

        public void HandleStimulus(KillerStimulus stimulus)
        {
            if (State == KillerState.Dormant || catalog == null || facts == null)
            {
                return;
            }

            if (stimulus.Type == KillerStimulusType.TelevisionHeavyHit)
            {
                RequestSprintInterrupt();
                if (State == KillerState.PerformanceLocked || _running)
                {
                    return;
                }
            }

            if (State == KillerState.PerformanceLocked)
            {
                return;
            }

            FactSnapshot snapshot = facts.CaptureSnapshotForStimulus(stimulus);
            CounterplayDefinitionAsset selected = catalog.Select(CounterplayTriggerMode.Reactive, snapshot);
            if (_endingMode)
            {
                selected = FilterEndingDefinition(selected, snapshot);
            }

            if (selected != null && (_entrySelectionPending || selected.SortOrder < GetSelectedOrder()))
            {
                StartCounterplay(selected);
            }
        }

        public void CaptureInitialState()
        {
            agent ??= GetComponent<NavMeshAgent>();
            _initialPosition = transform.position;
            _initialRotation = transform.rotation;
            _initialActive = gameObject.activeSelf;
        }

        public void Quiesce(in LoopResetContext context)
        {
            _running = false;
            _entrySelectionPending = false;
            _performanceGeneration++;
            _continuousEvaluationRemaining = 0f;
            _sprintInterruptUsed = false;
            _performancePlayer?.CancelAllForLoop(context.PreviousEpoch);
            StopAgentForPerformance();
            SetState(KillerState.Dormant);
        }

        public void Restore(in LoopResetContext context)
        {
            transform.SetPositionAndRotation(_initialPosition, _initialRotation);
            SelectedOutcomeId = string.Empty;
            _killCallback = null;
            _loopEpoch = context.NextEpoch;
            _entrySelectionPending = false;
            _endingMode = false;
            _sprintInterruptUsed = false;
            SetState(KillerState.Dormant);
        }

        public void Resume(in LoopResetContext context)
        {
            gameObject.SetActive(_initialActive);
        }

        private void StartCounterplay(CounterplayDefinitionAsset definition)
        {
            _entrySelectionPending = false;
            int generation = ++_performanceGeneration;
            SelectedOutcomeId = definition == null ? "D01" : definition.OutcomeId;
            string outcomeId = SelectedOutcomeId;
            CounterplaySelected?.Invoke(SelectedOutcomeId);
            if (generation != _performanceGeneration)
            {
                return;
            }

            SetState(KillerState.Navigating);
            if (generation != _performanceGeneration)
            {
                return;
            }

            if (definition != null && definition.Performance != null && _performancePlayer != null)
            {
                _performancePlayer.CancelAllForLoop(_loopEpoch);
                if (generation != _performanceGeneration)
                {
                    return;
                }

                StopAgentForPerformance();
                int performanceEpoch = _loopEpoch;
                SetState(KillerState.PerformanceLocked);
                if (generation != _performanceGeneration)
                {
                    return;
                }

                bool started = _performancePlayer.Play(definition, () =>
                                                                   {
                                                                       if (_loopEpoch == performanceEpoch &&
                                                                           generation == _performanceGeneration)
                                                                       {
                                                                           CompleteCurrentOutcome(outcomeId);
                                                                       }
                                                                   });
                if (generation != _performanceGeneration)
                {
                    return;
                }

                if (started)
                {
                    _running = false;
                    return;
                }

                SetState(KillerState.Navigating);
                if (generation != _performanceGeneration)
                {
                    return;
                }
            }

            _remainingDelay = config == null ? 2.5f : config.KillerCounterplayDelay;
            _running = true;
        }

        private void StopAgentForPerformance()
        {
            if (agent == null || !agent.enabled)
            {
                return;
            }

            if (agent.isOnNavMesh)
            {
                agent.ResetPath();
            }

            agent.enabled = false;
        }

        private void SetState(KillerState state)
        {
            if (State == state)
            {
                return;
            }

            State = state;
            StateChanged?.Invoke(state);
        }

        private void EvaluateContinuousCounterplay()
        {
            if (State == KillerState.PerformanceLocked || _entrySelectionPending || catalog == null || facts == null)
            {
                return;
            }

            FactSnapshot snapshot = facts.CaptureSnapshot();
            CounterplayDefinitionAsset selected = catalog.Select(CounterplayTriggerMode.Continuous, snapshot);
            if (_endingMode)
            {
                selected = FilterEndingDefinition(selected, snapshot);
            }

            if (selected != null && selected.SortOrder < GetSelectedOrder())
            {
                StartCounterplay(selected);
            }
        }

        private CounterplayDefinitionAsset SelectInitialCounterplay()
        {
            FactSnapshot snapshot = facts == null ? FactSnapshot.CreateBaseline() : facts.CaptureSnapshot();
            if (catalog == null)
            {
                return null;
            }

            CounterplayDefinitionAsset entry = catalog.Select(CounterplayTriggerMode.Entry, snapshot);
            CounterplayDefinitionAsset continuous = catalog.Select(CounterplayTriggerMode.Continuous, snapshot);
            CounterplayDefinitionAsset selected = entry;
            if (continuous != null && (selected == null || continuous.SortOrder < selected.SortOrder))
            {
                selected = continuous;
            }

            return _endingMode ? FilterEndingDefinition(selected, snapshot) : selected;
        }

        private CounterplayDefinitionAsset FilterEndingDefinition(
            CounterplayDefinitionAsset selected,
            in FactSnapshot snapshot)
        {
            if (selected != null && IsEndingAllowed(selected.OutcomeId))
            {
                return selected;
            }

            if (snapshot.IncomingProjectile)
            {
                return FindDefinition("D10") ?? selected;
            }

            if ((snapshot.HeldItemCategories & ItemCategory.Weapon) != 0)
            {
                return FindDefinition("D07") ?? selected;
            }

            return FindDefinition("D01") ?? selected;
        }

        private static bool IsEndingAllowed(string outcomeId)
        {
            return string.Equals(outcomeId, "D01", StringComparison.Ordinal) ||
                   string.Equals(outcomeId, "D07", StringComparison.Ordinal) ||
                   string.Equals(outcomeId, "D10", StringComparison.Ordinal);
        }

        private CounterplayDefinitionAsset FindDefinition(string outcomeId)
        {
            if (catalog == null)
            {
                return null;
            }

            for (int i = 0; i < catalog.Definitions.Count; i++)
            {
                CounterplayDefinitionAsset definition = catalog.Definitions[i];
                if (definition != null &&
                    string.Equals(definition.OutcomeId, outcomeId, StringComparison.Ordinal))
                {
                    return definition;
                }
            }

            return null;
        }

        private int GetSelectedOrder()
        {
            if (catalog == null || string.IsNullOrEmpty(SelectedOutcomeId))
            {
                return int.MaxValue;
            }

            for (int i = 0; i < catalog.Definitions.Count; i++)
            {
                CounterplayDefinitionAsset definition = catalog.Definitions[i];
                if (definition != null &&
                    string.Equals(definition.OutcomeId, SelectedOutcomeId, StringComparison.Ordinal))
                {
                    return definition.SortOrder;
                }
            }

            return int.MaxValue;
        }
    }
}
