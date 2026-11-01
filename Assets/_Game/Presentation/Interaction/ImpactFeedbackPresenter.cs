using ConGame.Runtime.Events;
using ConGame.Runtime.Interaction;
using UnityEngine;

namespace ConGame.Presentation.Interaction
{
    [DisallowMultipleComponent]
    public sealed class ImpactFeedbackPresenter : MonoBehaviour
    {
        [SerializeField] private GameplayEventHub events;
        [SerializeField] private ParticleSystem impactParticles;
        [SerializeField, Min(1)] private int particleCount = 14;

        public void Configure(GameplayEventHub gameplayEvents, ParticleSystem particles)
        {
            events = gameplayEvents;
            impactParticles = particles;
        }

        private void OnEnable()
        {
            if (events != null)
            {
                events.ImpactResolved += OnImpactResolved;
            }
        }

        private void OnDisable()
        {
            if (events != null)
            {
                events.ImpactResolved -= OnImpactResolved;
            }
        }

        private void OnImpactResolved(ImpactEvent impact)
        {
            if (impactParticles == null)
            {
                return;
            }

            impactParticles.transform.position = impact.Hit.Point;
            ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams
            {
                startColor = impact.Result == HitResult.Broke
                    ? new Color(1f, 0.82f, 0.42f, 1f)
                    : new Color(0.86f, 0.9f, 1f, 1f),
                startSize = impact.Result == HitResult.Broke ? 0.07f : 0.045f,
                velocity = (-impact.Hit.Direction + Vector3.up * 0.75f).normalized *
                           (impact.Result == HitResult.Broke ? 2.4f : 1.5f)
            };
            impactParticles.Emit(emit, impact.Result == HitResult.Broke ? particleCount * 2 : particleCount);
        }
    }
}