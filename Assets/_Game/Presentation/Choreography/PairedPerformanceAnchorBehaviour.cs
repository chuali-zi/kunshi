using ConGame.Domain.Choreography;
using UnityEngine;

namespace ConGame.Presentation.Choreography
{
    /// <summary>
    /// Scene-authored paired anchor: the player-side and killer-side start poses for
    /// one paired outcome (position, facing, and implicitly the separation between the
    /// two side transforms). Bound explicitly; never resolved by name or lookup.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PairedPerformanceAnchorBehaviour : MonoBehaviour
    {
        [SerializeField] private string outcomeId;
        [SerializeField] private Transform playerSide;
        [SerializeField] private Transform killerSide;

        public string OutcomeId => outcomeId ?? string.Empty;
        public Transform PlayerSide => playerSide;
        public Transform KillerSide => killerSide;
        public bool IsValid => playerSide != null && killerSide != null;

        public void Configure(string id, Transform playerSideTransform, Transform killerSideTransform)
        {
            outcomeId = id ?? string.Empty;
            playerSide = playerSideTransform;
            killerSide = killerSideTransform;
        }

        public PairedAnchorPose SnapshotPlayerPose()
        {
            return Snapshot(playerSide);
        }

        public PairedAnchorPose SnapshotKillerPose()
        {
            return Snapshot(killerSide);
        }

        public static PairedAnchorPose Snapshot(Transform side)
        {
            if (side == null)
            {
                return default;
            }

            Vector3 position = side.position;
            return new PairedAnchorPose(position.x, position.y, position.z, side.eulerAngles.y);
        }

        public static void Apply(Transform target, in PairedAnchorPose pose)
        {
            if (target == null)
            {
                return;
            }

            target.SetPositionAndRotation(
                new Vector3(pose.X, pose.Y, pose.Z),
                Quaternion.Euler(0f, pose.YawDegrees, 0f));
        }
    }
}
