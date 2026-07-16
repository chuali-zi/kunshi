using ConGame.Domain.Facts;
using ConGame.Domain.Interaction;
using ConGame.Runtime.Facts;
using UnityEngine;

namespace ConGame.Runtime.Interaction
{
    public enum GameplayAffordanceKind
    {
        None = 0,
        ArmBlanketTrap = 1,
        PullRugTrap = 2,
        BlockDoorWithFurniture = 3,
        BlockDoorWithBookcase = 4,
        StrikeWall = 5,
        StrikeWindow = 6,
        StartAmbush = 7,
        ActivateGlare = 8,
        StealPistol = 9
    }

    [DisallowMultipleComponent]
    public sealed class GameplayAffordanceHandler : MonoBehaviour, IUseHandler
    {
        [SerializeField] private GameplayAffordanceKind kind;
        [SerializeField] private GameplayFactService facts;

        public GameplayAffordanceKind Kind => kind;

        public void Configure(GameplayAffordanceKind affordanceKind, GameplayFactService factService)
        {
            kind = affordanceKind;
            facts = factService;
        }

        public bool CanUse(in InteractionContext context)
        {
            return facts != null && kind != GameplayAffordanceKind.None;
        }

        public InteractionResult Use(in InteractionContext context)
        {
            if (!CanUse(context))
            {
                return InteractionResult.Rejected;
            }

            switch (kind)
            {
                case GameplayAffordanceKind.ArmBlanketTrap:
                    facts.SetTrap(TrapState.BlanketArmed);
                    facts.SetTrapPulled(false);
                    break;
                case GameplayAffordanceKind.PullRugTrap:
                    facts.SetTrap(TrapState.RugArmed);
                    facts.SetTrapPulled(true);
                    break;
                case GameplayAffordanceKind.BlockDoorWithFurniture:
                    facts.SetDoorBlock(DoorBlockState.Furniture);
                    break;
                case GameplayAffordanceKind.BlockDoorWithBookcase:
                    facts.SetDoorBlock(DoorBlockState.Bookcase);
                    break;
                case GameplayAffordanceKind.StrikeWall:
                    facts.SetStrikingWall(true);
                    break;
                case GameplayAffordanceKind.StrikeWindow:
                    facts.SetStrikingWindow(true);
                    break;
                case GameplayAffordanceKind.StartAmbush:
                    facts.SetAmbushing(true);
                    break;
                case GameplayAffordanceKind.ActivateGlare:
                    facts.SetGlareActive(true);
                    break;
                case GameplayAffordanceKind.StealPistol:
                    facts.SetStolenPistol(true);
                    break;
            }

            return InteractionResult.Accepted;
        }
    }
}
