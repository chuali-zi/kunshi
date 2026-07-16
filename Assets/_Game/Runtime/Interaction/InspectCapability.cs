using System;
using ConGame.Domain.Interaction;
using ConGame.Runtime.Player;
using UnityEngine;

namespace ConGame.Runtime.Interaction
{
    public sealed class InspectCapability : InteractionCapability
    {
        public event Action<InteractableEntity> Inspected;

        public override InteractionVerb Verb => InteractionVerb.Inspect;
        public override int Priority => 10;

        public override bool CanExecute(in InteractionContext context)
        {
            return context.Entity != null;
        }

        public override InteractionResult Execute(in InteractionContext context)
        {
            InteractableEntity entity = context.Entity;
            PlayerInteractor player = context.Player;
            if (player != null && entity != null)
            {
                Vector3 toTarget = entity.transform.position - player.transform.position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.001f)
                {
                    player.transform.rotation = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
                }
            }

            Inspected?.Invoke(entity);
            return InteractionResult.Accepted;
        }
    }
}
