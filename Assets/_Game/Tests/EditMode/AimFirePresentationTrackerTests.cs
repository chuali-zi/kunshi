using ConGame.Runtime.AI;
using NUnit.Framework;

namespace ConGame.Tests.EditMode
{
    public sealed class AimFirePresentationTrackerTests
    {
        [Test]
        public void AimEnterFiresOnlyOncePerAimSession()
        {
            AimFirePresentationTracker tracker = new AimFirePresentationTracker();

            Assert.That(tracker.TryBeginAim(3), Is.True);
            Assert.That(tracker.Stage, Is.EqualTo(AimFirePresentationStage.AimEnter));
            Assert.That(tracker.TryBeginAim(3), Is.False, "E3/重试路径的重复 AimStarted 必须被吞掉");
        }

        [Test]
        public void HoldRequiresAimEnterAndFiresOnlyOnce()
        {
            AimFirePresentationTracker tracker = new AimFirePresentationTracker();

            Assert.That(tracker.TryEnterHold(3), Is.False, "未进入 Aim 不得发 AimHold");

            tracker.TryBeginAim(3);
            Assert.That(tracker.TryEnterHold(3), Is.True);
            Assert.That(tracker.Stage, Is.EqualTo(AimFirePresentationStage.AimHold));
            Assert.That(tracker.TryEnterHold(3), Is.False);
        }

        [Test]
        public void FireOnlyFromActiveAimSessionAndOnlyOnce()
        {
            AimFirePresentationTracker tracker = new AimFirePresentationTracker();

            Assert.That(tracker.TryCommitFire(3), Is.False, "无瞄准会话不得触发 Fire 语义");

            tracker.TryBeginAim(3);
            tracker.TryEnterHold(3);
            Assert.That(tracker.TryCommitFire(3), Is.True);
            Assert.That(tracker.Stage, Is.EqualTo(AimFirePresentationStage.Fire));
            Assert.That(tracker.TryCommitFire(3), Is.False, "同一发射击的 Fire 表现不得重复");
        }

        [Test]
        public void CancelOnlyWhileAimingAndBlocksLaterFire()
        {
            AimFirePresentationTracker tracker = new AimFirePresentationTracker();

            Assert.That(tracker.TryCancelAim(3), Is.False);

            tracker.TryBeginAim(3);
            Assert.That(tracker.TryCancelAim(3), Is.True);
            Assert.That(tracker.TryCancelAim(3), Is.False, "重复 AimCancelled 必须被吞掉");
            Assert.That(tracker.TryCommitFire(3), Is.False, "取消后不得再提交 Fire");
        }

        [Test]
        public void FireAfterCommittedIsNotCancellable()
        {
            AimFirePresentationTracker tracker = new AimFirePresentationTracker();
            tracker.TryBeginAim(3);
            tracker.TryCommitFire(3);

            Assert.That(tracker.TryCancelAim(3), Is.False, "已开火不得再发 AimCancelled");
        }

        [Test]
        public void EpochMismatchRejectsTransitions()
        {
            AimFirePresentationTracker tracker = new AimFirePresentationTracker();
            tracker.TryBeginAim(3);

            Assert.That(tracker.TryEnterHold(4), Is.False);
            Assert.That(tracker.TryCommitFire(4), Is.False);
            Assert.That(tracker.TryCancelAim(4), Is.False);
        }

        [Test]
        public void ResetAllowsNewAimSession()
        {
            AimFirePresentationTracker tracker = new AimFirePresentationTracker();
            tracker.TryBeginAim(3);
            tracker.TryCommitFire(3);

            tracker.Reset();
            Assert.That(tracker.Stage, Is.EqualTo(AimFirePresentationStage.Idle));
            Assert.That(tracker.TryBeginAim(4), Is.True);
        }

        [Test]
        public void AimFirePresentationSettingsValidatedFillsDefaults()
        {
            KillerAimFirePresentationSettings settings =
                new KillerAimFirePresentationSettings().Validated();

            Assert.That(settings.AimEnterClipKey, Is.EqualTo("AN_Killer_AimEnter"));
            Assert.That(settings.AimHoldClipKey, Is.EqualTo("AN_Killer_AimHold"));
            Assert.That(settings.FireClipKey, Is.EqualTo("AN_Killer_Fire"));
            Assert.That(settings.AimEnterFallbackSeconds, Is.GreaterThan(0f));
        }

        [TestCase(-1f)]
        [TestCase(0f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidAimEnterDurationUsesFiniteDefault(float duration)
        {
            var settings = new KillerAimFirePresentationSettings { AimEnterFallbackSeconds = duration }.Validated();
            Assert.That(settings.AimEnterFallbackSeconds,
                Is.EqualTo(KillerAimFirePresentationSettings.Default.AimEnterFallbackSeconds));
        }

        [Test]
        public void ValidAimEnterDurationIsPreserved()
        {
            var settings = new KillerAimFirePresentationSettings { AimEnterFallbackSeconds = 0.12f }.Validated();
            Assert.That(settings.AimEnterFallbackSeconds, Is.EqualTo(0.12f));
        }

        [Test]
        public void BreachKickTimingSettingsValidatedFillsDefaults()
        {
            BreachKickTimingSettings settings = new BreachKickTimingSettings().Validated();

            Assert.That(settings.WindupSeconds, Is.GreaterThan(0f));
            Assert.That(settings.ContactFallbackSeconds, Is.GreaterThan(0f));
        }
    }
}
