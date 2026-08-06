using System.Collections;
using ConGame.Runtime.Interaction;
using UnityEngine;

namespace ConGame.Runtime.AI
{
    [DisallowMultipleComponent]
    public sealed class KillerHitReceiver : MonoBehaviour, IHitReceiver
    {
        [SerializeField] private Transform feedbackTarget;
        [SerializeField, Min(0.05f)] private float reactionDuration = 0.28f;
        [SerializeField, Min(0f)] private float recoilDistance = 0.14f;
        [SerializeField, Range(0f, 25f)] private float recoilAngle = 9f;

        private Coroutine _reaction;
        private Vector3 _restPosition;
        private Quaternion _restRotation;

        public bool IsReacting => _reaction != null;
        public string LastHitSourceId { get; private set; } = string.Empty;

        public void Configure(Transform target)
        {
            RestoreTarget();
            feedbackTarget = target;
            CaptureTarget();
        }

        private void Awake()
        {
            if (feedbackTarget == null)
            {
                Animator modelAnimator = GetComponentInChildren<Animator>(true);
                feedbackTarget = modelAnimator == null ? null : modelAnimator.transform;
            }

            CaptureTarget();
        }

        public HitResult ReceiveHit(in HitInfo hit)
        {
            if (!isActiveAndEnabled || feedbackTarget == null)
            {
                return HitResult.Ignored;
            }

            LastHitSourceId = hit.SourceId;
            if (_reaction != null)
            {
                StopCoroutine(_reaction);
                RestoreTarget();
            }

            Vector3 direction = hit.Direction.sqrMagnitude < 0.001f ? transform.forward : hit.Direction.normalized;
            _reaction = StartCoroutine(PlayReaction(direction));
            return HitResult.Accepted;
        }

        private IEnumerator PlayReaction(Vector3 worldDirection)
        {
            Vector3 localDirection = feedbackTarget.parent == null
                ? worldDirection
                : feedbackTarget.parent.InverseTransformDirection(worldDirection);
            localDirection.y = 0f;
            localDirection = localDirection.sqrMagnitude < 0.001f ? Vector3.forward : localDirection.normalized;
            Vector3 axis = Vector3.Cross(Vector3.up, localDirection);
            Quaternion recoilRotation = Quaternion.AngleAxis(recoilAngle, axis) * _restRotation;
            float elapsed = 0f;

            while (elapsed < reactionDuration)
            {
                elapsed += Time.deltaTime;
                float pulse = Mathf.Sin(Mathf.Clamp01(elapsed / reactionDuration) * Mathf.PI);
                feedbackTarget.localPosition = _restPosition + localDirection * (recoilDistance * pulse);
                feedbackTarget.localRotation = Quaternion.Slerp(_restRotation, recoilRotation, pulse);
                yield return null;
            }

            RestoreTarget();
            _reaction = null;
        }

        private void OnDisable()
        {
            if (_reaction != null)
            {
                StopCoroutine(_reaction);
                _reaction = null;
            }

            RestoreTarget();
        }

        private void CaptureTarget()
        {
            if (feedbackTarget == null)
            {
                return;
            }

            _restPosition = feedbackTarget.localPosition;
            _restRotation = feedbackTarget.localRotation;
        }

        private void RestoreTarget()
        {
            if (feedbackTarget == null)
            {
                return;
            }

            feedbackTarget.localPosition = _restPosition;
            feedbackTarget.localRotation = _restRotation;
        }
    }
}
