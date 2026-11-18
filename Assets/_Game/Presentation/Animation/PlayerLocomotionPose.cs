using ConGame.Runtime.Player;
using UnityEngine;

namespace ConGame.Presentation.Animation
{
    [DisallowMultipleComponent, RequireComponent(typeof(Animator))]
    public sealed class PlayerLocomotionPose : MonoBehaviour
    {
        private Animator animator;
        private PlayerInteractor player;
        private CharacterController motor;
        private float gripWeight;
        private float lookYaw;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            player = GetComponentInParent<PlayerInteractor>();
            motor = GetComponentInParent<CharacterController>();
        }

        private void OnAnimatorIK(int layer)
        {
            if (layer != 0 || !animator.isHuman || player == null || player.IsMovementLocked) return;
            var state = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0)
                : animator.GetCurrentAnimatorStateInfo(0);
            if (!state.IsName("Idle") && !state.IsName("Walk"))
            {
                gripWeight = 0f;
                lookYaw = 0f;
                return;
            }

            // The eyes and shoulders lead the motor through a change of direction.
            Vector3 travel = motor != null && motor.enabled
                ? Vector3.ProjectOnPlane(motor.velocity, Vector3.up) : Vector3.zero;
            float turn = travel.sqrMagnitude > .25f ? Vector3.SignedAngle(transform.forward, travel, Vector3.up) : 0f;
            lookYaw = Mathf.Lerp(lookYaw, Mathf.Clamp(turn, -60f, 60f), 1f - Mathf.Exp(-Time.deltaTime * 16f));
            if (Mathf.Abs(lookYaw) > 1f)
            {
                Vector3 gaze = Quaternion.AngleAxis(lookYaw, Vector3.up) * transform.forward;
                animator.SetLookAtPosition(animator.GetBoneTransform(HumanBodyBones.Head).position + gaze);
                animator.SetLookAtWeight(1f, .30f, .72f, 0f, .5f);
            }

            bool hasBat = player.HeldEntity != null && player.HeldEntity.ItemId == "baseball_bat";
            gripWeight = Mathf.MoveTowards(gripWeight, hasBat ? 1f : 0f, Time.deltaTime * 6f);
            if (gripWeight <= 0f) return;

            Vector3 hips = animator.GetBoneTransform(HumanBodyBones.Hips).position;
            Vector3 grip = hips + transform.forward * .32f + transform.up * .23f + transform.right * .055f;
            Vector3 barrel = (transform.up + transform.right * .42f + transform.forward * .52f).normalized;
            // The bat pivot is in its handle and its local Y runs along the barrel.
            // Place the second palm a hand-width below the first, near the knob.
            SetHand(AvatarIKGoal.RightHand, grip, Quaternion.LookRotation(barrel, -transform.forward));
            SetHand(AvatarIKGoal.LeftHand, grip - barrel * .095f, Quaternion.LookRotation(barrel, transform.forward));
        }

        private void SetHand(AvatarIKGoal hand, Vector3 position, Quaternion rotation)
        {
            animator.SetIKPositionWeight(hand, gripWeight);
            animator.SetIKRotationWeight(hand, gripWeight);
            animator.SetIKPosition(hand, position);
            animator.SetIKRotation(hand, rotation);
        }
    }
}
