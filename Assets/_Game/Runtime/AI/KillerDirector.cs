using System;
using ConGame.Domain.Counterplay;
using ConGame.Domain.Facts;
using ConGame.Domain.Interaction;
using ConGame.Runtime.Config;
using ConGame.Runtime.Counterplay;
using ConGame.Runtime.Facts;
using ConGame.Runtime.Lighting;
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
        private const float ReactiveIntentHoldSeconds = 2.5f;
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
        [SerializeField]
        private MonoBehaviour performanceLauncherBehaviour;
        [SerializeField]
        private KillerTacticalController tacticalController;

        [Header("Doorway Entry")]
        [SerializeField] private Transform entryInsideTarget;
        [SerializeField, Min(0.1f)] private float entryWalkSpeed = 1.05f;
        [SerializeField, Min(0.01f)] private float entryStopDistance = 0.04f;
        private Action<string> _killCallback;
        private Func<string, bool> _attackCommitCallback;
        private bool _liveHuntActive;
        private bool _attackCommitPending;
        private string _committedOutcomeId = string.Empty;
        private float _reactiveIntentRemaining;
        private IPerformancePlayer _performancePlayer;
        private IPerformanceLaunchRequest _performanceLauncher;
        public bool HasEnteredRoom { get; private set; }
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

        private bool _entryPrepared;
        private bool _entryWalking;
private float _entryReactionRemaining;

        public event Action<KillerState> StateChanged;
        public event Action<string> CounterplaySelected;
        public event Action<int> AimStarted;
        public event Action<int> AimHoldStarted;
        public event Action<int> AimCancelled;
        public event Action<string, int> ShotFired;
        public event Action<ConfirmedPerformanceIntent> ConfirmedPerformanceLaunching;

        public int ResetOrder => 30;

        public KillerState State { get; private set; } = KillerState.Dormant;
        public string SelectedOutcomeId { get; private set; }

        public float MotionSpeed { get; private set; }
        public bool IsEntryWalking => _entryWalking;
public bool EndingMode => _endingMode;
        public bool IsLiveHuntActive => _liveHuntActive && tacticalController != null && tacticalController.IsHuntActive;
        public KillerTacticalController TacticalController => tacticalController;
        public CounterplayCatalogAsset Catalog => catalog;

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

        /// <summary>
        /// 装配正式 Performance 启动入口(Presentation/M1 双人编排实现
        /// IPerformanceLaunchRequest)。未装配时确认型演出仍走既有
        /// IPerformancePlayer/攻击提交路径,行为不变。
        /// </summary>
        public void ConfigurePerformanceLauncher(IPerformanceLaunchRequest launcher)
        {
            _performanceLauncher = launcher;
            performanceLauncherBehaviour = launcher as MonoBehaviour;
        }

