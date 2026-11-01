using ConGame.Domain.Interaction;
using ConGame.Runtime.Facts;
using ConGame.Runtime.Loop;
using UnityEngine;

namespace ConGame.Runtime.Interaction
{
    [DisallowMultipleComponent]
    public sealed class StrikeSurfaceHitReceiver : MonoBehaviour, IHitReceiver, ILoopResettable
    {
        [SerializeField] private GameplayFactService facts;
        [SerializeField] private bool wallSurface = true;
        [SerializeField] private GameObject brickVisual;

        private SurfaceDamageState _damage;

        public int ResetOrder => 71;
        public int Hits => _damage.Hits;
        public bool BrickRevealed => _damage.BrickRevealed;

        public void Configure(GameplayFactService factService, bool isWall, int maxHits = 0, GameObject brick = null)
        {
            facts = factService;
            wallSurface = isWall;
            brickVisual = brick;
            _damage = new SurfaceDamageState(
                maxHits > 0 ? maxHits : (isWall ? 3 : 5),
                !isWall);
            ApplyBrick();
        }

        public HitResult ReceiveHit(in HitInfo hit)
        {
            if (facts == null || (hit.Kind != ImpactKind.Heavy && hit.Kind != ImpactKind.Projectile))
            {
                return HitResult.Ignored;
            }

            if (_damage.MaxHits == 0)
            {
                _damage = new SurfaceDamageState(wallSurface ? 3 : 5, !wallSurface);
            }

            bool brickAlreadyRevealed = _damage.BrickRevealed;
            bool counted = _damage.RegisterHit();
            ApplyBrick();
            if (wallSurface || !brickAlreadyRevealed)
            {
                if (wallSurface)
                {
                    facts.SetStrikingWall(true);
                }
                else
                {
                    facts.SetStrikingWindow(true);
                }
            }
            else
            {
                facts.SetStrikingWall(true);
            }

            return counted || _damage.IsCapped ? HitResult.Accepted : HitResult.Ignored;
        }

        public void CaptureInitialState()
        {
            if (_damage.MaxHits == 0)
            {
                _damage = new SurfaceDamageState(wallSurface ? 3 : 5, !wallSurface);
            }
        }

        public void Quiesce(in LoopResetContext context)
        {
        }

        public void Restore(in LoopResetContext context)
        {
            _damage.Reset();
            ApplyBrick();
        }

        public void Resume(in LoopResetContext context)
        {
        }

        private void ApplyBrick()
        {
            if (brickVisual != null)
            {
                brickVisual.SetActive(_damage.BrickRevealed);
            }
        }
    }
}
