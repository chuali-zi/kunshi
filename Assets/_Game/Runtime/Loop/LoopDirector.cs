using System;
using ConGame.Domain.Facts;
using ConGame.Domain.Flow;
using ConGame.Domain.Loop;
using ConGame.Domain.Persistence;
using ConGame.Runtime.AI;
using ConGame.Runtime.Config;
using ConGame.Runtime.Events;
using ConGame.Runtime.Facts;
using ConGame.Runtime.Flow;
using ConGame.Runtime.Input;
using ConGame.Runtime.Persistence;
using ConGame.Runtime.Player;
using UnityEngine;

namespace ConGame.Runtime.Loop
{

    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent]
    public sealed class LoopDirector : MonoBehaviour
    {
        [SerializeField] private GameConfigAsset config;
        [SerializeField] private GameFlowController flow;
        [SerializeField] private LoopResetRegistry resetRegistry;
        [SerializeField] private GameplayFactService facts;
        [SerializeField] private GameplayEventHub events;
        [SerializeField] private PlayerInputRouter input;
        [SerializeField] private PlayerMotor playerMotor;
        [SerializeField] private KillerDirector killer;

        private EntryClock _entryClock;
        private DarkAdaptationTracker _darkAdaptation;
        private JsonSaveStore _saveStore;
        private PersistentProgressV1 _progress;
        private bool _initialized;
        private bool _resetting;
        private int _loopEpoch;

        public event Action EntryWarning;
        public event Action TelevisionCrackEchoRequested;
        public event Action<int, string> LoopResetCompleted;

        public int LoopEpoch => _loopEpoch;
        public string LastOutcomeId { get; private set; }
        public PersistentProgressV1 Progress => _progress;
        public EntryClock EntryClock => _entryClock;

        public void Configure(
            GameConfigAsset gameConfig,
            GameFlowController flowController,
            LoopResetRegistry registry,
            GameplayFactService factService,
            GameplayEventHub eventHub,
            PlayerInputRouter inputRouter,
            PlayerMotor motor,
            KillerDirector killerDirector)
        {
            config = gameConfig;
            flow = flowController;
            resetRegistry = registry;
            facts = factService;
            events = eventHub;
            input = inputRouter;
            playerMotor = motor;
            killer = killerDirector;
        }

        public void Initialize(JsonSaveStore saveStore = null)
        {
            if (_initialized)
            {
                return;
            }

            _saveStore = saveStore ?? new JsonSaveStore();
            _progress = _saveStore.Load();
            _loopEpoch = _progress.loopIndex;
            _entryClock = new EntryClock(config == null ? EntryClockSettings.Default : config.CreateEntryClockSettings());
            _darkAdaptation = new DarkAdaptationTracker(_progress.darkAdaptationXp);

            flow.Initialize(GamePhase.Awakening);
            flow.PhaseChanged += OnPhaseChanged;
            input.MeaningfulAction += NotifyMeaningfulAction;
            events.NoiseEmitted += OnNoiseEmitted;
            events.KillerStimulusEmitted += OnKillerStimulus;
            _initialized = true;

            ConsumeCrackEchoIfNeeded();
            flow.Request(GamePhase.FreeRoam, TransitionPriority.Normal, "Awakening is immediately interruptible in M1.");
        }

        private void OnDestroy()
        {
            if (!_initialized)
            {
                return;
            }

            flow.PhaseChanged -= OnPhaseChanged;
            input.MeaningfulAction -= NotifyMeaningfulAction;
            events.NoiseEmitted -= OnNoiseEmitted;
            events.KillerStimulusEmitted -= OnKillerStimulus;
        }

        private void Update()
        {
            if (!_initialized || _resetting)
            {
                return;
            }

            GamePhase phase = flow.CurrentPhase;
            bool clockActive = phase == GamePhase.Awakening || phase == GamePhase.FreeRoam;
            if (clockActive)
            {
                ProcessEntrySignal(_entryClock.Advance(Time.deltaTime));
            }

            bool persistentBlack = facts.CaptureSnapshot().RoomLight == RoomLightState.PersistentBlack;
            DarkAdaptationResult result = _darkAdaptation.Advance(persistentBlack, Time.deltaTime);
            if (result.ExperienceGained > 0)
            {
                _progress.darkAdaptationXp = _darkAdaptation.Experience;
                _saveStore.SaveAtomic(_progress);
            }
        }

        public void NotifyMeaningfulAction()
        {
            if (!_initialized || _entryClock.EntryIssued)
            {
                return;
            }

            facts.SetMeaningfulInput(true);
            ProcessEntrySignal(_entryClock.MarkMeaningfulAction());
        }

        public void RequestKill(string outcomeId)
        {
            if (!_initialized || _resetting || flow.CurrentPhase >= GamePhase.EndingE1)
            {
                return;
            }

            LastOutcomeId = string.IsNullOrWhiteSpace(outcomeId) ? "D01" : outcomeId;
            flow.Request(GamePhase.KillImpact, TransitionPriority.Death, LastOutcomeId);
        }

        public void SaveProgress()
        {
            if (_progress != null)
            {
                _saveStore.SaveAtomic(_progress);
            }
        }

        private void OnNoiseEmitted(NoiseEvent noise)
        {
            if (noise.Level == NoiseLevel.Loud && _initialized)
            {
                ProcessEntrySignal(_entryClock.MarkLoudNoise());
            }
        }

        private void OnKillerStimulus(KillerStimulus stimulus)
        {
            killer?.HandleStimulus(stimulus);
        }

        private void ProcessEntrySignal(EntryClockSignal signal)
        {
            if ((signal & EntryClockSignal.Warning) != 0)
            {
                EntryWarning?.Invoke();
                Debug.Log("[ConGame] Killer entry warning.");
            }

            if ((signal & EntryClockSignal.Enter) != 0)
            {
                flow.Request(GamePhase.Breach, TransitionPriority.Normal, "Entry clock elapsed.");
            }
        }

        private void OnPhaseChanged(GamePhase previous, GamePhase current, string reason)
        {
            if (current == GamePhase.Breach)
            {
                killer.BeginEntry(_loopEpoch, RequestKill);
                flow.Request(GamePhase.Counterplay, TransitionPriority.Normal, "Killer crossed the room threshold.");
            }
            else if (current == GamePhase.KillImpact)
            {
                PerformReset();
            }
            else if (current == GamePhase.Awakening)
            {
                ConsumeCrackEchoIfNeeded();
            }
        }

        private void PerformReset()
        {
            if (_resetting)
            {
                return;
            }

            _resetting = true;
            playerMotor?.SetInputLocked(true);
            input?.SetGameplayEnabled(false);

            FactSnapshot beforeReset = facts.CaptureSnapshot();
            if (beforeReset.TelevisionDamage == TelevisionDamageState.Cracked)
            {
                _progress.tvCrackEchoPending = true;
            }

            flow.Request(GamePhase.LoopReset, TransitionPriority.Normal, LastOutcomeId);
            flow.CommitPending();

            int previousEpoch = _loopEpoch;
            _loopEpoch++;
            resetRegistry.ResetAll(previousEpoch, _loopEpoch);
            _entryClock.Reset();
            _darkAdaptation.ResetPartial();
            _progress.AdvanceLoop();
            _saveStore.SaveAtomic(_progress);

            flow.Request(GamePhase.Awakening, TransitionPriority.Normal, "Room restored.");
            flow.CommitPending();
            flow.Request(GamePhase.FreeRoam, TransitionPriority.Normal, "Awakening input available.");

            input?.SetGameplayEnabled(true);
            playerMotor?.SetInputLocked(false);
            _resetting = false;
            LoopResetCompleted?.Invoke(_progress.loopIndex, LastOutcomeId);
        }

        private void ConsumeCrackEchoIfNeeded()
        {
            if (_progress == null || !_progress.tvCrackEchoPending)
            {
                return;
            }

            TelevisionCrackEchoRequested?.Invoke();
            Debug.Log("[ConGame] Consumed one-loop television crack echo.");
            _progress.tvCrackEchoPending = false;
            _saveStore.SaveAtomic(_progress);
        }
    }
}
