using System;
using ConGame.Domain.Interaction;
using ConGame.Runtime.Data;
using ConGame.Runtime.Player;
using UnityEngine;

namespace ConGame.Runtime.Interaction
{
    [DisallowMultipleComponent]
    public sealed class InteractableEntity : MonoBehaviour
    {
        [SerializeField] private ItemDefinitionAsset definition;
        [SerializeField] private string fallbackItemId;

        private InteractionCapability[] _capabilities;

        public string ItemId => definition == null ? fallbackItemId : definition.ItemId;
        public ItemCategory Categories => definition == null ? ItemCategory.None : definition.Categories;
        public bool IsHeld { get; private set; }

        public void Configure(ItemDefinitionAsset itemDefinition, string fallbackId = "")
        {
            definition = itemDefinition;
            fallbackItemId = fallbackId;
            _capabilities = null;
        }

        public bool TryExecute(PlayerInteractor player, InteractionVerb requestedVerb = InteractionVerb.None)
        {
            StatefulWorldItem stateful = GetComponent<StatefulWorldItem>();
            if (stateful != null && !stateful.AllowsOrdinaryInteraction)
            {
                return false;
            }

            EnsureCapabilities();
            InteractionContext context = new InteractionContext(player, this);
            InteractionCapability best = null;
            for (int i = 0; i < _capabilities.Length; i++)
            {
                InteractionCapability capability = _capabilities[i];
                if (capability == null || (requestedVerb != InteractionVerb.None && capability.Verb != requestedVerb))
                {
                    continue;
                }

                if (requestedVerb == InteractionVerb.None &&
                    (capability.Verb == InteractionVerb.Hide || capability.Verb == InteractionVerb.Swing ||
                     capability.Verb == InteractionVerb.Throw))
                {
                    continue;
                }

                if (capability.CanExecute(context) && (best == null || capability.Priority > best.Priority))
                {
                    best = capability;
                }
            }

            return best != null && best.Execute(context) == InteractionResult.Accepted;
        }

        public int GetBestExecutablePriority(PlayerInteractor player)
        {
            StatefulWorldItem stateful = GetComponent<StatefulWorldItem>();
            if (stateful != null && !stateful.AllowsOrdinaryInteraction)
            {
                return int.MinValue;
            }

            EnsureCapabilities();
            InteractionContext context = new InteractionContext(player, this);
            int bestPriority = int.MinValue;
            for (int i = 0; i < _capabilities.Length; i++)
            {
                InteractionCapability capability = _capabilities[i];
                if (capability == null || capability.Verb == InteractionVerb.Hide ||
                    capability.Verb == InteractionVerb.Swing || capability.Verb == InteractionVerb.Throw)
                {
                    continue;
                }

                if (capability.CanExecute(context))
                {
                    bestPriority = Math.Max(bestPriority, capability.Priority);
                }
            }

            return bestPriority;
        }

        public T GetCapability<T>() where T : InteractionCapability
        {
            EnsureCapabilities();
            for (int i = 0; i < _capabilities.Length; i++)
            {
                if (_capabilities[i] is T typed)
                {
                    return typed;
                }
            }

            return null;
        }

        public void SetHeld(bool value)
        {
            IsHeld = value;
        }

        private void EnsureCapabilities()
        {
            _capabilities ??= GetComponents<InteractionCapability>();
        }
    }
}
