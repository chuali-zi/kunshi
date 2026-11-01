using ConGame.Domain.Interaction;
using ConGame.Runtime.Lighting;
using UnityEngine;

namespace ConGame.Runtime.Interaction
{
    [DisallowMultipleComponent]
    public sealed class LampUnplugOnHold : MonoBehaviour
    {
        [SerializeField] private BreakableLight lamp;
        [SerializeField] private StatefulWorldItem item;

        public void Configure(BreakableLight target, StatefulWorldItem worldItem)
        {
            Unsubscribe();
            lamp = target;
            item = worldItem;
            Subscribe();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (item != null)
            {
                item.StateChanged -= OnStateChanged;
                item.StateChanged += OnStateChanged;
            }
        }

        private void Unsubscribe()
        {
            if (item != null)
            {
                item.StateChanged -= OnStateChanged;
            }
        }

        private void OnStateChanged(ItemPhysicalState state)
        {
            if (lamp != null && state == ItemPhysicalState.Held)
            {
                lamp.Unplug();
            }
        }
    }
}
