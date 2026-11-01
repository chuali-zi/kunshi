using System.Collections.Generic;
using ConGame.Domain.Counterplay;

namespace ConGame.Domain.Choreography
{
    /// <summary>
    /// Code-authored paired performance content for D08/D11/D21 (game_flow_script).
    /// Stage durations are design beats, not animation lengths; the visual layer binds
    /// clips separately. Semantic event ids under "D21.*" mark the ownership switches
    /// (dropped pistol/flashlight, flashlight seized, block-light, backup pistol) that
    /// the visual-ownership layer consumes.
    /// </summary>
    public static class PairedPerformanceCatalog
    {
        public const string FallbackOutcomeId = "D01";

        public static PairedPerformanceDefinition CreateD08()
        {
            var stages = new List<PairedPerformanceStageDefinition>
            {
                // Player bat connects from the door-side blind spot; killer staggers.
                new PairedPerformanceStageDefinition(
                    "strike",
                    0.4f,
                    PairedContactState.Contact,
                    enterEvents: new[] { "D08.Strike" }),
                // Grapple: three 1.0s struggle windows (gameplay_logic_spec §8).
                new PairedPerformanceStageDefinition(
                    "grapple",
                    3.0f,
                    PairedContactState.Contact,
                    allowGameplayCancel: false,
                    qteWindows: new[]
                    {
                        new QteWindowDefinition(1.0f, 3),
                        new QteWindowDefinition(1.0f, 3),
                        new QteWindowDefinition(1.0f, 3)
                    },
                    enterEvents: new[] { "D08.Pair.Begin" },
                    exitEvents: new[] { "D08.Pair.End" },
                    qteSuccessEvents: new[] { "D08.MainPistol.Dropped", "D08.LootWindow.Open" },
                    qteFailureEvents: new[] { "D08.GrappleFailed" }),
                // Knee escape: killers breaks contact and opens half a meter.
                new PairedPerformanceStageDefinition(
                    "breakaway",
                    0.5f,
                    PairedContactState.Detached,
                    allowGameplayCancel: false,
                    enterEvents: new[] { "D08.Breakaway" }),
                new PairedPerformanceStageDefinition(
                    "pointblank",
                    0.4f,
                    PairedContactState.Separated,
                    allowGameplayCancel: false,
                    enterEvents: new[] { "D08.PointBlankShot" })
            };

            return new PairedPerformanceDefinition(
                "D08",
                FallbackOutcomeId,
                0.9f,
                PairedAnchorTolerance.Default,
                stages);
        }

        public static PairedPerformanceDefinition CreateD11()
        {
            var stages = new List<PairedPerformanceStageDefinition>
            {
                // Killer walks straight to the bed; no search (game_flow_script D11).
                new PairedPerformanceStageDefinition(
                    "approach-bed",
                    1.5f,
                    PairedContactState.Separated,
                    enterEvents: new[] { "D11.ApproachBed" }),
                new PairedPerformanceStageDefinition(
                    "kneel-grab",
                    0.8f,
                    PairedContactState.Contact,
                    allowGameplayCancel: false,
                    enterEvents: new[] { "D11.GrabAnkle" }),
                new PairedPerformanceStageDefinition(
                    "drag-out",
                    1.6f,
                    PairedContactState.Contact,
                    allowGameplayCancel: false,
                    enterEvents: new[] { "D11.DragOut" }),
                new PairedPerformanceStageDefinition(
                    "execute",
                    0.6f,
                    PairedContactState.Contact,
                    allowGameplayCancel: false,
                    enterEvents: new[] { "D11.Execute" })
            };

            return new PairedPerformanceDefinition(
                "D11",
                FallbackOutcomeId,
                0.7f,
                PairedAnchorTolerance.Default,
                stages);
        }

        public static PairedPerformanceDefinition CreateD21()
        {
            var stages = new List<PairedPerformanceStageDefinition>
            {
                // Blanket pull: the killer's only full fall (beat 1).
                new PairedPerformanceStageDefinition(
                    "fall",
                    1.2f,
                    PairedContactState.Separated,
                    allowGameplayCancel: false,
                    enterEvents: new[] { "D21.Fall.Begin" },
                    exitEvents: new[] { "D21.Fall.Impact" }),
                // Main pistol and tactical flashlight leave his hands (beat 2).
                new PairedPerformanceStageDefinition(
                    "item-drop",
                    0.8f,
                    PairedContactState.Separated,
                    allowGameplayCancel: false,
                    enterEvents: new[] { "D21.Pistol.Dropped", "D21.Flashlight.Dropped" }),
                // Dark grapple: three 1.0s struggle windows on the floor (beat 3).
                new PairedPerformanceStageDefinition(
                    "dark-grapple",
                    3.0f,
                    PairedContactState.Contact,
                    allowGameplayCancel: false,
                    qteWindows: new[]
                    {
                        new QteWindowDefinition(1.0f, 3),
                        new QteWindowDefinition(1.0f, 3),
                        new QteWindowDefinition(1.0f, 3)
                    },
                    enterEvents: new[] { "D21.Pair.Begin" },
                    exitEvents: new[] { "D21.Pair.End" },
                    qteSuccessEvents: new[] { "D21.Pair.Dominated" },
                    qteFailureEvents: new[] { "D21.Pair.Struggled" }),
                // Player breaks free and seizes the flashlight (beat 4 start).
                new PairedPerformanceStageDefinition(
                    "detach-escape",
                    1.0f,
                    PairedContactState.Detached,
                    allowGameplayCancel: false,
                    enterEvents: new[] { "D21.Player.Escape", "D21.Flashlight.Dislodged" },
                    exitEvents: new[] { "D21.Flashlight.Recovery.Open" }),
                // Beam locked on his face; he kneels and blocks the light (beat 4, 3s).
                new PairedPerformanceStageDefinition(
                    "block-light",
                    3.0f,
                    PairedContactState.Separated,
                    allowGameplayCancel: false,
                    enterEvents: new[] { "D21.BlockLight.Begin" },
                    exitEvents: new[] { "D21.BlockLight.End" }),
                // Blocking hand slides into the coat: backup pistol (beat 5).
                new PairedPerformanceStageDefinition(
                    "backup-draw",
                    1.6f,
                    PairedContactState.Separated,
                    allowGameplayCancel: false,
                    enterEvents: new[] { "D21.BackupPistol.Draw" }),
                new PairedPerformanceStageDefinition(
                    "backup-fire",
                    0.5f,
                    PairedContactState.Separated,
                    allowGameplayCancel: false,
                    enterEvents: new[] { "D21.BackupPistol.Fire" })
            };

            return new PairedPerformanceDefinition(
                "D21",
                FallbackOutcomeId,
                1.1f,
                PairedAnchorTolerance.Default,
                stages);
        }

        public static PairedPerformanceDefinition GetByOutcomeId(string outcomeId)
        {
            switch (outcomeId)
            {
                case "D08": return CreateD08();
                case "D11": return CreateD11();
                case "D21": return CreateD21();
                default: return null;
            }
        }
    }
}
