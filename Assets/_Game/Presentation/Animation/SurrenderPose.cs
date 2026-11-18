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

            animator.bodyPosition -= Vector3.up * (KneelingDrop * weight);
        }
    }
}
