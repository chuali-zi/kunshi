using ConGame.Runtime.Config;
using ConGame.Runtime.Events;
using ConGame.Runtime.Facts;
using ConGame.Runtime.Input;
using ConGame.Runtime.Loop;
using UnityEngine;

namespace ConGame.Runtime.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerNoiseEmitter : MonoBehaviour, ILoopResettable
    {
        [SerializeField] private CharacterController characterController;
        [SerializeField] private PlayerInputRouter input;
        [SerializeField] private GameplayFactService facts;
        [SerializeField] private GameplayEventHub events;
        [SerializeField] private GameConfigAsset config;
        [SerializeField, Min(0.05f)] private float minimumMovingSpeed = 0.18f;

        private float _remaining;

        public int ResetOrder => 82;

        public void Configure(
            CharacterController controller,
            PlayerInputRouter inputRouter,
            GameplayFactService factService,
            GameplayEventHub eventHub,
            GameConfigAsset gameConfig)
        {
            characterController = controller;
            input = inputRouter;
            facts = factService;
            events = eventHub;
            config = gameConfig;
        }

        private void Awake()
        {
            characterController ??= GetComponent<CharacterController>();
            input ??= GetComponent<PlayerInputRouter>();
        }

        private void Update()
        {
            if (characterController == null || events == null ||
                (input != null && !input.GameplayEnabled))
            {
                return;
            }

            Vector3 velocity = characterController.velocity;
            velocity.y = 0f;
            bool moving = velocity.magnitude >= minimumMovingSpeed;
            facts?.SetMoving(moving);
            if (!moving)
            {
                _remaining = 0f;
                return;
            }

            _remaining -= Time.deltaTime;
            if (_remaining > 0f)
            {
                return;
            }

            bool sprinting = input != null && input.SprintHeld;
            events.EmitNoise(
                sprinting ? NoiseLevel.Loud : NoiseLevel.Normal,
                transform.position,
                sprinting ? "player_sprint" : "player_footstep");
            float interval = config == null ? 0.42f : config.PlayerFootstepNoiseInterval;
            _remaining = sprinting ? interval * 0.72f : interval;
        }

        public void CaptureInitialState()
        {
            _remaining = 0f;
        }

        public void Quiesce(in LoopResetContext context)
        {
            _remaining = 0f;
            facts?.SetMoving(false);
        }

        public void Restore(in LoopResetContext context)
        {
            _remaining = 0f;
            facts?.SetMoving(false);
        }

        public void Resume(in LoopResetContext context)
        {
        }
    }
}