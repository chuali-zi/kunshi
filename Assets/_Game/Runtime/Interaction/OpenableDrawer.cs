using ConGame.Domain.Interaction;
using ConGame.Runtime.Loop;
using UnityEngine;

namespace ConGame.Runtime.Interaction
{
    [DisallowMultipleComponent]
    public sealed class OpenableDrawer : MonoBehaviour, IUseHandler, ILoopResettable
    {
        [SerializeField] private int requiredPulls = 1;
        [SerializeField] private Transform drawerSlide;
        [SerializeField] private float openLocalZ = 0.28f;
        [SerializeField] private GameObject[] contents;

        private DrawerLatchState _latch;
        private Vector3 _closedLocalPosition;
        private bool _captured;

        public int ResetOrder => 72;
        public bool IsOpen => _latch.IsOpen;
        public int RequiredPulls => _latch.RequiredPulls;

        public void Configure(int pulls, Transform slide = null, float openedLocalZ = 0.28f, GameObject[] drawerContents = null)
        {
            requiredPulls = pulls < 1 ? 1 : pulls;
            drawerSlide = slide;
            openLocalZ = openedLocalZ;
            contents = drawerContents;
            _latch = new DrawerLatchState(requiredPulls);
            ApplyVisual();
        }

        public bool CanUse(in InteractionContext context)
        {
            return !_latch.IsOpen;
        }

        public InteractionResult Use(in InteractionContext context)
        {
            if (!CanUse(context))
            {
                return InteractionResult.Rejected;
            }

            _latch.TryPull();
            ApplyVisual();
            return InteractionResult.Accepted;
        }

        public void CaptureInitialState()
        {
            _latch = new DrawerLatchState(requiredPulls < 1 ? 1 : requiredPulls);
            Transform slide = drawerSlide == null ? transform : drawerSlide;
            _closedLocalPosition = slide.localPosition;
            _captured = true;
            ApplyVisual();
        }

        public void Quiesce(in LoopResetContext context)
        {
        }

        public void Restore(in LoopResetContext context)
        {
            if (!_captured)
            {
                CaptureInitialState();
            }

            _latch.Reset();
            ApplyVisual();
        }

        public void Resume(in LoopResetContext context)
        {
        }

        private void ApplyVisual()
        {
            Transform slide = drawerSlide == null ? transform : drawerSlide;
            if (_captured)
            {
                Vector3 opened = _closedLocalPosition;
                opened.z += openLocalZ;
                slide.localPosition = _latch.IsOpen ? opened : _closedLocalPosition;
            }

            if (contents == null)
            {
                return;
            }

            for (int i = 0; i < contents.Length; i++)
            {
                if (contents[i] != null)
                {
                    contents[i].SetActive(_latch.IsOpen);
                }
            }
        }
    }
}
