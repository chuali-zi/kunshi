namespace ConGame.Runtime.Loop
{

    public readonly struct LoopResetContext
    {
        public LoopResetContext(int previousEpoch, int nextEpoch)
        {
            PreviousEpoch = previousEpoch;
            NextEpoch = nextEpoch;
        }

        public int PreviousEpoch { get; }

        public int NextEpoch { get; }
    }

    public interface ILoopResettable
    {
        int ResetOrder { get; }

        void CaptureInitialState();

        void Quiesce(in LoopResetContext context);

        void Restore(in LoopResetContext context);

        void Resume(in LoopResetContext context);
    }
}
