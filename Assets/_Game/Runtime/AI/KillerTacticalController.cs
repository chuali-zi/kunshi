using System;
using ConGame.Domain.Facts;
using ConGame.Runtime.Config;
using ConGame.Runtime.Events;
using ConGame.Runtime.Facts;
using ConGame.Runtime.Interaction;
using ConGame.Runtime.Loop;
using UnityEngine;
using UnityEngine.AI;

namespace ConGame.Runtime.AI
{
    public enum KillerTacticalMode
    {
        Inactive = 0,
        Observing = 1,
        Investigating = 2,
        Searching = 3,
        Approaching = 4,
        Aiming = 5,
        Committing = 6,
        Firing = 7,
        Recovering = 8
    }

    [DisallowMultipleComponent]
    public sealed class KillerTacticalController : MonoBehaviour, ILoopResettable
    {
        private const float DestinationTolerance = 0.28f;
        private const float SearchStepSeconds = 1.6f;
        private const float ReplanPlayerDistance = 1.1f;

        [SerializeField] private NavMeshAgent agent;
        [SerializeField] private Transform playerTarget;
        [SerializeField] private Transform eyeOrigin;
        [SerializeField] private GameplayFactService facts;
        [SerializeField] private GameplayEventHub events;
        [SerializeField] private GameConfigAsset config;
        [SerializeField] private KillerPerceptionSensor perception;
        [SerializeField] private Transform[] tacticalAnchors = Array.Empty<Transform>();
        [SerializeField] private KillerAimFirePresentationSettings aimFirePresentationSettings =
            KillerAimFirePresentationSettings.Default;
        [SerializeField, Min(0.05f)] private float glareRecoverySeconds = 0.35f;

        private GameplayEventHub _subscribedEvents;
        private Vector3 _initialPosition;
        private Quaternion _initialRotation;
        private bool _initialActive;
        private bool _huntActive;
        private int _loopEpoch;
        private string _outcomeId = "D01";
        private float _detectionProgress;
        private float _aimProgress;
        private float _modeTimer;
        private float _repathRemaining;
        private float _searchStepRemaining;
        private float _shotImpactRemaining;
        private bool _commitIssued;
        private bool _shotResolved;
        private bool _sprintRequested;
        private bool _hasDestination;
        private Vector3 _destination;
        private bool _hasLastKnownPosition;
        private Vector3 _lastKnownPosition;
        private Vector3 _lastPlannedPlayerPosition;
        private int _searchAnchorIndex;
        private float _flashlightVisibilityRemaining;
        private float _searchRevealRemaining;
        private float _hitMemoryRemaining;
        private int _hitReactionCount;
        private NavMeshPath _anchorValidationPath;
        private readonly AimFirePresentationTracker _aimFirePresentation = new AimFirePresentationTracker();
        public event Action<KillerTacticalMode> ModeChanged;
        public event Action<int> AimStarted;
        public event Action<int> AimHoldStarted;
        public event Action<int> AimCancelled;
        public event Func<string, int, bool> AttackCommitRequested;
        public event Action<string, int> ShotFired;
        public event Action<string, int> ShotResolved;

        public int ResetOrder => 32;
        public KillerTacticalMode Mode { get; private set; } = KillerTacticalMode.Inactive;
        public bool IsHuntActive => _huntActive;
        public bool HasVisualContact { get; private set; }
        public bool HasLastKnownPosition => _hasLastKnownPosition;
        public Vector3 LastKnownPosition => _lastKnownPosition;
        public float AimProgress => _aimProgress;
        public float DetectionProgress => _detectionProgress;
        public float MotionSpeed { get; private set; }
        public Vector3 CurrentDestination => _destination;
        public bool HasDestination => _hasDestination;
        public KillerAimFirePresentationSettings AimFirePresentation => aimFirePresentationSettings.Validated();

        private KillerHuntSettings Settings =>
            (config == null ? KillerHuntSettings.Default : config.KillerHunt).Validated();

