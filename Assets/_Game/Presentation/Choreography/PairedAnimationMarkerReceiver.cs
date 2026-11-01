using System;
using UnityEngine;

namespace ConGame.Presentation.Choreography
{
    /// <summary>Receives authored markers for presentation bookkeeping only.</summary>
    [DisallowMultipleComponent]
    public sealed class PairedAnimationMarkerReceiver : MonoBehaviour
    {
        public string LastMarker { get; private set; } = string.Empty;
        public event Action<string> MarkerReceived;

        public void PairStart() => Receive(nameof(PairStart));
        public void PairContact() => Receive(nameof(PairContact));
        public void PairLoopStart() => Receive(nameof(PairLoopStart));
        public void PairLoopEnd() => Receive(nameof(PairLoopEnd));
        public void Detach() => Receive(nameof(Detach));
        public void BlockStart() => Receive(nameof(BlockStart));
        public void DrawStart() => Receive(nameof(DrawStart));
        public void PairRelease() => Receive(nameof(PairRelease));
        public void HitStart() => Receive(nameof(HitStart));
        public void HitEnd() => Receive(nameof(HitEnd));
        public void AimHold() => Receive(nameof(AimHold));
        public void Attach() => Receive(nameof(Attach));
        public void FallContact() => Receive(nameof(FallContact));
        public void Fire() => Receive(nameof(Fire));
        public void HardStunEnd() => Receive(nameof(HardStunEnd));
        public void KickContact() => Receive(nameof(KickContact));
        public void Muzzle() => Receive(nameof(Muzzle));
        public void RecoverEnd() => Receive(nameof(RecoverEnd));
        public void StumbleHeavyEnd() => Receive(nameof(StumbleHeavyEnd));
        public void StumbleLightEnd() => Receive(nameof(StumbleLightEnd));

        private void Receive(string marker)
        {
            LastMarker = marker;
            MarkerReceived?.Invoke(marker);
        }
    }
}
