using UnityEngine;

namespace ConGame.Presentation.Animation
{
    /// <summary>
    /// Presentation-only bridge that keeps the gameplay hand socket attached to the animated right-hand bone.
    /// PlayerInteractor continues to own only the socket Transform.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HumanoidHandSocketBinder : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private Transform handSocket;
        [SerializeField] private Vector3 localPosition;
        [SerializeField] private Vector3 localEulerAngles = new Vector3(0f, 0f, 0f);

        public void Configure(Animator targetAnimator, Transform socket, Vector3 position, Quaternion rotation)
        {
            animator = targetAnimator;
            handSocket = socket;
            localPosition = position;
            localEulerAngles = rotation.eulerAngles;
        }

        private void Awake()
        {
            BindNow();
        }

        public bool BindNow()
        {
            animator ??= GetComponentInChildren<Animator>(true);
            if (handSocket == null)
            {
                handSocket = FindDescendant(transform, "HandSocket_R");
            }

            Transform rightHand = null;
            if (animator != null && animator.isHuman && animator.avatar != null && animator.avatar.isValid)
            {
                rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            }

            rightHand ??= FindRightHandByName(animator == null ? transform : animator.transform);
            if (rightHand == null || handSocket == null)
            {
                return false;
            }

            handSocket.SetParent(rightHand, false);
            handSocket.localPosition = localPosition;
            handSocket.localRotation = Quaternion.Euler(localEulerAngles);
            Vector3 parentScale = rightHand.lossyScale;
            handSocket.localScale = new Vector3(
                SafeReciprocal(parentScale.x),
                SafeReciprocal(parentScale.y),
                SafeReciprocal(parentScale.z));            return true;
        }

        private static Transform FindRightHandByName(Transform root)
        {
            if (root == null)
            {
                return null;
            }

            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                string candidate = transforms[i].name.Replace("_", string.Empty).Replace(" ", string.Empty).ToLowerInvariant();
                if (candidate.EndsWith(":righthand") || candidate.EndsWith("righthand"))
                {
                    return transforms[i];
                }
            }

            return null;
        }

        private static Transform FindDescendant(Transform root, string targetName)
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i].name == targetName)
                {
                    return transforms[i];
                }
            }

            return null;
        }


private static float SafeReciprocal(float value) { return Mathf.Abs(value) < 0.0001f ? 1f : 1f / value; }
}
}