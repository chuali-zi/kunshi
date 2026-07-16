using ConGame.Domain.Facts;
using ConGame.Domain.Interaction;
using ConGame.Runtime.Facts;
using NUnit.Framework;
using UnityEngine;

namespace ConGame.Tests.EditMode
{
    public sealed class GameplayFactServiceExpansionTests
    {
        [Test]
        public void WorldSemanticSettersRoundTripThroughSnapshot()
        {
            GameObject owner = new GameObject("facts");
            try
            {
                GameplayFactService facts = owner.AddComponent<GameplayFactService>();
                facts.CaptureInitialState();
                facts.SetDoorBlock(DoorBlockState.Bookcase, 0f);
                Assert.That(facts.DoorBlockBreachExtraSeconds, Is.EqualTo(0f));
                facts.SetDoorBlock(DoorBlockState.Furniture, 3f);
                Assert.That(facts.DoorBlockBreachExtraSeconds, Is.EqualTo(3f));
                facts.SetDoorBlock(DoorBlockState.Bookcase);
                facts.SetTrap(TrapState.BlanketArmed);
                facts.SetTrapPulled(true);
                facts.SetStolenPistol(true);
                facts.SetGlareActive(true);
                facts.SetStrikingWall(true);
                facts.SetStrikingWindow(true);
                facts.SetAmbushing(true);
                facts.SetAtAwakeningPoint(false);
                facts.SetHeldCategories(ItemCategory.Weapon);

                FactSnapshot snapshot = facts.CaptureSnapshot();
                Assert.That(snapshot.DoorBlock, Is.EqualTo(DoorBlockState.Bookcase));
                Assert.That(snapshot.Trap, Is.EqualTo(TrapState.BlanketArmed));
                Assert.That(snapshot.TrapPulled, Is.True);
                Assert.That(snapshot.HasStolenPistol, Is.True);
                Assert.That(snapshot.IsGlareActive, Is.True);
                Assert.That(snapshot.IsStrikingWall, Is.True);
                Assert.That(snapshot.IsStrikingWindow, Is.True);
                Assert.That(snapshot.IsAmbushing, Is.True);
                Assert.That(snapshot.IsAtAwakeningPoint, Is.False);
                Assert.That(snapshot.HeldItemCategories, Is.EqualTo(ItemCategory.Weapon));
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void HeldFirearmCategoryDoesNotImplicitlyMeanStolenMainPistol()
        {
            GameObject owner = new GameObject("facts");
            try
            {
                GameplayFactService facts = owner.AddComponent<GameplayFactService>();
                facts.CaptureInitialState();
                facts.SetHeldCategories(ItemCategory.Firearm);
                Assert.That(facts.CaptureSnapshot().HasStolenPistol, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }
    }
}
