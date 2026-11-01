using ConGame.Domain.Interaction;
using ConGame.Runtime.Events;
using ConGame.Runtime.Loop;
using UnityEngine;

namespace ConGame.Runtime.Interaction
{

    // Authoritative breakage driver for fragile world items (wood chair,
    // vase, mirror, wall clock). Commits destruction exactly once per loop
    // through BreakageArbiter, then flips StatefulWorldItem to Broken so the
    // existing visual presenters react. Landing self-shatter covers the
    // thrown vase/clock case; swings and thrown projectiles arrive through
    // IHitReceiver. LoopReset restores the arbiter and the shard zone from
    // the same source that restores the item state.
    [DisallowMultipleComponent]
    public sealed class BreakableItemBehaviour : MonoBehaviour, IHitReceiver, ILoopResettable
    {
        [SerializeField] private StatefulWorldItem item;
        [SerializeField] private GameplayEventHub noiseEvents;
        [SerializeField] private InteractionEventChannel channel;
        [SerializeField, Min(0f)] private float minimumForce;
        [SerializeField] private bool acceptsLightImpacts;
        [SerializeField] private bool shattersOnLanding;
        [SerializeField, Min(0.05f)] private float minimumLandingSpeed = 1.5f;
        [SerializeField] private bool createsShardZone;
        [SerializeField, Min(0.05f)] private float shardZoneRadius = 0.45f;
        [SerializeField, Range(0.05f, 1f)] private float shardMovementMultiplier = 0.55f;
        [SerializeField, Min(0.05f)] private float shardLimpSeconds = 2f;
        [SerializeField] private bool silencesAmbientLoopOnBreak;

        private BreakageArbiter _arbiter;
        private ShardSlowZoneVolume _shardZone;

        public int ResetOrder => 91;
        public bool IsBroken => Arbiter.IsBroken;

        private BreakageArbiter Arbiter => _arbiter ??= BuildArbiter();

        public void Configure(
            StatefulWorldItem targetItem,
            GameplayEventHub gameplayEvents,
            InteractionEventChannel eventChannel,
            bool shatterOnLanding = false,
            bool createShardZone = false,
            bool silencesAmbientLoop = false,
            float minForce = 0f,
            bool acceptLightImpacts = false)
        {
            item = targetItem;
            noiseEvents = gameplayEvents;
            channel = eventChannel;
            shattersOnLanding = shatterOnLanding;
            createsShardZone = createShardZone;
            silencesAmbientLoopOnBreak = silencesAmbientLoop;
            minimumForce = minForce;
            acceptsLightImpacts = acceptLightImpacts;
            _arbiter = BuildArbiter();
        }

        private void Awake()
        {
            item ??= GetComponent<StatefulWorldItem>();
            _arbiter = BuildArbiter();
        }

        public HitResult ReceiveHit(in HitInfo hit)
        {
            if (!Arbiter.TryCommitImpact(hit.Kind, hit.Force))
            {
                return HitResult.Ignored;
            }

            CommitBreak(hit.Point, hit.Kind);
            return HitResult.Broke;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision == null || item == null || item.CurrentState != ItemPhysicalState.Flying)
            {
                return;
            }

            if (!Arbiter.TryCommitLanding(collision.relativeVelocity.magnitude))
            {
                return;
            }

            Vector3 point = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
            CommitBreak(point, ImpactKind.Projectile);
        }

        public void CaptureInitialState()
        {
            Arbiter.ResetState();
            _shardZone?.SetZoneActive(false);
        }

        public void Quiesce(in LoopResetContext context)
        {
        }

        public void Restore(in LoopResetContext context)
        {
            Arbiter.ResetState();
            _shardZone?.SetZoneActive(false);
        }

        public void Resume(in LoopResetContext context)
        {
        }

        private void CommitBreak(Vector3 point, ImpactKind kind)
        {
            if (item != null)
            {
                item.MarkBroken();
            }

            string sourceId = ResolveSourceId();
            // Fragile shatter is the loudest noise in the room; it reuses the
            // existing hearing-stimulus semantics (loud radius, early entry).
            noiseEvents?.EmitNoise(NoiseLevel.Loud, point, sourceId);

            ShardSlowZoneSpec? shardSpec = null;
            if (createsShardZone)
            {
                ShardSlowZoneVolume zone = EnsureShardZone();
                if (zone != null)
                {
                    zone.transform.position = item == null ? transform.position : item.transform.position;
                    zone.SetZoneActive(true);
                    shardSpec = Arbiter.ShardZone;
                }
            }

            channel?.EmitBreakage(new BreakageEvent(
                sourceId,
                point,
                kind,
                shardSpec,
                silencesAmbientLoopOnBreak));
        }

        private ShardSlowZoneVolume EnsureShardZone()
        {
            if (_shardZone != null)
            {
                return _shardZone;
            }

            GameObject zoneObject = new GameObject($"{name}_ShardSlowZone");
            _shardZone = zoneObject.AddComponent<ShardSlowZoneVolume>();
            _shardZone.Configure(Arbiter.ShardZone ?? new ShardSlowZoneSpec(
                shardZoneRadius, shardMovementMultiplier, shardLimpSeconds));
            return _shardZone;
        }

        private BreakageArbiter BuildArbiter()
        {
            ShardSlowZoneSpec? shardZone = createsShardZone
                ? new ShardSlowZoneSpec(shardZoneRadius, shardMovementMultiplier, shardLimpSeconds)
                : (ShardSlowZoneSpec?)null;
            return new BreakageArbiter(
                minimumForce,
                acceptsLightImpacts,
                shattersOnLanding,
                minimumLandingSpeed,
                shardZone);
        }

        private string ResolveSourceId()
        {
            InteractableEntity entity = GetComponent<InteractableEntity>();
            return entity == null || string.IsNullOrEmpty(entity.ItemId) ? name : entity.ItemId;
        }
    }
}
