using ConGame.Presentation.Animation.Rigging;
using NUnit.Framework;

namespace ConGame.Tests.EditMode
{
    public sealed class WeightBlenderTests
    {
        [Test]
        public void BlendReachesTargetInConfiguredSeconds()
        {
            WeightBlender blender = new WeightBlender();
            blender.SetTarget(1f, 0.2f);

            blender.Tick(0.1f);
            Assert.That(blender.Current, Is.EqualTo(0.5f).Within(0.001f));
            blender.Tick(0.1f);
            Assert.That(blender.Current, Is.EqualTo(1f).Within(0.001f));
            Assert.That(blender.IsSettled, Is.True);
        }

        [Test]
        public void ZeroBlendTimeSnapsImmediately()
        {
            WeightBlender blender = new WeightBlender();
            blender.SetTarget(1f, 0f);

            Assert.That(blender.Tick(0f), Is.EqualTo(1f));
        }

        [Test]
        public void RetargetingMidBlendClampsAndSettles()
        {
            WeightBlender blender = new WeightBlender();
            blender.SetTarget(1f, 0.4f);
            blender.Tick(0.1f);
            blender.SetTarget(0f, 0.5f);

            blender.Tick(0.05f);
            Assert.That(blender.Current, Is.EqualTo(0.15f).Within(0.001f));
            blender.Tick(0.05f);
            blender.Tick(0.05f);
            Assert.That(blender.Current, Is.EqualTo(0f));
            blender.Tick(0.5f);
            Assert.That(blender.Current, Is.EqualTo(0f), "权重不得越过 0");
        }

        [Test]
        public void SnapToOverridesInFlightBlend()
        {
            WeightBlender blender = new WeightBlender();
            blender.SetTarget(1f, 1f);
            blender.Tick(0.2f);

            blender.SnapTo(0f);

            Assert.That(blender.Current, Is.EqualTo(0f));
            Assert.That(blender.Target, Is.EqualTo(0f));
            Assert.That(blender.Tick(0.2f), Is.EqualTo(0f));
        }
    }
}
