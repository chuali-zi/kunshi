using ConGame.Domain.Facts;
using ConGame.Domain.Interaction;
using ConGame.Runtime.Events;
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
        [SerializeField, Min(0.05f)] private float glareDuration = 0.75f;
        [SerializeField] private GameplayEventHub events;

        public GameplayAffordanceKind Kind => kind;

        public void Configure(
            GameplayAffordanceKind affordanceKind,
            GameplayFactService factService,
            float activeGlareDuration = 0.75f,
            GameplayEventHub eventHub = null)
        {
            kind = affordanceKind;
            facts = factService;
            glareDuration = Mathf.Max(0.05f, activeGlareDuration);
            events = eventHub;
        }

        public bool CanUse(in InteractionContext context)
        {
            if (facts == null || kind == GameplayAffordanceKind.None)
            {
                return false;
            }

            // These facts must come from their physical world systems. Exposing
            // them as a generic Use action lets the player bypass the required
            // weapon impact or flashlight reflection.
            return kind != GameplayAffordanceKind.StrikeWall &&
                   kind != GameplayAffordanceKind.StrikeWindow &&
                   kind != GameplayAffordanceKind.ActivateGlare;
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
                    ResolveEvents()?.EmitStimulus(KillerStimulusType.TrapPull, "rug");
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
                    return InteractionResult.Rejected;
                case GameplayAffordanceKind.StealPistol:
                    facts.SetStolenPistol(true);
                    break;
            }

            return InteractionResult.Accepted;
        }

        private GameplayEventHub ResolveEvents()
        {
            events ??= UnityEngine.Object.FindAnyObjectByType<GameplayEventHub>();
            return events;
        }
    }
}
