using System;
using ConGame.Domain.Facts;
using ConGame.Domain.Interaction;
using ConGame.Runtime.Config;
using ConGame.Runtime.Events;
using ConGame.Runtime.Facts;
using ConGame.Runtime.Input;
using ConGame.Runtime.Interaction;
using ConGame.Runtime.Loop;
using UnityEngine;

namespace ConGame.Runtime.Player
{
    public enum PlayerControlMode
    {
        Free = 0,
        BreachBlocked = 1,
        Counterplay = 2,
        Blocked = 3
    }

    [Flags]
    public enum CounterplayInputWindow
    {
        None = 0,
        Qte = 1 << 0,
        Grab = 1 << 1,
        Attack = 1 << 2,
        Throw = 1 << 3,
        Use = 1 << 4,
        All = Qte | Grab | Attack | Throw | Use
    }

    [DisallowMultipleComponent]
    public sealed class PlayerInteractor : MonoBehaviour, ILoopResettable
    {
        [SerializeField] private PlayerInputRouter input;
        [SerializeField] private InteractionScanner scanner;
        [SerializeField] private Transform handSocket;
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private GameplayFactService facts;
        [SerializeField] private GameplayEventHub events;
        [SerializeField] private GameConfigAsset config;

        private Rigidbody _heldBody;
        private Collider[] _heldColliders;
        private Collider[] _playerColliders;
        private StatefulWorldItem _heldStateful;
        private float _heldMovementMultiplier = 1f;
        private bool _isHiding;
        private bool _hasHideReturnPose;
        private Vector3 _hideReturnPosition;
        private Quaternion _hideReturnRotation;
        private PlayerZone _hideReturnZone = PlayerZone.Open;
        private PlayerControlMode _controlMode;
        private CounterplayInputWindow _counterplayWindows = CounterplayInputWindow.All;
        private const float CounterplayAimSensitivity = 0.12f;

        public int ResetOrder => 80;
        public InteractableEntity HeldEntity { get; private set; }
        public float HeldMovementMultiplier => _heldMovementMultiplier;
        public bool IsHiding => _isHiding;
        public PlayerControlMode ControlMode => _controlMode;
        public CounterplayInputWindow CounterplayWindows => _counterplayWindows;
        public bool IsMovementLocked => _isHiding || _controlMode != PlayerControlMode.Free;
        public Vector3 AimForward => motor == null ? transform.forward : motor.AimForward;

        public void Configure(PlayerInputRouter inputRouter, InteractionScanner interactionScanner, Transform socket,
                              PlayerMotor playerMotor, GameplayFactService factService, GameplayEventHub eventHub,
                              GameConfigAsset gameConfig)
        {
            input = inputRouter;
            scanner = interactionScanner;
            handSocket = socket;
            motor = playerMotor;
            facts = factService;
            events = eventHub;
            config = gameConfig;
            _playerColliders = GetComponentsInChildren<Collider>(true);
        }

        private void Awake()
        {
            _playerColliders = GetComponentsInChildren<Collider>(true);
        }

        private void OnEnable()
        {
            if (input == null)
            {
                return;
            }

            input.InteractPressed += OnInteract;
            input.AttackPressed += OnAttack;
            input.ThrowPressed += OnThrow;
            input.StancePressed += OnStance;
        }

        private void OnDisable()
        {
            if (input == null)
            {
                return;
            }

            input.InteractPressed -= OnInteract;
            input.AttackPressed -= OnAttack;
            input.ThrowPressed -= OnThrow;
            input.StancePressed -= OnStance;
        }

        private void Update()
        {
            if (input == null || facts == null || _isHiding)
            {
                return;
            }

            if (_controlMode == PlayerControlMode.Counterplay)
            {
                if (HeldEntity != null && HeldEntity.ItemId == "flashlight" &&
                    (_counterplayWindows & CounterplayInputWindow.Use) != 0)
                {
                    float yaw = input.Look.x * CounterplayAimSensitivity;
                    transform.Rotate(0f, yaw, 0f, Space.World);
                }

                return;
            }

            if (_controlMode != PlayerControlMode.Free)
            {
                return;
            }

            if (input.Move.sqrMagnitude <= 0.01f)
            {
                return;
            }

            PlayerPose pose = facts.CaptureSnapshot().PlayerPose;
            if (pose == PlayerPose.Kneeling || pose == PlayerPose.Prone)
            {
                SetPose(PlayerPose.Standing);
            }
        }

        public bool TryHold(InteractableEntity entity, Rigidbody body)
        {
            return TryAcquire(entity, body, 1f, false);
        }

        public bool TryCarry(InteractableEntity entity, Rigidbody body, float movementSpeedMultiplier)
        {
            return TryAcquire(entity, body, movementSpeedMultiplier, true);
        }

        public bool TryPlaceHeld(PlacementSocket socket)
        {
            if (HeldEntity == null || _heldStateful == null || socket == null ||
                !socket.TryPlace(HeldEntity, _heldStateful))
            {
                return false;
            }

            ClearHeldReferences();
            return true;
        }

