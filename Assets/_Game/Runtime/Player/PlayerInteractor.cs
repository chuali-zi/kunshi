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

        public int ResetOrder => 80;
        public InteractableEntity HeldEntity { get; private set; }
        public float HeldMovementMultiplier => _heldMovementMultiplier;
        public bool IsHiding => _isHiding;

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

        public void EnterHide(Transform anchor, PlayerZone zone)
        {
            if (anchor == null || HeldEntity != null)
            {
                return;
            }

            if (!_isHiding)
            {
                _hideReturnPosition = transform.position;
                _hideReturnRotation = transform.rotation;
                _hasHideReturnPose = true;
            }

            motor?.Teleport(anchor.position, anchor.rotation);
            _isHiding = true;
            facts?.SetPlayerZone(zone);
            facts?.SetPlayerPose(PlayerPose.Hidden);
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
            facts?.SetPlayerZone(PlayerZone.Open);
            facts?.SetPlayerPose(PlayerPose.Standing);
        }

        public void SetPose(PlayerPose pose)
        {
            _isHiding = pose == PlayerPose.Hidden;
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
            if (HeldEntity == null)
            {
                return;
            }

            string sourceId = HeldEntity.ItemId;
            DetachHeld(true);
            events?.EmitStimulus(KillerStimulusType.IncomingProjectile, sourceId);
        }

        public void CaptureInitialState()
        {
            _isHiding = false;
            _hasHideReturnPose = false;
            _heldMovementMultiplier = 1f;
        }

        public void Quiesce(in LoopResetContext context)
        {
            DetachHeld(false);
            _isHiding = false;
            _hasHideReturnPose = false;
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
            motor?.SetMovementSpeedMultiplier(1f);
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

                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
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
            InteractableEntity target = scanner == null ? null : scanner.Refresh();
            if (target != null)
            {
                target.TryExecute(this);
                return;
            }

            if (HeldEntity != null || _isHiding || facts == null)
            {
                return;
            }

            // Empty-handed interact with no target = surrender pose (D06).
            SetPose(PlayerPose.Kneeling);
        }

        private void OnAttack()
        {
            if (HeldEntity == null)
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
            if (_isHiding)
            {
                ExitHide();
                return;
            }

            InteractableEntity target = scanner == null ? null : scanner.Refresh();
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
            Vector3 velocity = applyThrow ? transform.forward * (config == null ? 8f : config.ThrowSpeed) : Vector3.zero;
            Vector3 angularVelocity = applyThrow ? Random.onUnitSphere * 3f : Vector3.zero;

            if (stateful != null)
            {
                stateful.Release(velocity, angularVelocity);
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
    }
}
