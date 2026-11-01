using ConGame.Runtime.Ending;
using NUnit.Framework;

namespace ConGame.Tests.EditMode
{

    public sealed class EndingFallMotionTests
    {
        [Test]
        public void EndpointsMatchSettings()
        {
            EndingSequenceSettings settings = EndingSequenceSettings.Default;

            EndingFallPose start = EndingFallMotion.Sample(0f, settings);
            Assert.That(start.PitchDownDegrees, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(start.HeightDropMeters, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(start.RollDegrees, Is.EqualTo(0f).Within(0.0001f));

            EndingFallPose end = EndingFallMotion.Sample(1f, settings);
            Assert.That(end.PitchDownDegrees, Is.EqualTo(settings.FallPitchDegrees).Within(0.001f));
            Assert.That(end.HeightDropMeters, Is.EqualTo(settings.FallHeightDropMeters).Within(0.001f));
            Assert.That(end.RollDegrees, Is.LessThanOrEqualTo(settings.FallRollDegrees * 0.3f));
        }

        [Test]
        public void PitchAndHeightAreMonotonic()
        {
            EndingSequenceSettings settings = EndingSequenceSettings.Default;
            EndingFallPose previous = EndingFallMotion.Sample(0f, settings);

            for (int i = 1; i <= 60; i++)
            {
                EndingFallPose current = EndingFallMotion.Sample(i / 60f, settings);
                Assert.That(current.PitchDownDegrees, Is.GreaterThanOrEqualTo(previous.PitchDownDegrees));
                Assert.That(current.HeightDropMeters, Is.GreaterThanOrEqualTo(previous.HeightDropMeters));
                previous = current;
            }
        }

        [Test]
        public void RollNeverExceedsConfiguredLimit()
        {
            EndingSequenceSettings settings = EndingSequenceSettings.Default;

            for (int i = 0; i <= 120; i++)
            {
                EndingFallPose pose = EndingFallMotion.Sample(i / 120f, settings);
                Assert.That(pose.RollDegrees, Is.GreaterThanOrEqualTo(0f));
                Assert.That(pose.RollDegrees, Is.LessThanOrEqualTo(settings.FallRollDegrees + 0.0001f));
            }
        }

        [Test]
        public void SamplesClampOutsideUnitInterval()
        {
            EndingSequenceSettings settings = EndingSequenceSettings.Default;

            EndingFallPose below = EndingFallMotion.Sample(-0.5f, settings);
            EndingFallPose atZero = EndingFallMotion.Sample(0f, settings);
            Assert.That(below.PitchDownDegrees, Is.EqualTo(atZero.PitchDownDegrees));
            Assert.That(below.HeightDropMeters, Is.EqualTo(atZero.HeightDropMeters));

            EndingFallPose above = EndingFallMotion.Sample(1.5f, settings);
            EndingFallPose atOne = EndingFallMotion.Sample(1f, settings);
            Assert.That(above.PitchDownDegrees, Is.EqualTo(atOne.PitchDownDegrees).Within(0.0001f));
            Assert.That(above.HeightDropMeters, Is.EqualTo(atOne.HeightDropMeters).Within(0.0001f));
        }

        [Test]
        public void NullSettingsFallsBackToDefault()
        {
            EndingFallPose pose = EndingFallMotion.Sample(1f, null);
            Assert.That(pose.PitchDownDegrees,
                Is.EqualTo(EndingSequenceSettings.Default.FallPitchDegrees).Within(0.001f));
        }
    }
}