        public void EnterHide(Transform anchor, PlayerZone zone, PlayerPose pose = PlayerPose.Hidden)
        {
            if (anchor == null || HeldEntity != null)
            {
                return;
            }

            if (!_isHiding)
            {
                _hideReturnPosition = transform.position;
                _hideReturnRotation = transform.rotation;
                _hideReturnZone = facts == null ? PlayerZone.Open : facts.CaptureSnapshot().PlayerZone;
                _hasHideReturnPose = true;
            }

            motor?.Teleport(anchor.position, anchor.rotation);
            _isHiding = true;
            ApplyMovementLock();
            facts?.SetPlayerZone(zone);
            facts?.SetPlayerPose(pose);
        }

        public void ExitHide()
        {
            if (!_isHiding)
            {
                return;
            }

            if (_hasHideReturnPose)
            {
                motor?.Teleport(_hideReturnPosition, _hideReturnRotation);
            }

            _isHiding = false;
            _hasHideReturnPose = false;
            ApplyMovementLock();
            facts?.SetPlayerZone(_hideReturnZone);
            facts?.SetPlayerPose(PlayerPose.Standing);
        }

        public void SetPose(PlayerPose pose)
        {
            if (_isHiding && pose != PlayerPose.Hidden)
            {
                _isHiding = false;
                _hasHideReturnPose = false;
                facts?.SetPlayerZone(_hideReturnZone);
            }

            _isHiding = pose == PlayerPose.Hidden;
            ApplyMovementLock();
            facts?.SetPlayerPose(pose);
        }

        public void SetZone(PlayerZone zone)
        {
            facts?.SetPlayerZone(zone);
        }

        public void ForceDropHeld()
        {
            DetachHeld(false);
        }

        public void ThrowHeld()
        {
            if (HeldEntity == null || !AllowsCounterplayWindow(CounterplayInputWindow.Throw))
            {
                return;
            }

            // Carryable furniture such as the dresser is deliberately not
            // throwable. The input path is global, so enforce the authored
            // category here instead of letting every held rigidbody fly.
            if (HeldEntity.HasAuthoredDefinition &&
                (HeldEntity.Categories & ItemCategory.Throwable) == 0)
            {
                return;
            }

            DetachHeld(true);
        }

        public void SetControlMode(
            PlayerControlMode mode,
            CounterplayInputWindow counterplayWindows = CounterplayInputWindow.All)
        {
            _controlMode = mode;
            _counterplayWindows = mode == PlayerControlMode.Counterplay
                                      ? counterplayWindows
                                      : CounterplayInputWindow.None;
            ApplyMovementLock();
        }

        public void SetCounterplayWindows(CounterplayInputWindow windows)
        {
            _counterplayWindows = windows;
        }

        public bool TryInteract()
        {
            if (_controlMode == PlayerControlMode.BreachBlocked || _controlMode == PlayerControlMode.Blocked)
            {
                return false;
            }

            if (HeldEntity != null && AllowsCounterplayWindow(CounterplayInputWindow.Use) &&
                HeldEntity.TryExecute(this, InteractionVerb.Use))
            {
                return true;
            }

            InteractableEntity target = scanner == null ? null : scanner.Refresh();
            if (_controlMode == PlayerControlMode.Counterplay)
            {
                if (target == null)
                {
                    return false;
                }

                if ((_counterplayWindows & CounterplayInputWindow.Grab) != 0 &&
                    (target.TryExecute(this, InteractionVerb.PickUp) ||
                     target.TryExecute(this, InteractionVerb.Carry)))
                {
                    return true;
                }

                return (_counterplayWindows & CounterplayInputWindow.Use) != 0 &&
                       target.TryExecute(this, InteractionVerb.Use);
            }

            if (target != null)
            {
                return target.TryExecute(this);
            }

            if (HeldEntity != null || _isHiding || facts == null)
            {
                return false;
            }

            // Empty-handed interact with no target = surrender pose (D06).
            SetPose(PlayerPose.Kneeling);
            return true;
        }

        public void CaptureInitialState()
        {
            _isHiding = false;
            _hasHideReturnPose = false;
            _hideReturnZone = PlayerZone.Open;
            _heldMovementMultiplier = 1f;
        }

        public void Quiesce(in LoopResetContext context)
        {
            DetachHeld(false);
            _isHiding = false;
            _hasHideReturnPose = false;
            ApplyMovementLock();
        }

        public void Restore(in LoopResetContext context)
        {
            HeldEntity = null;
            _heldBody = null;
            _heldColliders = null;
            _heldStateful = null;
            _heldMovementMultiplier = 1f;
            _isHiding = false;
            _hasHideReturnPose = false;
            _hideReturnZone = PlayerZone.Open;
            _controlMode = PlayerControlMode.Free;
            _counterplayWindows = CounterplayInputWindow.None;
            motor?.SetMovementSpeedMultiplier(1f);
            ApplyMovementLock();
            facts?.SetHeldCategories(ItemCategory.None);
        }

        public void Resume(in LoopResetContext context)
        {
        }

