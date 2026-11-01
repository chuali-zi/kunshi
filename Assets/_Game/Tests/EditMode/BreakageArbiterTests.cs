using ConGame.Domain.Interaction;
using NUnit.Framework;

namespace ConGame.Tests.EditMode
{

    public sealed class BreakageArbiterTests
    {
        [Test]
        public void HeavyImpactCommitsBreakageExactlyOnce()
        {
            BreakageArbiter arbiter = new BreakageArbiter();

            Assert.That(arbiter.TryCommitImpact(ImpactKind.Heavy, 5f), Is.True);
            Assert.That(arbiter.IsBroken, Is.True);
            Assert.That(arbiter.TryCommitImpact(ImpactKind.Heavy, 5f), Is.False);
            Assert.That(arbiter.TryCommitLanding(10f), Is.False);
        }

        [Test]
        public void LightImpactIsIgnoredUnlessAccepted()
        {
            BreakageArbiter fragile = new BreakageArbiter();
            BreakageArbiter lightAccepting = new BreakageArbiter(acceptsLightImpacts: true);

            Assert.That(fragile.TryCommitImpact(ImpactKind.Light, 5f), Is.False);
            Assert.That(fragile.IsBroken, Is.False);
            Assert.That(lightAccepting.TryCommitImpact(ImpactKind.Light, 5f), Is.True);
        }

        [Test]
        public void ImpactBelowMinimumForceIsIgnored()
        {
            BreakageArbiter arbiter = new BreakageArbiter(minimumForce: 4f);

            Assert.That(arbiter.TryCommitImpact(ImpactKind.Projectile, 3.9f), Is.False);
            Assert.That(arbiter.TryCommitImpact(ImpactKind.Projectile, 4f), Is.True);
        }

        [Test]
        public void LandingShatterRequiresOptInAndSpeed()
        {
            BreakageArbiter solid = new BreakageArbiter();
            BreakageArbiter vase = new BreakageArbiter(shattersOnLanding: true, minimumLandingSpeed: 1.5f);

            Assert.That(solid.TryCommitLanding(10f), Is.False);
            Assert.That(vase.TryCommitLanding(1.4f), Is.False);
            Assert.That(vase.TryCommitLanding(1.5f), Is.True);
            Assert.That(vase.IsBroken, Is.True);
        }

        [Test]
        public void ResetStateAllowsBreakingAgainAfterLoopRestore()
        {
            BreakageArbiter arbiter = new BreakageArbiter(shattersOnLanding: true, minimumLandingSpeed: 1f);
            arbiter.TryCommitLanding(2f);
            Assert.That(arbiter.IsBroken, Is.True);

            arbiter.ResetState();

            Assert.That(arbiter.IsBroken, Is.False);
            Assert.That(arbiter.TryCommitImpact(ImpactKind.Heavy, 1f), Is.True);
        }

        [Test]
        public void ShardZoneSpecIsCarriedAndClamped()
        {
            ShardSlowZoneSpec spec = new ShardSlowZoneSpec(0.45f, 2f, 2f);
            BreakageArbiter withZone = new BreakageArbiter(shardZone: spec);
            BreakageArbiter withoutZone = new BreakageArbiter();

            Assert.That(withZone.ShardZone.HasValue, Is.True);
            Assert.That(withZone.ShardZone.Value.Radius, Is.EqualTo(0.45f).Within(0.001f));
            Assert.That(withZone.ShardZone.Value.MovementMultiplier, Is.EqualTo(1f).Within(0.001f));
            Assert.That(withZone.ShardZone.Value.LimpSeconds, Is.EqualTo(2f).Within(0.001f));
            Assert.That(withoutZone.ShardZone.HasValue, Is.False);
        }
    }
}
