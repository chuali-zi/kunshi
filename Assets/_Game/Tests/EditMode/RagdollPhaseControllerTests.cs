using ConGame.Presentation.Animation.Rigging;
using NUnit.Framework;

namespace ConGame.Tests.EditMode
{
    public sealed class RagdollPhaseControllerTests
    {
        [Test]
        public void HitTransitionsToRagdollAfterReactWindow()
        {
            RagdollPhaseController controller = new RagdollPhaseController();
            controller.Configure(0.35f);

            Assert.That(controller.BeginHit(), Is.True);
            Assert.That(controller.Phase, Is.EqualTo(RagdollPhase.HitReact));
            Assert.That(controller.Tick(0.2f), Is.False);
            Assert.That(controller.Phase, Is.EqualTo(RagdollPhase.HitReact));
            Assert.That(controller.Tick(0.15f), Is.True, "累计达到 0.35s 的当帧切入 Ragdoll");
            Assert.That(controller.Phase, Is.EqualTo(RagdollPhase.Ragdoll));
            Assert.That(controller.Tick(1f), Is.False, "已进入 Ragdoll 后不再重复切换");
        }

        [Test]
        public void BeginHitIsIdempotentWhileEngaged()
        {
            RagdollPhaseController controller = new RagdollPhaseController();
            controller.Configure(0.35f);

            Assert.That(controller.BeginHit(), Is.True);
            Assert.That(controller.BeginHit(), Is.False, "接管流程中不得重复触发");
        }

        [Test]
        public void ResetRestoresAnimatedPhaseForLoopReset()
        {
            RagdollPhaseController controller = new RagdollPhaseController();
            controller.Configure(0.35f);
            controller.BeginHit();
            controller.Tick(1f);

            controller.Reset();

            Assert.That(controller.Phase, Is.EqualTo(RagdollPhase.Animated));
            Assert.That(controller.BeginHit(), Is.True, "复原后必须能再次接管");
        }

        [Test]
        public void ReactWindowNeverExceedsManifestLimit()
        {
            // animation_manifest §7: 中弹动画进入 Ragdoll 前不得超过 0.5s。
            AnimationRigSettingsAsset settings =
                UnityEngine.ScriptableObject.CreateInstance<AnimationRigSettingsAsset>();
            try
            {
                settings.ragdollHitReactSeconds = 5f;
                settings.Normalize();

                Assert.That(settings.ragdollHitReactSeconds, Is.LessThanOrEqualTo(0.5f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }
    }
}
