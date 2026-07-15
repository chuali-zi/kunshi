using ConGame.Domain.Facts;
using ConGame.Runtime.Config;
using ConGame.Runtime.Facts;
using ConGame.Runtime.Input;
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
        private bool _inputLocked;

        public int ResetOrder => 300;

        public PlayerInputRouter Input => input;

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
            if (characterController == null || input == null || config == null || _inputLocked)
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

            float speed = input.SprintHeld ? config.SprintSpeed : config.WalkSpeed;
            if (characterController.isGrounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = -2f;
            }
            else
            {
                _verticalVelocity += Physics.gravity.y * Time.deltaTime;
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

        public void Teleport(Vector3 position, Quaternion rotation)
        {
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
            _initialPosition = transform.position;
            _initialRotation = transform.rotation;
        }

        public void Quiesce(in LoopResetContext context)
        {
            _inputLocked = true;
            _verticalVelocity = 0f;
            if (characterController != null)
            {
                characterController.enabled = false;
            }
        }

        public void Restore(in LoopResetContext context)
        {
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
    }
}
