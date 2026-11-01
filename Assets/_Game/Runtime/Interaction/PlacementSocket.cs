using ConGame.Domain.Facts;
using ConGame.Domain.Interaction;
using ConGame.Runtime.Facts;
using ConGame.Runtime.Loop;
using UnityEngine;

namespace ConGame.Runtime.Interaction
{
    [DisallowMultipleComponent]
    public sealed class PlacementSocket : MonoBehaviour, ILoopResettable
    {
        private const float ChairBreachExtraSeconds = 3f;
        private const float DresserBreachExtraSeconds = 5f;

        [SerializeField] private string socketId;
        [SerializeField] private Transform anchor;
        [SerializeField] private ItemCategory acceptedCategories = ItemCategory.None;
        [SerializeField] private GameplayFactService facts;
        [SerializeField] private DoorBlockState doorBlockOnPlace = DoorBlockState.None;
        [SerializeField] private TrapState trapOnPlace = TrapState.None;

        private InteractableEntity _initialOccupant;

        public int ResetOrder => 85;
        public string SocketId => socketId;
        public Transform Anchor => anchor == null ? transform : anchor;
        public InteractableEntity Occupant { get; private set; }

        public void Configure(string id, ItemCategory categories, Transform placementAnchor = null,
                              GameplayFactService factService = null,
                              DoorBlockState blockOnPlace = DoorBlockState.None,
                              TrapState armTrapOnPlace = TrapState.None)
        {
            socketId = id ?? string.Empty;
            acceptedCategories = categories;
            anchor = placementAnchor;
            facts = factService;
            doorBlockOnPlace = blockOnPlace;
            trapOnPlace = armTrapOnPlace;
        }

        public bool CanAccept(InteractableEntity entity)
        {
            if (entity == null || (Occupant != null && Occupant != entity))
            {
                return false;
            }

            // Trap facts are a single authoritative world state. Do not accept
            // a different trap into this socket while another trap is armed;
            // otherwise placement would succeed while its handler remained
            // unusable because the global fact still names the other trap.
            if (trapOnPlace != TrapState.None && facts != null)
            {
                TrapState currentTrap = facts.CaptureSnapshot().Trap;
                if (currentTrap != TrapState.None && currentTrap != trapOnPlace)
                {
                    return false;
                }
            }

            return acceptedCategories == ItemCategory.None ||
                   (entity.Categories & acceptedCategories) != ItemCategory.None;
        }

        public bool TryPlace(InteractableEntity entity, StatefulWorldItem item)
        {
            if (item == null || !CanAccept(entity) || !item.PlaceAt(this))
            {
                return false;
            }

            Occupant = entity;
            ApplyDoorBlockFacts(entity);
            ApplyTrapFacts();
            return true;
        }

        public void Release(InteractableEntity entity)
        {
            if (Occupant == entity)
            {
                Occupant = null;
                ClearDoorBlockFacts();
                ClearTrapFacts();
            }
        }

        public void CaptureInitialState()
        {
            _initialOccupant = Occupant;
        }

        public void Quiesce(in LoopResetContext context)
        {
        }

        public void Restore(in LoopResetContext context)
        {
            Occupant = _initialOccupant;
            if (_initialOccupant == null)
            {
                ClearDoorBlockFacts();
                ClearTrapFacts();
            }
            else
            {
                ApplyDoorBlockFacts(_initialOccupant);
                ApplyTrapFacts();
            }
        }

        public void Resume(in LoopResetContext context)
        {
        }

        private void ApplyDoorBlockFacts(InteractableEntity entity)
        {
            if (facts == null || doorBlockOnPlace == DoorBlockState.None || entity == null)
            {
                return;
            }

            float extra = doorBlockOnPlace == DoorBlockState.Bookcase
                              ? 0f
                              : ResolveFurnitureBreachExtra(entity.ItemId);
            facts.SetDoorBlock(doorBlockOnPlace, extra);
        }

        private void ClearDoorBlockFacts()
        {
            if (facts == null || doorBlockOnPlace == DoorBlockState.None)
            {
                return;
            }

            facts.SetDoorBlock(DoorBlockState.None, 0f);
        }

        private void ApplyTrapFacts()
        {
            if (facts == null || trapOnPlace == TrapState.None)
            {
                return;
            }

            FactSnapshot snapshot = facts.CaptureSnapshot();
            if (snapshot.Trap == TrapState.None || snapshot.Trap == trapOnPlace)
            {
                facts.SetTrap(trapOnPlace);
                facts.SetTrapPulled(false);
            }
        }

        private void ClearTrapFacts()
        {
            if (facts == null || trapOnPlace == TrapState.None)
            {
                return;
            }

            if (facts.CaptureSnapshot().Trap == trapOnPlace)
            {
                facts.SetTrap(TrapState.None);
                facts.SetTrapPulled(false);
            }
        }

        private static float ResolveFurnitureBreachExtra(string itemId)
        {
            if (string.Equals(itemId, "dresser", System.StringComparison.Ordinal))
            {
                return DresserBreachExtraSeconds;
            }

            return ChairBreachExtraSeconds;
        }
    }
}