public void ConfigureEntry(Transform insideTarget, float walkSpeed)
        {
            entryInsideTarget = insideTarget;
            entryWalkSpeed = Mathf.Max(0.1f, walkSpeed);
        }

        public void ConfigureHunt(KillerTacticalController controller)
        {
            UnbindTacticalController();
            tacticalController = controller;
            BindTacticalController();
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
            _liveHuntActive = false;
            tacticalController?.StopHunt();
            string completedOutcomeId = string.IsNullOrEmpty(SelectedOutcomeId) ? "D01" : SelectedOutcomeId;
            Action<string> callback = _killCallback;
            _performancePlayer?.CancelAllForLoop(_loopEpoch);
            (_performanceLauncher as IPerformanceCancellable)?.CancelAllForLoop(_loopEpoch);
            if (generation != _performanceGeneration)
            {
                return false;
            }

            _killCallback = null;
            _attackCommitCallback = null;
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
            (_performanceLauncher as IPerformanceCancellable)?.CancelAllForLoop(_loopEpoch);
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

            _liveHuntActive = false;
            tacticalController?.StopHunt();
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
            if (_liveHuntActive)
            {
                if (playerTarget != null)
                {
                    tacticalController?.AlertToPosition(playerTarget.position, true);
                }

                return;
            }

            if (State == KillerState.PerformanceLocked)
            {
                int generation = ++_performanceGeneration;
                _performancePlayer?.CancelAllForLoop(_loopEpoch);
                if (generation != _performanceGeneration)
                {
                    return;
                }

                SetState(KillerState.Navigating);
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
            tacticalController ??= GetComponent<KillerTacticalController>();
            _performancePlayer = performancePlayerBehaviour as IPerformancePlayer;
            _performanceLauncher ??= performanceLauncherBehaviour as IPerformanceLaunchRequest;
            BindTacticalController();
        }

        private void OnDestroy()
        {
            UnbindTacticalController();
        }


        private void Update()
        {
            if (State == KillerState.Dormant)
            {
                return;
            }

            if (_entryWalking)
            {
                UpdateDoorwayEntry();
                if (!_entryWalking)
                {
                    _entryReactionRemaining = EntryReactionWindowSeconds;
                }

                return;
            }

            if (_entryPrepared && !_entrySelectionPending)
            {
                return;
            }

            if (_entrySelectionPending)
            {
                _entryReactionRemaining -= Time.deltaTime;
                if (_entryReactionRemaining <= 0f)
                {
                    _entrySelectionPending = false;
                    CounterplayDefinitionAsset selected = SelectInitialCounterplay();
                    if (IsLiveHuntRequested() && tacticalController == null)
                    {
                        AbortLiveHuntWithoutTacticalController();
                    }
                    else if (IsLiveHuntRequested())
                    {
                        StartLiveHunt(selected);
                    }
                    else
                    {
                        StartCounterplay(selected);
                    }
                }

                return;
            }

            if (_liveHuntActive)
            {
                MotionSpeed = tacticalController == null ? 0f : tacticalController.MotionSpeed;
                _reactiveIntentRemaining = Mathf.Max(0f, _reactiveIntentRemaining - Time.deltaTime);
                _continuousEvaluationRemaining -= Time.deltaTime;
                if (_continuousEvaluationRemaining <= 0f)
                {
                    _continuousEvaluationRemaining = ContinuousEvaluationInterval;
                    EvaluateLiveIntent();
                }

                TryLaunchUnderBedContact();
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

        public void BeginEntry(
            int loopEpoch,
            Action<string> killCallback,
            Func<string, bool> attackCommitCallback = null)
        {
            if (entryInsideTarget != null)
            {
                if (!_entryPrepared)
                {
                    PrepareEntry(loopEpoch, killCallback, attackCommitCallback);
                }
                else
                {
                    _loopEpoch = loopEpoch;
                    _killCallback = killCallback;
                    _attackCommitCallback = attackCommitCallback;
                }

                StopAgentForPerformance();
                _entryWalking = true;
                _entrySelectionPending = true;
                _entryReactionRemaining = EntryReactionWindowSeconds;
                HasEnteredRoom = true;
                SetState(KillerState.Navigating);
                return;
            }

            _loopEpoch = loopEpoch;
            _killCallback = killCallback;
            _attackCommitCallback = attackCommitCallback;
            _continuousEvaluationRemaining = 0f;
            gameObject.SetActive(true);
            SetState(KillerState.Perceiving);

            _entrySelectionPending = true;
            _entryReactionRemaining = EntryReactionWindowSeconds;
        }

        public void PrepareEntry(
            int loopEpoch,
            Action<string> killCallback,
            Func<string, bool> attackCommitCallback = null)
        {
            _loopEpoch = loopEpoch;
            _killCallback = killCallback;
            _attackCommitCallback = attackCommitCallback;
            _continuousEvaluationRemaining = 0f;
            _entrySelectionPending = false;
            _entryWalking = false;
            _liveHuntActive = false;
            _attackCommitPending = false;
            _committedOutcomeId = string.Empty;
            _reactiveIntentRemaining = 0f;
            MotionSpeed = 0f;
            gameObject.SetActive(true);
            tacticalController?.StopHunt();
            StopAgentForPerformance();
            SetState(KillerState.Perceiving);
            _entryPrepared = true;
            HasEnteredRoom = true;
        }


        public void HandleStimulus(KillerStimulus stimulus)
        {
            if (State == KillerState.Dormant || catalog == null || facts == null || _attackCommitPending)
            {
                return;
            }

            if (stimulus.Type == KillerStimulusType.TelevisionHeavyHit)
            {
                RequestSprintInterrupt();
            }

            if (State == KillerState.PerformanceLocked && !_liveHuntActive)
            {
                return;
            }

            FactSnapshot snapshot = facts.CaptureSnapshotForStimulus(stimulus);
            CounterplayDefinitionAsset selected = catalog.Select(CounterplayTriggerMode.Reactive, snapshot);
            if (_endingMode)
            {
                selected = FilterEndingDefinition(selected, snapshot);
            }

            // A player-confirmed pull can land while the killer is still entering
            // or observing the room. It uses the same paired route as a live hunt.
            if (stimulus.Type == KillerStimulusType.TrapPull && selected != null)
            {
                if (selected.OutcomeId == "D08" || selected.OutcomeId == "D11" || selected.OutcomeId == "D21")
                {
                    LaunchConfirmedFromLive(selected, PerformanceConfirmationKind.TrapTrigger);
                }
                else
                {
                    StartCounterplay(selected);
                }

                return;
            }

            if (_liveHuntActive)
            {
                if (stimulus.Type == KillerStimulusType.PlayerExposed && selected != null &&
                    selected.OutcomeId == "D08")
                {
                    LaunchConfirmedFromLive(selected, PerformanceConfirmationKind.ConfirmedContact);

                    return;
                }

                if (selected != null && selected.SortOrder < GetSelectedOrder())
                {
                    SelectLiveIntent(selected);
                    _reactiveIntentRemaining = ReactiveIntentHoldSeconds;
                }

                if (playerTarget != null &&
                    (stimulus.Type == KillerStimulusType.Glare ||
                     stimulus.Type == KillerStimulusType.IncomingProjectile))
                {
                    if (stimulus.Type == KillerStimulusType.Glare)
                    {
                        tacticalController?.NotifyGlare();
                    }
                    else
                    {
                        tacticalController?.AlertToPosition(playerTarget.position, false);
                    }
                }

                return;
            }

            if (selected != null && (_entrySelectionPending || selected.SortOrder < GetSelectedOrder()))
            {
                StartCounterplay(selected);
            }
        }

        private void LaunchConfirmedFromLive(
            CounterplayDefinitionAsset definition,
            PerformanceConfirmationKind confirmationKind)
        {
            if (definition == null)
            {
                return;
            }

            _liveHuntActive = false;
            tacticalController?.StopHunt();
            _entrySelectionPending = false;
            _entryPrepared = false;
            SelectedOutcomeId = definition.OutcomeId;
            CounterplaySelected?.Invoke(SelectedOutcomeId);
            LaunchConfirmedPerformance(confirmationKind, SelectedOutcomeId);
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
            _entryWalking = false;
            _entryPrepared = false;
            _liveHuntActive = false;
            MotionSpeed = 0f;
            _performanceGeneration++;
            _continuousEvaluationRemaining = 0f;
            _sprintInterruptUsed = false;
            _attackCommitCallback = null;
            tacticalController?.StopHunt();
            _performancePlayer?.CancelAllForLoop(context.PreviousEpoch);
            StopAgentForPerformance();
            SetState(KillerState.Dormant);
        }

        public void Restore(in LoopResetContext context)
        {
            transform.SetPositionAndRotation(_initialPosition, _initialRotation);
            SelectedOutcomeId = string.Empty;
            _killCallback = null;
            _attackCommitCallback = null;
            _loopEpoch = context.NextEpoch;
            _entrySelectionPending = false;
            _entryWalking = false;
            _entryPrepared = false;
            _liveHuntActive = false;
            HasEnteredRoom = false;
            MotionSpeed = 0f;
            _endingMode = false;
            _sprintInterruptUsed = false;
            tacticalController?.StopHunt();
            SetState(KillerState.Dormant);
        }
        public void Resume(in LoopResetContext context)
        {
            gameObject.SetActive(_initialActive);
        }

        private void StartCounterplay(CounterplayDefinitionAsset definition)
        {
            _liveHuntActive = false;
            tacticalController?.StopHunt();
            _entrySelectionPending = false;
            _entryPrepared = false;
            _entryWalking = false;
            MotionSpeed = 0f;
            int generation = ++_performanceGeneration;
            (_performanceLauncher as IPerformanceCancellable)?.CancelAllForLoop(_loopEpoch);
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


private void UpdateDoorwayEntry()
        {
            if (entryInsideTarget == null)
            {
                _entryWalking = false;
                MotionSpeed = 0f;
                return;
            }

            Vector3 target = entryInsideTarget.position;
            Vector3 offset = target - transform.position;
            offset.y = 0f;
            float distance = offset.magnitude;
            if (distance <= entryStopDistance)
            {
                transform.position = new Vector3(target.x, transform.position.y, target.z);
                _entryWalking = false;
                MotionSpeed = 0f;
                SetState(KillerState.Perceiving);
                return;
            }

            Vector3 direction = offset / distance;
            float step = Mathf.Min(distance, entryWalkSpeed * Time.deltaTime);
            transform.position += direction * step;
            transform.rotation = Quaternion.RotateTowards(transform.rotation,
                Quaternion.LookRotation(direction, Vector3.up), 240f * Time.deltaTime);
            MotionSpeed = entryWalkSpeed;
        }


        public bool ConfirmAttackCommit(int loopEpoch, string expectedOutcomeId = null)
        {
            if (!_liveHuntActive || !_attackCommitPending || loopEpoch != _loopEpoch ||
                (!string.IsNullOrWhiteSpace(expectedOutcomeId) &&
                 !string.Equals(expectedOutcomeId, _committedOutcomeId, StringComparison.Ordinal)))
            {
                return false;
            }

            return tacticalController != null && tacticalController.ConfirmAttackCommit(loopEpoch);
        }

        private bool IsLiveHuntRequested()
        {
            return !_endingMode && _attackCommitCallback != null;
        }

        private void AbortLiveHuntWithoutTacticalController()
        {
            _entryPrepared = false;
            _entryWalking = false;
            _running = false;
            _liveHuntActive = false;
            MotionSpeed = 0f;
            SetState(KillerState.Perceiving);
            Debug.LogError(
                "[ConGame] Live killer hunt requires a KillerTacticalController; entry execution was cancelled so player control remains available.",
                this);
        }

        private void StartLiveHunt(CounterplayDefinitionAsset definition)
        {
            _entrySelectionPending = false;
            _entryPrepared = false;
            _entryWalking = false;
            _running = false;
            _liveHuntActive = true;
            _attackCommitPending = false;
            _committedOutcomeId = string.Empty;
            _reactiveIntentRemaining = 0f;
            MotionSpeed = 0f;
            SelectedOutcomeId = definition == null ? "D01" : definition.OutcomeId;
            if (string.IsNullOrWhiteSpace(SelectedOutcomeId))
            {
                SelectedOutcomeId = "D01";
            }

            CounterplaySelected?.Invoke(SelectedOutcomeId);
            SetState(KillerState.Perceiving);
            tacticalController.BeginHunt(_loopEpoch, SelectedOutcomeId);
            GetComponent<KillerLampBrightener>()?.TryBrightenForRecoverableDark();
        }

        private void SelectLiveIntent(CounterplayDefinitionAsset definition)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.OutcomeId))
            {
                return;
            }

            SelectedOutcomeId = definition.OutcomeId;
            tacticalController?.SetOutcomeIntent(SelectedOutcomeId);
            CounterplaySelected?.Invoke(SelectedOutcomeId);
        }

        private void EvaluateLiveIntent()
        {
            if (!_liveHuntActive || _attackCommitPending || _reactiveIntentRemaining > 0f ||
                catalog == null || facts == null || State == KillerState.PerformanceLocked)
            {
                return;
            }

            FactSnapshot snapshot = facts.CaptureSnapshot();
            CounterplayDefinitionAsset selected = catalog.Select(CounterplayTriggerMode.Continuous, snapshot);
            if (_endingMode)
            {
                selected = FilterEndingDefinition(selected, snapshot);
            }

            CounterplayDefinitionAsset current = FindDefinition(SelectedOutcomeId);
            bool currentStillMatches = DefinitionConditionsMatch(current, snapshot);
            if (selected != null &&
                !string.Equals(selected.OutcomeId, SelectedOutcomeId, StringComparison.Ordinal) &&
                (!currentStillMatches || selected.SortOrder < GetSelectedOrder()))
            {
                SelectLiveIntent(selected);
            }
        }

        private static bool DefinitionConditionsMatch(
            CounterplayDefinitionAsset definition,
            in FactSnapshot snapshot)
        {
            if (definition == null)
            {
                return false;
            }

            FactCondition[] conditions = definition.Conditions;
            for (int i = 0; i < conditions.Length; i++)
            {
                if (!conditions[i].Matches(snapshot))
                {
                    return false;
                }
            }

            return true;
        }


        private void BindTacticalController()
        {
            if (tacticalController == null)
            {
                return;
            }

            tacticalController.ModeChanged -= OnTacticalModeChanged;
            tacticalController.AimStarted -= OnTacticalAimStarted;
            tacticalController.AimHoldStarted -= OnTacticalAimHoldStarted;
            tacticalController.AimCancelled -= OnTacticalAimCancelled;
            tacticalController.AttackCommitRequested -= OnTacticalAttackCommitRequested;
            tacticalController.ShotFired -= OnTacticalShotFired;
            tacticalController.ShotResolved -= OnTacticalShotResolved;
            tacticalController.ModeChanged += OnTacticalModeChanged;
            tacticalController.AimStarted += OnTacticalAimStarted;
            tacticalController.AimHoldStarted += OnTacticalAimHoldStarted;
            tacticalController.AimCancelled += OnTacticalAimCancelled;
            tacticalController.AttackCommitRequested += OnTacticalAttackCommitRequested;
            tacticalController.ShotFired += OnTacticalShotFired;
            tacticalController.ShotResolved += OnTacticalShotResolved;
        }

        private void UnbindTacticalController()
        {
            if (tacticalController == null)
            {
                return;
            }

            tacticalController.ModeChanged -= OnTacticalModeChanged;
            tacticalController.AimStarted -= OnTacticalAimStarted;
            tacticalController.AimHoldStarted -= OnTacticalAimHoldStarted;
            tacticalController.AimCancelled -= OnTacticalAimCancelled;
            tacticalController.AttackCommitRequested -= OnTacticalAttackCommitRequested;
            tacticalController.ShotFired -= OnTacticalShotFired;
            tacticalController.ShotResolved -= OnTacticalShotResolved;
        }

        private void OnTacticalModeChanged(KillerTacticalMode mode)
        {
            if (!_liveHuntActive)
            {
                return;
            }

            switch (mode)
            {
                case KillerTacticalMode.Investigating:
                case KillerTacticalMode.Approaching:
                    SetState(KillerState.Navigating);
                    break;
                case KillerTacticalMode.Committing:
                case KillerTacticalMode.Firing:
                    SetState(KillerState.PerformanceLocked);
                    break;
                case KillerTacticalMode.Observing:
                case KillerTacticalMode.Searching:
                case KillerTacticalMode.Aiming:
                case KillerTacticalMode.Recovering:
                    SetState(KillerState.Perceiving);
                    break;
            }
        }

        private void OnTacticalAimStarted(int loopEpoch)
        {
            if (_liveHuntActive && loopEpoch == _loopEpoch)
            {
                AimStarted?.Invoke(loopEpoch);
            }
        }

        private void OnTacticalAimCancelled(int loopEpoch)
        {
            if (_liveHuntActive && loopEpoch == _loopEpoch)
            {
                AimCancelled?.Invoke(loopEpoch);
            }
        }

        private void OnTacticalAimHoldStarted(int loopEpoch)
        {
            if (_liveHuntActive && loopEpoch == _loopEpoch)
            {
                AimHoldStarted?.Invoke(loopEpoch);
            }
        }

        private bool OnTacticalAttackCommitRequested(string outcomeId, int loopEpoch)
        {
            if (!_liveHuntActive || _attackCommitPending || loopEpoch != _loopEpoch)
            {
                return false;
            }

            _committedOutcomeId = string.IsNullOrWhiteSpace(outcomeId) ? "D01" : outcomeId;
            SelectedOutcomeId = _committedOutcomeId;
            _attackCommitPending = true;
            _reactiveIntentRemaining = 0f;
            SetState(KillerState.PerformanceLocked);
            bool accepted = _attackCommitCallback?.Invoke(_committedOutcomeId) ?? false;
            if (!accepted)
            {
                _attackCommitPending = false;
                _committedOutcomeId = string.Empty;
            }
            else
            {
                LaunchConfirmedPerformance(PerformanceConfirmationKind.AttackCommit, _committedOutcomeId);
            }

            return accepted;
        }

        private void TryLaunchUnderBedContact()
        {
            if (_performanceLauncher == null || playerTarget == null ||
                State == KillerState.PerformanceLocked || SelectedOutcomeId != "D11")
            {
                return;
            }

            Vector3 delta = playerTarget.position - transform.position;
            delta.y = 0f;
            if (delta.sqrMagnitude > 1.5f * 1.5f)
            {
                return;
            }

            LaunchConfirmedPerformance(PerformanceConfirmationKind.ConfirmedContact, "D11");
        }

        /// <summary>
        /// 已确认演出的统一启动点:停止 Agent、按确认当帧双方位姿生成
        /// PerformanceAnchorPair,经 IPerformanceLaunchRequest 交给正式编排,
        /// 并广播语义事件。普通搜索/调查/瞄准永远不会走到这里。
        /// </summary>
        private void LaunchConfirmedPerformance(PerformanceConfirmationKind kind, string outcomeId)
        {
            if (string.IsNullOrWhiteSpace(outcomeId))
            {
                outcomeId = "D01";
            }

            // An ambush or sideways blanket pull is confirmed from its actual contact
            // pose. Inventing mutually-facing yaws here rejects valid rear/side contact
            // against the 10-degree drift tolerance before choreography can even start.
            PerformanceAnchorPair anchors = PerformanceAnchorPair.CaptureCurrent(transform, playerTarget);
            ConfirmedPerformanceIntent intent =
                new ConfirmedPerformanceIntent(outcomeId, _loopEpoch, kind, in anchors);
            bool usesPairedPerformance = outcomeId == "D08" || outcomeId == "D11" || outcomeId == "D21";
            if (!usesPairedPerformance)
            {
                // Ordinary shots stay with the tactical commit/fire lifecycle.
                ConfirmedPerformanceLaunching?.Invoke(intent);
                return;
            }
            _liveHuntActive = false;
            _entryWalking = false;
            _running = false;
            tacticalController?.StopHunt();
            StopAgentForPerformance();
            SetState(KillerState.PerformanceLocked);
            _performanceLauncher?.NotifyLoopEpoch(_loopEpoch);
            bool pairedStarted = _performanceLauncher != null && _performanceLauncher.TryLaunch(in intent);
            if (!pairedStarted)
            {
                CounterplayDefinitionAsset fallback = FindDefinition("D01");
                StartCounterplay(fallback);
            }
            ConfirmedPerformanceLaunching?.Invoke(intent);
        }

        private void OnTacticalShotFired(string outcomeId, int loopEpoch)
        {
            if (_liveHuntActive && loopEpoch == _loopEpoch)
            {
                ShotFired?.Invoke(outcomeId, loopEpoch);
            }
        }

        private void OnTacticalShotResolved(string outcomeId, int loopEpoch)
        {
            if (_liveHuntActive && loopEpoch == _loopEpoch)
            {
                CompleteCurrentOutcome(outcomeId);
            }
        }
}
}
