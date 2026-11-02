using ConGame.Domain.Interaction;
using ConGame.Runtime.Player;
using UnityEngine;

namespace ConGame.Runtime.Interaction
{

    [DisallowMultipleComponent]
    public sealed class InteractionScanner : MonoBehaviour
    {
        private const int MaxColliders = 32;
        private const int MaxTrapQueryColliders = 64;
        private const float TrapUseScanRadius = 2f;

        [SerializeField] private PlayerInteractor player;
        [SerializeField] private float radius = 1.2f;
        [SerializeField] private LayerMask layerMask = ~0;

        private readonly Collider[] _hits = new Collider[MaxColliders];
        private readonly Collider[] _trapHits = new Collider[MaxTrapQueryColliders];
        private InspectFocusTracker _focus;

        public InteractableEntity Current { get; private set; }
        public bool HasInspectFocusDwell => _focus.HasDwelled;

        public void Configure(PlayerInteractor owner, float scanRadius, LayerMask mask)
        {
            player = owner;
            radius = scanRadius;
            layerMask = mask;
        }

        private void Update()
        {
            Refresh();
        }

        public InteractableEntity Refresh()
        {
            return RefreshInternal(InteractionVerb.None, true);
        }

        public InteractableEntity RefreshForVerb(InteractionVerb verb)
        {
            return RefreshInternal(verb, false);
        }

        private InteractableEntity RefreshInternal(InteractionVerb verb, bool updateCurrent)
        {
            // Scan from the player's chest, so tabletop objects are judged from the same
            // point the player uses to aim instead of from the feet.
            Vector3 center = transform.position + Vector3.up;
            int count = Physics.OverlapSphereNonAlloc(center, radius, _hits, layerMask, QueryTriggerInteraction.Collide);
            InteractableEntity best = null;
            float bestScore = float.NegativeInfinity;
            for (int i = 0; i < count; i++)
            {
                ConsiderHit(_hits[i], verb, center, false, ref best, ref bestScore);
            }

            // Keep the regular query's collider-edge semantics and capacity for
            // all interactables. A separate narrow-purpose query finds only traps
            // whose own CanUse check accepts the player's active pull.
            if (verb != InteractionVerb.Place)
            {
                int trapCount = Physics.OverlapSphereNonAlloc(
                    center, TrapUseScanRadius, _trapHits, layerMask, QueryTriggerInteraction.Collide);
                for (int i = 0; i < trapCount; i++)
                {
                    ConsiderHit(_trapHits[i], verb, center, true, ref best, ref bestScore);
                }
            }

            if (updateCurrent)
            {
                Current = best;
                _focus.Tick(best, Time.deltaTime);
            }

            return best;
        }

        private void ConsiderHit(
            Collider hit,
            InteractionVerb verb,
            Vector3 center,
            bool extendedTrapPass,
            ref InteractableEntity best,
            ref float bestScore)
        {
            if (hit == null || hit.transform.IsChildOf(transform))
            {
                return;
            }

            InteractableEntity candidate = hit.GetComponentInParent<InteractableEntity>();
            if (candidate == null || !candidate.isActiveAndEnabled || candidate.IsHeld ||
                (extendedTrapPass && !IsUsableTrap(candidate, verb)))
            {
                return;
            }

            int capabilityPriority = candidate.GetExecutablePriority(player, verb);
            if (capabilityPriority == int.MinValue)
            {
                return;
            }

            Vector3 toTarget = hit.bounds.center - center;
            Vector3 horizontalTarget = toTarget;
            horizontalTarget.y = 0f;
            // A grab query ranks reachable props on the floor plane so a nearby
            // dropped item does not lose to a farther tabletop item just because
            // it is below the chest. Mixed furniture/use scans retain their 3D
            // ranking (e.g. the blanket on top of the climbable bed).
            // Both queries still use the same chest sphere for range.
            float distance = verb == InteractionVerb.PickUp ? horizontalTarget.magnitude : toTarget.magnitude;
            Vector3 horizontalForward = transform.forward;
            horizontalForward.y = 0f;
            horizontalForward = horizontalForward.sqrMagnitude < 0.001f
                ? Vector3.forward
                : horizontalForward.normalized;
            Vector3 direction = horizontalTarget.sqrMagnitude <= 0.001f
                ? horizontalForward
                : horizontalTarget.normalized;
            float facing = Vector3.Dot(horizontalForward, direction);
            if (facing < -0.25f)
            {
                return;
            }

            // Facing and proximity decide the target. Capability priority is only a
            // small tie-breaker, otherwise Carry (110) hides nearby Use/Pickup (40/100).
            float score = facing * 4f - distance + capabilityPriority * 0.001f;
            if (candidate == Current)
            {
                score += 0.025f;
            }

            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        private bool IsUsableTrap(InteractableEntity candidate, InteractionVerb verb)
        {
            if (verb != InteractionVerb.None && verb != InteractionVerb.Use)
            {
                return false;
            }

            TripTrapController trap = candidate.GetComponent<TripTrapController>();
            return trap != null && trap.CanPull(new InteractionContext(player, candidate));
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position + Vector3.up, radius);
        }
    }
}
