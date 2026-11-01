using System.Linq;
using ConGame.Domain.Choreography;
using NUnit.Framework;

namespace ConGame.Tests.EditMode
{
    public sealed class PairedPerformanceCatalogTests
    {
        [Test]
        public void AllPairedOutcomesExistAndValidate()
        {
            foreach (string outcomeId in new[] { "D08", "D11", "D21" })
            {
                PairedPerformanceDefinition definition =
                    PairedPerformanceCatalog.GetByOutcomeId(outcomeId);

                Assert.That(definition, Is.Not.Null, outcomeId);
                Assert.That(definition.Validate(out string error), Is.True, $"{outcomeId}: {error}");
                Assert.That(definition.FallbackOutcomeId, Is.EqualTo("D01"));
                Assert.That(definition.TotalDuration, Is.GreaterThan(0f));
            }
        }

        [Test]
        public void UnknownOutcomeReturnsNull()
        {
            Assert.That(PairedPerformanceCatalog.GetByOutcomeId("D01"), Is.Null);
            Assert.That(PairedPerformanceCatalog.GetByOutcomeId("D99"), Is.Null);
            Assert.That(PairedPerformanceCatalog.GetByOutcomeId(null), Is.Null);
        }

        [Test]
        public void AllDefinitionsUseSpecLaunchTolerance()
        {
            foreach (string outcomeId in new[] { "D08", "D11", "D21" })
            {
                PairedAnchorTolerance tolerance =
                    PairedPerformanceCatalog.GetByOutcomeId(outcomeId).Tolerance;

                Assert.That(tolerance.MaxPositionError, Is.EqualTo(0.15f).Within(1e-6f), outcomeId);
                Assert.That(tolerance.MaxYawErrorDegrees, Is.EqualTo(10f).Within(1e-6f), outcomeId);
            }
        }

        [Test]
        public void D08HasThreeStruggleWindowsAndPointBlankFinish()
        {
            PairedPerformanceDefinition d08 = PairedPerformanceCatalog.CreateD08();

            Assert.That(d08.Stages.Select(s => s.StageId),
                Is.EqualTo(new[] { "strike", "grapple", "breakaway", "pointblank" }));
            PairedPerformanceStageDefinition grapple = d08.Stages[1];
            Assert.That(grapple.QteWindows.Length, Is.EqualTo(3));
            Assert.That(grapple.QteWindows.All(w => w.DurationSeconds >= 1f), Is.True);
            Assert.That(grapple.QteSuccessEvents, Does.Contain("D08.MainPistol.Dropped"));
            Assert.That(d08.Stages[2].ContactState, Is.EqualTo(PairedContactState.Detached));
        }

        [Test]
        public void D11DragOutForbidsGameplayCancel()
        {
            PairedPerformanceDefinition d11 = PairedPerformanceCatalog.CreateD11();

            Assert.That(d11.Stages.Select(s => s.StageId),
                Is.EqualTo(new[] { "approach-bed", "kneel-grab", "drag-out", "execute" }));
            PairedPerformanceStageDefinition dragOut = d11.Stages[2];
            Assert.That(dragOut.ContactState, Is.EqualTo(PairedContactState.Contact));
            Assert.That(dragOut.AllowGameplayCancel, Is.False);
        }

        [Test]
        public void D21FollowsScriptedBeatOrder()
        {
            PairedPerformanceDefinition d21 = PairedPerformanceCatalog.CreateD21();

            Assert.That(d21.Stages.Select(s => s.StageId), Is.EqualTo(new[]
            {
                "fall", "item-drop", "dark-grapple", "detach-escape",
                "block-light", "backup-draw", "backup-fire"
            }));
        }

        [Test]
        public void D21ExposesOwnershipSwitchSemanticEvents()
        {
            PairedPerformanceDefinition d21 = PairedPerformanceCatalog.CreateD21();
            string[] allEvents = d21.Stages
                .SelectMany(s => (s.EnterEvents ?? new string[0]).Concat(s.ExitEvents ?? new string[0]))
                .ToArray();

            Assert.That(allEvents, Does.Contain("D21.Pistol.Dropped"));
            Assert.That(allEvents, Does.Contain("D21.Flashlight.Dropped"));
            Assert.That(allEvents, Does.Contain("D21.Flashlight.Dislodged"));
            Assert.That(allEvents, Does.Contain("D21.Flashlight.Recovery.Open"));
            Assert.That(allEvents, Does.Not.Contain("D21.Flashlight.OwnershipToPlayer"));
            Assert.That(allEvents, Does.Contain("D21.BlockLight.Begin"));
            Assert.That(allEvents, Does.Contain("D21.BlockLight.End"));
            Assert.That(allEvents, Does.Contain("D21.BackupPistol.Draw"));
            Assert.That(allEvents, Does.Contain("D21.BackupPistol.Fire"));
        }

        [Test]
        public void D21GrappleHasThreeWindowsAndBlockLightBeatLastsThreeSeconds()
        {
            PairedPerformanceDefinition d21 = PairedPerformanceCatalog.CreateD21();

            PairedPerformanceStageDefinition grapple =
                d21.Stages.First(s => s.StageId == "dark-grapple");
            PairedPerformanceStageDefinition blockLight =
                d21.Stages.First(s => s.StageId == "block-light");

            Assert.That(grapple.QteWindows.Length, Is.EqualTo(3));
            Assert.That(grapple.ContactState, Is.EqualTo(PairedContactState.Contact));
            Assert.That(blockLight.DurationSeconds, Is.EqualTo(3.0f).Within(1e-6f));
        }
    }
}