        public void Configure(
            NavMeshAgent navMeshAgent,
            Transform target,
            Transform eye,
            GameplayFactService factService,
            GameplayEventHub eventHub,
            GameConfigAsset gameConfig,
            Transform[] anchors = null)
        {
            agent = navMeshAgent;
            playerTarget = target;
            eyeOrigin = eye;
            facts = factService;
            events = eventHub;
            config = gameConfig;
            tacticalAnchors = anchors ?? Array.Empty<Transform>();
            _anchorValidationPath ??= new NavMeshPath();
            EnsurePerception();
            perception.Configure(eyeOrigin, playerTarget, facts, ~0);
            RebindEvents();
        }

        public void ConfigureAimFirePresentation(in KillerAimFirePresentationSettings settings)
        {
            aimFirePresentationSettings = settings.Validated();
        }

        public void BeginHunt(int loopEpoch, string outcomeId)
        {
            _loopEpoch = loopEpoch;
            _outcomeId = string.IsNullOrWhiteSpace(outcomeId) ? "D01" : outcomeId;
            _huntActive = true;
            _flashlightVisibilityRemaining = 0f;
            _searchRevealRemaining = 0f;
            _hitMemoryRemaining = 0f;
            _hitReactionCount = 0;
            _commitIssued = false;
            _shotResolved = false;
            _sprintRequested = false;
            _detectionProgress = 0f;
            _aimProgress = 0f;
            _modeTimer = Settings.InvestigatePauseSeconds;
            _repathRemaining = 0f;
            _searchStepRemaining = 0f;
            _shotImpactRemaining = 0f;
            _hasDestination = false;
            _hasLastKnownPosition = false;
            _searchAnchorIndex = 0;
            HasVisualContact = false;
            MotionSpeed = 0f;
            _aimFirePresentation.Reset();
            EnsurePerception();
            perception.Configure(eyeOrigin, playerTarget, facts, ~0);
            EnableNavigation();
            StopNavigation(false);
            SetMode(KillerTacticalMode.Observing);
        }

        public void SetOutcomeIntent(string outcomeId)
        {
            if (_huntActive && Mode != KillerTacticalMode.Committing && Mode != KillerTacticalMode.Firing &&
                !string.IsNullOrWhiteSpace(outcomeId))
            {
                _outcomeId = outcomeId;
            }
        }

        public void SetFlashlightTargetVisible(float seconds)
        {
            if (_huntActive)
            {
                _flashlightVisibilityRemaining = Mathf.Max(_flashlightVisibilityRemaining, seconds);
            }
        }



        public void StopHunt()
        {
            CancelAimIfNeeded();
            _aimFirePresentation.Reset();
            _huntActive = false;
            _flashlightVisibilityRemaining = 0f;
            _searchRevealRemaining = 0f;
            _hitMemoryRemaining = 0f;
            _hitReactionCount = 0;
            _commitIssued = false;
            _shotResolved = false;
            _sprintRequested = false;
            _detectionProgress = 0f;
            _aimProgress = 0f;
            _modeTimer = 0f;
            _repathRemaining = 0f;
            _searchStepRemaining = 0f;
            _shotImpactRemaining = 0f;
            _hasDestination = false;
            _hasLastKnownPosition = false;
            _searchAnchorIndex = 0;
            HasVisualContact = false;
            MotionSpeed = 0f;
            StopNavigation(true);
            SetMode(KillerTacticalMode.Inactive);
        }

        public bool ConfirmAttackCommit(int loopEpoch)
        {
            if (!_huntActive || loopEpoch != _loopEpoch || Mode != KillerTacticalMode.Committing)
            {
                return false;
            }

            SetMode(KillerTacticalMode.Firing);
            _shotImpactRemaining = Settings.ShotImpactDelay;
            if (_aimFirePresentation.TryCommitFire(loopEpoch))
            {
                ShotFired?.Invoke(_outcomeId, _loopEpoch);
            }

            return true;
        }

        public void AlertToPosition(Vector3 position, bool sprint)
        {
            if (!_huntActive || Mode == KillerTacticalMode.Committing || Mode == KillerTacticalMode.Firing)
            {
                return;
            }

            Remember(position);
            _sprintRequested |= sprint;
            BeginInvestigating(position);
        }

