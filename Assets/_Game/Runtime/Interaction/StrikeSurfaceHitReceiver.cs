using ConGame.Domain.Interaction;
using ConGame.Runtime.Facts;
using UnityEngine;

namespace ConGame.Runtime.Interaction
{
    [DisallowMultipleComponent]
    public sealed class StrikeSurfaceHitReceiver : MonoBehaviour, IHitReceiver
    {
        [SerializeField] private GameplayFactService facts;
        [SerializeField] private bool wallSurface = true;

        public void Configure(GameplayFactService factService, bool isWall)
        {
            facts = factService;
            wallSurface = isWall;
        }

        public HitResult ReceiveHit(in HitInfo hit)
        {
            if (facts == null || (hit.Kind != ImpactKind.Heavy && hit.Kind != ImpactKind.Projectile))
            {
                return HitResult.Ignored;
            }

            if (wallSurface)
            {
                facts.SetStrikingWall(true);
            }
            else
            {
                facts.SetStrikingWindow(true);
            }

            return HitResult.Accepted;
        }
    }
}
