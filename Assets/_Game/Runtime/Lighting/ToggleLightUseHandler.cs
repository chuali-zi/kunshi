using ConGame.Runtime.Interaction;
using ConGame.Runtime.Loop;
using UnityEngine;

namespace ConGame.Runtime.Lighting
{
    [DisallowMultipleComponent]
    public sealed class ToggleLightUseHandler : MonoBehaviour, IUseHandler, ILoopResettable
    {
        [SerializeField] private BreakableLight target;

        public int ResetOrder => 55;

        public void Configure(BreakableLight light)
        {
            target = light;
        }

        public bool CanUse(in InteractionContext context)
        {
            return target != null && !target.IsBroken;
        }

        public InteractionResult Use(in InteractionContext context)
        {
            if (!CanUse(context))
            {
                return InteractionResult.Rejected;
            }

            target.SetPowered(!target.IsPowered);
            return InteractionResult.Accepted;
        }

        public void CaptureInitialState() { }
        public void Quiesce(in LoopResetContext context) { }
        public void Restore(in LoopResetContext context) { }
        public void Resume(in LoopResetContext context) { }
    }
}
