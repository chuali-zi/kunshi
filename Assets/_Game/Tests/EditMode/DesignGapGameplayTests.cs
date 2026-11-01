using ConGame.Domain.Facts;
using ConGame.Domain.Interaction;
using ConGame.Runtime.Interaction;
using NUnit.Framework;
using UnityEngine;

namespace ConGame.Tests.EditMode
{
    public sealed class DesignGapGameplayTests
    {
        [Test]
        public void StuckDrawerOpensOnlyOnSecondPull()
        {
            DrawerLatchState stuck = new DrawerLatchState(2);
            Assert.That(stuck.TryPull(), Is.False);
            Assert.That(stuck.IsOpen, Is.False);
            Assert.That(stuck.TryPull(), Is.True);
            Assert.That(stuck.IsOpen, Is.True);
            Assert.That(stuck.TryPull(), Is.False);
        }

        [Test]
        public void FreeDrawerOpensOnFirstPullAndResetCloses()
        {
            DrawerLatchState drawer = new DrawerLatchState(1);
            Assert.That(drawer.TryPull(), Is.True);
            drawer.Reset();
            Assert.That(drawer.IsOpen, Is.False);
            Assert.That(drawer.Pulls, Is.EqualTo(0));
        }

        [Test]
        public void BookcaseBlocksDoorOnlyWhenTippedTowardIt()
        {
            Vector3 bookcase = new Vector3(-2.65f, 0f, 1.65f);
            Vector3 door = new Vector3(2.7f, 0f, 1.45f);
            Assert.That(BookcaseTipGate.BlocksDoor(bookcase, door, Vector3.right), Is.True);
            Assert.That(BookcaseTipGate.BlocksDoor(bookcase, door, Vector3.left), Is.False);
        }

        [Test]
        public void HoldDurationCompletesOnlyAfterRequiredSeconds()
        {
            HoldDurationGate gate = default;
            gate.Begin(1f, 2f);
            Assert.That(gate.IsComplete(2.9f), Is.False);
            Assert.That(gate.IsComplete(3f), Is.True);
            gate.Cancel();
            Assert.That(gate.IsPending, Is.False);
        }

        [Test]
        public void AmbushRequiresCoverPoseAndStillness()
        {
            Assert.That(AmbushFactDerivation.IsAmbushing(PlayerZone.DoorBlind, PlayerPose.Crouched, false), Is.True);
            Assert.That(AmbushFactDerivation.IsAmbushing(PlayerZone.SofaRear, PlayerPose.Hidden, false), Is.True);
            Assert.That(AmbushFactDerivation.IsAmbushing(PlayerZone.DoorBlind, PlayerPose.Standing, false), Is.False);
            Assert.That(AmbushFactDerivation.IsAmbushing(PlayerZone.DoorBlind, PlayerPose.Crouched, true), Is.False);
            Assert.That(AmbushFactDerivation.IsAmbushing(PlayerZone.Open, PlayerPose.Crouched, false), Is.False);
        }

        [Test]
        public void TrapPullRequiresOccupantAndArmedState()
        {
            Assert.That(TripTrapTiming.CanPull(true, TrapState.BlanketArmed, false), Is.True);
            Assert.That(TripTrapTiming.CanPull(false, TrapState.BlanketArmed, false), Is.False);
            Assert.That(TripTrapTiming.CanPull(true, TrapState.None, false), Is.False);
            Assert.That(TripTrapTiming.CanPull(true, TrapState.RugArmed, true), Is.False);
            Assert.That(TripTrapTiming.ShouldAutoPull(true, TrapState.RugArmed, false), Is.True);
            Assert.That(TripTrapTiming.ShouldAutoPull(false, TrapState.RugArmed, false), Is.False);
        }

        [Test]
        public void InspectFocusDwellsAfterQuarterSecond()
        {
            InspectFocusTracker tracker = new InspectFocusTracker();
            Assert.That(tracker.Tick("lamp", 0.1f), Is.False);
            Assert.That(tracker.Tick("lamp", 0.16f), Is.True);
            Assert.That(tracker.Tick(null, 0.1f), Is.False);
        }

        [Test]
        public void WallDamageCapsAtThreeAndWindowRevealsBrickAtFive()
        {
            SurfaceDamageState wall = new SurfaceDamageState(3);
            Assert.That(wall.RegisterHit(), Is.True);
            wall.RegisterHit();
            wall.RegisterHit();
            Assert.That(wall.Hits, Is.EqualTo(3));
            Assert.That(wall.RegisterHit(), Is.False);
            Assert.That(wall.IsCapped, Is.True);

            SurfaceDamageState window = new SurfaceDamageState(5, revealBrickAtCap: true);
            for (int i = 0; i < 4; i++)
            {
                window.RegisterHit();
            }

            Assert.That(window.BrickRevealed, Is.False);
            window.RegisterHit();
            Assert.That(window.BrickRevealed, Is.True);
        }

        [Test]
        public void ClockTwitchTicksTwicePerSecondUntilSilenced()
        {
            ClockTwitchState twitch = new ClockTwitchState(0.5f);
            Assert.That(twitch.ConsumeTicks(0.49f), Is.EqualTo(0));
            Assert.That(twitch.ConsumeTicks(0.02f), Is.EqualTo(1));
            twitch.Silence();
            Assert.That(twitch.ConsumeTicks(1f), Is.EqualTo(0));
        }

        [Test]
        public void SurrenderRequiresStandingEmptyFacingAndEnteredKiller()
        {
            Assert.That(SurrenderGate.CanSurrender(true, true, true, 0.8f), Is.True);
            Assert.That(SurrenderGate.CanSurrender(false, true, true, 0.8f), Is.False);
            Assert.That(SurrenderGate.CanSurrender(true, false, true, 0.8f), Is.False);
            Assert.That(SurrenderGate.CanSurrender(true, true, false, 0.8f), Is.False);
            Assert.That(SurrenderGate.CanSurrender(true, true, true, 0.1f), Is.False);
        }

        [Test]
        public void CorridorResistanceKillsInwardMotionAndPushesBack()
        {
            Vector3 filtered = CorridorResistance.FilterMove(Vector3.right, Vector3.right, 0f, 0.8f);
            Assert.That(filtered.x, Is.LessThan(0f));
            Vector3 along = CorridorResistance.FilterMove(Vector3.forward, Vector3.right, 0f, 0f);
            Assert.That(along.z, Is.EqualTo(1f).Within(0.01f));
        }
    }
}
