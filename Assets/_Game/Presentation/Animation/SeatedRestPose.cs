using UnityEngine;

namespace ConGame.Presentation.Animation
{
    /// <summary>Rest the wrists on the posed thighs and turn slightly toward the door.</summary>
    public sealed class SeatedRestPose : StateMachineBehaviour
    {
        public override void OnStateIK(Animator animator, AnimatorStateInfo state, int layer)
        {
            if (!animator.isHuman) return;
            float weight = 1f;
            if (animator.IsInTransition(layer))
            {
                float t = Mathf.Clamp01(animator.GetAnimatorTransitionInfo(layer).normalizedTime);
                weight = animator.GetNextAnimatorStateInfo(layer).fullPathHash == state.fullPathHash ? t : 1f - t;
            }

            RestHand(animator, AvatarIKGoal.LeftHand, HumanBodyBones.LeftLowerLeg, -.035f, weight);
            RestHand(animator, AvatarIKGoal.RightHand, HumanBodyBones.RightLowerLeg, .015f, weight);

            Transform root = animator.transform;
            Vector3 eyes = animator.GetBoneTransform(HumanBodyBones.Head).position;
            animator.SetLookAtPosition(eyes + root.forward - root.right * .85f - root.up * .17f);
            animator.SetLookAtWeight(weight, .16f, .67f, 0f, .45f);
        }

        private static void RestHand(Animator animator, AvatarIKGoal hand, HumanBodyBones knee,
            float inset, float weight)
        {
            Transform root = animator.transform;
            Vector3 wrist = animator.GetBoneTransform(knee).position - root.forward * inset + root.up * .20f;
            animator.SetIKPositionWeight(hand, weight);
            animator.SetIKPosition(hand, wrist);
        }
    }
}