        public void NotifyGlare()
        {
            if (!_huntActive || Mode == KillerTacticalMode.Committing || Mode == KillerTacticalMode.Firing)
            {
                return;
            }

            CancelAimIfNeeded();
            StopNavigation(false);
            _detectionProgress = 0f;
            _aimProgress = 0f;
            _modeTimer = Mathf.Max(0.05f, glareRecoverySeconds);
            SetMode(KillerTacticalMode.Recovering);
        }

        public void NotifyHit(in HitInfo hit)
        {
            if (!_huntActive || Mode == KillerTacticalMode.Committing || Mode == KillerTacticalMode.Firing)
            {
                return;
            }

            _hitMemoryRemaining = 4f;
            _hitReactionCount++;
            if (playerTarget != null)
            {
                Remember(playerTarget.position);
            }

            if (_hitReactionCount > 2)
            {
                _sprintRequested = true;
                return;
            }

            CancelAimIfNeeded();
            StopNavigation(false);
            _detectionProgress = 0f;
            _aimProgress = 0f;
            _modeTimer = Settings.HitRecoverySeconds / _hitReactionCount;
            SetMode(KillerTacticalMode.Recovering);
        }

        private void Awake()
        {
            agent ??= GetComponent<NavMeshAgent>();
            _anchorValidationPath ??= new NavMeshPath();
            EnsurePerception();
        }

        private void OnEnable()
        {
            RebindEvents();
        }

        private void OnDisable()
        {
            UnbindEvents();
        }

        private void Update()
        {
            if (!_huntActive || playerTarget == null || perception == null)
            {
                MotionSpeed = 0f;
                return;
            }

            KillerHuntSettings settings = Settings;
            KillerPerceptionSample sample = perception.Evaluate(settings);
            _flashlightVisibilityRemaining = Mathf.Max(0f, _flashlightVisibilityRemaining - Time.deltaTime);
            _searchRevealRemaining = Mathf.Max(0f, _searchRevealRemaining - Time.deltaTime);
            _hitMemoryRemaining = Mathf.Max(0f, _hitMemoryRemaining - Time.deltaTime);
            if (_hitMemoryRemaining <= 0f)
            {
                _hitReactionCount = 0;
            }

            if (!sample.CanSeePlayer &&
                (_flashlightVisibilityRemaining > 0f || _searchRevealRemaining > 0f))
            {
                Vector3 targetPoint = playerTarget.position + Vector3.up * 1.15f;
                sample = new KillerPerceptionSample(
                    true,
                    true,
                    true,
                    Vector3.Distance(eyeOrigin == null ? transform.position : eyeOrigin.position, targetPoint),
                    targetPoint);
            }
            HasVisualContact = sample.CanSeePlayer;
            if (HasVisualContact)
            {
                Remember(playerTarget.position);
            }

            _repathRemaining = Mathf.Max(0f, _repathRemaining - Time.deltaTime);
            MotionSpeed = 0f;

            switch (Mode)
            {
                case KillerTacticalMode.Observing:
                    TickObserving(sample, settings);
                    break;
                case KillerTacticalMode.Investigating:
                    TickInvestigating(sample, settings);
                    break;
                case KillerTacticalMode.Searching:
                    TickSearching(sample, settings);
                    break;
                case KillerTacticalMode.Approaching:
                    TickApproaching(sample, settings);
                    break;
                case KillerTacticalMode.Aiming:
                    TickAiming(sample, settings);
                    break;
                case KillerTacticalMode.Firing:
                    TickFiring();
                    break;
                case KillerTacticalMode.Recovering:
                    TickRecovering();
                    break;
            }

            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                Vector3 velocity = agent.velocity;
                velocity.y = 0f;
                MotionSpeed = Mathf.Max(MotionSpeed, velocity.magnitude);
            }
        }

