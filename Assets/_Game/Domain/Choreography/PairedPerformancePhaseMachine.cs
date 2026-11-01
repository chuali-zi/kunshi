using System;
using System.Collections.Generic;
using ConGame.Domain.Counterplay;

namespace ConGame.Domain.Choreography
{
    /// <summary>
    /// Pure C# stage state machine for paired performances (D08/D11/D21). Advances by
    /// authored beat durations, emits stage enter/exit plus Contact/Detach semantic
    /// markers, integrates per-stage QTE windows, and produces an atomic cleanup
    /// contract on cancel. All callbacks are loop-epoch checked; stale-loop input is
    /// discarded. Contains no engine dependencies so it is fully EditMode testable.
    /// </summary>
    public sealed class PairedPerformancePhaseMachine
    {
        private readonly PairedPerformanceDefinition _definition;
        private readonly float _accessibilityThresholdScale;
        private int _loopEpoch;
        private int _stageIndex = -1;
        private float _stageRemaining;
        private QteSequenceCounter _qteCounter;
        private QteSequenceSnapshot _lastQteSnapshot;
        private bool _hasQteSnapshot;
        private bool _started;
        private bool _completed;
        private bool _cancelled;

        public PairedPerformancePhaseMachine(
            PairedPerformanceDefinition definition,
            float accessibilityThresholdScale = 1f)
        {
            _definition = definition ?? throw new ArgumentNullException(nameof(definition));
            _accessibilityThresholdScale = Math.Max(0.1f, Math.Min(1f, accessibilityThresholdScale));
        }

        public PairedPerformanceDefinition Definition => _definition;
        public int LoopEpoch => _loopEpoch;
        public int CurrentStageIndex => _stageIndex;
        public bool IsRunning => _started && !_completed && !_cancelled;
        public bool IsComplete => _completed;
        public bool IsCancelled => _cancelled;
        public bool IsPaused { get; private set; }
        public PairedContactState CurrentContactState { get; private set; } = PairedContactState.Separated;
        public bool HasQteSnapshot => _hasQteSnapshot;
        public QteSequenceSnapshot LastQteSnapshot => _lastQteSnapshot;

        public string CurrentStageId
        {
            get
            {
                IReadOnlyList<PairedPerformanceStageDefinition> stages = _definition.Stages;
                return _stageIndex >= 0 && _stageIndex < stages.Count
                    ? stages[_stageIndex].StageId
                    : string.Empty;
            }
        }

        public bool TryGetCurrentStage(out PairedPerformanceStageDefinition stage)
        {
            IReadOnlyList<PairedPerformanceStageDefinition> stages = _definition.Stages;
            if (_stageIndex >= 0 && _stageIndex < stages.Count)
            {
                stage = stages[_stageIndex];
                return true;
            }

            stage = default;
            return false;
        }

        public void Start(int loopEpoch, ICollection<PairedPerformanceSemanticEvent> events)
        {
            if (_started || events == null)
            {
                return;
            }

            _started = true;
            _loopEpoch = loopEpoch;
            events.Add(new PairedPerformanceSemanticEvent(
                PairedPerformanceEventIds.Compose(_definition.OutcomeId, PairedPerformanceEventIds.Started),
                _definition.OutcomeId,
                string.Empty,
                CurrentContactState,
                _loopEpoch));
            EnterStage(0, events);
        }

        public void Advance(
            float deltaTime,
            bool interactIsPressed,
            int loopEpoch,
            ICollection<PairedPerformanceSemanticEvent> events)
        {
            if (deltaTime < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            }

            if (!IsRunning || IsPaused || loopEpoch != _loopEpoch || events == null)
            {
                return;
            }

            if (_qteCounter != null && !_qteCounter.IsComplete)
            {
                _lastQteSnapshot = _qteCounter.Advance(deltaTime, interactIsPressed);
                _hasQteSnapshot = true;
            }

            float remaining = deltaTime;
            int guard = _definition.Stages.Count + 1;
            while (IsRunning && remaining >= _stageRemaining && guard-- > 0)
            {
                remaining -= _stageRemaining;
                CompleteCurrentStage(events);
                if (IsRunning)
                {
                    _stageRemaining = CurrentStageDuration();
                }
            }

            if (IsRunning)
            {
                _stageRemaining = Math.Max(0f, _stageRemaining - remaining);
            }
        }

        public void SetPaused(bool paused)
        {
            if (IsRunning)
            {
                IsPaused = paused;
            }
        }

        public bool TryJumpToStage(string stageId, int loopEpoch,
            ICollection<PairedPerformanceSemanticEvent> events)
        {
            if (!IsRunning || loopEpoch != _loopEpoch || string.IsNullOrEmpty(stageId) || events == null)
            {
                return false;
            }

            int targetIndex = -1;
            for (int i = 0; i < _definition.Stages.Count; i++)
            {
                if (string.Equals(_definition.Stages[i].StageId, stageId, StringComparison.Ordinal))
                {
                    targetIndex = i;
                    break;
                }
            }

            if (targetIndex <= _stageIndex)
            {
                return false;
            }

            if (TryGetCurrentStage(out PairedPerformanceStageDefinition current))
            {
                EmitStageEvents(current.ExitEvents, current.StageId, events);
            }

            _qteCounter = null;
            EnterStage(targetIndex, events);
            IsPaused = false;
            return true;
        }

