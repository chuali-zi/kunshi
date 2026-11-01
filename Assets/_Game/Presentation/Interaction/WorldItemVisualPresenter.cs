using ConGame.Domain.Interaction;
using ConGame.Runtime.Interaction;
using ConGame.Runtime.Loop;
using UnityEngine;

namespace ConGame.Presentation.Interaction
{
    [DisallowMultipleComponent]
    public sealed class WorldItemVisualPresenter : MonoBehaviour, ILoopResettable
    {
        [SerializeField] private StatefulWorldItem item;
        [SerializeField] private GameObject intactVisual;
        [SerializeField] private GameObject brokenVisual;

        public int ResetOrder => 95;

        public void Configure(StatefulWorldItem worldItem, GameObject intact, GameObject broken)
        {
            Unsubscribe();
            item = worldItem;
            intactVisual = intact;
            brokenVisual = broken;
            Subscribe();
            ApplyCurrentState();
        }

        public void CaptureInitialState()
        {
            ApplyCurrentState();
        }

        public void Quiesce(in LoopResetContext context)
        {
        }

        public void Restore(in LoopResetContext context)
        {
            ApplyCurrentState();
        }

        public void Resume(in LoopResetContext context)
        {
            ApplyCurrentState();
        }

        private void OnEnable()
        {
            Subscribe();
            ApplyCurrentState();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (isActiveAndEnabled && item != null)
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
            Apply(state == ItemPhysicalState.Broken);
        }

        private void ApplyCurrentState()
        {
            Apply(item != null && item.CurrentState == ItemPhysicalState.Broken);
        }

        private void Apply(bool broken)
        {
            if (intactVisual != null)
            {
                intactVisual.SetActive(!broken);
            }

            if (brokenVisual != null)
            {
                brokenVisual.SetActive(broken);
            }
        }
    }
}