        private void TickObserving(in KillerPerceptionSample sample, in KillerHuntSettings settings)
        {
            ScanRoom(22f);
            if (!sample.CanSeePlayer)
            {
                _detectionProgress = Mathf.Max(0f, _detectionProgress - Time.deltaTime);
                _modeTimer -= Time.deltaTime;
                if (_modeTimer <= 0f)
                {
                    BeginSearching(settings);
                }
                return;
            }

            _modeTimer = settings.InvestigatePauseSeconds;
            TurnToward(sample.TargetPoint, 260f);
            _detectionProgress += Time.deltaTime;
            if (_detectionProgress < settings.DetectionSeconds)
            {
                return;
            }

            if (sample.Distance <= settings.ShootRange)
            {
                BeginAiming();
            }
            else
            {
                BeginApproaching(sample.TargetPoint, settings);
            }
        }

        private void TickInvestigating(in KillerPerceptionSample sample, in KillerHuntSettings settings)
        {
            if (TryAcquireVisiblePlayer(sample, settings))
            {
                return;
            }

            if (!MoveToDestination(_sprintRequested ? settings.SprintInterruptSpeed : settings.PatrolSpeed))
            {
                return;
            }

            _sprintRequested = false;
            _modeTimer -= Time.deltaTime;
            ScanRoom(55f);
            if (_modeTimer <= 0f)
            {
                BeginSearching(settings);
            }
        }

        private void TickSearching(in KillerPerceptionSample sample, in KillerHuntSettings settings)
        {
            if (TryAcquireVisiblePlayer(sample, settings))
            {
                return;
            }

            _modeTimer -= Time.deltaTime;
            _searchStepRemaining -= Time.deltaTime;
            bool reachedSearchPoint = _hasDestination && MoveToDestination(settings.PatrolSpeed);
            if (!_hasDestination)
            {
                ScanRoom(70f);
            }

            if (reachedSearchPoint && TryRevealHiddenPlayerAtSearchAnchor(settings))
            {
                return;
            }

            if (_searchStepRemaining <= 0f)
            {
                _searchStepRemaining = SearchStepSeconds;
                SelectNextSearchAnchor();
            }

            if (_modeTimer <= 0f)
            {
                _modeTimer = settings.InvestigatePauseSeconds;
                _hasDestination = false;
                SetMode(KillerTacticalMode.Observing);
            }
        }

        private bool TryRevealHiddenPlayerAtSearchAnchor(in KillerHuntSettings settings)
        {
            if (facts == null || playerTarget == null)
            {
                return false;
            }

            FactSnapshot snapshot = facts.CaptureSnapshot();
            bool concealed = snapshot.PlayerPose == ConGame.Domain.Facts.PlayerPose.Hidden ||
                             (snapshot.PlayerZone == ConGame.Domain.Facts.PlayerZone.SofaRear &&
                              snapshot.PlayerPose == ConGame.Domain.Facts.PlayerPose.Crouched);
            if (!concealed)
            {
                return false;
            }

            Vector3 offset = playerTarget.position - transform.position;
            offset.y = 0f;
            if (offset.magnitude > settings.HiddenSearchRevealDistance)
            {
                return false;
            }

            Remember(playerTarget.position);
            _searchRevealRemaining = Mathf.Max(
                _searchRevealRemaining,
                settings.DetectionSeconds + settings.AimSeconds + 0.35f);
            _hasDestination = false;
            StopNavigation(false);
            return true;
        }


        private void TickApproaching(in KillerPerceptionSample sample, in KillerHuntSettings settings)
        {
            if (!sample.CanSeePlayer)
            {
                CancelAimIfNeeded();
                BeginInvestigating(_lastKnownPosition);
                return;
            }

            if (sample.Distance <= settings.ShootRange)
            {
                BeginAiming();
                return;
            }

            if (_repathRemaining <= 0f &&
                Vector3.Distance(_lastPlannedPlayerPosition, playerTarget.position) >= ReplanPlayerDistance)
            {
                PlanApproach(playerTarget.position, settings);
            }

            MoveToDestination(settings.PursuitSpeed);
        }

