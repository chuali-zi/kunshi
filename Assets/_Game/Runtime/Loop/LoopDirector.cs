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
        [SerializeField]
        private GameConfigAsset config;
        [SerializeField]
        private GameFlowController flow;
        [SerializeField]
        private LoopResetRegistry resetRegistry;
        [SerializeField]
        private GameplayFactService facts;
        [SerializeField]
        private GameplayEventHub events;
        [SerializeField]
        private PlayerInputRouter input;
        [SerializeField]
        private PlayerMotor playerMotor;

        [SerializeField]
        private RoomDoorBreachController breach;
        [SerializeField]
        private KillerDirector killer;
        [SerializeField]
        private DarkAdaptationConfigAsset darkAdaptationConfig;

        private EntryClock _entryClock;
        private DarkAdaptationTracker _darkAdaptation;
        private JsonSaveStore _saveStore;
        private PersistentProgressV1 _progress;
        private bool _initialized;
        private bool _resetting;
        
        private float _awakeningRemaining;
private int _loopEpoch;
        private Color _baseAmbient = new Color(0.08f, 0.08f, 0.09f);

        
        public event Action<bool> AwakeningStarted;
public event Action EntryWarning;
        public event Action TelevisionCrackEchoRequested;
        public event Action<int, string> LoopResetCompleted;
        public event Action<string> EndingFightCompleted;

        public int LoopEpoch => _loopEpoch;
        public string LastOutcomeId { get; private set; }
        public PersistentProgressV1 Progress => _progress;
        public EntryClock EntryClock => _entryClock;
        public float CurrentAmbientIntensity { get; private set; }

        public void Configure(GameConfigAsset gameConfig, GameFlowController flowController, LoopResetRegistry registry,
                              GameplayFactService factService, GameplayEventHub eventHub, PlayerInputRouter inputRouter,
                              PlayerMotor motor, KillerDirector killerDirector,
                              RoomDoorBreachController breachController = null,
                              DarkAdaptationConfigAsset darkConfig = null)
        {
            config = gameConfig;
            flow = flowController;
            resetRegistry = registry;
            facts = factService;
            events = eventHub;
            input = inputRouter;
            playerMotor = motor;
            killer = killerDirector;
            breach = breachController;
            darkAdaptationConfig = darkConfig;
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
            _entryClock =
                new EntryClock(config == null ? EntryClockSettings.Default : config.CreateEntryClockSettings());
            _darkAdaptation = new DarkAdaptationTracker(_progress.darkAdaptationXp);
            _baseAmbient = RenderSettings.ambientLight;
            ApplyDarkAdaptationAmbient();

            flow.Initialize(GamePhase.Awakening);
            flow.PhaseChanged += OnPhaseChanged;
            input.MeaningfulAction += NotifyMeaningfulAction;
            events.NoiseEmitted += OnNoiseEmitted;
            events.KillerStimulusEmitted += OnKillerStimulus;
            _initialized = true;
            ConsumeCrackEchoIfNeeded();
            BeginAwakening();
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

            
            if (flow.CurrentPhase == GamePhase.Awakening && _awakeningRemaining > 0f)
            {
                _awakeningRemaining -= Time.deltaTime;
                if (_awakeningRemaining <= 0f)
                {
                    flow.Request(GamePhase.FreeRoam, TransitionPriority.Normal, "Awakening performance completed.");
                }
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
                ApplyDarkAdaptationAmbient();
                TrySaveProgress();
            }
            else if (persistentBlack)
            {
                ApplyDarkAdaptationAmbient();
            }
        }

        public void NotifyEndingFightCompleted(string outcomeId)
        {
            EndingFightCompleted?.Invoke(string.IsNullOrWhiteSpace(outcomeId) ? "D01" : outcomeId);
        }

        public void RequestKill(string outcomeId)
        {
            if (!_initialized || _resetting)
            {
                return;
            }

            string resolved = string.IsNullOrWhiteSpace(outcomeId) ? "D01" : outcomeId;
            if (flow.CurrentPhase == GamePhase.EndingE2)
            {
                NotifyEndingFightCompleted(resolved);
                return;
            }

            if (flow.CurrentPhase != GamePhase.Counterplay)
            {
                return;
            }

            LastOutcomeId = resolved;
            flow.Request(GamePhase.KillImpact, TransitionPriority.Death, LastOutcomeId);
        }

        public void NotifyMeaningfulAction()
        {
            if (!_initialized || _entryClock.EntryIssued)
            {
                return;
            }

            if (flow.CurrentPhase == GamePhase.Awakening)
            {
                flow.Request(GamePhase.FreeRoam, TransitionPriority.Normal, "Awakening interrupted by player input.");
            }

            facts.SetMeaningfulInput(true);
            ProcessEntrySignal(_entryClock.MarkMeaningfulAction());
        }

        public void SaveProgress()
        {
            if (_progress != null)
            {
                TrySaveProgress();
            }
        }

