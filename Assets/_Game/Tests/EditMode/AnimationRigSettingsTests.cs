using ConGame.Presentation.Animation.Rigging;
using NUnit.Framework;
using UnityEngine;

namespace ConGame.Tests.EditMode
{
    public sealed class AnimationRigSettingsTests
    {
        private AnimationRigSettingsAsset _settings;

        [SetUp]
        public void SetUp()
        {
            _settings = ScriptableObject.CreateInstance<AnimationRigSettingsAsset>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_settings);
        }

        [Test]
        public void NormalizeClampsBlendTimesIntoValidRange()
        {
            _settings.aimBlendInSeconds = -1f;
            _settings.aimBlendOutSeconds = 99f;
            _settings.upperBodyBlendInSeconds = 0f;
            _settings.gripBlendSeconds = 50f;
            _settings.footPlantBlendSeconds = 0.2f;

            _settings.Normalize();

            Assert.That(_settings.aimBlendInSeconds, Is.EqualTo(0.01f));
            Assert.That(_settings.aimBlendOutSeconds, Is.EqualTo(2f));
            Assert.That(_settings.upperBodyBlendInSeconds, Is.EqualTo(0.01f));
            Assert.That(_settings.gripBlendSeconds, Is.EqualTo(2f));
            Assert.That(_settings.footPlantBlendSeconds, Is.EqualTo(0.2f));
        }

        [Test]
        public void DefaultsSatisfyManifestReactBudget()
        {
            _settings.Normalize();

            Assert.That(_settings.ragdollHitReactSeconds, Is.GreaterThan(0f));
            Assert.That(_settings.ragdollHitReactSeconds, Is.LessThanOrEqualTo(0.5f));
        }
    }
}
