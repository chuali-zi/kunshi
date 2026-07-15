using System;
using ConGame.Domain.Counterplay;
using ConGame.Domain.Facts;
using ConGame.Runtime.Config;
using ConGame.Runtime.Counterplay;
using ConGame.Runtime.Facts;
using ConGame.Runtime.Loop;
using UnityEngine;
using UnityEngine.AI;

namespace ConGame.Runtime.AI
{

    [DisallowMultipleComponent]
    public sealed class KillerDirector : MonoBehaviour, ILoopResettable
    {
        private const float ContinuousEvaluationInterval = 0.1f;

        [SerializeField] private NavMeshAgent agent;
        [SerializeField] private Transform playerTarget;
        [SerializeField] private GameplayFactService facts;
        [SerializeField] private CounterplayCatalogAsset catalog;
        [SerializeField] private GameConfigAsset config;
        [SerializeField] private MonoBehaviour performancePlayerBehaviour;

        private Action<string> _killCallback;
        private IPerformancePlayer _performancePlayer;
        private Vector3 _initialPosition;
        private Quaternion _initialRotation;
        private bool _initialActive;
        private float _remainingDelay;
        private float _continuousEvaluationRemaining;
        private bool _running;
        private int _loopEpoch;

        public event Action<string> CounterplaySelected;

        public int ResetOrder => 30;

        public string SelectedOutcomeId { get; private set; }

        public void Configure(
            NavMeshAgent navMeshAgent,
            Transform target,
            GameplayFactService factService,
            CounterplayCatalogAsset counterplayCatalog,
            GameConfigAsset gameConfig,
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

        private void Awake()
        {
            agent ??= GetComponent<NavMeshAgent>();
            _performancePlayer = performancePlayerBehaviour as IPerformancePlayer;
        }

        private void Update()
        {
            if (!_running)
            {
                return;
            }

            if (agent != null && agent.enabled && agent.isOnNavMesh && playerTarget != null)
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
                _running = false;
                _killCallback?.Invoke(string.IsNullOrEmpty(SelectedOutcomeId) ? "D01" : SelectedOutcomeId);
            }
        }

        public void BeginEntry(int loopEpoch, Action<string> killCallback)
        {
            _loopEpoch = loopEpoch;
            _killCallback = killCallback;
            _continuousEvaluationRemaining = 0f;
            gameObject.SetActive(true);

            if (agent != null)
            {
                agent.enabled = true;
                if (agent.isOnNavMesh && playerTarget != null)
                {
                    agent.SetDestination(playerTarget.position);
                }
            }

            FactSnapshot snapshot = facts == null ? FactSnapshot.CreateBaseline() : facts.CaptureSnapshot();
            StartCounterplay(catalog == null ? null : catalog.Select(CounterplayTriggerMode.Entry, snapshot));
        }

        public void HandleStimulus(KillerStimulus stimulus)
        {
            if (!_running || catalog == null || facts == null)
            {
                return;
            }

            FactSnapshot snapshot = facts.CaptureSnapshotForStimulus(stimulus);
            CounterplayDefinitionAsset selected = catalog.Select(CounterplayTriggerMode.Reactive, snapshot);
            if (selected != null && selected.SortOrder < GetSelectedOrder())
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
            _continuousEvaluationRemaining = 0f;
            _performancePlayer?.CancelAllForLoop(context.PreviousEpoch);
            if (agent != null && agent.enabled)
            {
                if (agent.isOnNavMesh)
                {
                    agent.ResetPath();
                }

                agent.enabled = false;
            }
        }

        public void Restore(in LoopResetContext context)
        {
            transform.SetPositionAndRotation(_initialPosition, _initialRotation);
            SelectedOutcomeId = string.Empty;
            _killCallback = null;
            _loopEpoch = context.NextEpoch;
        }

        public void Resume(in LoopResetContext context)
        {
            gameObject.SetActive(_initialActive);
        }

        private void StartCounterplay(CounterplayDefinitionAsset definition)
        {
            SelectedOutcomeId = definition == null ? "D01" : definition.OutcomeId;
            CounterplaySelected?.Invoke(SelectedOutcomeId);

            if (definition != null && definition.Performance != null && _performancePlayer != null)
            {
                int performanceEpoch = _loopEpoch;
                bool started = _performancePlayer.Play(definition, () =>
                {
                    if (_loopEpoch == performanceEpoch)
                    {
                        _killCallback?.Invoke(SelectedOutcomeId);
                    }
                });
                if (started)
                {
                    _running = false;
                    return;
                }
            }

            _remainingDelay = config == null ? 2.5f : config.KillerCounterplayDelay;
            _running = true;
        }

        private void EvaluateContinuousCounterplay()
        {
            if (catalog == null || facts == null)
            {
                return;
            }

            FactSnapshot snapshot = facts.CaptureSnapshot();
            CounterplayDefinitionAsset selected = catalog.Select(CounterplayTriggerMode.Continuous, snapshot);
            if (selected != null && selected.SortOrder < GetSelectedOrder())
            {
                StartCounterplay(selected);
            }
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
                if (definition != null && string.Equals(definition.OutcomeId, SelectedOutcomeId, StringComparison.Ordinal))
                {
                    return definition.SortOrder;
                }
            }

            return int.MaxValue;
        }
    }
}
