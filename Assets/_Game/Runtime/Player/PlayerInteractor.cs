using System;
using System.Collections;
using ConGame.Domain.Facts;
using ConGame.Domain.Interaction;
using ConGame.Runtime.AI;
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
        [SerializeField] private HandoffSignalReceiver handoffSignals;
        [SerializeField] private InteractionHandoffSettings handoffSettings = InteractionHandoffSettings.Default;

        private readonly ItemHandoffTransaction _handoff = new ItemHandoffTransaction();
        private Action _handoffCompletion;
        private Coroutine _handoffFallbackRoutine;
        private int _handoffSession;
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
        private CounterplayInputWindow _counterplayWindows = CounterplayInputWindow.None;
        private InteractableEntity _pendingDualInteraction;
        private float _pendingInteractStartedAt;
        private HoldDurationGate _interactHold;
        private HoldDurationGate _stanceHold;
        private InteractableEntity _pendingHoldTarget;
        private PendingHoldKind _pendingHoldKind;
        private bool _isClimbing;
        private bool _isGroundDragging;
        private Vector3 _groundDragLocalOffset;
        [SerializeField] private Transform killerTarget;
        private readonly RaycastHit[] _groundPlacementHits = new RaycastHit[16];
        private const float CounterplayAimSensitivity = 0.12f;
        private const float CarryHoldSeconds = 0.35f;

        private enum PendingHoldKind
        {
            None = 0,
            DualClimbCarry = 1,
            Carry = 2,
            HoldUse = 3,
            Surrender = 4,
            UseOrPickUp = 5,
            PutDown = 6,
            UseOrPutDown = 7,
            UseOrCarry = 8
        }

        public int ResetOrder => 80;
        public InteractableEntity HeldEntity { get; private set; }
        public bool IsHandoffPending => _handoff.IsPending;
        public float HeldMovementMultiplier => _heldMovementMultiplier;
        public bool IsHiding => _isHiding;
        public PlayerControlMode ControlMode => _controlMode;
        public CounterplayInputWindow CounterplayWindows => _counterplayWindows;
        public bool IsMovementLocked => _isHiding || _isClimbing || _controlMode != PlayerControlMode.Free;
        public Vector3 AimForward => motor == null ? transform.forward : motor.AimForward;

        public void Configure(PlayerInputRouter inputRouter, InteractionScanner interactionScanner, Transform socket,
                              PlayerMotor playerMotor, GameplayFactService factService, GameplayEventHub eventHub,
                              GameConfigAsset gameConfig, HandoffSignalReceiver handoffSignalReceiver = null,
                              Transform killer = null)
        {
            input = inputRouter;
            scanner = interactionScanner;
            handSocket = socket;
            motor = playerMotor;
            facts = factService;
            events = eventHub;
            config = gameConfig;
            handoffSignals = handoffSignalReceiver;
            killerTarget = killer;
            _playerColliders = GetComponentsInChildren<Collider>(true);
        }

        public void ConfigureKiller(Transform killer)
        {
            killerTarget = killer;
        }

        private void Awake()
        {
            _playerColliders = GetComponentsInChildren<Collider>(true);
        }

        private void OnEnable()
        {
            if (handoffSignals != null)
            {
                handoffSignals.AttachSignaled += OnHandoffAttachSignaled;
                handoffSignals.DetachSignaled += OnHandoffDetachSignaled;
            }

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
            CancelPendingDualInteraction();
            CancelHandoff();
            if (handoffSignals != null)
            {
                handoffSignals.AttachSignaled -= OnHandoffAttachSignaled;
                handoffSignals.DetachSignaled -= OnHandoffDetachSignaled;
            }

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
            if (input == null)
            {
                return;
            }

            UpdatePendingDualInteraction();
            UpdatePendingInteractHold();
            UpdatePendingStanceHold();
            UpdateGroundDrag();
            if (_isHiding)
            {
                if (input.Move.sqrMagnitude > 0.01f)
                {
                    ExitHide();
                }

                return;
            }

            if (facts == null || _isClimbing)
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
            return BeginHandoff(HandoffKind.PickUp, () => TryAcquire(entity, body, 1f, false));
        }

        public bool TryCarry(InteractableEntity entity, Rigidbody body, float movementSpeedMultiplier)
        {
            return BeginHandoff(HandoffKind.Carry,
                () => TryAcquire(entity, body, movementSpeedMultiplier, true));
        }

        public bool TryDrag(InteractableEntity entity, Rigidbody body, float movementSpeedMultiplier)
        {
            return BeginHandoff(HandoffKind.Carry,
                () => TryAcquire(entity, body, movementSpeedMultiplier, true));
        }

        public bool TryPlaceHeld(PlacementSocket socket)
        {
            if (HeldEntity == null || _heldStateful == null || socket == null ||
                !socket.CanAccept(HeldEntity))
            {
                return false;
            }

            return BeginHandoff(HandoffKind.PutDown, () =>
            {
                if (HeldEntity == null || _heldStateful == null ||
                    !socket.TryPlace(HeldEntity, _heldStateful))
                {
                    return;
                }

                ClearHeldReferences();
            });
        }

        public bool TryClimb(Transform standAnchor, float duration)
        {
            if (standAnchor == null || motor == null || HeldEntity != null || _isHiding || _isClimbing ||
                _controlMode != PlayerControlMode.Free)
            {
                return false;
            }

            _isClimbing = true;
            facts?.SetPlayerPose(PlayerPose.Scripted);
            ApplyMovementLock();
            if (motor.TryClimb(standAnchor, duration, OnClimbFinished))
            {
                return true;
            }

            _isClimbing = false;
            facts?.SetPlayerPose(PlayerPose.Standing);
            ApplyMovementLock();
            return false;
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
            // Death/performance paths must take ownership back atomically, so
            // any pending handoff is cancelled and the drop happens now.
            CancelHandoff();
            DetachHeld(false);
        }

        public void ThrowHeld()
        {
            if (HeldEntity == null || _handoff.IsPending ||
                !AllowsCounterplayWindow(CounterplayInputWindow.Throw))
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

            BeginHandoff(HandoffKind.Throw, () => DetachHeld(true));
        }

        public void SetControlMode(
            PlayerControlMode mode,
            CounterplayInputWindow counterplayWindows = CounterplayInputWindow.All)
        {
            CounterplayInputWindow normalizedWindows = mode == PlayerControlMode.Counterplay
                ? counterplayWindows & CounterplayInputWindow.All
                : CounterplayInputWindow.None;
            if (_controlMode == mode && _counterplayWindows == normalizedWindows)
            {
                return;
            }

            CancelPendingDualInteraction();
            CancelHandoff();
            _controlMode = mode;
            _counterplayWindows = normalizedWindows;
            ApplyMovementLock();
        }

        public void SetCounterplayWindows(CounterplayInputWindow windows)
        {
            _counterplayWindows = _controlMode == PlayerControlMode.Counterplay
                ? windows & CounterplayInputWindow.All
                : CounterplayInputWindow.None;
        }

        public bool TryInteract()
        {
            if (_isClimbing || _handoff.IsPending || _controlMode == PlayerControlMode.BreachBlocked ||
                _controlMode == PlayerControlMode.Blocked)
            {
                return false;
            }

            if (_controlMode == PlayerControlMode.Counterplay)
            {
                if (HeldEntity != null && AllowsCounterplayWindow(CounterplayInputWindow.Use) &&
                    HeldEntity.TryExecute(this, InteractionVerb.Use))
                {
                    return true;
                }

                if (AllowsCounterplayWindow(CounterplayInputWindow.Grab))
                {
                    InteractableEntity grabTarget = scanner == null
                        ? null
                        : scanner.RefreshForVerb(InteractionVerb.PickUp);
                    if (grabTarget != null && grabTarget.TryExecute(this, InteractionVerb.PickUp))
                    {
                        return true;
                    }

                    grabTarget = scanner == null ? null : scanner.RefreshForVerb(InteractionVerb.Carry);
                    if (grabTarget != null && grabTarget.TryExecute(this, InteractionVerb.Carry))
                    {
                        return true;
                    }
                }

                if (AllowsCounterplayWindow(CounterplayInputWindow.Use))
                {
                    InteractableEntity useTarget = scanner == null
                        ? null
                        : scanner.RefreshForVerb(InteractionVerb.Use);
                    return useTarget != null && useTarget.TryExecute(this, InteractionVerb.Use);
                }

                return false;
            }

            if (HeldEntity != null)
            {
                InteractableEntity placement = scanner == null
                    ? null
                    : scanner.RefreshForVerb(InteractionVerb.Place);
                if (placement != null && placement.TryExecute(this, InteractionVerb.Place))
                {
                    return true;
                }

                return TryPlaceHeldOnGround();
            }

            InteractableEntity target = scanner == null ? null : scanner.Refresh();
            if (target != null)
            {
                return target.TryExecute(this);
            }

            return false;
        }

        public void CaptureInitialState()
        {
            CancelPendingDualInteraction();
            _stanceHold.Cancel();
            CancelHandoff();
            _isClimbing = false;
            _isGroundDragging = false;
            _isHiding = false;
            _hasHideReturnPose = false;
            _hideReturnZone = PlayerZone.Open;
            _heldMovementMultiplier = 1f;
        }

        public void Quiesce(in LoopResetContext context)
        {
            CancelPendingDualInteraction();
            _stanceHold.Cancel();
            CancelHandoff();
            motor?.CancelClimb();
            _isClimbing = false;
            _isGroundDragging = false;
            DetachHeld(false);
            _isHiding = false;
            _hasHideReturnPose = false;
            ApplyMovementLock();
        }

        public void Restore(in LoopResetContext context)
        {
            CancelHandoff();
            HeldEntity = null;
            _heldBody = null;
            _heldColliders = null;
            _heldStateful = null;
            _isGroundDragging = false;
            _heldMovementMultiplier = 1f;
            CancelPendingDualInteraction();
            _stanceHold.Cancel();
            _isClimbing = false;
            _isGroundDragging = false;
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
                CarryCapability carry = entity.GetComponent<CarryCapability>();
                if (carry != null && carry.IsGroundDrag)
                {
                    BeginGroundDrag();
                }
            }

            if (entity.ItemId == "pistol_main")
            {
                facts?.SetStolenPistol(true);
            }

            return true;
        }

        private void BeginGroundDrag()
        {
            if (HeldEntity == null)
            {
                return;
            }

            _isGroundDragging = true;
            _groundDragLocalOffset = new Vector3(0f, 0.06f, 0.7f);
            HeldEntity.transform.SetParent(null, true);
            UpdateGroundDrag();
        }

        private void UpdateGroundDrag()
        {
            if (!_isGroundDragging || HeldEntity == null || motor == null)
            {
                return;
            }

            Vector3 forward = motor.AimForward;
            forward.y = 0f;
            forward = forward.sqrMagnitude < 0.001f ? transform.forward : forward.normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 position = transform.position + right * _groundDragLocalOffset.x +
                               forward * _groundDragLocalOffset.z;
            Vector3 probe = position + Vector3.up * 2f;
            if (Physics.Raycast(probe, Vector3.down, out RaycastHit ground, 5f, ~0,
                                QueryTriggerInteraction.Ignore) &&
                !ground.collider.transform.IsChildOf(transform) &&
                !ground.collider.transform.IsChildOf(HeldEntity.transform))
            {
                position.y = ground.point.y + _groundDragLocalOffset.y;
            }
            else
            {
                position.y = transform.position.y + _groundDragLocalOffset.y;
            }
            HeldEntity.transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward, Vector3.up));
        }

        private void OnInteract()
        {
            if (_controlMode == PlayerControlMode.Counterplay)
            {
                TryInteract();
                return;
            }

            if (HeldEntity != null && TryHandleHeldInteractPress())
            {
                return;
            }

            if (_controlMode == PlayerControlMode.Free && HeldEntity == null && TryBeginInteractHold())
            {
                return;
            }

            TryInteract();
        }

        private void OnAttack()
        {
            if (HeldEntity == null || _handoff.IsPending ||
                !AllowsCounterplayWindow(CounterplayInputWindow.Attack))
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
            if (_controlMode != PlayerControlMode.Free || _isClimbing)
            {
                return;
            }

            if (_isHiding)
            {
                ExitHide();
                return;
            }

            // Hide is a tap; deciding it on key-down consumes the same input
            // that must remain available for surrender or playing dead.
            _stanceHold.Begin(Time.unscaledTime, SurrenderGate.HoldSeconds);
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
            _isGroundDragging = false;
            previous?.SetHeld(false);
            motor?.SetMovementSpeedMultiplier(1f);
            facts?.SetHeldCategories(ItemCategory.None);
            if (facts != null && facts.CaptureSnapshot().PlayerPose == PlayerPose.Carrying)
            {
                facts.SetPlayerPose(PlayerPose.Standing);
            }
        }

        private bool TryDeferDualInteraction()
        {
            return TryBeginInteractHold();
        }

        private bool TryBeginInteractHold()
        {
            InteractableEntity target = scanner == null ? null : scanner.Refresh();
            if (target == null)
            {
                if (facts == null || HeldEntity != null || _isHiding)
                {
                    return false;
                }

                FactSnapshot snapshot = facts.CaptureSnapshot();
                if (snapshot.PlayerPose != PlayerPose.Standing)
                {
                    return false;
                }

                _pendingHoldKind = PendingHoldKind.Surrender;
                _pendingHoldTarget = null;
                _interactHold.Begin(Time.unscaledTime, SurrenderGate.HoldSeconds);
                return true;
            }

            InteractionContext context = new InteractionContext(this, target);
            ClimbCapability climb = target.GetCapability<ClimbCapability>();
            CarryCapability carry = target.GetCapability<CarryCapability>();
            UseCapability use = target.GetCapability<UseCapability>();
            IHoldUseHandler holdUse = use == null ? null : ResolveHoldUse(target);

            if (climb != null && carry != null && climb.isActiveAndEnabled && carry.isActiveAndEnabled &&
                climb.CanExecute(context) && carry.CanExecute(context))
            {
                _pendingHoldKind = PendingHoldKind.DualClimbCarry;
                _pendingHoldTarget = target;
                _pendingDualInteraction = target;
                _pendingInteractStartedAt = Time.unscaledTime;
                _interactHold.Begin(Time.unscaledTime, CarryHoldSeconds);
                return true;
            }

            if (carry != null && carry.IsGroundDrag && carry.isActiveAndEnabled && carry.CanExecute(context) &&
                use != null && use.isActiveAndEnabled && use.CanExecute(context))
            {
                _pendingHoldKind = PendingHoldKind.UseOrCarry;
                _pendingHoldTarget = target;
                _interactHold.Begin(Time.unscaledTime, CarryHoldSeconds);
                return true;
            }

            PickupCapability pickup = target.GetCapability<PickupCapability>();
            if (use != null && pickup != null && use.CanExecute(context) && pickup.CanExecute(context))
            {
                _pendingHoldKind = PendingHoldKind.UseOrPickUp;
                _pendingHoldTarget = target;
                _interactHold.Begin(Time.unscaledTime, CarryHoldSeconds);
                return true;
            }

            if (carry != null && carry.isActiveAndEnabled && carry.CanExecute(context))
            {
                _pendingHoldKind = PendingHoldKind.Carry;
                _pendingHoldTarget = target;
                _interactHold.Begin(Time.unscaledTime, CarryHoldSeconds);
                return true;
            }

            if (holdUse != null && holdUse.CanUse(context))
            {
                _pendingHoldKind = PendingHoldKind.HoldUse;
                _pendingHoldTarget = target;
                _interactHold.Begin(Time.unscaledTime, holdUse.HoldSeconds);
                return true;
            }

            return false;
        }

        private bool TryHandleHeldInteractPress()
        {
            if (_controlMode != PlayerControlMode.Free || _handoff.IsPending)
            {
                return false;
            }

            InteractableEntity placement = scanner == null
                ? null
                : scanner.RefreshForVerb(InteractionVerb.Place);
            if (placement != null && placement.TryExecute(this, InteractionVerb.Place))
            {
                return true;
            }

            if (HeldEntity != null && HeldEntity.GetExecutablePriority(this, InteractionVerb.Use) != int.MinValue)
            {
                _pendingHoldKind = PendingHoldKind.UseOrPutDown;
                _pendingHoldTarget = HeldEntity;
                _interactHold.Begin(Time.unscaledTime, CarryHoldSeconds);
                return true;
            }

            _pendingHoldKind = PendingHoldKind.PutDown;
            _pendingHoldTarget = null;
            _interactHold.Begin(Time.unscaledTime, CarryHoldSeconds);
            return true;
        }

        private static IHoldUseHandler ResolveHoldUse(InteractableEntity target)
        {
            MonoBehaviour[] behaviours = target.GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IHoldUseHandler holdUse)
                {
                    return holdUse;
                }
            }

            return null;
        }

        private void UpdatePendingDualInteraction()
        {
            if (_pendingDualInteraction == null)
            {
                return;
            }

            if (_controlMode != PlayerControlMode.Free || HeldEntity != null || _isClimbing ||
                !input.GameplayEnabled)
            {
                CancelPendingDualInteraction();
                return;
            }

            InteractableEntity target = _pendingDualInteraction;
            if (Time.unscaledTime - _pendingInteractStartedAt >= CarryHoldSeconds)
            {
                CancelPendingDualInteraction();
                target.TryExecute(this, InteractionVerb.Carry);
                return;
            }

            if (!input.InteractHeld)
            {
                CancelPendingDualInteraction();
                target.TryExecute(this, InteractionVerb.Use);
            }
        }

        private void CancelPendingDualInteraction()
        {
            _pendingDualInteraction = null;
            _pendingInteractStartedAt = 0f;
            _pendingHoldKind = PendingHoldKind.None;
            _pendingHoldTarget = null;
            _interactHold.Cancel();
        }

        private void UpdatePendingInteractHold()
        {
            if (_pendingHoldKind == PendingHoldKind.None || _pendingHoldKind == PendingHoldKind.DualClimbCarry)
            {
                return;
            }

            if (_controlMode != PlayerControlMode.Free || _isClimbing || input == null || !input.GameplayEnabled)
            {
                CancelPendingDualInteraction();
                return;
            }

            if (_interactHold.IsComplete(Time.unscaledTime))
            {
                PendingHoldKind kind = _pendingHoldKind;
                InteractableEntity target = _pendingHoldTarget;
                CancelPendingDualInteraction();
                CompleteHold(kind, target);
                return;
            }

            if (!input.InteractHeld)
            {
                if (_pendingHoldKind == PendingHoldKind.UseOrPickUp ||
                    _pendingHoldKind == PendingHoldKind.UseOrPutDown ||
                    _pendingHoldKind == PendingHoldKind.UseOrCarry)
                {
                    InteractableEntity target = _pendingHoldTarget;
                    CancelPendingDualInteraction();
                    target?.TryExecute(this, InteractionVerb.Use);
                    return;
                }

                CancelPendingDualInteraction();
            }
        }

        private void UpdatePendingStanceHold()
        {
            if (!_stanceHold.IsPending || facts == null || input == null)
            {
                return;
            }

            if (_controlMode != PlayerControlMode.Free || _isClimbing || !input.GameplayEnabled)
            {
                _stanceHold.Cancel();
                return;
            }

            if (_stanceHold.IsComplete(Time.unscaledTime))
            {
                _stanceHold.Cancel();
                CompleteStanceHold();
                return;
            }

            if (!input.StanceHeld)
            {
                _stanceHold.Cancel();
                CycleStanceTap();
            }
        }

        private void CompleteHold(PendingHoldKind kind, InteractableEntity target)
        {
            switch (kind)
            {
                case PendingHoldKind.Carry:
                    target?.TryExecute(this, InteractionVerb.Carry);
                    break;
                case PendingHoldKind.HoldUse:
                    target?.TryExecute(this, InteractionVerb.Use);
                    break;
                case PendingHoldKind.UseOrPickUp:
                    target?.TryExecute(this, InteractionVerb.PickUp);
                    break;
                case PendingHoldKind.PutDown:
                    TryPlaceHeldOnGround();
                    break;
                case PendingHoldKind.UseOrPutDown:
                    TryPlaceHeldOnGround();
                    break;
                case PendingHoldKind.UseOrCarry:
                    target?.TryExecute(this, InteractionVerb.Carry);
                    break;
                case PendingHoldKind.Surrender:
                    if (CanSurrenderNow())
                    {
                        SetPose(PlayerPose.Kneeling);
                    }

                    break;
            }
        }

        private void CompleteStanceHold()
        {
            FactSnapshot snapshot = facts.CaptureSnapshot();
            if (snapshot.PlayerPose == PlayerPose.Crouched)
            {
                SetPose(PlayerPose.Prone);
                return;
            }

            if (CanSurrenderNow())
            {
                SetPose(PlayerPose.Kneeling);
            }
        }

        private void CycleStanceTap()
        {
            InteractableEntity target = scanner == null ? null : scanner.RefreshForVerb(InteractionVerb.Hide);
            if (target != null && target.TryExecute(this, InteractionVerb.Hide))
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
                case PlayerPose.Kneeling:
                case PlayerPose.Prone:
                    SetPose(PlayerPose.Standing);
                    break;
                default:
                    SetPose(PlayerPose.Crouched);
                    break;
            }
        }

        private bool CanSurrenderNow()
        {
            if (facts == null || HeldEntity != null || _isHiding)
            {
                return false;
            }

            FactSnapshot snapshot = facts.CaptureSnapshot();
            float facing = 0f;
            if (killerTarget != null)
            {
                Vector3 toKiller = killerTarget.position - transform.position;
                toKiller.y = 0f;
                if (toKiller.sqrMagnitude > 0.001f)
                {
                    facing = Vector3.Dot(transform.forward, toKiller.normalized);
                }
            }

            bool entered = false;
            if (killerTarget != null)
            {
                KillerDirector director = killerTarget.GetComponent<KillerDirector>();
                entered = director != null && director.HasEnteredRoom;
            }

            return SurrenderGate.CanSurrender(
                snapshot.PlayerPose == PlayerPose.Standing,
                true,
                entered,
                facing);
        }

        private bool TryPlaceHeldOnGround()
        {
            if (HeldEntity == null)
            {
                return false;
            }

            return BeginHandoff(HandoffKind.PutDown, PlaceHeldOnGroundNow);
        }

        private void PlaceHeldOnGroundNow()
        {
            if (HeldEntity == null)
            {
                return;
            }

            InteractableEntity entity = HeldEntity;
            Vector3 forward = AimForward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f)
            {
                forward = transform.forward;
                forward.y = 0f;
            }
            forward.Normalize();

            float placementDistance = ResolvePlacementDistance(entity);
            Vector3 probeCenter = transform.position + forward * placementDistance;
            Vector3 groundPoint = new Vector3(probeCenter.x, transform.position.y, probeCenter.z);
            int hitCount = Physics.RaycastNonAlloc(
                probeCenter + Vector3.up * 2f,
                Vector3.down,
                _groundPlacementHits,
                5f,
                ~0,
                QueryTriggerInteraction.Ignore);
            float nearestDistance = float.PositiveInfinity;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = _groundPlacementHits[i];
                if (hit.collider == null || hit.collider.transform.IsChildOf(entity.transform) ||
                    hit.collider.transform.IsChildOf(transform) || hit.distance >= nearestDistance)
                {
                    continue;
                }

                nearestDistance = hit.distance;
                groundPoint = hit.point;
            }

            Quaternion rotation = Quaternion.LookRotation(forward, Vector3.up);
            if (_heldStateful != null)
            {
                if (!_heldStateful.PlaceInWorld(groundPoint, rotation))
                {
                    return;
                }
            }
            else
            {
                entity.transform.SetParent(null, true);
                entity.transform.SetPositionAndRotation(groundPoint, rotation);
                Physics.SyncTransforms();
                PlaceNonStatefulBody(entity, groundPoint);
            }

            ClearHeldReferences();
        }

        private float ResolvePlacementDistance(InteractableEntity entity)
        {
            Collider[] colliders = entity.GetComponentsInChildren<Collider>(true);
            float radius = 0f;
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null && colliders[i].enabled)
                {
                    Vector3 extents = colliders[i].bounds.extents;
                    radius = Mathf.Max(radius, Mathf.Sqrt(extents.x * extents.x + extents.z * extents.z));
                }
            }

            return Mathf.Max(1f, radius + 0.6f);
        }

        private void PlaceNonStatefulBody(InteractableEntity entity, Vector3 groundPoint)
        {
            float lowestPoint = entity.transform.position.y;
            if (_heldColliders != null)
            {
                for (int i = 0; i < _heldColliders.Length; i++)
                {
                    Collider heldCollider = _heldColliders[i];
                    if (heldCollider == null)
                    {
                        continue;
                    }

                    heldCollider.enabled = true;
                    lowestPoint = Mathf.Min(lowestPoint, heldCollider.bounds.min.y);
                }
            }

            entity.transform.position += Vector3.up * (groundPoint.y - lowestPoint);
            if (_heldBody != null)
            {
                bool isHeavy = (entity.Categories & ItemCategory.Heavy) != 0 ||
                               entity.GetComponent<CarryCapability>() != null;
                _heldBody.isKinematic = false;
                _heldBody.linearVelocity = Vector3.zero;
                _heldBody.angularVelocity = Vector3.zero;
                _heldBody.collisionDetectionMode = CollisionDetectionMode.Discrete;
                _heldBody.useGravity = !isHeavy;
                _heldBody.isKinematic = isHeavy;
            }
        }

        private void OnClimbFinished()
        {
            _isClimbing = false;
            ApplyMovementLock();
            facts?.SetPlayerPose(PlayerPose.Standing);
        }

        private bool AllowsCounterplayWindow(CounterplayInputWindow window)
        {
            if (_controlMode == PlayerControlMode.Free)
            {
                return true;
            }

            return _controlMode == PlayerControlMode.Counterplay && (_counterplayWindows & window) != 0;
        }

        private bool BeginHandoff(HandoffKind kind, Action onCompleted)
        {
            if (_handoff.IsPending || onCompleted == null)
            {
                return false;
            }

            float fallbackSeconds = ResolveHandoffFallback(kind);
            if (!_handoff.TryBegin(kind, fallbackSeconds, Time.unscaledTime))
            {
                return false;
            }

            _handoffCompletion = onCompleted;
            _handoffSession++;
            if (handoffSignals == null || !isActiveAndEnabled)
            {
                // No animation marker source is wired, so the deterministic
                // fallback is immediate and the reparenting happens now.
                CompleteHandoff(HandoffCompletion.CompletedByFallback);
                return true;
            }

            _handoffFallbackRoutine = StartCoroutine(HandoffFallbackAfter(fallbackSeconds, _handoffSession));
            return true;
        }

        private IEnumerator HandoffFallbackAfter(float fallbackSeconds, int session)
        {
            yield return new WaitForSeconds(fallbackSeconds);
            _handoffFallbackRoutine = null;
            // A cancel or LoopReset invalidates the session before this
            // delayed completion is allowed to run.
            if (session != _handoffSession)
            {
                yield break;
            }

            if (_handoff.Advance(Time.unscaledTime) == HandoffCompletion.CompletedByFallback)
            {
                InvokeHandoffCompletion();
            }
        }

        private void OnHandoffAttachSignaled()
        {
            if (_handoff.NotifyAttachMarker() == HandoffCompletion.CompletedByMarker)
            {
                InvokeHandoffCompletion();
            }
        }

        private void OnHandoffDetachSignaled()
        {
            if (_handoff.NotifyDetachMarker() == HandoffCompletion.CompletedByMarker)
            {
                InvokeHandoffCompletion();
            }
        }

        private void CompleteHandoff(HandoffCompletion completion)
        {
            if (completion == HandoffCompletion.CompletedByMarker)
            {
                _handoff.NotifyAttachMarker();
                _handoff.NotifyDetachMarker();
            }
            else if (completion == HandoffCompletion.CompletedByFallback)
            {
                _handoff.Advance(float.PositiveInfinity);
            }

            InvokeHandoffCompletion();
        }

        private void InvokeHandoffCompletion()
        {
            StopHandoffFallback();
            Action completion = _handoffCompletion;
            _handoffCompletion = null;
            completion?.Invoke();
        }

        private void CancelHandoff()
        {
            if (!_handoff.IsPending)
            {
                return;
            }

            StopHandoffFallback();
            // Cancelling before completion means the item was never attached
            // or is still held, so ownership restores atomically.
            _handoff.Cancel();
            _handoffCompletion = null;
            _handoffSession++;
        }

        private void StopHandoffFallback()
        {
            if (_handoffFallbackRoutine == null)
            {
                return;
            }

            StopCoroutine(_handoffFallbackRoutine);
            _handoffFallbackRoutine = null;
        }

        private float ResolveHandoffFallback(HandoffKind kind)
        {
            InteractionHandoffSettings settings = handoffSettings.Validated();
            switch (kind)
            {
                case HandoffKind.PickUp:
                case HandoffKind.Carry:
                    return settings.AttachFallbackSeconds;
                case HandoffKind.Throw:
                    return settings.ThrowFallbackSeconds;
                default:
                    return settings.DetachFallbackSeconds;
            }
        }

        private void ApplyMovementLock()
        {
            motor?.SetInputLocked(IsMovementLocked);
        }
    }
}
