
using System.Collections.Generic;
using ConGame.Runtime.AI;
using System.Collections;
using ConGame.Domain.Facts;
using ConGame.Domain.Interaction;

using ConGame.Runtime.Ending;
using ConGame.Runtime.Events;
using ConGame.Runtime.Facts;

using ConGame.Runtime.Lighting;
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
        [SerializeField, Min(0.1f)] private float hitboxHalfWidth = 0.8f;
        [SerializeField, Min(0.1f)] private float hitboxHalfHeight = 0.75f;
        [SerializeField, Min(0f)] private float windupSeconds = 0.55f;
        [SerializeField, Min(0.05f)] private float activeSeconds = 0.22f;
        [SerializeField, Min(0f)] private float recoverySeconds = 0.18f;
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
            events?.EmitStimulus(KillerStimulusType.PlayerExposed, name);
            yield return new WaitForSeconds(windupSeconds);

            bool resolved = false;
            float remaining = activeSeconds;
            while (!resolved && remaining > 0f)
            {
                resolved = TryResolveHit(player, events);
                remaining -= Time.deltaTime;
                if (!resolved)
                {
                    yield return null;
                }
            }

            yield return new WaitForSeconds(recoverySeconds);
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


private bool TryResolveHit(PlayerInteractor player, GameplayEventHub events)
        {
            if (player == null)
            {
                return false;
            }

            Vector3 aimForward = player.AimForward;
            aimForward.y = 0f;
            aimForward = aimForward.sqrMagnitude < 0.001f ? player.transform.forward : aimForward.normalized;
            // The authored half-height is intentionally small for the body of the
            // swing, but the weapon's reach also needs to cover overhead targets.
            // Keep the previous spherical reach's vertical coverage for existing
            // scene values (which serialize hitboxHalfHeight as 0.75).
            float verticalHalfHeight = Mathf.Max(hitboxHalfHeight, reach);
            Vector3 origin = player.transform.position + Vector3.up * verticalHalfHeight +
                             aimForward * (reach * 0.8f);
            Vector3 halfExtents = new Vector3(hitboxHalfWidth, verticalHalfHeight, reach * 0.8f);
            Collider[] hits = Physics.OverlapBox(origin, halfExtents, Quaternion.LookRotation(aimForward), ~0,
                QueryTriggerInteraction.Ignore);
            List<HitCandidate> candidates = new List<HitCandidate>();
            HashSet<MonoBehaviour> seen = new HashSet<MonoBehaviour>();

            for (int i = 0; i < hits.Length; i++)
            {
                Collider hit = hits[i];
                if (hit == null || hit.transform.IsChildOf(transform) || hit.transform.IsChildOf(player.transform))
                {
                    continue;
                }

                Vector3 delta = hit.bounds.center - player.transform.position;
                delta.y = 0f;
                if (Vector3.Dot(delta, aimForward) < -0.05f)
                {
                    continue;
                }

                MonoBehaviour[] behaviours = hit.GetComponentsInParent<MonoBehaviour>(true);
                for (int j = 0; j < behaviours.Length; j++)
                {
                    MonoBehaviour behaviour = behaviours[j];
                    if (behaviour is not IHitReceiver || !seen.Add(behaviour))
                    {
                        continue;
                    }

                    Vector3 point = ResolveHitPoint(hit, origin);
                    candidates.Add(new HitCandidate(behaviour, hit, point,
                        GetPriority(behaviour), (point - origin).sqrMagnitude));
                }
            }

            candidates.Sort((left, right) =>
            {
                int priority = left.Priority.CompareTo(right.Priority);
                return priority != 0 ? priority : left.DistanceSquared.CompareTo(right.DistanceSquared);
            });

            InteractableEntity entity = GetComponent<InteractableEntity>();
            string sourceId = entity == null || string.IsNullOrEmpty(entity.ItemId) ? name : entity.ItemId;
            for (int i = 0; i < candidates.Count; i++)
            {
                HitCandidate candidate = candidates[i];
                IHitReceiver receiver = (IHitReceiver)candidate.Behaviour;
                HitInfo hitInfo = new HitInfo(impactKind, sourceId, candidate.Point, aimForward, force);
                HitResult result = receiver.ReceiveHit(hitInfo);
                if (result == HitResult.Ignored)
                {
                    continue;
                }

                events?.EmitNoise(NoiseLevel.Loud, candidate.Point, sourceId);
                events?.EmitImpact(hitInfo, result, candidate.Behaviour);
                return true;
            }

            return false;
        }

        private static Vector3 ResolveHitPoint(Collider hit, Vector3 origin)
        {
            if (hit is BoxCollider ||
                hit is SphereCollider ||
                hit is CapsuleCollider ||
                (hit is MeshCollider mesh && mesh.convex))
            {
                return hit.ClosestPoint(origin);
            }

            return hit.bounds.ClosestPoint(origin);
        }

        private static int GetPriority(MonoBehaviour receiver)
        {
            if (receiver is BreakableLight)
            {
                return 0;
            }

            if (receiver is TelevisionController)
            {
                return 1;
            }

            if (receiver is KillerHitReceiver)
            {
                return 2;
            }

            return receiver is StrikeSurfaceHitReceiver ? 10 : 5;
        }

        private readonly struct HitCandidate
        {
            public HitCandidate(MonoBehaviour behaviour, Collider collider, Vector3 point, int priority,
                float distanceSquared)
            {
                Behaviour = behaviour;
                Collider = collider;
                Point = point;
                Priority = priority;
                DistanceSquared = distanceSquared;
            }

            public MonoBehaviour Behaviour { get; }
            public Collider Collider { get; }
            public Vector3 Point { get; }
            public int Priority { get; }
            public float DistanceSquared { get; }
        }
}
}
