using ConGame.Domain.Interaction;
using UnityEngine;

namespace ConGame.Runtime.Interaction
{

    [DisallowMultipleComponent]
    [RequireComponent(typeof(InteractableEntity))]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class PickupCapability : InteractionCapability
    {
        private InteractableEntity _entity;
        private Rigidbody _body;

        public override InteractionVerb Verb => InteractionVerb.PickUp;

        public override int Priority => 100;

        private void Awake()
        {
            _entity = GetComponent<InteractableEntity>();
            _body = GetComponent<Rigidbody>();
        }

        public override bool CanExecute(in InteractionContext context)
        {
            if (context.Player == null || context.Player.HeldEntity != null || _entity.IsHeld)
            {
                return false;
            }

            StatefulWorldItem stateful = GetComponent<StatefulWorldItem>();
            UseCapability use = GetComponent<UseCapability>();
            return stateful == null || stateful.CurrentSocket == null || use == null ||
                   !use.CanExecute(new InteractionContext(context.Player, _entity));
        }

        public override InteractionResult Execute(in InteractionContext context)
        {
            return context.Player.TryHold(_entity, _body)
                ? InteractionResult.Accepted
                : InteractionResult.Rejected;
        }
    }
}
