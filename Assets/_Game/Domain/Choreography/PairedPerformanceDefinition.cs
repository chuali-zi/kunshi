using System;
using System.Collections.Generic;
using ConGame.Domain.Counterplay;

namespace ConGame.Domain.Choreography
{
    /// <summary>
    /// Physical contact relationship between the two performers during a stage.
    /// Contact/Detach semantic markers are derived from transitions of this state so
    /// props and gameplay arbitration never depend on animation clip lengths.
    /// </summary>
    public enum PairedContactState
    {
        Separated = 0,
        Contact = 1,
        Detached = 2
    }

    public enum PairedCancelReason
    {
        GameplayInterrupt = 0,
        LoopReset = 1
    }

    /// <summary>
    /// Atomic cleanup obligations produced when a paired performance is cancelled.
    /// The presentation player executes every flag in a single cancel call.
    /// </summary>
    [Flags]
    public enum PairedCleanupAction
    {
        None = 0,
        StopAnimationGraph = 1,
        ReleaseRootLock = 2,
        RestorePropOwnership = 4,
        RestoreFacts = 8,
        All = StopAnimationGraph | ReleaseRootLock | RestorePropOwnership | RestoreFacts
    }

    /// <summary>
    /// One authored beat of a paired performance. Durations are design beats from the
    /// script (game_flow_script D08/D11/D21), never derived from animation lengths.
    /// </summary>
    [Serializable]
    public struct PairedPerformanceStageDefinition
    {
        public PairedPerformanceStageDefinition(
            string stageId,
            float durationSeconds,
            PairedContactState contactState,
            bool locksRoots = true,
            bool allowGameplayCancel = true,
            QteWindowDefinition[] qteWindows = null,
            string[] enterEvents = null,
            string[] exitEvents = null,
            string[] qteSuccessEvents = null,
            string[] qteFailureEvents = null)
        {
            StageId = stageId ?? string.Empty;
            DurationSeconds = durationSeconds;
            ContactState = contactState;
            LocksRoots = locksRoots;
            AllowGameplayCancel = allowGameplayCancel;
            QteWindows = qteWindows;
            EnterEvents = enterEvents;
            ExitEvents = exitEvents;
            QteSuccessEvents = qteSuccessEvents;
            QteFailureEvents = qteFailureEvents;
        }

        public string StageId;
        public float DurationSeconds;
        public PairedContactState ContactState;
        public bool LocksRoots;
        public bool AllowGameplayCancel;
        public QteWindowDefinition[] QteWindows;
        public string[] EnterEvents;
        public string[] ExitEvents;
        public string[] QteSuccessEvents;
        public string[] QteFailureEvents;

        public float SafeDurationSeconds => Math.Max(0.05f, DurationSeconds);
        public bool HasQte => QteWindows != null && QteWindows.Length > 0;
    }

    /// <summary>
    /// Semantic event emitted by the phase machine. Presentation fans these out to
    /// prop ownership, VFX/audio and gameplay arbitration consumers.
    /// </summary>
    public readonly struct PairedPerformanceSemanticEvent
    {
        public PairedPerformanceSemanticEvent(
            string eventId,
            string outcomeId,
            string stageId,
            PairedContactState contactState,
            int loopEpoch)
        {
            EventId = eventId ?? string.Empty;
            OutcomeId = outcomeId ?? string.Empty;
            StageId = stageId ?? string.Empty;
            ContactState = contactState;
            LoopEpoch = loopEpoch;
        }

        public string EventId { get; }
        public string OutcomeId { get; }
        public string StageId { get; }
        public PairedContactState ContactState { get; }
        public int LoopEpoch { get; }
    }

    /// <summary>Well-known machine-generated event suffixes ("{outcomeId}.{suffix}").</summary>
    public static class PairedPerformanceEventIds
    {
        public const string Started = "Started";
        public const string Completed = "Completed";
        public const string Cancelled = "Cancelled";
        public const string Contact = "Contact";
        public const string Detach = "Detach";
        public const string LaunchRejected = "LaunchRejected";

        public static string Compose(string outcomeId, string suffix)
        {
            return string.IsNullOrEmpty(outcomeId) ? suffix ?? string.Empty : outcomeId + "." + suffix;
        }
    }

    /// <summary>
    /// Pure-data paired performance definition: stage list, transition data, cancel
    /// boundaries and the D01 fallback contract (gameplay_logic_spec §8/§12).
    /// </summary>
    public sealed class PairedPerformanceDefinition
    {
        private readonly PairedPerformanceStageDefinition[] _stages;

        public PairedPerformanceDefinition(
            string outcomeId,
            string fallbackOutcomeId,
            float nominalSeparation,
            in PairedAnchorTolerance tolerance,
            IList<PairedPerformanceStageDefinition> stages)
        {
            if (stages == null || stages.Count == 0)
            {
                throw new ArgumentException("Paired performance requires at least one stage.", nameof(stages));
            }

            OutcomeId = outcomeId ?? string.Empty;
            FallbackOutcomeId = string.IsNullOrWhiteSpace(fallbackOutcomeId) ? "D01" : fallbackOutcomeId;
            NominalSeparation = Math.Max(0f, nominalSeparation);
            Tolerance = tolerance;
            _stages = new PairedPerformanceStageDefinition[stages.Count];
            for (int i = 0; i < stages.Count; i++)
            {
                _stages[i] = stages[i];
            }
        }

        public string OutcomeId { get; }
        public string FallbackOutcomeId { get; }
        public float NominalSeparation { get; }
        public PairedAnchorTolerance Tolerance { get; }
        public IReadOnlyList<PairedPerformanceStageDefinition> Stages => _stages;

        public float TotalDuration
        {
            get
            {
                float total = 0f;
                for (int i = 0; i < _stages.Length; i++)
                {
                    total += _stages[i].SafeDurationSeconds;
                }

                return total;
            }
        }

        /// <summary>Content validation used by catalog tests and editor validators.</summary>
        public bool Validate(out string error)
        {
            if (string.IsNullOrWhiteSpace(OutcomeId))
            {
                error = "Outcome id is empty.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(FallbackOutcomeId))
            {
                error = "Fallback outcome id is empty.";
                return false;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < _stages.Length; i++)
            {
                PairedPerformanceStageDefinition stage = _stages[i];
                if (string.IsNullOrWhiteSpace(stage.StageId))
                {
                    error = $"Stage {i} has an empty id.";
                    return false;
                }

                if (!seen.Add(stage.StageId))
                {
                    error = $"Duplicate stage id '{stage.StageId}'.";
                    return false;
                }

                if (stage.DurationSeconds < 0f)
                {
                    error = $"Stage '{stage.StageId}' has a negative duration.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }
    }
}
