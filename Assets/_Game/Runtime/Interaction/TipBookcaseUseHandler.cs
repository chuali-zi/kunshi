using ConGame.Domain.Facts;
using ConGame.Domain.Interaction;
using ConGame.Runtime.Events;
using ConGame.Runtime.Facts;
using ConGame.Runtime.Loop;
using UnityEngine;

namespace ConGame.Runtime.Interaction
{
    [DisallowMultipleComponent]
    public sealed class TipBookcaseUseHandler : MonoBehaviour, IUseHandler, ILoopResettable
    {
        [SerializeField] private GameplayFactService facts;
        [SerializeField] private GameplayEventHub events;
        [SerializeField] private Vector3 fallenLocalEuler = new Vector3(0f, 0f, 80f);

        private Quaternion _uprightLocalRotation;
        private bool _tipped;
        private bool _captured;

        public int ResetOrder => 75;
        public bool IsTipped => _tipped;

        public void Configure(GameplayFactService factService, GameplayEventHub eventHub,
                              Vector3? tipEuler = null)
        {
            facts = factService;
            events = eventHub;
            if (tipEuler.HasValue)
            {
                fallenLocalEuler = tipEuler.Value;
            }
        }

        public bool CanUse(in InteractionContext context)
        {
            return facts != null && !_tipped;
        }

        public InteractionResult Use(in InteractionContext context)
        {
            if (!CanUse(context))
            {
                return InteractionResult.Rejected;
            }

            EnsureCaptured();
            _tipped = true;
            transform.localRotation = _uprightLocalRotation * Quaternion.Euler(fallenLocalEuler);
            facts.SetDoorBlock(DoorBlockState.Bookcase, 0f);
            events?.EmitNoise(NoiseLevel.Loud, transform.position, "bookcase");
            return InteractionResult.Accepted;
        }

        public void CaptureInitialState()
        {
            _uprightLocalRotation = transform.localRotation;
            _tipped = false;
            _captured = true;
        }

        public void Quiesce(in LoopResetContext context)
        {
        }

        public void Restore(in LoopResetContext context)
        {
            EnsureCaptured();
            _tipped = false;
            transform.localRotation = _uprightLocalRotation;
        }

        public void Resume(in LoopResetContext context)
        {
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
