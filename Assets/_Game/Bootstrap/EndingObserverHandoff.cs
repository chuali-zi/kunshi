using System.Collections;
using ConGame.Runtime.Ending;
using ConGame.Runtime.Player;
using UnityEngine;

namespace ConGame.Bootstrap
{
    /// <summary>
    /// Separates the E2 observer (the real PlayerMotor) from the visible original
    /// actor. The clone is deliberately outside the reset registry so a pass reset
    /// cannot teleport it back to the player.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EndingObserverHandoff : MonoBehaviour
    {
        private PlayerMotor _player;
        private Transform _screenAnchor;
        private Transform _originalActorVisual;
        private Transform _exitAnchor;
        private EndingSequenceSettings _settings;
        private GameObject _observerClone;
        private Vector3 _capturedPlayerPosition;
        private Quaternion _capturedPlayerRotation;
        private bool _captured;
        private Coroutine _exitRoutine;
        private bool _originalActive = true;

        public bool IsConfigured => _player != null && _screenAnchor != null &&
                                     _originalActorVisual != null && _exitAnchor != null;
        public bool HasCapturedOriginalPose => _captured;
        public bool HasObserverClone => _observerClone != null;
        public bool ObserverOwnsOriginalVisual => _originalActorVisual != null &&
                                                   !_originalActorVisual.gameObject.activeSelf;
        public Vector3 CapturedPlayerPosition => _capturedPlayerPosition;

        public void Configure(
            PlayerMotor player,
            EndingSequenceSettingsAsset settingsAsset,
            Transform screenAnchor,
            Transform originalActorVisual,
            Transform exitAnchor)
        {
            _player = player;
            _settings = settingsAsset == null
                ? EndingSequenceSettings.Default
                : settingsAsset.CreateSettings();
            _screenAnchor = screenAnchor;
            _originalActorVisual = originalActorVisual;
            _originalActive = originalActorVisual != null && originalActorVisual.gameObject.activeSelf;
            _exitAnchor = exitAnchor;
        }

        /// <summary>Must be called before E1 blackout while the original actor is visible.</summary>
        public void CaptureOriginalPose()
        {
            if (_player == null)
            {
                return;
            }

            _capturedPlayerPosition = _player.transform.position;
            _capturedPlayerRotation = _player.transform.rotation;
            _captured = true;
        }

        /// <summary>
        /// Creates a visual-only copy at the captured player pose and moves the real
        /// PlayerMotor to the TV observation anchor. The original visual is detached
        /// from the motor by disabling that visual hierarchy.
        /// </summary>
        public bool BeginObserverTakeover()
        {
            if (!IsConfigured || !_captured)
            {
                return false;
            }

            StopExitRoutine();
            DestroyObserverClone();
            _observerClone = Object.Instantiate(_originalActorVisual.gameObject);
            _observerClone.name = "EndingOriginalActorVisual";
            _observerClone.SetActive(true);
            _observerClone.transform.SetPositionAndRotation(
                _capturedPlayerPosition, _capturedPlayerRotation);
            StripGameplayComponents(_observerClone);
            _originalActorVisual.gameObject.SetActive(false);

            _player.Teleport(_screenAnchor.position, _screenAnchor.rotation);
            return true;
        }

        public void BeginExitMotion()
        {
            if (_observerClone == null || _exitAnchor == null)
            {
                return;
            }

            StopExitRoutine();
            _exitRoutine = StartCoroutine(MoveCloneToExit());
        }

        public void ResetHandoff()
        {
            StopExitRoutine();
            DestroyObserverClone();
            if (_originalActorVisual != null)
            {
                _originalActorVisual.gameObject.SetActive(_originalActive);
            }

            _captured = false;
        }

        public void CompleteHandoff()
        {
            StopExitRoutine();
            DestroyObserverClone();
            _captured = false;
        }

        private IEnumerator MoveCloneToExit()
        {
            Transform clone = _observerClone.transform;
            Vector3 startPosition = clone.position;
            Quaternion startRotation = clone.rotation;
            float duration = Mathf.Clamp((_settings?.E2FreeControlSeconds ?? 4f) - 0.5f, 0.25f, 4f);
            Animator animator = clone.GetComponentInChildren<Animator>();
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                animator.Rebind();
                animator.SetFloat("Speed", Vector3.Distance(startPosition, _exitAnchor.position) / duration);
                animator.SetFloat("LocomotionRate", 0.75f);
                animator.Play("Walk", 0, 0f);
            }

            Vector3 direction = _exitAnchor.position - startPosition;
            direction.y = 0f;
            Quaternion facing = direction.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(direction) : startRotation;

            float elapsed = 0f;
            while (elapsed < duration && clone != null && _exitAnchor != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                clone.SetPositionAndRotation(
                    Vector3.Lerp(startPosition, _exitAnchor.position, t),
                    Quaternion.Slerp(startRotation, facing, Mathf.Clamp01(t * 4f)));
                yield return null;
            }

            if (clone != null && _exitAnchor != null)
            {
                clone.SetPositionAndRotation(_exitAnchor.position, _exitAnchor.rotation);
                clone.gameObject.SetActive(false);
            }

            _exitRoutine = null;
        }

        private void StopExitRoutine()
        {
            if (_exitRoutine != null)
            {
                StopCoroutine(_exitRoutine);
                _exitRoutine = null;
            }
        }

        private void DestroyObserverClone()
        {
            if (_observerClone != null)
            {
                if (Application.isPlaying) Destroy(_observerClone);
                else DestroyImmediate(_observerClone);
                _observerClone = null;
            }
        }

        private static void StripGameplayComponents(GameObject clone)
        {
            foreach (Collider collider in clone.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (Rigidbody body in clone.GetComponentsInChildren<Rigidbody>(true))
            {
                body.isKinematic = true;
                body.detectCollisions = false;
            }
            MonoBehaviour[] behaviours = clone.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] != null)
                {
                    behaviours[i].enabled = false;
                    if (Application.isPlaying) Object.Destroy(behaviours[i]);
                    else Object.DestroyImmediate(behaviours[i]);
                }
            }
        }

        private void OnDestroy()
        {
            StopExitRoutine();
            DestroyObserverClone();
        }
    }
}
