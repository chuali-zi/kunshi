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
            // Lead with the free hand so the rising knee passes behind its fingertips.
            float lift = Mathf.SmoothStep(0f, 1f, supporting
                ? Mathf.InverseLerp(.06f, .44f, rise) : Mathf.InverseLerp(.01f, .31f, rise));

            // The left palm rests on the crown of the knee. The right elbow hangs
            // outside the suit, with the fingers slanting back across the thigh.
            Vector3 seated = supporting ? new Vector3(0f, .178f, -.010f) : new Vector3(.040f, .170f, -.040f);
            Vector3 lifted = supporting ? new Vector3(-.07f, .220f, .125f) : new Vector3(.10f, .385f, .135f);
            Vector3 wrist = animator.GetBoneTransform(knee).position
                + root.rotation * Vector3.Lerp(seated, lifted, lift);

            if (rise > 0f)
                weight = Mathf.SmoothStep(1f, 0f, supporting
                    ? Mathf.InverseLerp(.32f, .70f, rise) : Mathf.InverseLerp(.51f, .95f, rise));
            animator.SetIKPositionWeight(hand, weight);
            animator.SetIKPosition(hand, wrist);

            float yaw = supporting ? 0f : -32f * (1f - lift);
            float pitch = supporting ? Mathf.Lerp(22f, 18f, lift) : Mathf.Lerp(39f, -10f, lift);
            animator.SetIKRotationWeight(hand, weight);
            animator.SetIKRotation(hand, Quaternion.AngleAxis(yaw, root.up) * Quaternion.AngleAxis(pitch, root.right)
                * animator.GetIKRotation(hand));

            if (!supporting)
            {
                Vector3 shoulder = animator.GetBoneTransform(HumanBodyBones.RightUpperArm).position;
                animator.SetIKHintPosition(AvatarIKHint.RightElbow,
                    shoulder + root.rotation * new Vector3(.15f, -.24f, -.12f));
                animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow, .65f * weight * (1f - lift));
            }

            // The suit weights the row of fingers to the index chain. Ease the
            // bend at both knuckles, then return it to the incoming gesture.
            float opening = 30f * weight * (1f - lift);
            if (opening <= 0f) return;
            HumanBodyBones middle = supporting ? HumanBodyBones.LeftIndexIntermediate : HumanBodyBones.RightIndexIntermediate;
            RelaxKnuckle(animator, middle, supporting ? HumanBodyBones.LeftIndexDistal : HumanBodyBones.RightIndexDistal, opening);
            RelaxKnuckle(animator, supporting ? HumanBodyBones.LeftIndexProximal : HumanBodyBones.RightIndexProximal, middle, opening);
        }

        private static void RelaxKnuckle(Animator animator, HumanBodyBones bone, HumanBodyBones child, float degrees)
        {
            Transform joint = animator.GetBoneTransform(bone);
            Transform tip = animator.GetBoneTransform(child);
            if (joint == null || tip == null) return;
            Transform parent = joint.parent;
            Vector3 finger = parent.InverseTransformDirection(tip.position - joint.position);
            Vector3 palm = parent.InverseTransformDirection(joint.position - parent.position);
            Quaternion straighten = Quaternion.FromToRotation(finger, palm);
            animator.SetBoneLocalRotation(bone, Quaternion.RotateTowards(Quaternion.identity, straighten, degrees)
                * joint.localRotation);
        }
    }
}
