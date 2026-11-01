using ConGame.Domain.Facts;
using ConGame.Runtime.Facts;
using ConGame.Runtime.Input;
using ConGame.Runtime.Loop;
using ConGame.Runtime.Player;
using UnityEngine;

namespace ConGame.Presentation.Animation
{
    /// <summary>
    /// Places the player on the room chair during awakening and offsets the model
    /// into its authored seated pose. The player root returns to its clear standing
    /// point on the first meaningful gameplay input.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerSeatedPosePresenter : MonoBehaviour, ILoopResettable
    {
        private const float SeatedVisualDrop = 0.4f;
        private const float StandBlendSeconds = 0.25f;

        [SerializeField] private LoopDirector loop;
        [SerializeField] private PlayerInputRouter input;
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private GameplayFactService facts;
        [SerializeField] private Animator animator;
        [SerializeField] private Transform chair;
        [SerializeField] private Transform standAnchor;

        private CharacterController _characterController;
        private Vector3 _standingPosition;
        private Quaternion _standingRotation;
        private Vector3 _initialVisualLocalPosition;
        private Quaternion _initialVisualLocalRotation;
        private bool _hasCapturedInitialState;
        private bool _isAwakeningSeated;
        private bool _isVisualSeated;
        private bool _isBlendingVisual;
        private float _visualBlendElapsed;
        private Vector3 _visualBlendStart;
        private Vector3 _visualBlendTarget;
        private Quaternion _visualBlendStartRotation;
        private Quaternion _visualBlendTargetRotation;

        public Vector3 StandingPosition => _standingPosition;
        public bool IsAwakeningSeated => _isAwakeningSeated;

        public int ResetOrder => 290;

        public void Configure(
            LoopDirector loopDirector,
            PlayerInputRouter inputRouter,
            PlayerMotor playerMotor,
            GameplayFactService factService,
            Animator playerAnimator,
            Transform chairTransform,
            Transform playerStandAnchor = null)
        {
            Unsubscribe();
            loop = loopDirector;
            input = inputRouter;
            motor = playerMotor;
            facts = factService;
            animator = playerAnimator;
            chair = chairTransform;
            standAnchor = playerStandAnchor;
            _characterController = motor == null ? null : motor.GetComponent<CharacterController>();
            CaptureInitialState();
            if (isActiveAndEnabled)
            {
                Subscribe();
            }
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Update()
        {
            if (facts == null || animator == null || _isAwakeningSeated)
            {
                return;
            }

            if (facts.CaptureSnapshot().PlayerPose == PlayerPose.Sitting && !_isVisualSeated)
            {
                // E3 has already placed the root at the chair. Its Sitting fact
                // only needs the model offset, not the awakening relocation.
                SetVisualPose(seated: true, blendSeconds: 0f);
                motor?.SetInputLocked(true);
                if (_characterController != null)
                {
                    _characterController.enabled = false;
                }
            }

            if (_isBlendingVisual)
            {
                _visualBlendElapsed += Time.deltaTime;
                float t = Mathf.Clamp01(_visualBlendElapsed / StandBlendSeconds);
                t = t * t * (3f - 2f * t);
                animator.transform.localPosition = Vector3.Lerp(_visualBlendStart, _visualBlendTarget, t);
                animator.transform.localRotation = Quaternion.Slerp(
                    _visualBlendStartRotation, _visualBlendTargetRotation, t);
                if (t >= 1f)
                {
                    _isBlendingVisual = false;
                }
            }
        }

        public void CaptureInitialState()
        {
            if (motor == null || animator == null)
            {
                return;
            }

            Transform root = motor.transform;
            _standingPosition = standAnchor == null ? root.position : standAnchor.position;
            _standingRotation = standAnchor == null ? root.rotation : standAnchor.rotation;
            _initialVisualLocalPosition = animator.transform.localPosition;
            _initialVisualLocalRotation = animator.transform.localRotation;
            _hasCapturedInitialState = true;
        }

        public void Quiesce(in LoopResetContext context)
        {
            _isBlendingVisual = false;
            motor?.SetInputLocked(true);
            if (_characterController != null)
            {
                _characterController.enabled = false;
            }
        }

        public void Restore(in LoopResetContext context)
        {
            _isAwakeningSeated = false;
            _isVisualSeated = false;
            _isBlendingVisual = false;
            if (_hasCapturedInitialState && animator != null)
            {
                animator.transform.localPosition = _initialVisualLocalPosition;
                animator.transform.localRotation = _initialVisualLocalRotation;
            }
        }

        public void Resume(in LoopResetContext context)
        {
            if (_hasCapturedInitialState && motor != null)
            {
                motor.transform.SetPositionAndRotation(_standingPosition, _standingRotation);
            }
        }

        private void Subscribe()
        {
            if (loop != null)
            {
                loop.AwakeningStarted -= OnAwakeningStarted;
                loop.AwakeningStarted += OnAwakeningStarted;
            }

            if (input != null)
            {
                input.MeaningfulAction -= OnMeaningfulAction;
                input.MeaningfulAction += OnMeaningfulAction;
            }
        }

        private void Unsubscribe()
        {
            if (loop != null)
            {
                loop.AwakeningStarted -= OnAwakeningStarted;
            }

            if (input != null)
            {
                input.MeaningfulAction -= OnMeaningfulAction;
            }
        }

        private void OnAwakeningStarted(bool firstLaunch)
        {
            if (!_hasCapturedInitialState || motor == null || chair == null)
            {
                return;
            }

            Vector3 seatedPosition = new Vector3(chair.position.x, _standingPosition.y, chair.position.z);
            if (_characterController != null)
            {
                _characterController.enabled = false;
            }

            motor.SetInputLocked(true);
            motor.transform.SetPositionAndRotation(seatedPosition, chair.rotation);
            facts?.SetPlayerPose(PlayerPose.Sitting);
            _isAwakeningSeated = true;
            SetVisualPose(seated: true, blendSeconds: 0f);
        }

        private void OnMeaningfulAction()
        {
            if (!_isAwakeningSeated || motor == null)
            {
                return;
            }

            if (_characterController != null)
            {
                _characterController.enabled = false;
            }

            Vector3 seatedVisualPosition = animator == null ? Vector3.zero : animator.transform.position;
            Quaternion seatedVisualRotation = animator == null ? Quaternion.identity : animator.transform.rotation;
            motor.Teleport(_standingPosition, _standingRotation);
            if (animator != null)
            {
                animator.transform.SetPositionAndRotation(seatedVisualPosition, seatedVisualRotation);
            }

            if (_characterController != null)
            {
                _characterController.enabled = true;
            }

            motor.SetInputLocked(false);
            facts?.SetPlayerPose(PlayerPose.Standing);
            _isAwakeningSeated = false;
            SetVisualPose(seated: false, blendSeconds: StandBlendSeconds);
        }

        private void SetVisualPose(bool seated, float blendSeconds)
        {
            if (animator == null)
            {
                return;
            }

            _isVisualSeated = seated;
            _isBlendingVisual = false;
            Vector3 target = _initialVisualLocalPosition + (seated ? Vector3.down * SeatedVisualDrop : Vector3.zero);
            if (blendSeconds <= 0f)
            {
                animator.transform.localPosition = target;
                return;
            }

            _visualBlendStart = animator.transform.localPosition;
            _visualBlendTarget = target;
            _visualBlendStartRotation = animator.transform.localRotation;
            _visualBlendTargetRotation = _initialVisualLocalRotation;
            _visualBlendElapsed = 0f;
            _isBlendingVisual = true;
        }
    }
}
