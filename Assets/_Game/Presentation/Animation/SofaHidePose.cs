using UnityEngine;

namespace ConGame.Presentation.Animation
{
    // The sofa hide anchor is at floor level. Tuck the hips behind the
    // backrest and let the retargeted knees flex over two planted shoes.
    public sealed class SofaHidePose : StateMachineBehaviour
    {
        // Distance from the humanoid foot contact plane to the suit's leather outsole.
        private const float ShoeSoleOffset = .049f;

        public override void OnStateIK(Animator animator, AnimatorStateInfo state, int layer)
        {
            if (!animator.isHuman) return;
            float weight = 1f;
            if (animator.IsInTransition(layer))
            {
                float t = Mathf.Clamp01(animator.GetAnimatorTransitionInfo(layer).normalizedTime);
                weight = animator.GetNextAnimatorStateInfo(layer).fullPathHash == state.fullPathHash ? t : 1f - t;
            }

            Transform anchor = animator.transform.parent;
            Vector3 forward = anchor.forward;
            Vector3 right = anchor.right;
            animator.bodyPosition -= (Vector3.up * .37f + forward * .06f) * weight;
            PlaceFoot(animator, AvatarIKGoal.LeftFoot, anchor.position - right * .15f + forward * .11f,
                animator.leftFeetBottomHeight, weight);
            PlaceFoot(animator, AvatarIKGoal.RightFoot, anchor.position + right * .16f - forward * .07f,
                animator.rightFeetBottomHeight, weight);
            // Brace the near wrist over the forward knee.
            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, weight);
            animator.SetIKPosition(AvatarIKGoal.LeftHand, anchor.position - right * .20f
                + forward * .24f + Vector3.up * .53f);
        }

        private static void PlaceFoot(Animator animator, AvatarIKGoal foot, Vector3 floor, float heelHeight, float weight)
        {
            animator.SetIKPositionWeight(foot, weight);
            animator.SetIKRotationWeight(foot, weight);
            animator.SetIKPosition(foot, floor + Vector3.up * (heelHeight + ShoeSoleOffset));
            animator.SetIKRotation(foot, animator.transform.rotation);
        }
    }
}