        private bool TryAcquire(InteractableEntity entity, Rigidbody body, float movementSpeedMultiplier, bool carrying)
        {
            if (HeldEntity != null || entity == null || body == null || handSocket == null)
            {
                return false;
            }

            _playerColliders ??= GetComponentsInChildren<Collider>(true);
            StatefulWorldItem stateful = entity.GetComponent<StatefulWorldItem>();
            if (stateful != null && !stateful.BeginHeld(handSocket, _playerColliders))
            {
                return false;
            }

            HeldEntity = entity;
            _heldBody = body;
            _heldStateful = stateful;
            _heldColliders = entity.GetComponentsInChildren<Collider>(true);

            if (stateful == null)
            {
                for (int i = 0; i < _heldColliders.Length; i++)
                {
                    if (_heldColliders[i] != null)
                    {
                        _heldColliders[i].enabled = false;
                    }
                }

                if (!body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
                body.isKinematic = true;
                body.useGravity = false;
                entity.transform.SetParent(handSocket, false);
                entity.transform.localPosition = Vector3.zero;
                entity.transform.localRotation = Quaternion.identity;
            }

            entity.SetHeld(true);
            _heldMovementMultiplier = Mathf.Clamp(movementSpeedMultiplier, 0.1f, 1f);
            motor?.SetMovementSpeedMultiplier(_heldMovementMultiplier);
            facts?.SetHeldCategories(entity.Categories);
            if (carrying)
            {
                facts?.SetPlayerPose(PlayerPose.Carrying);
            }

            if (entity.ItemId == "pistol_main")
            {
                facts?.SetStolenPistol(true);
            }

            return true;
        }

        private void OnInteract()
        {
            TryInteract();
        }

        private void OnAttack()
        {
            if (HeldEntity == null || !AllowsCounterplayWindow(CounterplayInputWindow.Attack))
            {
                return;
            }

            MeleeWeaponCapability weapon = HeldEntity.GetComponent<MeleeWeaponCapability>();
            weapon?.Swing(this, facts, events);
        }

        private void OnThrow()
        {
            ThrowHeld();
        }

        private void OnStance()
        {
            if (_controlMode != PlayerControlMode.Free)
            {
                return;
            }

            if (_isHiding)
            {
                ExitHide();
                return;
            }

            InteractableEntity target = scanner == null ? null : scanner.RefreshForVerb(InteractionVerb.Hide);
            if (target != null && target.TryExecute(this, InteractionVerb.Hide))
            {
                return;
            }

            if (facts == null)
            {
                return;
            }

            FactSnapshot snapshot = facts.CaptureSnapshot();
            switch (snapshot.PlayerPose)
            {
                case PlayerPose.Standing:
                    SetPose(PlayerPose.Crouched);
                    break;
                case PlayerPose.Crouched:
                    SetPose(PlayerPose.Prone);
                    break;
                case PlayerPose.Kneeling:
                case PlayerPose.Prone:
                    SetPose(PlayerPose.Standing);
                    break;
                default:
                    SetPose(PlayerPose.Crouched);
                    break;
            }
        }

        private void DetachHeld(bool applyThrow)
        {
            if (HeldEntity == null)
            {
                return;
            }

            InteractableEntity entity = HeldEntity;
            Rigidbody body = _heldBody;
            StatefulWorldItem stateful = _heldStateful;
            Vector3 aimForward = motor == null ? transform.forward : motor.AimForward;
            Vector3 velocity = applyThrow ? aimForward * (config == null ? 8f : config.ThrowSpeed) : Vector3.zero;
            Vector3 angularVelocity = applyThrow ? UnityEngine.Random.onUnitSphere * 3f : Vector3.zero;

            if (stateful != null)
            {
                stateful.Release(velocity, angularVelocity, applyThrow ? events : null);
            }
            else
            {
                entity.transform.SetParent(null, true);
                if (_heldColliders != null)
                {
                    for (int i = 0; i < _heldColliders.Length; i++)
                    {
                        if (_heldColliders[i] != null)
                        {
                            _heldColliders[i].enabled = true;
                        }
                    }
                }

                if (body != null)
                {
                    body.isKinematic = false;
                    body.useGravity = true;
                    body.linearVelocity = velocity;
                    body.angularVelocity = angularVelocity;
                }
            }

            ClearHeldReferences();
        }

        private void ClearHeldReferences()
        {
            InteractableEntity previous = HeldEntity;
            HeldEntity = null;
            _heldBody = null;
            _heldColliders = null;
            _heldStateful = null;
            _heldMovementMultiplier = 1f;
            previous?.SetHeld(false);
            motor?.SetMovementSpeedMultiplier(1f);
            facts?.SetHeldCategories(ItemCategory.None);
            if (facts != null && facts.CaptureSnapshot().PlayerPose == PlayerPose.Carrying)
            {
                facts.SetPlayerPose(PlayerPose.Standing);
            }
        }

        private bool AllowsCounterplayWindow(CounterplayInputWindow window)
        {
            if (_controlMode == PlayerControlMode.Free)
            {
                return true;
            }

            return _controlMode == PlayerControlMode.Counterplay && (_counterplayWindows & window) != 0;
        }

        private void ApplyMovementLock()
        {
            motor?.SetInputLocked(IsMovementLocked);
        }
    }
}
