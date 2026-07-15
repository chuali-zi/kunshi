using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ConGame.Runtime.Input
{

    [DisallowMultipleComponent]
    public sealed class PlayerInputRouter : MonoBehaviour
    {
        private InputActionMap _gameplay;
        private InputAction _move;
        private InputAction _sprint;
        private InputAction _interact;
        private InputAction _attack;
        private InputAction _throw;
        private InputAction _stance;
        private InputAction _pause;

        public event Action MeaningfulAction;
        public event Action InteractPressed;
        public event Action AttackPressed;
        public event Action ThrowPressed;
        public event Action StancePressed;
        public event Action PausePressed;

        public Vector2 Move => _move == null ? Vector2.zero : _move.ReadValue<Vector2>();

        public bool SprintHeld => _sprint != null && _sprint.IsPressed();

        public bool GameplayEnabled => _gameplay != null && _gameplay.enabled;

        private void Awake()
        {
            BuildActions();
        }

        private void OnEnable()
        {
            _gameplay?.Enable();
        }

        private void OnDisable()
        {
            _gameplay?.Disable();
        }

        private void OnDestroy()
        {
            _gameplay?.Dispose();
        }

        public void SetGameplayEnabled(bool enabled)
        {
            if (_gameplay == null)
            {
                BuildActions();
            }

            if (enabled)
            {
                _gameplay.Enable();
            }
            else
            {
                _gameplay.Disable();
            }
        }

        private void BuildActions()
        {
            if (_gameplay != null)
            {
                return;
            }

            _gameplay = new InputActionMap("GameplayThirdPerson");

            _move = _gameplay.AddAction("Move", InputActionType.Value);
            _move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            _move.AddBinding("<Gamepad>/leftStick");

            _sprint = AddButton("Sprint", "<Keyboard>/leftShift", "<Gamepad>/leftStickPress");
            _interact = AddButton("Interact", "<Keyboard>/e", "<Gamepad>/buttonSouth");
            _attack = AddButton("Attack", "<Mouse>/leftButton", "<Gamepad>/rightTrigger");
            _throw = AddButton("Throw", "<Mouse>/rightButton", "<Gamepad>/leftTrigger");
            _stance = AddButton("Stance", "<Keyboard>/c", "<Gamepad>/buttonEast");
            _pause = AddButton("Pause", "<Keyboard>/escape", "<Gamepad>/start");

            _move.performed += _ => MeaningfulAction?.Invoke();
            _sprint.performed += _ => MeaningfulAction?.Invoke();
            _interact.performed += _ =>
            {
                MeaningfulAction?.Invoke();
                InteractPressed?.Invoke();
            };
            _attack.performed += _ =>
            {
                MeaningfulAction?.Invoke();
                AttackPressed?.Invoke();
            };
            _throw.performed += _ =>
            {
                MeaningfulAction?.Invoke();
                ThrowPressed?.Invoke();
            };
            _stance.performed += _ =>
            {
                MeaningfulAction?.Invoke();
                StancePressed?.Invoke();
            };
            _pause.performed += _ => PausePressed?.Invoke();
        }

        private InputAction AddButton(string name, string keyboardPath, string gamepadPath)
        {
            InputAction action = _gameplay.AddAction(name, InputActionType.Button);
            action.AddBinding(keyboardPath);
            action.AddBinding(gamepadPath);
            return action;
        }
    }
}
