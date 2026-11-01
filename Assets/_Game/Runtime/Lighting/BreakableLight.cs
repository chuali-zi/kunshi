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

        private bool _initialBroken;
        private bool _initialPowered = true;
        private bool _initialSwitchOn = true;
        private bool _initialCableConnected = true;
        private bool _initialDraped;
        private Color _initialColor = Color.white;
        private float _initialIntensity = 1f;

        public event Action<BreakableLight, bool> BrokenChanged;

        public int ResetOrder => 50;
        public bool IsBroken { get; private set; }
        public bool IsPowered { get; private set; } = true;
        public bool IsSwitchOn { get; private set; } = true;
        public bool IsCableConnected { get; private set; } = true;
        public bool IsDraped { get; private set; }
        public bool IsEmitting => !IsBroken && IsPowered && targetLight != null && targetLight.enabled;

        public void Configure(Light lightComponent)
        {
            targetLight = lightComponent;
            IsSwitchOn = lightComponent == null || lightComponent.enabled;
            IsCableConnected = true;
            SyncPowerState();
            if (targetLight != null && !IsDraped)
            {
                _initialColor = targetLight.color;
                _initialIntensity = targetLight.intensity;
            }
            ApplyDrapedAppearance();
            ApplyLightState();
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
            SetSwitchOn(powered);
        }

        public void SetSwitchOn(bool on)
        {
            IsSwitchOn = on;
            SyncPowerState();
            ApplyLightState();
            BrokenChanged?.Invoke(this, IsBroken);
        }

        public void Unplug()
        {
            IsCableConnected = false;
            SyncPowerState();
            ApplyLightState();
            BrokenChanged?.Invoke(this, IsBroken);
        }

        public void SetDraped(bool draped)
        {
            if (IsDraped == draped)
            {
                return;
            }

            IsDraped = draped;
            ApplyDrapedAppearance();
            BrokenChanged?.Invoke(this, IsBroken);
        }

        public void CaptureInitialState()
        {
            _initialBroken = IsBroken;
            _initialPowered = IsPowered;
            _initialSwitchOn = IsSwitchOn;
            _initialCableConnected = IsCableConnected;
            _initialDraped = IsDraped;
            if (targetLight != null && !IsDraped)
            {
                _initialColor = targetLight.color;
                _initialIntensity = targetLight.intensity;
            }
            ApplyLightState();
        }

        public void Quiesce(in LoopResetContext context)
        {
        }

        public void Restore(in LoopResetContext context)
        {
            IsPowered = _initialPowered;
            IsSwitchOn = _initialSwitchOn;
            IsCableConnected = _initialCableConnected;
            IsDraped = _initialDraped;
            IsBroken = _initialBroken;
            if (targetLight != null)
            {
                targetLight.color = _initialColor;
                targetLight.intensity = _initialIntensity;
            }
            ApplyDrapedAppearance();
            ApplyLightState();
            BrokenChanged?.Invoke(this, IsBroken);
        }

        public void Resume(in LoopResetContext context)
        {
        }

        private void SetBroken(bool broken)
        {
            IsBroken = broken;
            ApplyLightState();
            BrokenChanged?.Invoke(this, broken);
        }

        private void ApplyLightState()
        {
            if (targetLight != null)
            {
                targetLight.enabled = !IsBroken && IsPowered;
            }
        }

        private void ApplyDrapedAppearance()
        {
            if (targetLight == null)
            {
                return;
            }

            targetLight.color = IsDraped ? new Color(0.42f, 0.055f, 0.035f) : _initialColor;
            targetLight.intensity = IsDraped ? _initialIntensity * 0.35f : _initialIntensity;
        }

        private void SyncPowerState()
        {
            IsPowered = IsSwitchOn && IsCableConnected;
        }
    }
}
