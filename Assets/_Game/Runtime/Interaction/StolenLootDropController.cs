using System;
using System.Collections.Generic;
using ConGame.Domain.Counterplay;
using ConGame.Domain.Facts;
using ConGame.Domain.Interaction;
using ConGame.Runtime.AI;
using ConGame.Runtime.Counterplay;
using ConGame.Runtime.Events;
using ConGame.Runtime.Loop;
using ConGame.Runtime.Player;
using ConGame.Domain.Choreography;
using UnityEngine;

namespace ConGame.Runtime.Interaction
{
    /// <summary>Plays D22's terminal draw/fire beat and reports its authored cues.</summary>
    public interface ID22FinalePresentation
    {
        bool PlayD22Finale(Action backupDrawCue, Action shotCue, Action completed);
    }

    public interface ID21FlashlightGrapplePresentation
    {
        bool NotifyD21FlashlightPicked(int loopEpoch);
    }

    public enum CounterplayLootWindowState
    {
        Inactive = 0,
        D08Struggle = 1,
        D08LootWindow = 2,
        D21Choice = 3,
        D21FlashlightArmed = 4,
        D22AwaitMainShot = 5,
        D22Dodging = 6,
        D22AwaitMainDryFire = 7,
        Resolved = 8,
        D21Grappling = 9,
        D21FlashlightRecovery = 10
    }

    [DisallowMultipleComponent]
    public sealed class StolenLootDropController : MonoBehaviour, ILoopResettable
    {
        private static readonly int DodgeHash = Animator.StringToHash("Dodge");
        [SerializeField] private GameObject pistolDrop;
        [SerializeField] private GameObject flashlightDrop;
        [SerializeField] private GameObject backupPistolDrop;
        [SerializeField] private CounterplayPerformanceRuntimeAdapter adapter;
        [SerializeField] private GameplayEventHub events;
        [SerializeField] private KillerDirector killer;
        [SerializeField, Min(0.25f)] private float d08LootWindowSeconds = 3f;
        [SerializeField, Min(1f)] private float d21ChoiceWindowSeconds = 12f;
        [SerializeField, Min(1f)] private float d22WindowSeconds = 12f;
        [SerializeField, Min(0.1f)] private float dodgeSeconds = 0.4f;
        [SerializeField, Min(0.1f)] private float dodgeDistance = 1.25f;

        private StatefulWorldItem _pistolStateful;
        private StatefulWorldItem _flashlightStateful;
        private StatefulWorldItem _backupStateful;
        private FirearmUseHandler _mainFirearm;
        private FirearmUseHandler _backupFirearm;
        private bool _initialPistolActive;
        private bool _initialFlashlightActive;
        private bool _initialBackupActive;
        private bool _subscribed;
        private bool _d22FinaleStarted;
        private ID22FinalePresentation _d22FinalePresentation;
        private Transform _d22BackupShotCueSource;
        private ID21FlashlightGrapplePresentation _d21PairedPerformance;
        private PlayerInteractor _playerInteractor;
        private Transform _d21PistolDropOrigin;
        private Transform _d21FlashlightDropOrigin;
        private int _d21PistolDropHandledEpoch = int.MinValue;
        private int _d21FlashlightDropHandledEpoch = int.MinValue;
        private int _d21FlashlightDislodgedEpoch = int.MinValue;
        private int _activeLoopEpoch;
        private string _activeOutcomeId;
        private float _remainingWindow;
        private float _dodgeElapsed;
        private Vector3 _dodgeStart;
        private Vector3 _dodgeTarget;

        public int ResetOrder => 95;
        public CounterplayLootWindowState State { get; private set; } = CounterplayLootWindowState.Inactive;
        public float RemainingWindowSeconds => Mathf.Max(0f, _remainingWindow);
        public FirearmUseHandler MainFirearm => _mainFirearm;
        public FirearmUseHandler BackupFirearm => _backupFirearm;
        public event Action<int> D21FlashlightSuccess;
        public event Action<bool, Transform, int> D22ShotCueRequested;
        public event Action<bool, int> D22BackupVisualRequested;

        public void ConfigureD22FinalePresentation(ID22FinalePresentation presentation)
        {
            _d22FinalePresentation = presentation;
        }

        public void ConfigureD22ShotCueSource(Transform backupVisualOrMuzzle)
        {
            _d22BackupShotCueSource = backupVisualOrMuzzle;
        }