        private void TickAiming(in KillerPerceptionSample sample, in KillerHuntSettings settings)
        {
            StopNavigation(false);
            if (!sample.CanSeePlayer || sample.Distance > settings.ShootRange + 0.4f)
            {
                CancelAimIfNeeded();
                BeginInvestigating(_lastKnownPosition);
                return;
            }

            TurnToward(sample.TargetPoint, 360f);
            _aimProgress += Time.deltaTime;
            if (_aimProgress >= AimFirePresentation.AimEnterFallbackSeconds &&
                _aimFirePresentation.TryEnterHold(_loopEpoch))
            {
                AimHoldStarted?.Invoke(_loopEpoch);
            }

            if (_aimProgress < settings.AimSeconds || _commitIssued)
            {
                return;
            }

            _commitIssued = true;
            SetMode(KillerTacticalMode.Committing);
            bool accepted = AttackCommitRequested?.Invoke(_outcomeId, _loopEpoch) ?? false;
            if (!accepted)
            {
                _commitIssued = false;
                CancelAimIfNeeded();
                if (_hasLastKnownPosition)
                {
                    BeginInvestigating(_lastKnownPosition);
                }
                else
                {
                    _detectionProgress = 0f;
                    SetMode(KillerTacticalMode.Observing);
                }
            }
        }

        private void TickFiring()
        {
            if (_shotResolved)
            {
                return;
            }

            _shotImpactRemaining -= Time.deltaTime;
            if (_shotImpactRemaining > 0f)
            {
                return;
            }

            _shotResolved = true;
            ShotResolved?.Invoke(_outcomeId, _loopEpoch);
        }

        private void TickRecovering()
        {
            _modeTimer -= Time.deltaTime;
            if (_modeTimer > 0f)
            {
                return;
            }

            if (_hasLastKnownPosition)
            {
                BeginInvestigating(_lastKnownPosition);
            }
            else
            {
                SetMode(KillerTacticalMode.Observing);
            }
        }

        private bool TryAcquireVisiblePlayer(
            in KillerPerceptionSample sample,
            in KillerHuntSettings settings)
        {
            if (!sample.CanSeePlayer)
            {
                _detectionProgress = Mathf.Max(0f, _detectionProgress - Time.deltaTime);
                return false;
            }

            _detectionProgress += Time.deltaTime;
            if (_detectionProgress < settings.DetectionSeconds)
            {
                TurnToward(sample.TargetPoint, 240f);
                return true;
            }

            if (sample.Distance <= settings.ShootRange)
            {
                BeginAiming();
            }
            else
            {
                BeginApproaching(sample.TargetPoint, settings);
            }

            return true;
        }

        private void BeginAiming()
        {
            StopNavigation(false);
            _aimProgress = 0f;
            SetMode(KillerTacticalMode.Aiming);
            if (_aimFirePresentation.TryBeginAim(_loopEpoch))
            {
                AimStarted?.Invoke(_loopEpoch);
            }
        }

        private void BeginApproaching(Vector3 targetPoint, in KillerHuntSettings settings)
        {
            _aimProgress = 0f;
            SetMode(KillerTacticalMode.Approaching);
            PlanApproach(targetPoint, settings);
        }

        private void BeginInvestigating(Vector3 worldPosition)
        {
            CancelAimIfNeeded();
            _detectionProgress = 0f;
            _aimProgress = 0f;
            _modeTimer = Settings.InvestigatePauseSeconds;
            SetMode(KillerTacticalMode.Investigating);
            Transform anchor = FindClosestAnchor(worldPosition);
            SetAnchorDestination(anchor, worldPosition);
        }

        private void BeginSearching(in KillerHuntSettings settings)
        {
            _detectionProgress = 0f;
            _aimProgress = 0f;
            _modeTimer = settings.SearchSeconds;
            _searchStepRemaining = 0f;
            _hasDestination = false;
            SetMode(KillerTacticalMode.Searching);
        }

        private void PlanApproach(Vector3 targetPoint, in KillerHuntSettings settings)
        {
            Transform selected = FindBestStandoffAnchor(targetPoint, settings.StandoffDistance);
            _lastPlannedPlayerPosition = playerTarget == null ? targetPoint : playerTarget.position;
            _repathRemaining = settings.RepathSeconds;
            SetAnchorDestination(selected, targetPoint);
        }