        /// <summary>
        /// Cancels the performance. A gameplay interrupt is refused outside the stage's
        /// cancel boundary; a loop reset always cancels. Returns the atomic cleanup
        /// obligations the caller must execute in the same call.
        /// </summary>
        public PairedCleanupAction Cancel(
            PairedCancelReason reason,
            int loopEpoch,
            ICollection<PairedPerformanceSemanticEvent> events)
        {
            if (!IsRunning || loopEpoch != _loopEpoch)
            {
                return PairedCleanupAction.None;
            }

            if (reason == PairedCancelReason.GameplayInterrupt &&
                TryGetCurrentStage(out PairedPerformanceStageDefinition current) &&
                !current.AllowGameplayCancel)
            {
                return PairedCleanupAction.None;
            }

            if (events != null)
            {
                if (TryGetCurrentStage(out PairedPerformanceStageDefinition stage))
                {
                    EmitStageEvents(stage.ExitEvents, stage.StageId, events);
                }

                EmitContactTransition(PairedContactState.Detached, CurrentStageId, events);
                events.Add(new PairedPerformanceSemanticEvent(
                    PairedPerformanceEventIds.Compose(_definition.OutcomeId, PairedPerformanceEventIds.Cancelled),
                    _definition.OutcomeId,
                    CurrentStageId,
                    CurrentContactState,
                    _loopEpoch));
            }

            _cancelled = true;
            _qteCounter = null;
            return PairedCleanupAction.All;
        }

        private void EnterStage(int stageIndex, ICollection<PairedPerformanceSemanticEvent> events)
        {
            IReadOnlyList<PairedPerformanceStageDefinition> stages = _definition.Stages;
            _stageIndex = stageIndex;
            PairedPerformanceStageDefinition stage = stages[stageIndex];
            _stageRemaining = stage.SafeDurationSeconds;
            _qteCounter = stage.HasQte
                ? new QteSequenceCounter(stage.QteWindows, _accessibilityThresholdScale)
                : null;
            _hasQteSnapshot = false;

            EmitContactTransition(stage.ContactState, stage.StageId, events);
            EmitStageEvents(stage.EnterEvents, stage.StageId, events);
        }

        private void CompleteCurrentStage(ICollection<PairedPerformanceSemanticEvent> events)
        {
            IReadOnlyList<PairedPerformanceStageDefinition> stages = _definition.Stages;
            if (!TryGetCurrentStage(out PairedPerformanceStageDefinition stage))
            {
                return;
            }

            EmitStageEvents(stage.ExitEvents, stage.StageId, events);
            if (stage.HasQte)
            {
                bool succeeded = _qteCounter != null && _qteCounter.AllWindowsSucceeded;
                EmitStageEvents(succeeded ? stage.QteSuccessEvents : stage.QteFailureEvents, stage.StageId, events);
            }

            int nextIndex = _stageIndex + 1;
            _qteCounter = null;
            if (nextIndex >= stages.Count)
            {
                EmitContactTransition(PairedContactState.Detached, stage.StageId, events);
                _completed = true;
                events.Add(new PairedPerformanceSemanticEvent(
                    PairedPerformanceEventIds.Compose(_definition.OutcomeId, PairedPerformanceEventIds.Completed),
                    _definition.OutcomeId,
                    stage.StageId,
                    CurrentContactState,
                    _loopEpoch));
                return;
            }

            EnterStage(nextIndex, events);
        }

        private float CurrentStageDuration()
        {
            return TryGetCurrentStage(out PairedPerformanceStageDefinition stage)
                ? stage.SafeDurationSeconds
                : 0f;
        }

        private void EmitContactTransition(
            PairedContactState next,
            string stageId,
            ICollection<PairedPerformanceSemanticEvent> events)
        {
            PairedContactState previous = CurrentContactState;
            if (previous == next)
            {
                return;
            }

            CurrentContactState = next;
            if (next == PairedContactState.Contact)
            {
                events.Add(new PairedPerformanceSemanticEvent(
                    PairedPerformanceEventIds.Compose(_definition.OutcomeId, PairedPerformanceEventIds.Contact),
                    _definition.OutcomeId,
                    stageId,
                    next,
                    _loopEpoch));
            }
            else if (previous == PairedContactState.Contact)
            {
                events.Add(new PairedPerformanceSemanticEvent(
                    PairedPerformanceEventIds.Compose(_definition.OutcomeId, PairedPerformanceEventIds.Detach),
                    _definition.OutcomeId,
                    stageId,
                    next,
                    _loopEpoch));
            }
        }

        private void EmitStageEvents(
            string[] eventIds,
            string stageId,
            ICollection<PairedPerformanceSemanticEvent> events)
        {
            if (eventIds == null)
            {
                return;
            }

            for (int i = 0; i < eventIds.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(eventIds[i]))
                {
                    continue;
                }

                events.Add(new PairedPerformanceSemanticEvent(
                    eventIds[i],
                    _definition.OutcomeId,
                    stageId,
                    CurrentContactState,
                    _loopEpoch));
            }
        }
    }
}
