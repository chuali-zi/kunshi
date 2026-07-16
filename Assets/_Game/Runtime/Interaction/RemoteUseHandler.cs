using ConGame.Domain.Interaction;
using ConGame.Runtime.Ending;
using UnityEngine;

namespace ConGame.Runtime.Interaction
{
    [DisallowMultipleComponent]
    public sealed class RemoteUseHandler : MonoBehaviour, IUseHandler
    {
        [SerializeField] private TelevisionController television;

        public void Configure(TelevisionController televisionController)
        {
            television = televisionController;
        }

        // Compatibility overload used by scaffold after CRT wiring.
        public void Configure(TelevisionController televisionController, object unusedPass, object unusedLoop)
        {
            television = televisionController;
        }

        public bool CanUse(in InteractionContext context)
        {
            return television != null;
        }

        public InteractionResult Use(in InteractionContext context)
        {
            if (!CanUse(context))
            {
                return InteractionResult.Rejected;
            }

            television.PulseLightImpact();
            return InteractionResult.Accepted;
        }
    }
}
