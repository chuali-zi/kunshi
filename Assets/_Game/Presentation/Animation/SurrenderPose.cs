using UnityEngine;

namespace ConGame.Presentation.Animation
{
    public sealed class SurrenderPose : StateMachineBehaviour
    {
        // The retargeted pose folds the shins but keeps its hips at standing height.
        // This is the mesh-to-floor clearance with both knees folded in Room_M1.
        private const float KneelingDrop = .42f;

        public override void OnStateIK(Animator animator, AnimatorStateInfo state, int layer)
        {
            if (!animator.isHuman) return;
            float weight = 1f;
            if (animator.IsInTransition(layer))
            {
                float t = Mathf.Clamp01(animator.GetAnimatorTransitionInfo(layer).normalizedTime);
                weight = animator.GetNextAnimatorStateInfo(layer).fullPathHash == state.fullPathHash ? t : 1f - t;
            }

            Vector3 drop = Vector3.down * (KneelingDrop * weight);
            // Keep one palm beside the temple and offer the other towards the killer.
            // Get the authored goals before lowering the body so both travel with the knees.
            Vector3 nearHand = animator.GetIKPosition(AvatarIKGoal.RightHand) + drop;
            Vector3 farHand = animator.GetIKPosition(AvatarIKGoal.LeftHand) + drop;
            animator.bodyPosition += drop;
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, weight);
            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, weight);
            animator.SetIKPosition(AvatarIKGoal.RightHand, nearHand - animator.transform.forward * .20f
                + Vector3.up * .06f);
            animator.SetIKPosition(AvatarIKGoal.LeftHand, farHand + animator.transform.forward * .11f);
        }
    }
}
