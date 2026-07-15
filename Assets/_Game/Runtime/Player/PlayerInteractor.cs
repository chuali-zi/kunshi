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
        [SerializeField]
        private PlayerInputRouter input;
        [SerializeField]
        private InteractionScanner scanner;
        [SerializeField]
        private Transform handSocket;
        [SerializeField]
        private PlayerMotor motor;
        [SerializeField]
        private GameplayFactService facts;
        [SerializeField]
        private GameplayEventHub events;
        [SerializeField]
        private GameConfigAsset config;

        private Rigidbody _heldBody;
        private Collider[] _heldColliders;

        public int ResetOrder => 80;

        public InteractableEntity HeldEntity { get; private set; }

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

        public bool TryHold(InteractableEntity entity, Rigidbody body)
        {
            if (HeldEntity != null || entity == null || body == null || handSocket == null)
            {
                return false;
            }

            HeldEntity = entity;
            _heldBody = body;
            _heldColliders = entity.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < _heldColliders.Length; i++)
            {
                _heldColliders[i].enabled = false;
            }

            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
            body.useGravity = false;
            entity.transform.SetParent(handSocket, false);
            entity.transform.localPosition = Vector3.zero;
            entity.transform.localRotation = Quaternion.identity;
            entity.SetHeld(true);
            facts?.SetHeldCategories(entity.Categories);
            return true;
        }

        public void EnterHide(Transform anchor, PlayerZone zone)
        {
            if (anchor == null)
            {
                return;
            }

            motor?.Teleport(anchor.position, anchor.rotation);
            facts?.SetPlayerZone(zone);
            facts?.SetPlayerPose(PlayerPose.Crouched);
        }

        public void ForceDropHeld()
        {
            DetachHeld(false);
        }

        public void CaptureInitialState()
        {
        }

        public void Quiesce(in LoopResetContext context)
        {
            DetachHeld(false);
        }

        public void Restore(in LoopResetContext context)
        {
            HeldEntity = null;
            _heldBody = null;
            _heldColliders = null;
            facts?.SetHeldCategories(ItemCategory.None);
        }

        public void Resume(in LoopResetContext context)
        {
        }

        private void OnInteract()
        {
            InteractableEntity target = scanner == null ? null : scanner.Refresh();
            target?.TryExecute(this);
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
            if (HeldEntity == null)
            {
                return;
            }

            string sourceId = HeldEntity.ItemId;
            DetachHeld(true);
            events?.EmitStimulus(KillerStimulusType.IncomingProjectile, sourceId);
        }

        private void OnStance()
        {
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
            facts.SetPlayerPose(snapshot.PlayerPose == PlayerPose.Crouched ? PlayerPose.Standing : PlayerPose.Crouched);
        }

        private void DetachHeld(bool applyThrow)
        {
            if (HeldEntity == null)
            {
                return;
            }

            InteractableEntity entity = HeldEntity;
            Rigidbody body = _heldBody;
            HeldEntity = null;
            _heldBody = null;
            entity.SetHeld(false);
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

            _heldColliders = null;
            if (body != null)
            {
                body.isKinematic = false;
                body.useGravity = true;
                body.linearVelocity =
                    applyThrow ? transform.forward * (config == null ? 8f : config.ThrowSpeed) : Vector3.zero;
                body.angularVelocity = applyThrow ? Random.onUnitSphere * 3f : Vector3.zero;
            }

            facts?.SetHeldCategories(ItemCategory.None);
        }
    }
}
