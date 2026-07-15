using System;

namespace ConGame.Runtime.Counterplay
{

    public interface IPerformancePlayer
    {
        bool Play(CounterplayDefinitionAsset definition, Action completed);

        void CancelAllForLoop(int loopEpoch);
    }
}
