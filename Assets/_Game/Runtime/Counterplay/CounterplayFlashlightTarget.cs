using ConGame.Runtime.Interaction;
using UnityEngine;

namespace ConGame.Runtime.Counterplay
{
    [DisallowMultipleComponent]
    public sealed class CounterplayFlashlightTarget : MonoBehaviour, IFlashlightReactive
    {
        [SerializeField] private StolenLootDropController lootWindow;

        public void Configure(StolenLootDropController controller)
        {
            lootWindow = controller;
        }

        public void React(in FlashlightHit hit)
        {
            lootWindow?.NotifyKillerIlluminated(hit);
        }
    }
}
