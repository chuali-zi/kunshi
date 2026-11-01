using System;
using System.Collections;
using ConGame.Domain.Facts;
using ConGame.Domain.Interaction;
using ConGame.Runtime.Config;
using ConGame.Runtime.Facts;
using ConGame.Runtime.Input;
using ConGame.Runtime.Interaction;
using ConGame.Runtime.Loop;
using UnityEngine;

namespace ConGame.Runtime.Player
{

    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMotor : MonoBehaviour, ILoopResettable
    {
        [SerializeField] private CharacterController characterController;
        [SerializeField] private PlayerInputRouter input;
        [SerializeField] private Transform movementCamera;
        [SerializeField] private GameplayFactService facts;
        [SerializeField] private GameConfigAsset config;

        private Vector3 _initialPosition;
        private Quaternion _initialRotation;
        private float _verticalVelocity;
        
        private float _movementSpeedMultiplier = 1f;
        private bool _inputLocked;
        private Transform _initialMovementCamera;
        private bool _useMovementReferenceForAim;
        private bool _initialUseMovementReferenceForAim;
        private Coroutine _climbRoutine;
        private bool _controllerEnabledBeforeClimb;
        private Action _climbCompleted;
        private float _limpUntil;
        private float _limpMultiplier = 1f;
        private Vector3 _corridorInward;
        private float _corridorScale;
        private float _corridorPushback;
        private bool _corridorActive;

        /// <summary>攀爬开始(参数为攀爬时长秒),供表现层同步攀爬动画。</summary>
        public event Action<float> ClimbStarted;

        public int ResetOrder => 300;

        

        public float MovementSpeedMultiplier => _movementSpeedMultiplier;
public PlayerInputRouter Input => input;
        public Vector3 AimForward => _useMovementReferenceForAim && movementCamera != null
            ? movementCamera.forward
            : transform.forward;

        public void Configure(
            CharacterController controller,
            PlayerInputRouter inputRouter,
            Transform cameraTransform,
            GameplayFactService factService,
            GameConfigAsset gameConfig)
        {
            characterController = controller;
            input = inputRouter;
            movementCamera = cameraTransform;
            facts = factService;
            config = gameConfig;
        }

        private void Awake()
        {
            characterController ??= GetComponent<CharacterController>();
        }

        private void Update()
        {
            if (characterController == null || !characterController.enabled || input == null || config == null ||
                _inputLocked)
            {
                facts?.SetMoving(false);
                return;
            }

            Vector2 moveInput = input.Move;
            Vector3 forward = movementCamera == null ? Vector3.forward : movementCamera.forward;
            Vector3 right = movementCamera == null ? Vector3.right : movementCamera.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            Vector3 planar = Vector3.ClampMagnitude(forward * moveInput.y + right * moveInput.x, 1f);
            bool moving = planar.sqrMagnitude > 0.01f;
            facts?.SetMoving(moving);

            if (moving)
            {
                FactSnapshot snapshot = facts == null ? default : facts.CaptureSnapshot();
                if (snapshot.PlayerPose == PlayerPose.Crouched || snapshot.PlayerPose == PlayerPose.Hidden)
                {
                    facts.SetPlayerPose(PlayerPose.Standing);
                }

                Quaternion targetRotation = Quaternion.LookRotation(planar, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    targetRotation,
                    config.TurnSpeedDegrees * Time.deltaTime);
            }

            RefreshLimpFromShardZones();
            float limp = Time.time < _limpUntil ? _limpMultiplier : 1f;
            float speed = (input.SprintHeld ? config.SprintSpeed : config.WalkSpeed) *
                          _movementSpeedMultiplier * limp;
            if (characterController.isGrounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = -2f;
            }
            else
            {
                _verticalVelocity += Physics.gravity.y * Time.deltaTime;
            }

            if (_corridorActive)
            {
                planar = CorridorResistance.FilterMove(planar, _corridorInward, _corridorScale, _corridorPushback);
                _corridorActive = false;
            }

            Vector3 velocity = planar * speed;
            velocity.y = _verticalVelocity;
            characterController.Move(velocity * Time.deltaTime);
        }

        public void SetInputLocked(bool locked)
        {
            _inputLocked = locked;
            if (locked)
            {
                facts?.SetMoving(false);
            }
        }

        public void SetMovementReference(Transform reference, bool useForAim = false)
        {
            movementCamera = reference;
            _useMovementReferenceForAim = useForAim;
        }

        public bool TryClimb(Transform standAnchor, float duration, Action completed)
        {
            if (standAnchor == null || _climbRoutine != null)
            {
                return false;
            }

            _inputLocked = true;
            facts?.SetMoving(false);
            _climbCompleted = completed;
            _controllerEnabledBeforeClimb = characterController != null && characterController.enabled;
            if (characterController != null)
            {
                characterController.enabled = false;
            }

            float climbDuration = Mathf.Max(0.01f, duration);
            ClimbStarted?.Invoke(climbDuration);
            _climbRoutine = StartCoroutine(ClimbTo(
                standAnchor.position,
                standAnchor.rotation,
                climbDuration,
                completed));
            return true;
        }

        public void CancelClimb()
        {
            if (_climbRoutine == null)
            {
                return;
            }

            StopCoroutine(_climbRoutine);
            _climbRoutine = null;
            Action completed = _climbCompleted;
            _climbCompleted = null;
            if (characterController != null)
            {
                characterController.enabled = _controllerEnabledBeforeClimb;
            }

            completed?.Invoke();
        }

        public void SetMovementSpeedMultiplier(float multiplier)
        {
            _movementSpeedMultiplier = Mathf.Clamp(multiplier, 0.1f, 1f);
        }

        public void ApplyLimp(float multiplier, float seconds)
        {
            _limpMultiplier = Mathf.Clamp(multiplier, 0.1f, 1f);
            _limpUntil = Time.time + Mathf.Max(0f, seconds);
        }

        public void ApplyCorridorResistance(Vector3 inwardDirection, float inwardScale, float pushback)
        {
            _corridorInward = inwardDirection;
            _corridorScale = inwardScale;
            _corridorPushback = pushback;
            _corridorActive = true;
        }

        private void RefreshLimpFromShardZones()
        {
            for (int i = 0; i < ShardSlowZoneVolume.ActiveCount; i++)
            {
                ShardSlowZoneVolume zone = ShardSlowZoneVolume.ActiveAt(i);
                if (zone == null || !zone.IsZoneActive)
                {
                    continue;
                }

                Vector3 delta = transform.position - zone.transform.position;
                delta.y = 0f;
                if (delta.sqrMagnitude <= zone.Radius * zone.Radius)
                {
                    ApplyLimp(zone.MovementMultiplier, zone.LimpSeconds);
                }
            }
        }


        public void Teleport(Vector3 position, Quaternion rotation)
        {
            CancelClimb();
            PlayerZoneTrigger.ClearOccupancy(facts);
            bool wasEnabled = characterController != null && characterController.enabled;
            if (characterController != null)
            {
                characterController.enabled = false;
            }

            transform.SetPositionAndRotation(position, rotation);
            if (characterController != null)
            {
                characterController.enabled = wasEnabled;
            }
        }
        public void CaptureInitialState()
        {
            _movementSpeedMultiplier = 1f;
            _initialMovementCamera = movementCamera;
            _initialUseMovementReferenceForAim = _useMovementReferenceForAim;
            _initialPosition = transform.position;
            _initialRotation = transform.rotation;
        }

        public void Quiesce(in LoopResetContext context)
        {
            CancelClimb();
            _inputLocked = true;
            _verticalVelocity = 0f;
            PlayerZoneTrigger.ClearOccupancy(facts);
            if (characterController != null)
            {
                characterController.enabled = false;
            }
        }
        public void Restore(in LoopResetContext context)
        {
            CancelClimb();
            _movementSpeedMultiplier = 1f;
            _limpUntil = 0f;
            _limpMultiplier = 1f;
            _corridorActive = false;
            movementCamera = _initialMovementCamera;
            _useMovementReferenceForAim = _initialUseMovementReferenceForAim;
            transform.SetPositionAndRotation(_initialPosition, _initialRotation);
        }

        public void Resume(in LoopResetContext context)
        {
            if (characterController != null)
            {
                characterController.enabled = true;
            }

            _inputLocked = false;
        }

        private IEnumerator ClimbTo(Vector3 targetPosition, Quaternion targetRotation, float duration, Action completed)
        {
            Vector3 startPosition = transform.position;
            Quaternion startRotation = transform.rotation;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                transform.SetPositionAndRotation(
                    Vector3.Lerp(startPosition, targetPosition, t),
                    Quaternion.Slerp(startRotation, targetRotation, t));
                yield return null;
            }

            transform.SetPositionAndRotation(targetPosition, targetRotation);
            _climbRoutine = null;
            _climbCompleted = null;
            if (characterController != null)
            {
                characterController.enabled = _controllerEnabledBeforeClimb;
            }

            completed?.Invoke();
        }
    }
}