        public void ConfigureD21PairedPerformance(
            ID21FlashlightGrapplePresentation pairedPerformance,
            PlayerInteractor playerInteractor)
        {
            _d21PairedPerformance = pairedPerformance;
            _playerInteractor = playerInteractor;
        }

        public void ConfigureD21DropOrigins(Transform mainPistolVisual, Transform railFlashlightVisual)
        {
            _d21PistolDropOrigin = mainPistolVisual;
            _d21FlashlightDropOrigin = railFlashlightVisual;
        }

        public void Configure(
            GameObject pistol,
            GameObject flashlight,
            CounterplayPerformanceRuntimeAdapter performanceAdapter,
            GameObject backupPistol = null,
            GameplayEventHub eventHub = null,
            KillerDirector killerDirector = null)
        {
            Unsubscribe();
            pistolDrop = pistol;
            flashlightDrop = flashlight;
            backupPistolDrop = backupPistol;
            adapter = performanceAdapter;
            events = eventHub;
            killer = killerDirector;
            ResolveDependencies();
            if (isActiveAndEnabled)
            {
                Subscribe();
            }
        }

        public void ConfigureWindowDurations(float d08Seconds, float d21Seconds, float d22Seconds, float dodgeDuration)
        {
            d08LootWindowSeconds = Mathf.Max(0.01f, d08Seconds);
            d21ChoiceWindowSeconds = Mathf.Max(0.01f, d21Seconds);
            d22WindowSeconds = Mathf.Max(0.01f, d22Seconds);
            dodgeSeconds = Mathf.Max(0.01f, dodgeDuration);
        }

        private void Awake()
        {
            ResolveDependencies();
        }

