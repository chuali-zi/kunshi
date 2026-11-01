using ConGame.Domain.Interaction;
using ConGame.Runtime.Loop;
using UnityEngine;

namespace ConGame.Runtime.Interaction
{
    [DisallowMultipleComponent]
    public sealed class BookFlipUseHandler : MonoBehaviour, IUseHandler, ILoopResettable
    {
        [SerializeField] private bool open;

        public int ResetOrder => 73;
        public bool IsOpen => open;

        public bool CanUse(in InteractionContext context)
        {
            return true;
        }

        public InteractionResult Use(in InteractionContext context)
        {
            open = !open;
            return InteractionResult.Accepted;
        }

        public void CaptureInitialState()
        {
            open = false;
        }

        public void Quiesce(in LoopResetContext context)
        {
        }

        public void Restore(in LoopResetContext context)
        {
            open = false;
        }

        public void Resume(in LoopResetContext context)
        {
        }
    }
}
