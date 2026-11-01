using System;
using System.Collections.Generic;
using ConGame.Domain.Choreography;

namespace ConGame.Presentation.Choreography
{
    /// <summary>
    /// Detailed outcome of a paired launch attempt. The Runtime/AI contract
    /// (IPerformanceLaunchRequest.TryLaunch) only returns bool; bootstrap and tests
    /// can use the richer overload on PairedPerformancePlayer to inspect why a launch
    /// was rejected and whether the D01 fallback engaged.
    /// </summary>
    public readonly struct PairedPerformanceLaunchVerdict
    {
        public PairedPerformanceLaunchVerdict(
            bool accepted,
            bool fellBackToD01,
            string rejectReason,
            in PairedAnchorValidationResult anchorResult)
        {
            Accepted = accepted;
            FellBackToD01 = fellBackToD01;
            RejectReason = rejectReason ?? string.Empty;
            AnchorResult = anchorResult;
        }

        public bool Accepted { get; }
        public bool FellBackToD01 { get; }
        public string RejectReason { get; }
        public PairedAnchorValidationResult AnchorResult { get; }
    }

    /// <summary>
    /// Presentation-side semantic event fan-out for paired performances. Props, VFX,
    /// audio and the visual-ownership layer subscribe here; GameplayEventHub stays
    /// untouched.
    /// </summary>
    public sealed class PairedPerformanceEventDispatcher
    {
        private const int MaxRecentEvents = 64;
        private readonly List<PairedPerformanceSemanticEvent> _recent =
            new List<PairedPerformanceSemanticEvent>(MaxRecentEvents);

        public event Action<PairedPerformanceSemanticEvent> SemanticEventEmitted;

        public IReadOnlyList<PairedPerformanceSemanticEvent> RecentEvents => _recent;

        public void Publish(in PairedPerformanceSemanticEvent semanticEvent)
        {
            if (_recent.Count >= MaxRecentEvents)
            {
                _recent.RemoveAt(0);
            }

            _recent.Add(semanticEvent);
            SemanticEventEmitted?.Invoke(semanticEvent);
        }

        public void Publish(IEnumerable<PairedPerformanceSemanticEvent> semanticEvents)
        {
            if (semanticEvents == null)
            {
                return;
            }

            foreach (PairedPerformanceSemanticEvent semanticEvent in semanticEvents)
            {
                Publish(semanticEvent);
            }
        }

        public void ClearHistory()
        {
            _recent.Clear();
        }
    }
}
