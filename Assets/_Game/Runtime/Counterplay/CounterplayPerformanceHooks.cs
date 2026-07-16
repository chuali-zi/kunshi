using ConGame.Domain.Counterplay;

namespace ConGame.Runtime.Counterplay
{
    /// <summary>
    /// Adapter seam for the main input layer. The presentation player samples held
    /// state and lets the pure-domain counter perform rising-edge detection.
    /// </summary>
    public interface ICounterplayPerformanceHookTarget
    {
        void ConfigureHooks(
            ICounterplayPerformanceInput performanceInput,
            ICounterplayPerformanceProgress progress);
        void PrepareLoopEpoch(int loopEpoch);
    }

    public interface ICounterplayPerformanceInput
    {
        bool InteractIsPressed { get; }
        float AccessibilityThresholdScale { get; }
    }

    /// <summary>
    /// Receives non-visual performance progress. The main flow layer owns persistence,
    /// fact changes and transitions to chained outcomes such as D17 or D22.
    /// </summary>
    public interface ICounterplayPerformanceProgress
    {
        void OnPerformanceStarted(string outcomeId, int loopEpoch, PerformanceHeldItemPolicy heldItemPolicy);
        void OnQteProgress(string outcomeId, int loopEpoch, in QteSequenceSnapshot snapshot);
        void OnBranchResolved(
            string outcomeId,
            int loopEpoch,
            in PerformanceBranchDefinition branch,
            CounterplayNarrativeOutcome narrativeOutcome);
        void OnPerformanceCancelled(string outcomeId, int loopEpoch);
    }
}
