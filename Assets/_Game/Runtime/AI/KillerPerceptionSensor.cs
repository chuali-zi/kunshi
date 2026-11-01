using ConGame.Domain.Facts;
using ConGame.Runtime.Config;
using ConGame.Runtime.Facts;
using UnityEngine;

namespace ConGame.Runtime.AI
{
    public readonly struct KillerPerceptionSample
    {
        public KillerPerceptionSample(
            bool canSeePlayer,
            bool inViewCone,
            bool hasLineOfSight,
            float distance,
            Vector3 targetPoint)
        {
            CanSeePlayer = canSeePlayer;
            InViewCone = inViewCone;
            HasLineOfSight = hasLineOfSight;
            Distance = distance;
            TargetPoint = targetPoint;
        }

        public bool CanSeePlayer { get; }
        public bool InViewCone { get; }
        public bool HasLineOfSight { get; }
        public float Distance { get; }
        public Vector3 TargetPoint { get; }
    }

    [DisallowMultipleComponent]
    public sealed class KillerPerceptionSensor : MonoBehaviour
    {
        [SerializeField] private Transform eyeOrigin;
        [SerializeField] private Transform playerTarget;
        [SerializeField] private GameplayFactService facts;
        [SerializeField] private LayerMask occlusionMask = ~0;
        [SerializeField, Min(0.1f)] private float targetChestHeight = 1.15f;
        [SerializeField, Min(0.1f)] private float targetHeadHeight = 1.65f;

        public void Configure(
            Transform eye,
            Transform target,
            GameplayFactService factService,
            LayerMask blockingMask)
        {
            eyeOrigin = eye;
            playerTarget = target;
            facts = factService;
            occlusionMask = blockingMask;
        }

        public KillerPerceptionSample Evaluate(in KillerHuntSettings rawSettings)
        {
            KillerHuntSettings settings = rawSettings.Validated();
            if (eyeOrigin == null || playerTarget == null)
            {
                return default;
            }

            FactSnapshot snapshot = facts == null ? FactSnapshot.CreateBaseline() : facts.CaptureSnapshot();
            Vector3 chest = playerTarget.position + Vector3.up * targetChestHeight;
            Vector3 toChest = chest - eyeOrigin.position;
            float distance = toChest.magnitude;
            float viewDistance = snapshot.RoomLight == RoomLightState.PersistentBlack
                ? settings.DarkViewDistance
                : settings.LitViewDistance;

            if (snapshot.PlayerPose == PlayerPose.Hidden ||
                (snapshot.PlayerZone == PlayerZone.SofaRear && snapshot.PlayerPose == PlayerPose.Crouched) ||
                distance > viewDistance || distance < 0.01f)
            {
                return new KillerPerceptionSample(false, false, false, distance, chest);
            }

            Vector3 flatForward = eyeOrigin.forward;
            flatForward.y = 0f;
            Vector3 flatDirection = toChest;
            flatDirection.y = 0f;
            bool inCone = flatDirection.sqrMagnitude < 0.001f ||
                          Vector3.Angle(flatForward, flatDirection) <= settings.ViewAngle * 0.5f;
            if (!inCone)
            {
                return new KillerPerceptionSample(false, false, false, distance, chest);
            }

            Vector3 head = playerTarget.position + Vector3.up * targetHeadHeight;
            bool lineOfSight = HasLineOfSightFrom(eyeOrigin.position, chest) ||
                               HasLineOfSightFrom(eyeOrigin.position, head);
            return new KillerPerceptionSample(lineOfSight, true, lineOfSight, distance, chest);
        }

        public bool HasLineOfSightFrom(Vector3 origin, Vector3 targetPoint)
        {
            Vector3 delta = targetPoint - origin;
            float distance = delta.magnitude;
            if (distance < 0.01f)
            {
                return true;
            }

            RaycastHit[] hits = Physics.RaycastAll(
                origin,
                delta / distance,
                distance,
                occlusionMask,
                QueryTriggerInteraction.Ignore);
            float nearestDistance = float.PositiveInfinity;
            Transform nearest = null;
            for (int i = 0; i < hits.Length; i++)
            {
                Transform candidate = hits[i].transform;
                if (candidate == null || candidate == transform || candidate.IsChildOf(transform))
                {
                    continue;
                }

                if (hits[i].distance < nearestDistance)
                {
                    nearestDistance = hits[i].distance;
                    nearest = candidate;
                }
            }

            if (nearest == null)
            {
                return true;
            }

            return nearest == playerTarget || nearest.IsChildOf(playerTarget);
        }
    }
}