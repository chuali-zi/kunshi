using ConGame.Runtime.AI;
using UnityEngine;

namespace ConGame.Presentation.Animation
{
    // The baked navigation surface can run above the visible floor. Keep this
    // correction in the humanoid pose so navigation, hurtboxes and recoil keep
    // their own transforms.
    [DisallowMultipleComponent, RequireComponent(typeof(Animator))]
    public sealed class KillerFloorContact : MonoBehaviour
    {
        private Animator animator;
        private KillerDirector killer;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            killer = GetComponentInParent<KillerDirector>();
        }

        private void OnAnimatorIK(int layer)
        {
            if (layer != 0 || killer == null || !animator.isHuman ||
                (!killer.IsLiveHuntActive && killer.SelectedOutcomeId != "D16")) return;

            Vector3 basePoint = killer.transform.position;
            if (!Physics.Raycast(basePoint + Vector3.up * .06f, Vector3.down, out var support,
                    .50f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) ||
                support.transform.IsChildOf(killer.transform)) return;

            Vector3 shift = Vector3.up * (support.point.y - basePoint.y);
            Vector3 left = animator.GetIKPosition(AvatarIKGoal.LeftFoot);
            Vector3 right = animator.GetIKPosition(AvatarIKGoal.RightFoot);
            animator.bodyPosition += shift;
            animator.SetIKPosition(AvatarIKGoal.LeftFoot, left + shift);
            animator.SetIKPosition(AvatarIKGoal.RightFoot, right + shift);
        }
    }
}
