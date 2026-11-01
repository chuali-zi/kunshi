using ConGame.Domain.Facts;
using ConGame.Domain.Interaction;
using ConGame.Runtime.Facts;
using ConGame.Runtime.Loop;
using UnityEngine;

namespace ConGame.Runtime.Interaction
{
    [DisallowMultipleComponent]
    public sealed class RoomDoorUseHandler : MonoBehaviour, IUseHandler, ILoopResettable
    {
        [SerializeField] private Transform hinge;
        [SerializeField] private GameplayFactService facts;
        [SerializeField] private float openAngle = 95f;

        private Quaternion _closedLocalRotation;
        private bool _open;
        private bool _captured;

        public int ResetOrder => 21;
        public bool IsOpen => _open;

        public void Configure(Transform doorHinge, GameplayFactService factService, float doorOpenAngle = 95f)
        {
            hinge = doorHinge;
            facts = factService;
            openAngle = doorOpenAngle;
        }

        public bool CanUse(in InteractionContext context)
        {
            if (hinge == null || facts == null)
            {
                return false;
            }

            FactSnapshot snapshot = facts.CaptureSnapshot();
            return snapshot.DoorBlock == DoorBlockState.None;
        }

        public InteractionResult Use(in InteractionContext context)
        {
            if (!CanUse(context))
            {
                return InteractionResult.Rejected;
            }

            EnsureCaptured();
            _open = !_open;
            ApplyPose();
            return InteractionResult.Accepted;
        }

        public void CaptureInitialState()
        {
            if (hinge == null)
            {
                return;
            }

            _closedLocalRotation = hinge.localRotation;
            _open = false;
            _captured = true;
        }

        public void Quiesce(in LoopResetContext context)
        {
        }

        public void Restore(in LoopResetContext context)
        {
            EnsureCaptured();
            _open = false;
            ApplyPose();
        }

        public void Resume(in LoopResetContext context)
        {
        }

        private void ApplyPose()
        {
            if (hinge == null || !_captured)
            {
                return;
            }

            hinge.localRotation = _open
                ? _closedLocalRotation * Quaternion.Euler(0f, openAngle, 0f)
                : _closedLocalRotation;
        }

        private void EnsureCaptured()
        {
            if (!_captured)
            {
                CaptureInitialState();
            }
        }
    }
}