public void MarkEndingCompleted()
        {
            if (_progress == null)
            {
                return;
            }

            _progress.endingCompleted = true;
            TrySaveProgress();
        }

public void MarkD21Seen()
        {
            if (_progress == null || _progress.d21Seen)
            {
                return;
            }

            _progress.d21Seen = true;
            TrySaveProgress();
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
                playerMotor?.SetInputLocked(true);
                input?.SetGameplayEnabled(false);

                if (breach == null)
                {
                    OnBreachCompleted();
                }
                else
                {
                    float baseDuration = config == null ? 0.9f : config.BreachDuration;
                    float extra = facts == null ? 0f : facts.DoorBlockBreachExtraSeconds;
                    breach.Begin(_loopEpoch, OnBreachCompleted, baseDuration + extra);
                }
            }
            else if (current == GamePhase.KillImpact)
            {
                PerformReset();
            }
            else if (current == GamePhase.Awakening)
            {
                ConsumeCrackEchoIfNeeded();
                BeginAwakening();
            }
        }

        private void OnBreachCompleted()
        {
            if (!_initialized || _resetting || flow.CurrentPhase != GamePhase.Breach)
            {
                return;
            }

            killer?.BeginEntry(_loopEpoch, RequestKill);
            flow.Request(GamePhase.Counterplay, TransitionPriority.Normal, "Door breach completed.");
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
            TrySaveProgress();

            flow.Request(GamePhase.Awakening, TransitionPriority.Normal, "Room restored.");
            flow.CommitPending();


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
            TrySaveProgress();
        }
    

private void BeginAwakening()
        {
            int loopIndex = _progress == null ? 1 : Mathf.Max(1, _progress.loopIndex);
            _awakeningRemaining = Mathf.Clamp(4.2f - (loopIndex - 1) * 0.25f, 2.5f, 4.2f);
            ApplyDarkAdaptationAmbient();
            AwakeningStarted?.Invoke(_progress == null || _progress.loopIndex <= 1);
        }

        private void ApplyDarkAdaptationAmbient()
        {
            int xp = _darkAdaptation == null ? 0 : _darkAdaptation.Experience;
            float intensity = darkAdaptationConfig == null ? 0.002f : darkAdaptationConfig.GetIntensity(xp);
            CurrentAmbientIntensity = intensity;
            bool persistentBlack = facts != null &&
                                   facts.CaptureSnapshot().RoomLight == RoomLightState.PersistentBlack;
            if (persistentBlack)
            {
                RenderSettings.ambientLight = new Color(intensity, intensity, intensity * 1.05f);
            }
            else
            {
                RenderSettings.ambientLight = Color.Lerp(_baseAmbient, Color.white * 0.2f, intensity * 4f);
            }
        }


private void TrySaveProgress()
        {
            if (_saveStore == null || _progress == null)
            {
                return;
            }

            try
            {
                _saveStore.SaveAtomic(_progress);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[ConGame] Progress save failed without blocking gameplay: {exception.Message}", this);
            }
        }
}
}
