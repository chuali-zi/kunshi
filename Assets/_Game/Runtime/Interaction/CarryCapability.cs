using ConGame.Domain.Interaction;
using UnityEngine;

namespace ConGame.Runtime.Interaction
{
    [RequireComponent(typeof(InteractableEntity))]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(StatefulWorldItem))]
    public sealed class CarryCapability : InteractionCapability
    {
        [SerializeField, Range(0.1f, 1f)] private float movementSpeedMultiplier = 0.5f;
        [SerializeField] private bool groundDrag;

        private InteractableEntity _entity;
        private Rigidbody _body;

        public override InteractionVerb Verb => InteractionVerb.Carry;
        public override int Priority => 110;
        public float MovementSpeedMultiplier => movementSpeedMultiplier;
        public bool IsGroundDrag => groundDrag;

        public void Configure(float speedMultiplier)
        {
            movementSpeedMultiplier = Mathf.Clamp(speedMultiplier, 0.1f, 1f);
            groundDrag = false;
        }

        public void ConfigureDrag(float speedMultiplier = 0.5f)
        {
            movementSpeedMultiplier = Mathf.Clamp(speedMultiplier, 0.1f, 1f);
            groundDrag = true;
        }

        private void Awake()
        {
            _entity = GetComponent<InteractableEntity>();
            _body = GetComponent<Rigidbody>();
        }

        public override bool CanExecute(in InteractionContext context)
        {
            StatefulWorldItem stateful = GetComponent<StatefulWorldItem>();
            return context.Player != null && context.Player.HeldEntity == null && _entity != null &&
                   stateful != null && stateful.AllowsOrdinaryInteraction;
        }

        public override InteractionResult Execute(in InteractionContext context)
        {
            return (groundDrag
                    ? context.Player.TryDrag(_entity, _body, movementSpeedMultiplier)
                    : context.Player.TryCarry(_entity, _body, movementSpeedMultiplier))
                ? InteractionResult.Accepted
                : InteractionResult.Rejected;
        }
    }
}