        private void OnEnable()
        {
            ResolveDependencies();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Update()
        {
            if (!IsTimedWindow(State))
            {
                return;
            }

            _remainingWindow -= Time.deltaTime;
            if (_remainingWindow <= 0f)
            {
                string outcomeId = State == CounterplayLootWindowState.D08LootWindow ? "D08" :
                    State == CounterplayLootWindowState.D21Choice ||
                    State == CounterplayLootWindowState.D21FlashlightArmed ||
                    State == CounterplayLootWindowState.D21FlashlightRecovery ? "D21" : "D22";
                CompleteOutcome(outcomeId, false);
            }
        }

        private void LateUpdate()
        {
            if (State == CounterplayLootWindowState.D22Dodging)
            {
                AdvanceDodge(Time.deltaTime);
            }
        }

        public void CaptureInitialState()
        {
            _initialPistolActive = pistolDrop != null && pistolDrop.activeSelf;
            _initialFlashlightActive = flashlightDrop != null && flashlightDrop.activeSelf;
            _initialBackupActive = backupPistolDrop != null && backupPistolDrop.activeSelf;
        }

        public void Quiesce(in LoopResetContext context)
        {
            _activeOutcomeId = null;
            State = CounterplayLootWindowState.Inactive;
            _remainingWindow = 0f;
            _d22FinaleStarted = false;
        }

        public void Restore(in LoopResetContext context)
        {
            SetActive(pistolDrop, _initialPistolActive);
            SetActive(flashlightDrop, _initialFlashlightActive);
            SetActive(backupPistolDrop, _initialBackupActive);
            _mainFirearm?.Arm(0);
            _backupFirearm?.Arm(0);
            _activeLoopEpoch = context.NextEpoch;
            _activeOutcomeId = null;
            State = CounterplayLootWindowState.Inactive;
            _remainingWindow = 0f;
            _d22FinaleStarted = false;
            _d21PistolDropHandledEpoch = int.MinValue;
            _d21FlashlightDropHandledEpoch = int.MinValue;
            _d21FlashlightDislodgedEpoch = int.MinValue;
        }

        public void Resume(in LoopResetContext context)
        {
        }

        public void NotifyKillerIlluminated(in FlashlightHit hit)
        {
            if (State == CounterplayLootWindowState.D21FlashlightArmed)
            {
                State = CounterplayLootWindowState.Resolved;
                _remainingWindow = 0f;
                adapter?.SetCounterplayInputWindows(CounterplayInputWindow.None);
                D21FlashlightSuccess?.Invoke(_activeLoopEpoch);
            }
        }

        public void NotifyPairedSemanticEvent(in PairedPerformanceSemanticEvent semanticEvent)
        {
            if (semanticEvent.LoopEpoch != _activeLoopEpoch ||
                !string.Equals(semanticEvent.OutcomeId, _activeOutcomeId, StringComparison.Ordinal))
            {
                return;
            }

            if (string.Equals(semanticEvent.OutcomeId, "D08", StringComparison.Ordinal) &&
                semanticEvent.LoopEpoch == _activeLoopEpoch &&
                string.Equals(semanticEvent.EventId, "D08.LootWindow.Open", StringComparison.Ordinal))
            {
                SetActive(pistolDrop, true);
                SetActiveIfNotHeld(flashlightDrop, false);
                SetActive(backupPistolDrop, false);
                PlaceDropNearKiller(pistolDrop, 0.35f);
                _mainFirearm?.Arm(0);
                State = CounterplayLootWindowState.D08LootWindow;
                _remainingWindow = d08LootWindowSeconds;
                adapter?.SetCounterplayInputWindows(CounterplayInputWindow.Grab);
                return;
            }

            if (!string.Equals(semanticEvent.OutcomeId, "D21", StringComparison.Ordinal))
            {
                return;
            }

            if (semanticEvent.LoopEpoch != _activeLoopEpoch)
            {
                return;
            }

            if (string.Equals(semanticEvent.EventId, "D21.Pair.Begin", StringComparison.Ordinal))
            {
                if (State != CounterplayLootWindowState.D21Choice &&
                    State != CounterplayLootWindowState.D21Grappling) return;
                State = CounterplayLootWindowState.D21Grappling;
                _remainingWindow = 0f;
                adapter?.SetCounterplayInputWindows(CounterplayInputWindow.Qte);
                return;
            }

            if (string.Equals(semanticEvent.EventId, "D21.Flashlight.Dislodged", StringComparison.Ordinal))
            {
                if (State != CounterplayLootWindowState.D21Grappling) return;
                if (_d21FlashlightDislodgedEpoch == semanticEvent.LoopEpoch)
                {
                    return;
                }

                _d21FlashlightDislodgedEpoch = semanticEvent.LoopEpoch;
                InteractableEntity flashlightEntity = flashlightDrop == null
                    ? null
                    : flashlightDrop.GetComponent<InteractableEntity>();
                if (_playerInteractor != null && flashlightEntity != null &&
                    _playerInteractor.HeldEntity == flashlightEntity)
                {
                    _playerInteractor.ForceDropHeld();
                    _flashlightStateful?.AddFlightImpulse(
                        ResolveD21TossDirection() * 0.4f + Vector3.up * 0.15f,
                        Vector3.right * 1.2f + Vector3.up * 0.5f);
                }

                State = CounterplayLootWindowState.D21Grappling;
                _remainingWindow = 0f;
                adapter?.SetCounterplayInputWindows(CounterplayInputWindow.None);
                return;
            }

            if (string.Equals(semanticEvent.EventId, "D21.Flashlight.Recovery.Open", StringComparison.Ordinal))
            {
                if (State != CounterplayLootWindowState.D21Grappling ||
                    _d21FlashlightDislodgedEpoch != semanticEvent.LoopEpoch) return;
                State = CounterplayLootWindowState.D21FlashlightRecovery;
                _remainingWindow = d21ChoiceWindowSeconds;
                adapter?.SetCounterplayInputWindows(CounterplayInputWindow.Grab);
                return;
            }

            if (string.Equals(semanticEvent.EventId, "D21.Pistol.Dropped", StringComparison.Ordinal))
            {
                if (_d21PistolDropHandledEpoch == semanticEvent.LoopEpoch)
                {
                    return;
                }

                _d21PistolDropHandledEpoch = semanticEvent.LoopEpoch;
                LaunchD21Drop(pistolDrop, _pistolStateful, _d21PistolDropOrigin, 0.35f, 4f);
                TryOpenD21ChoiceWindow(semanticEvent.LoopEpoch);
                return;
            }

            if (string.Equals(semanticEvent.EventId, "D21.Flashlight.Dropped", StringComparison.Ordinal))
            {
                if (_d21FlashlightDropHandledEpoch == semanticEvent.LoopEpoch)
                {
                    return;
                }

                _d21FlashlightDropHandledEpoch = semanticEvent.LoopEpoch;
                LaunchD21Drop(flashlightDrop, _flashlightStateful, _d21FlashlightDropOrigin, -0.35f, -4f);
                TryOpenD21ChoiceWindow(semanticEvent.LoopEpoch);
            }
        }

        public void NotifyMirrorIlluminated()
        {
            if (State != CounterplayLootWindowState.D21FlashlightArmed)
            {
                return;
            }

            State = CounterplayLootWindowState.Resolved;
            _remainingWindow = 0f;
            adapter?.RequestNextOutcome("D21", _activeLoopEpoch, "D17");
        }

        private void OnPerformanceStarted(string outcomeId, int loopEpoch, PerformanceHeldItemPolicy policy)
        {
            EnsureKillerTarget();
            _activeLoopEpoch = loopEpoch;
            _activeOutcomeId = outcomeId;
            if (string.Equals(outcomeId, "D08", StringComparison.Ordinal))
            {
                HideWindowLoot();
                State = CounterplayLootWindowState.D08Struggle;
                _remainingWindow = 0f;
                return;
            }

            if (string.Equals(outcomeId, "D21", StringComparison.Ordinal))
            {
                // The paired phase owns D21 timing; open the choice window only when
                // its authored pistol/flashlight drop semantic event arrives.
                return;
            }

            if (string.Equals(outcomeId, "D22", StringComparison.Ordinal))
            {
                SetActive(pistolDrop, true);
                SetActiveIfNotHeld(flashlightDrop, false);
                SetActive(backupPistolDrop, false);
                _mainFirearm?.Arm(1);
                _backupFirearm?.Arm(0);
                _d22FinaleStarted = false;
                State = CounterplayLootWindowState.D22AwaitMainShot;
                _remainingWindow = d22WindowSeconds;
                adapter?.SetCounterplayInputWindows(CounterplayInputWindow.Use);
            }
        }

        private void OnBranchResolved(
            string outcomeId,
            int loopEpoch,
            PerformanceBranchDefinition branch,
            CounterplayNarrativeOutcome narrative)
        {
            if (!string.Equals(outcomeId, "D08", StringComparison.Ordinal))
            {
                return;
            }

            if (branch.RequiresAllQteWindows &&
                string.Equals(branch.BranchId, "disarm_window", StringComparison.Ordinal))
            {
                SetActive(pistolDrop, true);
                SetActive(flashlightDrop, false);
                SetActive(backupPistolDrop, false);
                State = CounterplayLootWindowState.D08LootWindow;
                _remainingWindow = d08LootWindowSeconds;
                adapter?.SetCounterplayInputWindows(CounterplayInputWindow.Grab);
                return;
            }

            State = CounterplayLootWindowState.Resolved;
            _remainingWindow = 0f;
        }

        private void OnPistolStateChanged(ItemPhysicalState itemState)
        {
            if (itemState != ItemPhysicalState.Held)
            {
                return;
            }

            if (State == CounterplayLootWindowState.D08LootWindow)
            {
                RequestNextOutcome("D08", "D22");
            }
            else if (State == CounterplayLootWindowState.D21Choice)
            {
                RequestNextOutcome("D21", "D22");
            }
        }

        private void OnFlashlightStateChanged(ItemPhysicalState itemState)
        {
            if (itemState != ItemPhysicalState.Held)
            {
                return;
            }

            if (State == CounterplayLootWindowState.D21Choice)
            {
                if (_d21PairedPerformance != null &&
                    _d21PairedPerformance.NotifyD21FlashlightPicked(_activeLoopEpoch))
                {
                    State = CounterplayLootWindowState.D21Grappling;
                    _remainingWindow = 0f;
                    adapter?.SetCounterplayInputWindows(CounterplayInputWindow.Qte);
                    return;
                }

                State = CounterplayLootWindowState.D21FlashlightArmed;
                adapter?.SetCounterplayInputWindows(CounterplayInputWindow.Use);
                return;
            }

            if (State == CounterplayLootWindowState.D21FlashlightRecovery)
            {
                State = CounterplayLootWindowState.D21FlashlightArmed;
                _remainingWindow = d21ChoiceWindowSeconds;
                adapter?.SetCounterplayInputWindows(CounterplayInputWindow.Use);
            }
        }

        private void OnMainShot(FirearmUseHandler firearm)
        {
            if (State != CounterplayLootWindowState.D22AwaitMainShot)
            {
                return;
            }

            killer?.SuspendPerformanceForInteractiveWindow();
            D22ShotCueRequested?.Invoke(false, firearm == null ? null : firearm.transform, _activeLoopEpoch);
            _dodgeElapsed = 0f;
            if (killer != null)
            {
                _dodgeStart = killer.transform.position;
                _dodgeTarget = _dodgeStart + killer.transform.right * dodgeDistance;
                Animator animator = killer.GetComponentInChildren<Animator>(true);
                if (animator != null && Array.Exists(animator.parameters, parameter => parameter.nameHash == DodgeHash))
                {
                    animator.SetTrigger(DodgeHash);
                }
            }

            State = CounterplayLootWindowState.D22Dodging;
        }

        private void OnMainDryFire(FirearmUseHandler firearm)
        {
            if (State == CounterplayLootWindowState.D22AwaitMainDryFire)
            {
                CompleteOutcome("D22", false);
            }
        }

        private void OnStimulus(KillerStimulus stimulus)
        {
            if (stimulus.Type == KillerStimulusType.Glare &&
                string.Equals(stimulus.SourceId, "mirror", StringComparison.Ordinal))
            {
                NotifyMirrorIlluminated();
            }
        }

        private void AdvanceDodge(float deltaTime)
        {
            _dodgeElapsed += Mathf.Max(0f, deltaTime);
            float progress = Mathf.Clamp01(_dodgeElapsed / dodgeSeconds);
            if (killer != null)
            {
                killer.transform.position = Vector3.Lerp(_dodgeStart, _dodgeTarget, Mathf.SmoothStep(0f, 1f, progress));
            }

            if (progress < 1f)
            {
                return;
            }

            _mainFirearm?.Arm(0);
            SetActive(backupPistolDrop, false);
            State = CounterplayLootWindowState.D22AwaitMainDryFire;
            adapter?.SetCounterplayInputWindows(CounterplayInputWindow.Use);
        }

        private void CompleteOutcome(string outcomeId, bool markD21Seen)
        {
            if (State == CounterplayLootWindowState.Resolved)
            {
                return;
            }

            State = CounterplayLootWindowState.Resolved;
            _remainingWindow = 0f;
            adapter?.SetCounterplayInputWindows(CounterplayInputWindow.None);

            if (string.Equals(outcomeId, "D22", StringComparison.Ordinal))
            {
                _d22FinaleStarted = true;
                int finaleEpoch = _activeLoopEpoch;
                bool finaleStarted = _d22FinalePresentation != null &&
                                     _d22FinalePresentation.PlayD22Finale(
                                         () =>
                                         {
                                             if (IsCurrentD22Finale(finaleEpoch))
                                             {
                                                 D22BackupVisualRequested?.Invoke(true, finaleEpoch);
                                             }
                                         },
                                         () =>
                                         {
                                             if (IsCurrentD22Finale(finaleEpoch))
                                             {
                                                 D22ShotCueRequested?.Invoke(true,
                                                     _d22BackupShotCueSource != null
                                                         ? _d22BackupShotCueSource
                                                         : (killer == null ? transform : killer.transform),
                                                     finaleEpoch);
                                             }
                                         },
                                         () =>
                                         {
                                             if (IsCurrentD22Finale(finaleEpoch))
                                             {
                                                 adapter?.RequestOutcomeCompletion(outcomeId, finaleEpoch, markD21Seen);
                                             }
                                         });
                if (finaleStarted)
                {
                    return;
                }
            }

            _d22FinaleStarted = false;
            adapter?.RequestOutcomeCompletion(outcomeId, _activeLoopEpoch, markD21Seen);
        }

        private bool IsCurrentD22Finale(int loopEpoch)
        {
            return _d22FinaleStarted && _activeLoopEpoch == loopEpoch &&
                   State == CounterplayLootWindowState.Resolved;
        }

        private void RequestNextOutcome(string outcomeId, string nextOutcomeId)
        {
            State = CounterplayLootWindowState.Resolved;
            _remainingWindow = 0f;
            adapter?.SetCounterplayInputWindows(CounterplayInputWindow.None);
            adapter?.RequestNextOutcome(outcomeId, _activeLoopEpoch, nextOutcomeId);
        }

        private void HideWindowLoot()
        {
            SetActiveIfNotHeld(pistolDrop, false);
            SetActiveIfNotHeld(flashlightDrop, false);
            SetActiveIfNotHeld(backupPistolDrop, false);
        }

        private void PlaceDropNearKiller(GameObject item, float lateralOffset)
        {
            if (item == null || killer == null)
            {
                return;
            }

            item.transform.SetPositionAndRotation(
                killer.transform.position + killer.transform.right * lateralOffset,
                killer.transform.rotation);
        }

        private void TryOpenD21ChoiceWindow(int loopEpoch)
        {
            if (loopEpoch != _activeLoopEpoch ||
                _d21PistolDropHandledEpoch != loopEpoch ||
                _d21FlashlightDropHandledEpoch != loopEpoch ||
                State == CounterplayLootWindowState.D21Choice)
            {
                return;
            }

            SetActive(backupPistolDrop, false);
            _mainFirearm?.Arm(0);
            State = CounterplayLootWindowState.D21Choice;
            _remainingWindow = d21ChoiceWindowSeconds;
            adapter?.SetCounterplayInputWindows(CounterplayInputWindow.Grab);
        }

        private void LaunchD21Drop(
            GameObject item,
            StatefulWorldItem stateful,
            Transform authoredOrigin,
            float fallbackLateralOffset,
            float angularSpeed)
        {
            if (item == null || stateful == null || stateful.CurrentState != ItemPhysicalState.Placed)
            {
                return;
            }

            SetActive(item, true);
            if (killer == null)
            {
                return;
            }

            Vector3 launchPosition = authoredOrigin != null
                ? authoredOrigin.position
                : killer.transform.position + Vector3.up * 0.65f + killer.transform.right * fallbackLateralOffset;
            Quaternion launchRotation = authoredOrigin != null
                ? authoredOrigin.rotation
                : killer.transform.rotation;
            Vector3 direction = ResolveD21TossDirection(launchPosition);
            launchPosition += direction * 0.25f;
            // Fall clips can put a hand below the floor. Release the prop clear
            // of the room floor instead of spawning a dynamic collider through it.
            launchPosition.y = Mathf.Max(launchPosition.y, killer.transform.position.y + 0.18f);
            // The confirmed pull can happen from either side of the killer.
            // Spill into the player's approach, not the killer's stale entry
            // heading (which can throw both props into the bed/nightstand gap).
            // Distinct lateral impulses keep the two deliberate choices apart
            // even when their weapon-mounted release sockets almost coincide.
            Vector3 lateral = Vector3.Cross(Vector3.up, direction) * fallbackLateralOffset;
            Vector3 velocity = direction * 1.05f + lateral + Vector3.up * 1.5f;
            Vector3 angularVelocity = killer.transform.right * angularSpeed + Vector3.up * (angularSpeed * 0.35f);

            if (stateful.TossIntoWorld(launchPosition, launchRotation, velocity, angularVelocity,
                    GetD21IgnoredColliders()))
            {
                return;
            }

            // Keep the drop usable if a production prop temporarily lacks a body.
            item.transform.SetParent(null, true);
            item.transform.SetPositionAndRotation(launchPosition, launchRotation);
            stateful.MarkPlaced();
        }

        private Vector3 ResolveD21TossDirection()
        {
            return ResolveD21TossDirection(killer == null ? transform.position : killer.transform.position);
        }

        private Vector3 ResolveD21TossDirection(Vector3 origin)
        {
            Vector3 direction = _playerInteractor == null
                ? (killer == null ? Vector3.forward : killer.transform.forward)
                : _playerInteractor.transform.position - origin;
            direction.y = 0f;
            return direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.forward;
        }

        private Collider[] GetD21IgnoredColliders()
        {
            var ignored = new List<Collider>(16);
            if (killer != null)
            {
                ignored.AddRange(killer.GetComponentsInChildren<Collider>(true));
            }

            if (_playerInteractor != null)
            {
                ignored.AddRange(_playerInteractor.GetComponentsInChildren<Collider>(true));
            }

            return ignored.ToArray();
        }

        private void ResolveDependencies()
        {
            _pistolStateful = pistolDrop == null ? null : pistolDrop.GetComponent<StatefulWorldItem>();
            _flashlightStateful = flashlightDrop == null ? null : flashlightDrop.GetComponent<StatefulWorldItem>();
            _backupStateful = backupPistolDrop == null ? null : backupPistolDrop.GetComponent<StatefulWorldItem>();
            _playerInteractor ??= UnityEngine.Object.FindAnyObjectByType<PlayerInteractor>();
            events ??= UnityEngine.Object.FindAnyObjectByType<GameplayEventHub>();
            EnsureKillerTarget();
            EnsureFirearm(pistolDrop, FirearmRole.MainPistol, out _mainFirearm);
            EnsureFirearm(backupPistolDrop, FirearmRole.ScriptedBackup, out _backupFirearm);
        }

        private void EnsureKillerTarget()
        {
            if (killer == null)
            {
                KillerDirector[] candidates = Resources.FindObjectsOfTypeAll<KillerDirector>();
                for (int i = 0; i < candidates.Length; i++)
                {
                    if (candidates[i] != null && candidates[i].gameObject.scene.IsValid())
                    {
                        killer = candidates[i];
                        break;
                    }
                }
            }

            if (killer != null)
            {
                CounterplayFlashlightTarget target = killer.GetComponent<CounterplayFlashlightTarget>();
                if (target == null)
                {
                    target = killer.gameObject.AddComponent<CounterplayFlashlightTarget>();
                }

                target.Configure(this);
            }
        }

        private static void EnsureFirearm(GameObject item, FirearmRole role, out FirearmUseHandler handler)
        {
            handler = null;
            if (item == null)
            {
                return;
            }

            handler = item.GetComponent<FirearmUseHandler>();
            if (handler == null)
            {
                handler = item.AddComponent<FirearmUseHandler>();
            }

            handler.Configure(role, 0);
            UseCapability use = item.GetComponent<UseCapability>();
            if (use == null)
            {
                use = item.AddComponent<UseCapability>();
            }

            use.Configure(handler);
        }

        private void Subscribe()
        {
            if (_subscribed || adapter == null)
            {
                return;
            }

            adapter.PerformanceStarted += OnPerformanceStarted;
            adapter.BranchResolved += OnBranchResolved;
            adapter.RegisterLootWindowController();
            if (_pistolStateful != null)
            {
                _pistolStateful.StateChanged += OnPistolStateChanged;
            }

            if (_flashlightStateful != null)
            {
                _flashlightStateful.StateChanged += OnFlashlightStateChanged;
            }

            if (_mainFirearm != null)
            {
                _mainFirearm.ShotFired += OnMainShot;
                _mainFirearm.DryFired += OnMainDryFire;
            }

            if (events != null)
            {
                events.KillerStimulusEmitted += OnStimulus;
            }

            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            if (adapter != null)
            {
                adapter.PerformanceStarted -= OnPerformanceStarted;
                adapter.BranchResolved -= OnBranchResolved;
                adapter.UnregisterLootWindowController();
            }

            if (_pistolStateful != null)
            {
                _pistolStateful.StateChanged -= OnPistolStateChanged;
            }

            if (_flashlightStateful != null)
            {
                _flashlightStateful.StateChanged -= OnFlashlightStateChanged;
            }

            if (_mainFirearm != null)
            {
                _mainFirearm.ShotFired -= OnMainShot;
                _mainFirearm.DryFired -= OnMainDryFire;
            }

            if (events != null)
            {
                events.KillerStimulusEmitted -= OnStimulus;
            }

            _subscribed = false;
        }

        private static bool IsTimedWindow(CounterplayLootWindowState state)
        {
            return state == CounterplayLootWindowState.D08LootWindow ||
                   state == CounterplayLootWindowState.D21Choice ||
                   state == CounterplayLootWindowState.D21FlashlightArmed ||
                   state == CounterplayLootWindowState.D21FlashlightRecovery ||
                   state == CounterplayLootWindowState.D22AwaitMainShot ||
                   state == CounterplayLootWindowState.D22Dodging ||
                   state == CounterplayLootWindowState.D22AwaitMainDryFire;
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }

        private static void SetActiveIfNotHeld(GameObject target, bool active)
        {
            InteractableEntity entity = target == null ? null : target.GetComponent<InteractableEntity>();
            if (entity == null || !entity.IsHeld)
            {
                SetActive(target, active);
            }
        }
    }
}