        private Transform FindBestStandoffAnchor(Vector3 targetPoint, float standoffDistance)
        {
            if (tacticalAnchors == null || tacticalAnchors.Length == 0)
            {
                return null;
            }

            Transform best = null;
            float bestScore = float.PositiveInfinity;
            Vector3 sightPoint = targetPoint + Vector3.up * 1.15f;
            for (int i = 0; i < tacticalAnchors.Length; i++)
            {
                Transform candidate = tacticalAnchors[i];
                if (candidate == null)
                {
                    continue;
                }

                if (!TryGetAnchorDestination(candidate, out _))
                {
                    continue;
                }

                float ringError = Mathf.Abs(Vector3.Distance(candidate.position, targetPoint) - standoffDistance);
                float travelCost = Vector3.Distance(transform.position, candidate.position) * 0.12f;
                Vector3 eyePoint = candidate.position + Vector3.up *
                    (eyeOrigin == null ? 1.55f : Mathf.Max(1f, eyeOrigin.position.y - transform.position.y));
                float occlusionPenalty = perception.HasLineOfSightFrom(eyePoint, sightPoint) ? 0f : 20f;
                float score = ringError + travelCost + occlusionPenalty;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            return best;
        }

        private Transform FindClosestAnchor(Vector3 position)
        {
            if (tacticalAnchors == null || tacticalAnchors.Length == 0)
            {
                return null;
            }

            Transform best = null;
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < tacticalAnchors.Length; i++)
            {
                Transform candidate = tacticalAnchors[i];
                if (candidate == null)
                {
                    continue;
                }

                if (!TryGetAnchorDestination(candidate, out _))
                {
                    continue;
                }

                float distance = (candidate.position - position).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                }
            }

