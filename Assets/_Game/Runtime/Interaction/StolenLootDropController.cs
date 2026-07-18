using System;
using ConGame.Domain.Counterplay;
using ConGame.Domain.Facts;
using ConGame.Domain.Interaction;
using ConGame.Runtime.AI;
using ConGame.Runtime.Counterplay;
using ConGame.Runtime.Events;
using ConGame.Runtime.Loop;
using ConGame.Runtime.Player;
using UnityEngine;

namespace ConGame.Runtime.Interaction
{
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
        Resolved = 8
    }

    [DisallowMultipleComponent]
    public sealed class StolenLootDropController : MonoBehaviour, ILoopResettable
    {
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
        private int _activeLoopEpoch;
        private float _remainingWindow;
        private float _dodgeElapsed;
        private Vector3 _dodgeStart;
        private Vector3 _dodgeTarget;

        public int ResetOrder => 95;
        public CounterplayLootWindowState State { get; private set; } = CounterplayLootWindowState.Inactive;
        public float RemainingWindowSeconds => Mathf.Max(0f, _remainingWindow);
        public FirearmUseHandler MainFirearm => _mainFirearm;
        public FirearmUseHandler BackupFirearm => _backupFirearm;

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
                    State == CounterplayLootWindowState.D21FlashlightArmed ? "D21" : "D22";
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
            State = CounterplayLootWindowState.Inactive;
            _remainingWindow = 0f;
        }

        public void Restore(in LoopResetContext context)
        {
            SetActive(pistolDrop, _initialPistolActive);
            SetActive(flashlightDrop, _initialFlashlightActive);
            SetActive(backupPistolDrop, _initialBackupActive);
            _mainFirearm?.Arm(0);
            _backupFirearm?.Arm(0);
            _activeLoopEpoch = context.NextEpoch;
            State = CounterplayLootWindowState.Inactive;
            _remainingWindow = 0f;
        }

        public void Resume(in LoopResetContext context)
        {
        }

        public void NotifyKillerIlluminated(in FlashlightHit hit)
        {
            if (State == CounterplayLootWindowState.D21FlashlightArmed)
            {
                CompleteOutcome("D21", true);
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
            if (string.Equals(outcomeId, "D08", StringComparison.Ordinal))
            {
                HideWindowLoot();
                State = CounterplayLootWindowState.D08Struggle;
                _remainingWindow = 0f;
                return;
            }

            if (string.Equals(outcomeId, "D21", StringComparison.Ordinal))
            {
                SetActive(pistolDrop, true);
                SetActive(flashlightDrop, true);
                SetActive(backupPistolDrop, false);
                _mainFirearm?.Arm(0);
                State = CounterplayLootWindowState.D21Choice;
                _remainingWindow = d21ChoiceWindowSeconds;
                adapter?.SetCounterplayInputWindows(CounterplayInputWindow.Grab);
                return;
            }

            if (string.Equals(outcomeId, "D22", StringComparison.Ordinal))
            {
                SetActive(pistolDrop, true);
                SetActiveIfNotHeld(flashlightDrop, false);
                SetActive(backupPistolDrop, false);
                _mainFirearm?.Arm(1);
                _backupFirearm?.Arm(0);
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
            if (itemState == ItemPhysicalState.Held && State == CounterplayLootWindowState.D21Choice)
            {
                State = CounterplayLootWindowState.D21FlashlightArmed;
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
            _dodgeElapsed = 0f;
            if (killer != null)
            {
                _dodgeStart = killer.transform.position;
                _dodgeTarget = _dodgeStart + killer.transform.right * dodgeDistance;
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
            SetActive(backupPistolDrop, true);
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
            adapter?.RequestOutcomeCompletion(outcomeId, _activeLoopEpoch, markD21Seen);
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

        private void ResolveDependencies()
        {
            _pistolStateful = pistolDrop == null ? null : pistolDrop.GetComponent<StatefulWorldItem>();
            _flashlightStateful = flashlightDrop == null ? null : flashlightDrop.GetComponent<StatefulWorldItem>();
            _backupStateful = backupPistolDrop == null ? null : backupPistolDrop.GetComponent<StatefulWorldItem>();
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
