using System.Collections;
using ConGame.Domain.Interaction;
using ConGame.Runtime.Events;
using ConGame.Runtime.Facts;
using ConGame.Runtime.Loop;
using ConGame.Runtime.Player;
using UnityEngine;

namespace ConGame.Runtime.Interaction
{

    [DisallowMultipleComponent]
    public sealed class MeleeWeaponCapability : MonoBehaviour, ILoopResettable
    {
        [SerializeField, Min(0.1f)] private float reach = 1.25f;
        [SerializeField, Min(0.1f)] private float force = 6f;
        [SerializeField] private ImpactKind impactKind = ImpactKind.Heavy;

        private readonly Collider[] _hits = new Collider[24];
        private Coroutine _swingRoutine;
        private GameplayFactService _activeFacts;

        public int ResetOrder => 70;

        public void Swing(PlayerInteractor player, GameplayFactService facts, GameplayEventHub events)
        {
            if (_swingRoutine != null)
            {
                return;
            }

            _activeFacts = facts;
            _swingRoutine = StartCoroutine(SwingWindow(player, facts, events));
        }

        private IEnumerator SwingWindow(PlayerInteractor player, GameplayFactService facts, GameplayEventHub events)
        {
            facts?.SetSwinging(true);
            yield return new WaitForSeconds(0.12f);

            Vector3 origin = player.transform.position + Vector3.up + player.transform.forward * 0.8f;
            int count = Physics.OverlapSphereNonAlloc(origin, reach, _hits, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                Collider hit = _hits[i];
                if (hit == null || hit.transform.IsChildOf(transform) || hit.transform.IsChildOf(player.transform))
                {
                    continue;
                }

                MonoBehaviour[] behaviours = hit.GetComponentsInParent<MonoBehaviour>(true);
                for (int j = 0; j < behaviours.Length; j++)
                {
                    if (behaviours[j] is IHitReceiver receiver)
                    {
                        Vector3 point = hit.ClosestPoint(origin);
                        receiver.ReceiveHit(new HitInfo(impactKind, name, point, player.transform.forward, force));
                        events?.EmitNoise(NoiseLevel.Loud, point, name);
                        i = count;
                        break;
                    }
                }
            }

            yield return new WaitForSeconds(0.18f);
            facts?.SetSwinging(false);
            _activeFacts = null;
            _swingRoutine = null;
        }

        private void OnDisable()
        {
            CancelSwing();
        }

        public void CaptureInitialState()
        {
        }

        public void Quiesce(in LoopResetContext context)
        {
            CancelSwing();
        }

        public void Restore(in LoopResetContext context)
        {
        }

        public void Resume(in LoopResetContext context)
        {
        }

        private void CancelSwing()
        {
            if (_swingRoutine != null)
            {
                StopCoroutine(_swingRoutine);
                _swingRoutine = null;
            }

            _activeFacts?.SetSwinging(false);
            _activeFacts = null;
        }
    }
}