            return best;
        }

        private void SelectNextSearchAnchor()
        {
            if (tacticalAnchors == null || tacticalAnchors.Length == 0)
            {
                _hasDestination = false;
                return;
            }

            for (int i = 0; i < tacticalAnchors.Length; i++)
            {
                int index = (_searchAnchorIndex + i) % tacticalAnchors.Length;
                Transform candidate = tacticalAnchors[index];
                if (candidate == null)
                {
                    continue;
                }

                _searchAnchorIndex = (index + 1) % tacticalAnchors.Length;
                SetAnchorDestination(candidate, candidate.position);
                return;
            }

            _hasDestination = false;
        }

        private void SetAnchorDestination(Transform preferred, Vector3 fallbackReference)
        {
            if (!TryGetAnchorDestination(preferred, out Vector3 destination))
            {
                preferred = FindClosestAnchor(fallbackReference);
                if (!TryGetAnchorDestination(preferred, out destination))
                {
                    _hasDestination = false;
                    StopNavigation(false);
                    return;
                }
            }

            _destination = destination;
            _hasDestination = true;
            if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            {
                return;
            }

            agent.isStopped = false;
            if (!agent.SetDestination(_destination))
            {
                _hasDestination = false;
            }
        }

        private bool TryGetAnchorDestination(Transform anchor, out Vector3 destination)
        {
            destination = default;
            if (anchor == null)
            {
                return false;
            }

            destination = anchor.position;
            if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            {
                destination.y = transform.position.y;
                return true;
            }

            if (!NavMesh.SamplePosition(anchor.position, out NavMeshHit hit, 1f, agent.areaMask))
            {
                return false;
            }

            _anchorValidationPath ??= new NavMeshPath();
            if (!agent.CalculatePath(hit.position, _anchorValidationPath) ||
                _anchorValidationPath.status != NavMeshPathStatus.PathComplete)
            {
                return false;
            }

            destination = hit.position;
            return true;
        }

        private bool MoveToDestination(float speed)
        {
            if (!_hasDestination)
            {
                return true;
            }

            Vector3 offset = _destination - transform.position;
            offset.y = 0f;
            if (offset.magnitude <= DestinationTolerance)
            {
                _hasDestination = false;
                StopNavigation(false);
                return true;
            }

            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = false;
                agent.speed = Mathf.Max(0.1f, speed);
                return false;
            }

            Vector3 direction = offset.normalized;
            float step = Mathf.Min(offset.magnitude, Mathf.Max(0.1f, speed) * Time.deltaTime);
            transform.position += direction * step;
            TurnToward(transform.position + direction, 300f);
            MotionSpeed = Mathf.Max(MotionSpeed, speed);
            return false;
        }

        private void EnableNavigation()
        {
            if (agent == null)
            {
                return;
            }

            if (!agent.enabled)
            {
                agent.enabled = true;
            }

            if (!agent.isOnNavMesh && NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 1.5f,
                    NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
            }

            if (!agent.isOnNavMesh)
            {
                agent.enabled = false;
                return;
            }

            agent.updateRotation = true;
            agent.stoppingDistance = DestinationTolerance;
            agent.isStopped = true;
        }

        private void StopNavigation(bool disableAgent)
        {
            if (agent == null || !agent.enabled)
            {
                return;
            }

            if (agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }

            if (disableAgent)
            {
                agent.enabled = false;
            }
        }

        private void ScanRoom(float degreesPerSecond)
        {
            transform.Rotate(0f, degreesPerSecond * Time.deltaTime, 0f, Space.World);
        }

        private void TurnToward(Vector3 worldPoint, float degreesPerSecond)
        {
            Vector3 direction = worldPoint - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f)
            {
                return;
            }

            Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                degreesPerSecond * Time.deltaTime);
        }

        private void Remember(Vector3 position)
        {
            _hasLastKnownPosition = true;
            _lastKnownPosition = position;
        }

        private void CancelAimIfNeeded()
        {
            if (_aimFirePresentation.TryCancelAim(_loopEpoch))
            {
                AimCancelled?.Invoke(_loopEpoch);
            }

            _aimProgress = 0f;
        }

        private void SetMode(KillerTacticalMode mode)
        {
            if (Mode == mode)
            {
                return;
            }

            Mode = mode;
            ModeChanged?.Invoke(mode);
        }

        private void EnsurePerception()
        {
            perception ??= GetComponent<KillerPerceptionSensor>();
            if (perception == null)
            {
                perception = gameObject.AddComponent<KillerPerceptionSensor>();
            }
        }

        private void OnNoiseEmitted(NoiseEvent noise)
        {
            if (!_huntActive || Mode == KillerTacticalMode.Aiming ||
                Mode == KillerTacticalMode.Committing || Mode == KillerTacticalMode.Firing ||
                Mode == KillerTacticalMode.Recovering || HasVisualContact)
            {
                return;
            }

            KillerHuntSettings settings = Settings;
            float radius = noise.Level == NoiseLevel.Loud
                ? settings.LoudHearingRadius
                : settings.NormalHearingRadius;
            if (Vector3.Distance(transform.position, noise.Position) > radius)
            {
                return;
            }

            Remember(noise.Position);
            BeginInvestigating(noise.Position);
        }

        private void RebindEvents()
        {
            UnbindEvents();
            if (!isActiveAndEnabled || events == null)
            {
                return;
            }

            _subscribedEvents = events;
            _subscribedEvents.NoiseEmitted += OnNoiseEmitted;
        }

        private void UnbindEvents()
        {
            if (_subscribedEvents == null)
            {
                return;
            }

            _subscribedEvents.NoiseEmitted -= OnNoiseEmitted;
            _subscribedEvents = null;
        }

        public void CaptureInitialState()
        {
            _initialPosition = transform.position;
            _initialRotation = transform.rotation;
            _initialActive = gameObject.activeSelf;
        }

        public void Quiesce(in LoopResetContext context)
        {
            StopHunt();
            _loopEpoch = context.NextEpoch;
        }

        public void Restore(in LoopResetContext context)
        {
            transform.SetPositionAndRotation(_initialPosition, _initialRotation);
            _loopEpoch = context.NextEpoch;
            _outcomeId = "D01";
            _hasLastKnownPosition = false;
            _hasDestination = false;
            HasVisualContact = false;
            MotionSpeed = 0f;
            _aimFirePresentation.Reset();
            SetMode(KillerTacticalMode.Inactive);
        }

        public void Resume(in LoopResetContext context)
        {
            if (gameObject.activeSelf != _initialActive)
            {
                gameObject.SetActive(_initialActive);
            }
        }
    }
}
