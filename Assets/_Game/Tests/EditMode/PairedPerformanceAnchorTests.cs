using ConGame.Domain.Choreography;
using NUnit.Framework;

namespace ConGame.Tests.EditMode
{
    public sealed class PairedPerformanceAnchorTests
    {
        [Test]
        public void DefaultToleranceMatchesSpec()
        {
            PairedAnchorTolerance tolerance = PairedAnchorTolerance.Default;

            Assert.That(tolerance.MaxPositionError, Is.EqualTo(0.15f).Within(1e-6f));
            Assert.That(tolerance.MaxYawErrorDegrees, Is.EqualTo(10f).Within(1e-6f));
        }

        [Test]
        public void DistanceUsesFull3D()
        {
            var a = new PairedAnchorPose(0f, 0f, 0f, 0f);
            var b = new PairedAnchorPose(0.1f, 0.05f, 0.1f, 0f);

            float distance = PairedAnchorPose.Distance(a, b);

            Assert.That(distance, Is.EqualTo(0.15f).Within(1e-5f));
        }

        [Test]
        public void YawDeltaWrapsAroundZero()
        {
            Assert.That(PairedAnchorPose.YawDeltaDegrees(350f, 10f), Is.EqualTo(20f).Within(1e-4f));
            Assert.That(PairedAnchorPose.YawDeltaDegrees(-170f, 170f), Is.EqualTo(20f).Within(1e-4f));
            Assert.That(PairedAnchorPose.YawDeltaDegrees(45f, 45f), Is.EqualTo(0f).Within(1e-4f));
        }

        [Test]
        public void ExactlyAtToleranceStillPasses()
        {
            PairedAnchorPose expected = new PairedAnchorPose(0f, 0f, 0f, 0f);
            PairedAnchorPose actual = new PairedAnchorPose(0.15f, 0f, 0f, 10f);
            PairedAnchorPose killer = new PairedAnchorPose(0f, 0f, 1f, 180f);

            PairedAnchorValidationResult result = PairedAnchorValidator.Validate(
                expected, killer, actual, killer, PairedAnchorTolerance.Default);

            Assert.That(result.IsWithinTolerance, Is.True);
            Assert.That(result.Failure, Is.EqualTo(PairedAnchorFailure.None));
        }

        [Test]
        public void PlayerPositionBeyondToleranceFails()
        {
            PairedAnchorPose expected = new PairedAnchorPose(0f, 0f, 0f, 0f);
            PairedAnchorPose actual = new PairedAnchorPose(0.151f, 0f, 0f, 0f);
            PairedAnchorPose killer = new PairedAnchorPose(0f, 0f, 1f, 180f);

            PairedAnchorValidationResult result = PairedAnchorValidator.Validate(
                expected, killer, actual, killer, PairedAnchorTolerance.Default);

            Assert.That(result.IsWithinTolerance, Is.False);
            Assert.That(result.Failure, Is.EqualTo(PairedAnchorFailure.PlayerPosition));
            Assert.That(result.PlayerPositionError, Is.GreaterThan(0.15f));
        }

        [Test]
        public void KillerYawBeyondToleranceFails()
        {
            PairedAnchorPose player = new PairedAnchorPose(0f, 0f, 0f, 0f);
            PairedAnchorPose expectedKiller = new PairedAnchorPose(0f, 0f, 1f, 180f);
            PairedAnchorPose actualKiller = new PairedAnchorPose(0f, 0f, 1f, 169f);

            PairedAnchorValidationResult result = PairedAnchorValidator.Validate(
                player, expectedKiller, player, actualKiller, PairedAnchorTolerance.Default);

            Assert.That(result.IsWithinTolerance, Is.False);
            Assert.That(result.Failure, Is.EqualTo(PairedAnchorFailure.KillerYaw));
            Assert.That(result.KillerYawError, Is.EqualTo(11f).Within(1e-4f));
        }

        [Test]
        public void SeparationMismatchFailsEvenWhenBothNearOwnAnchors()
        {
            PairedAnchorPose expectedPlayer = new PairedAnchorPose(0f, 0f, 0f, 0f);
            PairedAnchorPose expectedKiller = new PairedAnchorPose(0f, 0f, 1f, 180f);
            PairedAnchorPose actualPlayer = new PairedAnchorPose(0f, 0f, -0.14f, 0f);
            PairedAnchorPose actualKiller = new PairedAnchorPose(0f, 0f, 1.14f, 180f);

            PairedAnchorValidationResult result = PairedAnchorValidator.Validate(
                expectedPlayer, expectedKiller, actualPlayer, actualKiller, PairedAnchorTolerance.Default);

            Assert.That(result.Failure, Is.EqualTo(PairedAnchorFailure.Separation));
            Assert.That(result.SeparationError, Is.EqualTo(0.28f).Within(1e-4f));
        }

        [Test]
        public void PlayerFailureTakesPriorityOverKillerFailure()
        {
            PairedAnchorPose expected = new PairedAnchorPose(0f, 0f, 0f, 0f);
            PairedAnchorPose actual = new PairedAnchorPose(1f, 0f, 0f, 90f);
            PairedAnchorPose killer = new PairedAnchorPose(0f, 0f, 2f, 45f);

            PairedAnchorValidationResult result = PairedAnchorValidator.Validate(
                expected, killer, actual, killer, PairedAnchorTolerance.Default);

            Assert.That(result.Failure, Is.EqualTo(PairedAnchorFailure.PlayerPosition));
        }
    }
}
