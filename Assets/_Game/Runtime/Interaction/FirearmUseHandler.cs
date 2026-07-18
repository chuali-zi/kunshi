using System;
using ConGame.Runtime.Player;
using UnityEngine;

namespace ConGame.Runtime.Interaction
{
    public enum FirearmRole
    {
        MainPistol = 0,
        ScriptedBackup = 1
    }

    public interface IFirearmUseHandler : IUseHandler
    {
        int RoundsRemaining { get; }
        FirearmRole Role { get; }
        void Arm(int rounds);
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(InteractableEntity))]
    public sealed class FirearmUseHandler : MonoBehaviour, IFirearmUseHandler
    {
        [SerializeField] private FirearmRole role;
        [SerializeField, Min(0)] private int roundsRemaining;

        private InteractableEntity _entity;

        public event Action<FirearmUseHandler> ShotFired;
        public event Action<FirearmUseHandler> DryFired;

        public int RoundsRemaining => roundsRemaining;
        public FirearmRole Role => role;

        public void Configure(FirearmRole firearmRole, int rounds)
        {
            role = firearmRole;
            roundsRemaining = Mathf.Max(0, rounds);
            _entity = GetComponent<InteractableEntity>();
        }

        public void Arm(int rounds)
        {
            roundsRemaining = Mathf.Max(0, rounds);
        }

        private void Awake()
        {
            _entity = GetComponent<InteractableEntity>();
        }

        public bool CanUse(in InteractionContext context)
        {
            _entity ??= GetComponent<InteractableEntity>();
            PlayerInteractor player = context.Player;
            return _entity != null && _entity.IsHeld && player != null;
        }

        public InteractionResult Use(in InteractionContext context)
        {
            if (!CanUse(context))
            {
                return InteractionResult.Rejected;
            }

            if (roundsRemaining > 0)
            {
                roundsRemaining--;
                ShotFired?.Invoke(this);
            }
            else
            {
                DryFired?.Invoke(this);
            }

            return InteractionResult.Accepted;
        }
    }
}
