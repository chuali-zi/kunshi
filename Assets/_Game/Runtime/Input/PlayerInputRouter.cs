using System;
using ConGame.Runtime.Settings;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ConGame.Runtime.Input
{
    public enum GameplayInputMode
    {
        Disabled = 0,
        Full = 1,
        LookOnly = 2
    }

    [DisallowMultipleComponent]
    public sealed class PlayerInputRouter : MonoBehaviour
    {
        private InputActionMap _gameplay;
        private InputActionMap _system;
        private InputAction _move;
        private InputAction _look;
        private InputAction _sprint;
        private InputAction _interact;
        private InputAction _attack;
        private InputAction _throw;
        private InputAction _stance;
        private InputAction _pause;
        private InputAction _navigate;
        private InputAction _submit;
        private InputAction _cancel;
        private GameSettingsV1 _settings = new GameSettingsV1();
        private GameplayInputMode _inputMode = GameplayInputMode.Full;

        public event Action MeaningfulAction;
        public event Action InteractPressed;
        public event Action AttackPressed;
        public event Action ThrowPressed;
        public event Action StancePressed;
        public event Action PausePressed;
        public event Action<Vector2> MenuNavigate;
        public event Action MenuSubmitPressed;
        public event Action MenuCancelPressed;

        public Vector2 Move => _move == null ? Vector2.zero : _move.ReadValue<Vector2>();
        public Vector2 Look => _look == null ? Vector2.zero : _settings.ApplyLookSettings(_look.ReadValue<Vector2>());
        public bool SprintHeld => _sprint != null && _sprint.IsPressed();
        public bool GameplayEnabled => _gameplay != null && _gameplay.enabled;
        public bool SystemInputEnabled => _system != null && _system.enabled;
        public GameplayInputMode InputMode => _inputMode;

        private void Awake()
        {
            BuildActions();
        }

        private void OnEnable()
        {
            ApplyInputMode();
            _system?.Enable();
        }

        private void OnDisable()
        {
            _gameplay?.Disable();
            _system?.Disable();
        }

        private void OnDestroy()
        {
            _gameplay?.Dispose();
            _system?.Dispose();
        }

        public void ApplySettings(GameSettingsV1 settings)
        {
            _settings = settings ?? new GameSettingsV1();
            _settings.Normalize();
        }

        public void SetGameplayEnabled(bool enabled)
        {
            if (_gameplay == null)
            {
                BuildActions();
            }

            _inputMode = enabled ? GameplayInputMode.Full : GameplayInputMode.Disabled;
            ApplyInputMode();
        }

        public void SetLookOnlyEnabled(bool enabled)
        {
            if (_gameplay == null)
            {
                BuildActions();
            }

            _inputMode = enabled ? GameplayInputMode.LookOnly : GameplayInputMode.Disabled;
            ApplyInputMode();
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
            _move.AddBinding("<Gamepad>/leftStick").WithProcessor("stickDeadzone(min=0.15)");

            _look = _gameplay.AddAction("Look", InputActionType.Value);
            _look.AddBinding("<Mouse>/delta");
            _look.AddBinding("<Gamepad>/rightStick").WithProcessor("stickDeadzone(min=0.15)");

            _sprint = AddGameplayButton("Sprint", "<Keyboard>/leftShift", "<Gamepad>/leftStickPress");
            _interact = AddGameplayButton("Interact", "<Keyboard>/e", "<Gamepad>/buttonSouth");
            _attack = AddGameplayButton("Attack", "<Mouse>/leftButton", "<Gamepad>/rightTrigger");
            _throw = AddGameplayButton("Throw", "<Mouse>/rightButton", "<Gamepad>/leftTrigger");
            _stance = AddGameplayButton("Stance", "<Keyboard>/c", "<Gamepad>/buttonEast");

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

            BuildSystemActions();
        }

        private void BuildSystemActions()
        {
            _system = new InputActionMap("SystemAndMenus");
            _pause = AddSystemButton("Pause", "<Keyboard>/escape", "<Gamepad>/start");
            _submit = AddSystemButton("Submit", "<Keyboard>/enter", "<Gamepad>/buttonSouth");
            _cancel = AddSystemButton("Cancel", "<Keyboard>/backspace", "<Gamepad>/buttonEast");

            _navigate = _system.AddAction("Navigate", InputActionType.Value);
            _navigate.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");
            _navigate.AddBinding("<Gamepad>/dpad");
            _navigate.AddBinding("<Gamepad>/leftStick").WithProcessor("stickDeadzone(min=0.5)");

            _pause.performed += _ => PausePressed?.Invoke();
            _submit.performed += _ => MenuSubmitPressed?.Invoke();
            _cancel.performed += _ => MenuCancelPressed?.Invoke();
            _navigate.performed += context => MenuNavigate?.Invoke(context.ReadValue<Vector2>());
        }

        private InputAction AddGameplayButton(string name, string keyboardPath, string gamepadPath)
        {
            InputAction action = _gameplay.AddAction(name, InputActionType.Button);
            action.AddBinding(keyboardPath);
            action.AddBinding(gamepadPath);
            return action;
        }

        private InputAction AddSystemButton(string name, string keyboardPath, string gamepadPath)
        {
            InputAction action = _system.AddAction(name, InputActionType.Button);
            action.AddBinding(keyboardPath);
            action.AddBinding(gamepadPath);
            return action;
        }

        private void ApplyInputMode()
        {
            if (_gameplay == null)
            {
                return;
            }

            if (_inputMode == GameplayInputMode.Disabled)
            {
                _gameplay.Disable();
                return;
            }

            _gameplay.Enable();
            if (_inputMode == GameplayInputMode.Full)
            {
                _move.Enable();
                _look.Enable();
                _sprint.Enable();
                _interact.Enable();
                _attack.Enable();
                _throw.Enable();
                _stance.Enable();
                return;
            }

            _move.Disable();
            _sprint.Disable();
            _interact.Disable();
            _attack.Disable();
            _throw.Disable();
            _stance.Disable();
            _look.Enable();
        }
    }
}
