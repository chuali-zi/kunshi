using System;
using ConGame.Domain.Interaction;
using ConGame.Runtime.Interaction;
using ConGame.Runtime.Loop;
using UnityEngine;

namespace ConGame.Runtime.Lighting
{
    [DisallowMultipleComponent]
    public sealed class BreakableLight : MonoBehaviour, IHitReceiver, ILoopResettable
    {
        [SerializeField] private Light targetLight;
        [SerializeField] private GameObject intactVisual;
        [SerializeField] private GameObject brokenVisual;

        private bool _initialBroken;
        private bool _initialPowered = true;

        public event Action<BreakableLight, bool> BrokenChanged;

        public int ResetOrder => 50;
        public bool IsBroken { get; private set; }
        public bool IsPowered { get; private set; } = true;
        public bool IsEmitting => !IsBroken && IsPowered && targetLight != null && targetLight.enabled;

        public void Configure(Light lightComponent, GameObject intact = null, GameObject broken = null)
        {
            targetLight = lightComponent;
            intactVisual = intact;
            brokenVisual = broken;
            IsPowered = lightComponent == null || lightComponent.enabled;
            ApplyVisuals();
        }

        public HitResult ReceiveHit(in HitInfo hit)
        {
            if (IsBroken || (hit.Kind != ImpactKind.Heavy && hit.Kind != ImpactKind.Projectile))
            {
                return HitResult.Ignored;
            }

            SetBroken(true);
            return HitResult.Broke;
        }

        public void SetPowered(bool powered)
        {
            IsPowered = powered;
            ApplyVisuals();
            BrokenChanged?.Invoke(this, IsBroken);
        }

        public void CaptureInitialState()
        {
            _initialBroken = IsBroken;
            _initialPowered = IsPowered;
            ApplyVisuals();
        }

        public void Quiesce(in LoopResetContext context)
        {
        }

        public void Restore(in LoopResetContext context)
        {
            IsPowered = _initialPowered;
            SetBroken(_initialBroken);
        }

        public void Resume(in LoopResetContext context)
        {
        }

        private void SetBroken(bool broken)
        {
            IsBroken = broken;
            ApplyVisuals();
            BrokenChanged?.Invoke(this, broken);
        }

        private void ApplyVisuals()
        {
            if (targetLight != null)
            {
                targetLight.enabled = !IsBroken && IsPowered;
            }

            if (intactVisual != null)
            {
                intactVisual.SetActive(!IsBroken);
            }

            if (brokenVisual != null)
            {
                brokenVisual.SetActive(IsBroken);
            }
        }
    }
}
