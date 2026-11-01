using System;

namespace ConGame.Domain.Interaction
{

    // Pure-data description of a shard slow zone left behind when a fragile
    // item breaks (vase spec: barefoot player limps for 2 seconds). Movement
    // systems consume this through the runtime contract; the arbiter only
    // carries the data.
    public readonly struct ShardSlowZoneSpec
    {
        public ShardSlowZoneSpec(float radius, float movementMultiplier, float limpSeconds)
        {
            Radius = Math.Max(0.05f, radius);
            MovementMultiplier = Math.Max(0.05f, Math.Min(1f, movementMultiplier));
            LimpSeconds = Math.Max(0.05f, limpSeconds);
        }

        public float Radius { get; }
        public float MovementMultiplier { get; }
        public float LimpSeconds { get; }
    }

    // Decides whether an impact or landing commits destruction of a fragile
    // item. Committing is idempotent: once broken, further hits are ignored
    // until ResetState runs (LoopReset restores the item and the arbiter
    // together).
    public sealed class BreakageArbiter
    {
        private readonly float _minimumForce;
        private readonly bool _acceptsLightImpacts;
        private readonly bool _shattersOnLanding;
        private readonly float _minimumLandingSpeed;

        public BreakageArbiter(
            float minimumForce = 0f,
            bool acceptsLightImpacts = false,
            bool shattersOnLanding = false,
            float minimumLandingSpeed = 1.5f,
            ShardSlowZoneSpec? shardZone = null)
        {
            _minimumForce = Math.Max(0f, minimumForce);
            _acceptsLightImpacts = acceptsLightImpacts;
            _shattersOnLanding = shattersOnLanding;
            _minimumLandingSpeed = Math.Max(0.05f, minimumLandingSpeed);
            ShardZone = shardZone;
        }

        public bool IsBroken { get; private set; }
        public ShardSlowZoneSpec? ShardZone { get; }

        public bool TryCommitImpact(ImpactKind kind, float force)
        {
            if (IsBroken)
            {
                return false;
            }

            if (kind == ImpactKind.Light && !_acceptsLightImpacts)
            {
                return false;
            }

            if (force < _minimumForce)
            {
                return false;
            }

            IsBroken = true;
            return true;
        }

        public bool TryCommitLanding(float relativeSpeed)
        {
            if (IsBroken || !_shattersOnLanding || relativeSpeed < _minimumLandingSpeed)
            {
                return false;
            }

            IsBroken = true;
            return true;
        }

        public void ResetState()
        {
            IsBroken = false;
        }
    }
}
