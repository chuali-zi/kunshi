using UnityEngine;

namespace ConGame.Presentation.Animation
{
    /// <summary>Rest on the thighs, then shift weight from shoe to shoe when leaving the chair.</summary>
    public sealed class SeatedRestPose : StateMachineBehaviour
    {
        private Vector3 leftPlant;
        private Vector3 rightPlant;
        private Quaternion seatedFacing;
        private bool hasPlants;
        private bool rightLanded;
        private Vector3 rightLanding;

        public override void OnStateEnter(Animator animator, AnimatorStateInfo state, int layer)
        {
            hasPlants = false;
            rightLanded = false;
        }

        public override void OnStateIK(Animator animator, AnimatorStateInfo state, int layer)
        {
            if (!animator.isHuman) return;
            float weight = 1f;
            float rise = 0f;
            if (animator.IsInTransition(layer))
            {
                float t = Mathf.Clamp01(animator.GetAnimatorTransitionInfo(layer).normalizedTime);
                var next = animator.GetNextAnimatorStateInfo(layer);
                weight = next.fullPathHash == state.fullPathHash ? t : 1f - t;
                if (hasPlants && animator.transform.parent != null && (next.IsName("Idle") || next.IsName("Walk")))
                {
                    rise = t;
                    Rise(animator, t, next.IsName("Walk"));
                }
            }
            else
            {
                leftPlant = animator.GetBoneTransform(HumanBodyBones.LeftFoot).position;
                rightPlant = animator.GetBoneTransform(HumanBodyBones.RightFoot).position;
                seatedFacing = animator.transform.rotation;
                hasPlants = true;
            }

            RestHand(animator, AvatarIKGoal.LeftHand, HumanBodyBones.LeftLowerLeg, weight, rise);
            RestHand(animator, AvatarIKGoal.RightHand, HumanBodyBones.RightLowerLeg, weight, rise);

            Transform root = animator.transform;
            Vector3 eyes = animator.GetBoneTransform(HumanBodyBones.Head).position;
            animator.SetLookAtPosition(eyes + root.forward - root.right * .85f - root.up * .17f);
            animator.SetLookAtWeight(weight, .16f, .67f, 0f, .45f);
        }

        private void Rise(Animator animator, float t, bool walking)
        {
            // Fold over the knees to leave the seat. Uncurl as the trailing shoe comes through.
            Transform root = animator.transform;
            float load = Mathf.SmoothStep(0f, 1f, t / .28f)
                * (1f - Mathf.SmoothStep(0f, 1f, (t - .28f) / .61f));
            animator.bodyRotation = Quaternion.AngleAxis(30f * load, root.right) * animator.bodyRotation;
            animator.bodyPosition += (root.forward * .07f - Vector3.up * .085f) * load;

            // The moving controller opens a longer stride. Keep the supporting knee bent
            // until the second sole can take the weight of the upright body.
            if (walking)
            {
                float transfer = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.49f, .77f, t)) *
                                 Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(.84f, 1f, t));
                animator.bodyPosition -= Vector3.up * (.10f * transfer);
            }

            Transform motor = root.parent;
            Vector3 stance = motor.position - motor.forward * .115f;
            if (!rightLanded)
            {
                rightLanding = stance + motor.right * .17f;
                rightLanded = t >= .50f;
            }
            Step(animator, AvatarIKGoal.RightFoot, rightPlant, rightLanding,
                Mathf.InverseLerp(.16f, .50f, t), Mathf.InverseLerp(walking ? .73f : .85f, 1f, t));
            Vector3 leftLanding = walking ? motor.position + motor.forward * .14f - motor.right * .09f
                : stance - motor.right * .15f;
            Step(animator, AvatarIKGoal.LeftFoot, leftPlant, leftLanding,
                Mathf.InverseLerp(.50f, walking ? .80f : .94f, t), Mathf.InverseLerp(.85f, 1f, t));
        }

        private void Step(Animator animator, AvatarIKGoal foot, Vector3 start, Vector3 end,
            float swing, float release)
        {
            end.y = Mathf.Min(leftPlant.y, rightPlant.y);
            Vector3 ankle = Vector3.Lerp(start, end, Mathf.SmoothStep(0f, 1f, swing));
            ankle.y += .085f * Mathf.Sin(Mathf.PI * swing);
            float weight = Mathf.SmoothStep(1f, 0f, release);
            animator.SetIKPositionWeight(foot, weight);
            animator.SetIKPosition(foot, ankle);
            animator.SetIKRotationWeight(foot, weight);
            animator.SetIKRotation(foot, Quaternion.Slerp(seatedFacing, animator.transform.parent.rotation, swing));
        }

        private static void RestHand(Animator animator, AvatarIKGoal hand, HumanBodyBones knee,
            float weight, float rise)
        {
            Transform root = animator.transform;
            bool supporting = hand == AvatarIKGoal.LeftHand;
            Vector3 wrist = animator.GetBoneTransform(knee).position
                - root.forward * (supporting ? .015f : .045f) + root.up * .155f;
            // The left palm bears weight as the right arm floats forward for balance.
            // Release each wrist into the incoming clip before the body reaches full height.
            float lift = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.06f, .44f, rise));
            wrist += lift * (supporting
                ? root.forward * .14f - root.right * .07f + root.up * .065f
                : root.forward * .18f + root.right * .10f + root.up * .23f);
            if (rise > 0f)
                weight = Mathf.SmoothStep(1f, 0f, supporting
                    ? Mathf.InverseLerp(.32f, .70f, rise) : Mathf.InverseLerp(.51f, .95f, rise));
            animator.SetIKPositionWeight(hand, weight);
            animator.SetIKPosition(hand, wrist);
            animator.SetIKRotationWeight(hand, weight);
            float wristPitch = supporting ? 18f : Mathf.Lerp(12f, -10f, lift);
            animator.SetIKRotation(hand, Quaternion.AngleAxis(wristPitch, root.right) * animator.GetIKRotation(hand));
        }
    }
}
