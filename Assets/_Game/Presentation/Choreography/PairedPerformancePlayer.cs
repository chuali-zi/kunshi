using System;
using System.Collections;
using System.Collections.Generic;
using ConGame.Domain.Choreography;
using ConGame.Runtime.Counterplay;
using ConGame.Runtime.Interaction;
using ConGame.Domain.Counterplay;
using ConGame.Runtime.AI;
using ConGame.Runtime.Loop;
using ConGame.Runtime.Player;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace ConGame.Presentation.Choreography
{
    /// <summary>
    /// Paired (player + killer) performance orchestrator for D08/D11/D21. Implements
    /// the confirmed-launch contract from Runtime/AI (IPerformanceLaunchRequest) and
    /// adds what M1GreyboxPerformancePlayer lacks: authored two-sided anchors with
    /// launch error validation (0.15m/10°, D01 fallback on violation), same-frame
    /// root lock driven by the anchor, Contact/Detach semantic markers, stage-based
    /// progression with cancel boundaries, and residue-free cancel/LoopReset
    /// (graph stopped, root lock released, semantic cleanup events emitted atomically).
    /// Assembled exclusively through public Configure methods.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PairedPerformancePlayer : MonoBehaviour, IPerformanceLaunchRequest,
        ILoopResettable, ICounterplayPerformanceHookTarget, IPerformanceCancellable,
        ID21FlashlightGrapplePresentation
    {
        private sealed class AnimationSet
        {
            public AnimationClip PlayerClip;
            public AnimationClip KillerClip;
        }

        private sealed class StageAnimationSet
        {
            public AnimationClip PlayerClip;
            public AnimationClip KillerClip;
            public float SegmentStart;
            public float SegmentEnd;
        }

        private sealed class GroundedVisual
        {
            public Transform Root;
            public Transform GameplayRoot;
            public Vector3 OriginalLocalPosition;
            public Transform[] Bones;
            public bool IsPlayer;
        }

        [SerializeField] private Transform killer;
        [SerializeField] private PlayerMotor playerMotor;
        [SerializeField] private PlayerInteractor playerInteractor;
        [SerializeField] private AnimationClip d21FallPlayerClip;
        [SerializeField] private AnimationClip d21FallKillerClip;
        [SerializeField] private AnimationClip d21BlockLightKillerClip;
        [SerializeField] private AnimationClip d21BackupDrawFireKillerClip;

        private readonly Dictionary<string, PairedPerformanceAnchorBehaviour> _anchors =
            new Dictionary<string, PairedPerformanceAnchorBehaviour>(StringComparer.Ordinal);
        private readonly Dictionary<string, AnimationSet> _animations =
            new Dictionary<string, AnimationSet>(StringComparer.Ordinal);
        private readonly Dictionary<string, Dictionary<string, StageAnimationSet>> _stageAnimations =
            new Dictionary<string, Dictionary<string, StageAnimationSet>>(StringComparer.Ordinal);
        private readonly List<PairedPerformanceSemanticEvent> _pendingEvents =
            new List<PairedPerformanceSemanticEvent>(32);
        private readonly List<GroundedVisual> _d21GroundedVisuals = new List<GroundedVisual>(2);
        private readonly List<double> _pausedAnimationSpeeds = new List<double>(2);

        private PairedPerformanceEventDispatcher _dispatcher;
        private ICounterplayPerformanceInput _performanceInput;
        private ICounterplayPerformanceProgress _progress;
        private IPerformancePlayer _fallbackPlayer;
        private CounterplayDefinitionAsset _fallbackDefinition;
        private CounterplayPerformanceRuntimeAdapter _runtimeAdapter;
        private StolenLootDropController _lootWindow;

        private PairedPerformancePhaseMachine _machine;
        private Coroutine _routine;
        private PlayableGraph _animationGraph;
        private Animator _playerAnimationTarget;
        private AnimationClipPlayable _playerAnimationPlayable;
        private AnimationPlayableOutput _playerAnimationOutput;
        private bool _hasPlayerAnimationOutput;
        private bool _playerAnimationDetached;
        private Action _completed;
        private string _activeOutcomeId = string.Empty;
        private int _loopEpoch;
        private int _generation;
        private bool _rootsLocked;
        private bool _awaitingLootDecision;
        private bool _awaitingBlockLight;
        private PairedAnchorPose _playerLockPose;
        private PairedAnchorPose _killerLockPose;
        private string _activeAnimationStage = string.Empty;

        private void Awake()
        {
            CounterplayPerformanceRuntimeAdapter adapter =
                UnityEngine.Object.FindAnyObjectByType<CounterplayPerformanceRuntimeAdapter>();
            _runtimeAdapter = adapter;
            if (_runtimeAdapter != null)
            {
                _runtimeAdapter.BranchResolved += OnAdapterBranchResolved;
            }
            if (adapter != null)
            {
                adapter.ConfigurePerformanceHooks(this);
            }

            _lootWindow = UnityEngine.Object.FindAnyObjectByType<StolenLootDropController>();
            if (_lootWindow != null)
            {
                _lootWindow.D21FlashlightSuccess += OnD21FlashlightSuccess;
            }
            Events.SemanticEventEmitted += OnSemanticEvent;
        }

        private void OnDestroy()
        {
            if (_dispatcher != null)
            {
                _dispatcher.SemanticEventEmitted -= OnSemanticEvent;
            }
            if (_runtimeAdapter != null)
            {
                _runtimeAdapter.BranchResolved -= OnAdapterBranchResolved;
            }
            if (_lootWindow != null)
            {
                _lootWindow.D21FlashlightSuccess -= OnD21FlashlightSuccess;
            }
        }

        private void OnSemanticEvent(PairedPerformanceSemanticEvent semanticEvent)
        {
            _lootWindow?.NotifyPairedSemanticEvent(in semanticEvent);
            if (_machine == null || !_machine.IsRunning ||
                semanticEvent.LoopEpoch != _loopEpoch ||
                !string.Equals(semanticEvent.OutcomeId, _activeOutcomeId, StringComparison.Ordinal))
            {
                return;
            }

            if (string.Equals(semanticEvent.EventId, "D08.LootWindow.Open", StringComparison.Ordinal) ||
                string.Equals(semanticEvent.EventId, "D21.Pistol.Dropped", StringComparison.Ordinal) ||
                string.Equals(semanticEvent.EventId, "D21.Flashlight.Dropped", StringComparison.Ordinal))
            {
                _awaitingLootDecision = true;
                _machine.SetPaused(true);
                PauseAnimations();
                ReleaseRootLock();
            }

            if (string.Equals(_activeOutcomeId, "D21", StringComparison.Ordinal) &&
                string.Equals(semanticEvent.EventId, "D21.BlockLight.Begin", StringComparison.Ordinal))
            {
                _awaitingLootDecision = true;
                _awaitingBlockLight = true;
                _machine.SetPaused(true);
                PauseAnimations();
                ReleaseRootLock();
            }
        }

        /// <summary>
        /// Called by the loot controller when the player first picks up the dropped
        /// D21 flashlight. The pickup is the deliberate entry into the dark grapple.
        /// </summary>
        public bool NotifyD21FlashlightPicked(int loopEpoch)
        {
            if (!_awaitingLootDecision || _machine == null ||
                !string.Equals(_activeOutcomeId, "D21", StringComparison.Ordinal) ||
                loopEpoch != _loopEpoch || _machine.CurrentStageId != "item-drop" ||
                !_machine.TryJumpToStage("dark-grapple", loopEpoch, _pendingEvents))
            {
                return false;
            }

            _awaitingLootDecision = false;
            _awaitingBlockLight = false;
            CaptureCurrentRootLock();
            FlushEvents();
            RefreshStageAnimation();
            ApplyRootLock();
            return true;
        }

        private void OnD21FlashlightSuccess(int loopEpoch)
        {
            if (!_awaitingLootDecision || !_awaitingBlockLight || _machine == null ||
                !string.Equals(_activeOutcomeId, "D21", StringComparison.Ordinal) ||
                loopEpoch != _loopEpoch ||
                _machine.CurrentStageId != "block-light")
            {
                return;
            }

            _awaitingLootDecision = false;
            _awaitingBlockLight = false;
            _machine.SetPaused(false);
            CaptureCurrentRootLock();
            FlushEvents();
            ResumeAnimations();
            ApplyRootLock();
        }

        private void CaptureCurrentRootLock()
        {
            _playerLockPose = PairedPerformanceAnchorBehaviour.Snapshot(playerMotor.transform);
            _killerLockPose = PairedPerformanceAnchorBehaviour.Snapshot(killer);
            _rootsLocked = true;
            playerMotor.SetInputLocked(true);
        }

        private void OnAdapterBranchResolved(
            string outcomeId,
            int loopEpoch,
            PerformanceBranchDefinition branch,
            CounterplayNarrativeOutcome narrativeOutcome)
        {
            if (!_awaitingLootDecision || loopEpoch != _loopEpoch ||
                !string.Equals(outcomeId, _activeOutcomeId, StringComparison.Ordinal))
            {
                return;
            }

            _awaitingLootDecision = false;
            _machine?.SetPaused(false);
            ResumeAnimations();
            ApplyRootLock();
        }

        public int ResetOrder => 26;
        public bool IsPlaying { get; private set; }
        public string ActiveOutcomeId => _activeOutcomeId;
        public int ActiveLoopEpoch => _loopEpoch;
        public bool RootsLocked => _rootsLocked;
        public PairedCleanupAction LastCleanupActions { get; private set; } = PairedCleanupAction.None;
        public bool HasActiveAnimationGraph => _animationGraph.IsValid();
        public PairedPerformanceEventDispatcher Events => _dispatcher ??=
            new PairedPerformanceEventDispatcher();

        public void Configure(Transform killerTransform, PlayerMotor motor, PlayerInteractor interactor)
        {
            killer = killerTransform;
            playerMotor = motor;
            playerInteractor = interactor;
        }

        public void ConfigureHooks(
            ICounterplayPerformanceInput performanceInput,
            ICounterplayPerformanceProgress progress)
        {
            _performanceInput = performanceInput;
            _progress = progress;
        }

        /// <summary>
        /// Optional internal D01 fallback. When absent, TryLaunch returns false on
        /// rejection and the caller (KillerDirector) performs the D01 fallback itself,
        /// per the Runtime/AI launch contract.
        /// </summary>
        public void ConfigureD01Fallback(IPerformancePlayer player, CounterplayDefinitionAsset definition)
        {
            _fallbackPlayer = player;
            _fallbackDefinition = definition;
        }

        public void ConfigureAnchors(params PairedPerformanceAnchorBehaviour[] anchors)
        {
            _anchors.Clear();
            if (anchors == null)
            {
                return;
            }

            for (int i = 0; i < anchors.Length; i++)
            {
                PairedPerformanceAnchorBehaviour anchor = anchors[i];
                if (anchor != null && !string.IsNullOrWhiteSpace(anchor.OutcomeId))
                {
                    _anchors[anchor.OutcomeId] = anchor;
                }
            }
        }

        public void ConfigureAnimations(string outcomeId, AnimationClip playerClip, AnimationClip killerClip)
        {
            if (string.IsNullOrWhiteSpace(outcomeId))
            {
                return;
            }

            _animations[outcomeId] = new AnimationSet { PlayerClip = playerClip, KillerClip = killerClip };
            if (string.Equals(outcomeId, "D21", StringComparison.Ordinal))
            {
                ConfigureD21StageAnimations();
            }
        }

        /// <summary>
        /// Connects the production D21 single-side beats. Pair clips are supplied via
        /// ConfigureAnimations("D21", ...); they provide only the struggle and escape
        /// ranges. Segment timing follows the provisional source markers.
        /// </summary>
        public void ConfigureD21StageAnimations(
            AnimationClip fallPlayerClip,
            AnimationClip fallKillerClip,
            AnimationClip blockLightKillerClip,
            AnimationClip backupDrawFireKillerClip)
        {
            d21FallPlayerClip = fallPlayerClip;
            d21FallKillerClip = fallKillerClip;
            d21BlockLightKillerClip = blockLightKillerClip;
            d21BackupDrawFireKillerClip = backupDrawFireKillerClip;
            ConfigureD21StageAnimations();
        }

        /// <summary>Uses clips serialized on this component, when assigned in the scene.</summary>
        public void ConfigureD21StageAnimations()
        {
            if (!_animations.TryGetValue("D21", out AnimationSet pair))
            {
                return;
            }

            ConfigureStageAnimation("D21", "fall", d21FallPlayerClip, d21FallKillerClip, 0f, 0.5f);
            // Hold the killer's impact pose during the player's loot choice. The
            // player has no paired output in these beats and stays under Idle.
            ConfigureStageAnimation("D21", "item-drop", null, d21FallKillerClip, 0.5f, 0.5f);
            ConfigureStageAnimation("D21", "dark-grapple", pair.PlayerClip, pair.KillerClip, 0.25f, 0.60f);
            ConfigureStageAnimation("D21", "detach-escape", pair.PlayerClip, pair.KillerClip, 0.60f, 0.70f);
            ConfigureStageAnimation("D21", "block-light", null, d21BlockLightKillerClip, 0f, 1f);
            ConfigureStageAnimation("D21", "backup-draw", null, d21BackupDrawFireKillerClip, 0f, 0.35f);
            ConfigureStageAnimation("D21", "backup-fire", null, d21BackupDrawFireKillerClip, 0.58f, 0.70f);
        }

        /// <summary>
        /// Configures a clip pair for one authored phase. Segment bounds are normalized
        /// clip times; they let D21's 3-second grapple use its authored loop section
        /// instead of stretching the whole 8-second performance clip across D21.
        /// </summary>
        public void ConfigureStageAnimation(
            string outcomeId,
            string stageId,
            AnimationClip playerClip,
            AnimationClip killerClip,
            float segmentStart = 0f,
            float segmentEnd = 1f)
        {
            if (string.IsNullOrWhiteSpace(outcomeId) || string.IsNullOrWhiteSpace(stageId))
            {
                return;
            }

            if (!_stageAnimations.TryGetValue(outcomeId, out Dictionary<string, StageAnimationSet> stages))
            {
                stages = new Dictionary<string, StageAnimationSet>(StringComparer.Ordinal);
                _stageAnimations.Add(outcomeId, stages);
            }

            float start = Mathf.Clamp01(segmentStart);
            float end = Mathf.Clamp(segmentEnd, start, 1f);
            stages[stageId] = new StageAnimationSet
            {
                PlayerClip = playerClip,
                KillerClip = killerClip,
                SegmentStart = start,
                SegmentEnd = end
            };
        }

        public void PrepareLoopEpoch(int loopEpoch)
        {
            if (IsPlaying && _loopEpoch != loopEpoch)
            {
                CancelActive(PairedCancelReason.LoopReset, true);
            }

            _loopEpoch = loopEpoch;
        }

        public void NotifyLoopEpoch(int loopEpoch)
        {
            PrepareLoopEpoch(loopEpoch);
        }

        /// <summary>
        /// Confirmed-launch contract implementation. Returns true when the paired
        /// performance started or an internally configured D01 fallback engaged;
        /// returns false so the caller falls back to D01 itself.
        /// </summary>
        public bool TryLaunch(in ConfirmedPerformanceIntent intent)
        {
            bool accepted = TryLaunch(intent, null, out PairedPerformanceLaunchVerdict verdict);
            return accepted || verdict.FellBackToD01;
        }

        /// <summary>
        /// Rich launch entry. Validates both participants against the authored scene
        /// anchor (or the confirm-frame anchor pair carried by the intent); a
        /// violation forbids playback and falls back to D01. Returns true only when
        /// the paired performance itself started.
        /// </summary>
        public bool TryLaunch(
            in ConfirmedPerformanceIntent intent,
            Action completed,
            out PairedPerformanceLaunchVerdict verdict)
        {
            if (!intent.IsValid)
            {
                verdict = new PairedPerformanceLaunchVerdict(false, false, "Intent is not confirmed.", default);
                return false;
            }

            if (intent.LoopEpoch != _loopEpoch)
            {
                verdict = new PairedPerformanceLaunchVerdict(false, false, "Stale loop epoch.", default);
                return false;
            }

            PairedPerformanceDefinition definition =
                PairedPerformanceCatalog.GetByOutcomeId(intent.OutcomeId);
            if (definition == null || killer == null || playerMotor == null)
            {
                verdict = RejectWithFallback(
                    intent, completed, "No paired definition or missing performers.", default);
                return false;
            }

            PairedAnchorTolerance tolerance = ResolveTolerance(definition, intent.Anchors);
            bool hasSceneAnchor = _anchors.TryGetValue(definition.OutcomeId, out PairedPerformanceAnchorBehaviour anchor) &&
                                  anchor != null && anchor.IsValid;
            PairedAnchorPose expectedPlayer = hasSceneAnchor
                ? anchor.SnapshotPlayerPose()
                : ConvertPose(intent.Anchors.Player);
            PairedAnchorPose expectedKiller = hasSceneAnchor
                ? anchor.SnapshotKillerPose()
                : ConvertPose(intent.Anchors.Killer);
            if (!hasSceneAnchor && expectedPlayer.Equals(default(PairedAnchorPose)) &&
                expectedKiller.Equals(default(PairedAnchorPose)))
            {
                verdict = RejectWithFallback(
                    intent, completed, "No scene anchor and empty intent anchor pair.", default);
                return false;
            }

            PairedAnchorPose actualPlayer = PairedPerformanceAnchorBehaviour.Snapshot(playerMotor.transform);
            PairedAnchorPose actualKiller = PairedPerformanceAnchorBehaviour.Snapshot(killer);
            PairedAnchorValidationResult anchorResult = PairedAnchorValidator.Validate(
                expectedPlayer, expectedKiller, actualPlayer, actualKiller, tolerance);
            if (!anchorResult.IsWithinTolerance)
            {
                verdict = RejectWithFallback(
                    intent, completed, $"Anchor mismatch: {anchorResult.Failure}.", anchorResult);
                return false;
            }

            int expectedCancelGeneration = _generation + 1;
            CancelActive(PairedCancelReason.LoopReset, true);
            if (_generation != expectedCancelGeneration || IsPlaying)
            {
                verdict = new PairedPerformanceLaunchVerdict(false, false, "Cancel was superseded.", default);
                return false;
            }

            _generation++;
            int generation = _generation;
            _activeOutcomeId = definition.OutcomeId;
            _awaitingLootDecision = false;
            _awaitingBlockLight = false;
            _completed = completed;
            _playerLockPose = expectedPlayer;
            _killerLockPose = expectedKiller;

            float thresholdScale = _performanceInput == null ? 1f : _performanceInput.AccessibilityThresholdScale;
            _machine = new PairedPerformancePhaseMachine(definition, thresholdScale);
            IsPlaying = true;
            _rootsLocked = true;
            playerMotor.SetInputLocked(true);
            // Door ambush and blanket contact both release the carried item before
            // the paired animation takes over; under-bed contact has no such drop.
            PerformanceHeldItemPolicy itemPolicy = _activeOutcomeId == "D08" || _activeOutcomeId == "D21"
                ? PerformanceHeldItemPolicy.DropAtStart
                : PerformanceHeldItemPolicy.Preserve;
            if (itemPolicy == PerformanceHeldItemPolicy.DropAtStart)
            {
                playerInteractor?.ForceDropHeld();
            }
            _progress?.OnPerformanceStarted(_activeOutcomeId, _loopEpoch, itemPolicy);
            if (generation != _generation)
            {
                verdict = new PairedPerformanceLaunchVerdict(false, false, "Launch was superseded.", default);
                return false;
            }

            _machine.Start(_loopEpoch, _pendingEvents);
            FlushEvents();
            StartAnimations(_activeOutcomeId);
            ApplyRootLock();
            _routine = StartCoroutine(RunPerformance(generation));

            verdict = new PairedPerformanceLaunchVerdict(true, false, string.Empty, anchorResult);
            return true;
        }

        /// <summary>Gameplay-driven cancel; refused outside the stage cancel boundary.</summary>
        public bool RequestGameplayCancel()
        {
            if (!IsPlaying || _machine == null || !_machine.IsRunning)
            {
                return false;
            }

            PairedCleanupAction actions =
                _machine.Cancel(PairedCancelReason.GameplayInterrupt, _loopEpoch, _pendingEvents);
            if (actions == PairedCleanupAction.None)
            {
                return false;
            }

            ExecuteCleanup(actions, true);
            return true;
        }

        public void CancelAllForLoop(int loopEpoch)
        {
            if (IsPlaying && _loopEpoch <= loopEpoch)
            {
                CancelActive(PairedCancelReason.LoopReset, true);
            }

            _loopEpoch = Math.Max(_loopEpoch, loopEpoch);
        }

        public void CaptureInitialState()
        {
        }

        public void Quiesce(in LoopResetContext context)
        {
            CancelAllForLoop(context.PreviousEpoch);
        }

        public void Restore(in LoopResetContext context)
        {
            IsPlaying = false;
            _rootsLocked = false;
            _machine = null;
            _awaitingLootDecision = false;
            _awaitingBlockLight = false;
            _activeOutcomeId = string.Empty;
            _completed = null;
            _loopEpoch = context.NextEpoch;
            LastCleanupActions = PairedCleanupAction.None;
            _dispatcher?.ClearHistory();
        }

        public void Resume(in LoopResetContext context)
        {
        }

        private static PairedAnchorPose ConvertPose(in PerformanceAnchorPose pose)
        {
            return new PairedAnchorPose(
                pose.Position.x,
                pose.Position.y,
                pose.Position.z,
                pose.Rotation.eulerAngles.y);
        }

        private static PairedAnchorTolerance ResolveTolerance(
            PairedPerformanceDefinition definition,
            in PerformanceAnchorPair anchors)
        {
            return new PairedAnchorTolerance(
                anchors.MaxPositionError > 0f
                    ? anchors.MaxPositionError
                    : definition.Tolerance.MaxPositionError,
                anchors.MaxAngleErrorDegrees > 0f
                    ? anchors.MaxAngleErrorDegrees
                    : definition.Tolerance.MaxYawErrorDegrees,
                definition.Tolerance.MaxSeparationError);
        }

        private IEnumerator RunPerformance(int generation)
        {
            while (_machine != null && _machine.IsRunning && generation == _generation)
            {
                float delta = Time.deltaTime;
                bool pressed = _performanceInput != null && _performanceInput.InteractIsPressed;
                _machine.Advance(delta, pressed, _loopEpoch, _pendingEvents);
                FlushEvents();
                if (generation != _generation)
                {
                    yield break;
                }

                if (_machine.HasQteSnapshot)
                {
                    _progress?.OnQteProgress(_activeOutcomeId, _loopEpoch, _machine.LastQteSnapshot);
                }

                RefreshStageAnimation();
                ApplyRootLock();
                yield return null;
            }

            if (generation != _generation)
            {
                yield break;
            }

            // Natural completion: release everything in the same frame.
            string completedOutcomeId = _activeOutcomeId;
            int completedEpoch = _loopEpoch;
            StopAnimations();
            ReleaseRootLock();
            _machine = null;
            _awaitingLootDecision = false;
            _awaitingBlockLight = false;
            IsPlaying = false;
            _activeOutcomeId = string.Empty;
            _routine = null;
            Action callback = _completed;
            _completed = null;
            if (callback != null)
            {
                callback.Invoke();
            }
            else
            {
                // The plain KillerDirector launch contract has no completion
                // delegate. Keep natural paired completion on the existing runtime
                // adapter path so CounterplayNextOutcomeBridge can finish the
                // confirmed outcome exactly once.
                _runtimeAdapter?.RequestOutcomeCompletion(completedOutcomeId, completedEpoch,
                    string.Equals(completedOutcomeId, "D21", StringComparison.Ordinal));
            }
        }

        private void ApplyRootLock()
        {
            if (!_rootsLocked)
            {
                return;
            }

            // Same-frame lock: both roots are driven by the anchor in one pass so the
            // pair can never drift a frame apart.
            if (_machine == null ||
                (_machine.TryGetCurrentStage(out PairedPerformanceStageDefinition stage) && stage.LocksRoots))
            {
                PairedPerformanceAnchorBehaviour.Apply(playerMotor.transform, _playerLockPose);
                PairedPerformanceAnchorBehaviour.Apply(killer, _killerLockPose);
            }
        }

        private void ReleaseRootLock()
        {
            if (!_rootsLocked)
            {
                return;
            }

            _rootsLocked = false;
            if (playerMotor != null)
            {
                playerMotor.SetInputLocked(false);
            }
        }

        private PairedPerformanceLaunchVerdict RejectWithFallback(
            in ConfirmedPerformanceIntent intent,
            Action completed,
            string reason,
            in PairedAnchorValidationResult anchorResult)
        {
            Events.Publish(new PairedPerformanceSemanticEvent(
                PairedPerformanceEventIds.Compose(intent.OutcomeId, PairedPerformanceEventIds.LaunchRejected),
                intent.OutcomeId,
                string.Empty,
                PairedContactState.Separated,
                _loopEpoch));

            bool fellBack = false;
            if (_fallbackPlayer != null && _fallbackDefinition != null)
            {
                fellBack = _fallbackPlayer.Play(_fallbackDefinition, completed);
            }

            return new PairedPerformanceLaunchVerdict(false, fellBack, reason, anchorResult);
        }

        private void CancelActive(PairedCancelReason reason, bool notifyProgress)
        {
            bool wasPlaying = IsPlaying;
            string cancelledOutcomeId = _activeOutcomeId;
            int cancelledEpoch = _loopEpoch;
            _generation++;
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }

            PairedCleanupAction actions = _machine != null && _machine.IsRunning
                ? _machine.Cancel(reason, _loopEpoch, _pendingEvents)
                : PairedCleanupAction.All;
            ExecuteCleanup(actions, notifyProgress && wasPlaying, cancelledOutcomeId, cancelledEpoch);
        }

        private void ExecuteCleanup(
            PairedCleanupAction actions,
            bool notifyProgress,
            string outcomeId = null,
            int epoch = 0)
        {
            string cancelledOutcomeId = outcomeId ?? _activeOutcomeId;
            LastCleanupActions = actions;
            FlushEvents();
            if ((actions & PairedCleanupAction.StopAnimationGraph) != 0)
            {
                StopAnimations();
            }

            if ((actions & PairedCleanupAction.ReleaseRootLock) != 0)
            {
                ReleaseRootLock();
            }

            // Prop ownership and fact restoration are executed by semantic-event
            // consumers in the same call; the machine already emitted Detach, stage
            // exit and Cancelled markers atomically above.

            _machine = null;
            IsPlaying = false;
            _completed = null;
            _activeOutcomeId = string.Empty;
            if (notifyProgress && !string.IsNullOrEmpty(cancelledOutcomeId))
            {
                _progress?.OnPerformanceCancelled(cancelledOutcomeId, epoch);
            }
        }

        private void FlushEvents()
        {
            if (_pendingEvents.Count == 0)
            {
                return;
            }

            Events.Publish(_pendingEvents);
            _pendingEvents.Clear();
        }

        private void StartAnimations(string outcomeId)
        {
            StopAnimations();
            string stageId = _machine == null ? string.Empty : _machine.CurrentStageId;
            _activeAnimationStage = stageId;
            StageAnimationSet stageSet = null;
            if (_stageAnimations.TryGetValue(outcomeId, out Dictionary<string, StageAnimationSet> stages))
            {
                stages.TryGetValue(stageId, out stageSet);
            }

            AnimationSet set = null;
            if (stageSet == null)
            {
                // D21's paired clip is only the ground struggle section. Fall,
                // escape, block-light and draw require phase-specific clips.
                if (outcomeId == "D21" && stageId != "dark-grapple")
                {
                    return;
                }

                if (!_animations.TryGetValue(outcomeId, out set))
                {
                    return;
                }
            }

            AnimationClip playerClip = stageSet == null ? set.PlayerClip : stageSet.PlayerClip;
            AnimationClip killerClip = stageSet == null ? set.KillerClip : stageSet.KillerClip;
            if (playerClip == null && killerClip == null)
            {
                return;
            }

            Animator playerAnimator = playerMotor == null ? null : playerMotor.GetComponentInChildren<Animator>(true);
            Animator killerAnimator = killer == null ? null : killer.GetComponentInChildren<Animator>(true);
            if ((playerClip == null || playerAnimator == null) &&
                (killerClip == null || killerAnimator == null))
            {
                return;
            }

            _animationGraph = PlayableGraph.Create($"Paired Performance {outcomeId}");
            _animationGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            bool usesStageAnimations = _stageAnimations.ContainsKey(outcomeId);
            float stageDuration = _machine == null
                ? 0f
                : usesStageAnimations && _machine.TryGetCurrentStage(out PairedPerformanceStageDefinition currentStage)
                    ? currentStage.SafeDurationSeconds
                    : _machine.Definition.TotalDuration;
            float segmentStart = stageSet == null ? (outcomeId == "D21" ? 0.25f : 0f) : stageSet.SegmentStart;
            float segmentEnd = stageSet == null ? (outcomeId == "D21" ? 0.60f : 1f) : stageSet.SegmentEnd;
            CreateAnimationOutput(playerAnimator, playerClip, "Player", segmentStart, segmentEnd, stageDuration);
            CreateAnimationOutput(killerAnimator, killerClip, "Killer", segmentStart, segmentEnd, stageDuration);
            if (outcomeId == "D21")
            {
                RegisterGroundedVisual(playerAnimator, playerMotor.transform, true);
                RegisterGroundedVisual(killerAnimator, killer, false);
            }
            _animationGraph.Play();
        }

        private void RegisterGroundedVisual(Animator animator, Transform gameplayRoot, bool isPlayer)
        {
            if (animator == null || !animator.isHuman) return;
            var bones = new List<Transform>();
            for (int i = 0; i < (int)HumanBodyBones.LastBone; i++)
            {
                Transform bone = animator.GetBoneTransform((HumanBodyBones)i);
                if (bone != null) bones.Add(bone);
            }
            _d21GroundedVisuals.Add(new GroundedVisual
            {
                Root = animator.transform,
                GameplayRoot = gameplayRoot,
                OriginalLocalPosition = animator.transform.localPosition,
                Bones = bones.ToArray(),
                IsPlayer = isPlayer
            });
        }

        private void LateUpdate()
        {
            // NIUA's in-place floor clips retain standing hip height. Ground only
            // the D21 visual layer, leaving collider roots and trap anchors intact.
            foreach (GroundedVisual visual in _d21GroundedVisuals)
            {
                if (visual.Root == null || visual.GameplayRoot == null) continue;
                visual.Root.localPosition = visual.OriginalLocalPosition;
                if (visual.IsPlayer && _playerAnimationDetached) continue;
                float lowestBone = float.PositiveInfinity;
                foreach (Transform bone in visual.Bones)
                    if (bone != null) lowestBone = Mathf.Min(lowestBone, bone.position.y);
                if (float.IsPositiveInfinity(lowestBone)) continue;
                float offset = Mathf.Min(0f, visual.GameplayRoot.position.y + 0.08f - lowestBone);
                visual.Root.position += Vector3.up * offset;
            }
        }

        private void CreateAnimationOutput(
            Animator target,
            AnimationClip clip,
            string outputName,
            float segmentStart,
            float segmentEnd,
            float stageDuration)
        {
            if (target == null || clip == null)
            {
                return;
            }

            AnimationClipPlayable playable = AnimationClipPlayable.Create(_animationGraph, clip);
            playable.SetApplyFootIK(true);
            playable.SetApplyPlayableIK(true);
            float clipStart = Mathf.Clamp01(segmentStart) * clip.length;
            float clipEnd = Mathf.Clamp(segmentEnd, segmentStart, 1f) * clip.length;
            playable.SetTime(clipStart);
            playable.SetSpeed((clipEnd - clipStart) / Mathf.Max(0.05f, stageDuration));
            playable.SetDuration(clip.length);
            AnimationPlayableOutput output = AnimationPlayableOutput.Create(_animationGraph, outputName, target);
            output.SetSourcePlayable(playable);
            if (string.Equals(outputName, "Player", StringComparison.Ordinal))
            {
                _playerAnimationTarget = target;
                _playerAnimationPlayable = playable;
                _playerAnimationOutput = output;
                _hasPlayerAnimationOutput = true;
                _playerAnimationDetached = false;
            }
        }

        private void StopAnimations()
        {
            foreach (GroundedVisual visual in _d21GroundedVisuals)
                if (visual.Root != null) visual.Root.localPosition = visual.OriginalLocalPosition;
            _d21GroundedVisuals.Clear();
            if (_animationGraph.IsValid())
            {
                _animationGraph.Destroy();
            }

            if (_playerAnimationDetached && _playerAnimationTarget != null &&
                _playerAnimationTarget.isActiveAndEnabled)
            {
                _playerAnimationTarget.Play("Idle", 0, 0f);
            }

            _playerAnimationTarget = null;
            _playerAnimationPlayable = default;
            _playerAnimationOutput = default;
            _hasPlayerAnimationOutput = false;
            _playerAnimationDetached = false;
        }

        private void PauseAnimations()
        {
            if (!_animationGraph.IsValid()) return;
            if (_hasPlayerAnimationOutput)
            {
                _playerAnimationOutput.SetTarget(null);
                _playerAnimationDetached = true;
                if (_playerAnimationTarget != null && _playerAnimationTarget.isActiveAndEnabled)
                    _playerAnimationTarget.Play("Idle", 0, 0f);
            }
            _pausedAnimationSpeeds.Clear();
            for (int i = 0; i < _animationGraph.GetRootPlayableCount(); i++)
            {
                Playable root = _animationGraph.GetRootPlayable(i);
                _pausedAnimationSpeeds.Add(root.GetSpeed());
                root.SetSpeed(0d);
            }
        }

        private void ResumeAnimations(bool seekToStage = false)
        {
            if (!_animationGraph.IsValid() || _machine == null) return;
            if (_playerAnimationDetached && _hasPlayerAnimationOutput)
            {
                _playerAnimationOutput.SetSourcePlayable(_playerAnimationPlayable);
                _playerAnimationOutput.SetTarget(_playerAnimationTarget);
                _playerAnimationDetached = false;
            }
            for (int i = 0; i < _animationGraph.GetRootPlayableCount(); i++)
            {
                Playable root = _animationGraph.GetRootPlayable(i);
                double rate = i < _pausedAnimationSpeeds.Count ? _pausedAnimationSpeeds[i] : 1d;
                root.SetSpeed(rate);
            }
            _pausedAnimationSpeeds.Clear();
            if (seekToStage) _animationGraph.Evaluate(0f);
        }

        private void RefreshStageAnimation()
        {
            if (_machine == null || !_machine.IsRunning || _activeAnimationStage == _machine.CurrentStageId)
            {
                return;
            }

            // D08 and D11 use one authored animation across their complete
            // definition. Their phase machine has semantic stages, but those stages
            // are not animation segments and must not restart that clip.
            if (!string.Equals(_activeOutcomeId, "D21", StringComparison.Ordinal) ||
                !_stageAnimations.ContainsKey(_activeOutcomeId))
            {
                _activeAnimationStage = _machine.CurrentStageId;
                return;
            }

            StartAnimations(_activeOutcomeId);
        }
    }
}
