using System.Collections;
using ConGame.Domain.Facts;
using ConGame.Domain.Interaction;
using ConGame.Runtime.Events;
using ConGame.Runtime.Facts;
using ConGame.Runtime.Loop;
using UnityEngine;

namespace ConGame.Runtime.Interaction
{
    [DisallowMultipleComponent]
    public sealed class TipBookcaseUseHandler : MonoBehaviour, IHoldUseHandler, ILoopResettable
    {
        [SerializeField] private GameplayFactService facts;
        [SerializeField] private GameplayEventHub events;
        [SerializeField] private Vector3 fallenLocalEuler = new Vector3(0f, 0f, 80f);
        [SerializeField] private Transform doorway;
        [SerializeField] private Vector3 tipWorldDirection = Vector3.right;

        private Quaternion _uprightLocalRotation;
        private bool _tipped;
        private bool _captured;
        private bool _tipping;
        private Coroutine _tipRoutine;

        public int ResetOrder => 75;
        public bool IsTipped => _tipped;
        public float HoldSeconds => 2f;

        public void Configure(GameplayFactService factService, GameplayEventHub eventHub,
                              Vector3? tipEuler = null, Transform door = null, Vector3? worldTipDirection = null)
        {
            facts = factService;
            events = eventHub;
            doorway = door;
            if (tipEuler.HasValue)
            {
                fallenLocalEuler = tipEuler.Value;
            }

            if (worldTipDirection.HasValue)
            {
                tipWorldDirection = worldTipDirection.Value;
            }
        }

        public bool CanUse(in InteractionContext context)
        {
            return facts != null && !_tipped && !_tipping;
        }

        public InteractionResult Use(in InteractionContext context)
        {
            if (!CanUse(context))
            {
                return InteractionResult.Rejected;
            }

            EnsureCaptured();
            _tipping = true;
            _tipRoutine = StartCoroutine(TipRoutine());
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
            _tipping = false;
            if (_tipRoutine != null)
            {
                StopCoroutine(_tipRoutine);
                _tipRoutine = null;
            }
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

        private IEnumerator TipRoutine()
        {
            Quaternion from = transform.localRotation;
            Quaternion to = _uprightLocalRotation * Quaternion.Euler(fallenLocalEuler);
            const float duration = 0.35f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                transform.localRotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0f, 1f, elapsed / duration));
                yield return null;
            }

            transform.localRotation = to;
            _tipped = true;
            _tipping = false;
            _tipRoutine = null;
            if (doorway == null || BookcaseTipGate.BlocksDoor(transform.position, doorway.position, tipWorldDirection))
            {
                facts?.SetDoorBlock(DoorBlockState.Bookcase, 0f);
            }

            events?.EmitNoise(NoiseLevel.Loud, transform.position, "bookcase");
        }
    }
}
