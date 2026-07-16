using ConGame.Runtime.Player;
using UnityEngine;

namespace ConGame.Runtime.Interaction
{

    [DisallowMultipleComponent]
    public sealed class InteractionScanner : MonoBehaviour
    {
        private const int MaxColliders = 32;

        [SerializeField] private PlayerInteractor player;
        [SerializeField] private float radius = 1.2f;
        [SerializeField] private LayerMask layerMask = ~0;

        private readonly Collider[] _hits = new Collider[MaxColliders];

        public InteractableEntity Current { get; private set; }

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
            Vector3 center = transform.position + Vector3.up;
            int count = Physics.OverlapSphereNonAlloc(center, radius, _hits, layerMask, QueryTriggerInteraction.Collide);
            InteractableEntity best = null;
            float bestScore = float.NegativeInfinity;

            for (int i = 0; i < count; i++)
            {
                Collider hit = _hits[i];
                if (hit == null || hit.transform.IsChildOf(transform))
                {
                    continue;
                }

                InteractableEntity candidate = hit.GetComponentInParent<InteractableEntity>();
                if (candidate == null || !candidate.isActiveAndEnabled || candidate.IsHeld)
                {
                    continue;
                }

                int capabilityPriority = candidate.GetBestExecutablePriority(player);
                if (capabilityPriority == int.MinValue)
                {
                    continue;
                }

                Vector3 toTarget = candidate.transform.position - transform.position;
                float distance = toTarget.magnitude;
                Vector3 direction = distance <= 0.001f ? transform.forward : toTarget / distance;
                float facing = Vector3.Dot(transform.forward, direction);
                if (facing < -0.25f)
                {
                    continue;
                }

                float score = capabilityPriority * 100f + facing * 2f - distance;
                if (candidate == Current)
                {
                    score += 0.1f;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            Current = best;
            return Current;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position + Vector3.up, radius);
        }
    }
}
