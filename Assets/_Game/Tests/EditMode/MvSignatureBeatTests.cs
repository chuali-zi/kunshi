using ConGame.Domain.Facts;
using ConGame.Domain.Flow;
using ConGame.Domain.Interaction;
using ConGame.Presentation.CRT;
using ConGame.Presentation.Vfx;
using ConGame.Runtime.Ending;
using ConGame.Runtime.Events;
using ConGame.Runtime.Facts;
using ConGame.Runtime.Flow;
using ConGame.Runtime.Interaction;
using ConGame.Runtime.Loop;
using NUnit.Framework;
using UnityEngine;

namespace ConGame.Tests.EditMode
{
    public sealed class MvSignatureBeatTests
    {
        private GameObject _root;
        private MvSignaturePresenter _presenter;
        private MvSignatureSettings _settings;
        private GameFlowController _flow;
        private GameplayFactService _facts;
        private GameplayEventHub _events;
        private TelevisionController _tv;
        private SemanticVfxDirector _vfx;
        private CrtFullscreenPass _pass;
        private Material _material;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("MV beat tests");
            _settings = ScriptableObject.CreateInstance<MvSignatureSettings>();
            _flow = _root.AddComponent<GameFlowController>();
            _flow.Initialize(GamePhase.FreeRoam);
            _facts = _root.AddComponent<GameplayFactService>();
            _facts.CaptureInitialState();
            _events = _root.AddComponent<GameplayEventHub>();
            _tv = _root.AddComponent<TelevisionController>();
            _tv.Configure(_facts, _events, _flow);
            _vfx = _root.AddComponent<SemanticVfxDirector>();
            _vfx.Configure(null, null, null, null, _root.transform, null);
            _pass = _root.AddComponent<CrtFullscreenPass>();
            _material = new Material(Shader.Find("ConGame/CRT/FullscreenOverlay"));
            _pass.Configure(null, _material);
            _presenter = _root.AddComponent<MvSignaturePresenter>();
            Configure();
        }

        private void Configure() => _presenter.Configure(_settings, _flow, null, _facts, _events,
            null, _tv, _vfx, _pass);

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_settings);
            Object.DestroyImmediate(_material);
        }

        [TestCase(RoomLightState.Lit, 0f)]
        [TestCase(RoomLightState.Dim, 0f)]
        [TestCase(RoomLightState.RecoverableDark, 1f)]
        [TestCase(RoomLightState.PersistentBlack, 1f)]
        public void GradeFollowsAuthoritativeLightWithoutChangingFacts(RoomLightState state, float expected)
        {
            _facts.SetRoomLight(state);
            _presenter.Advance(0f);
            Assert.That(_material.GetFloat("_DarkAmount"), Is.EqualTo(expected));
            Assert.That(_facts.CaptureSnapshot().RoomLight, Is.EqualTo(state));
        }

        [Test]
        public void DeathTitleFitsInsideKillImpactAndNeverRequestsReset()
        {
            _flow.Initialize(GamePhase.Counterplay);
            Assert.That(_flow.Request(GamePhase.KillImpact, TransitionPriority.Death, "test"), Is.True);
            _flow.CommitPending();
            _presenter.Advance(0.2f);
            Assert.That(_presenter.TitleAmount, Is.Zero);
            _presenter.Advance(0.15f);
            Assert.That(_presenter.TitleAmount, Is.EqualTo(1f));
            Assert.That(_flow.CurrentPhase, Is.EqualTo(GamePhase.KillImpact));
            Assert.That(_flow.CommitPending(), Is.False);
            _flow.Request(GamePhase.LoopReset, TransitionPriority.Normal, "test");
            _flow.CommitPending();
            Assert.That(_presenter.TitleAmount, Is.Zero);
            Assert.That(_presenter.MuzzleLight.enabled, Is.False);
        }

        [Test]
        public void CameraReceiverAcceptsOnlyBulletsAndOtherRoomImpactsDoNotFractureScreen()
        {
            var bullet = new HitInfo(ImpactKind.Bullet, "pistol_main", Vector3.zero, Vector3.forward, 1f);
            _events.EmitImpact(bullet, HitResult.Accepted, _tv);
            Assert.That(_presenter.ScreenCrackAmount, Is.Zero);
            Assert.That(_presenter.ReceiveHit(new HitInfo(ImpactKind.Heavy, "bat", Vector3.zero,
                Vector3.forward, 1f)), Is.EqualTo(HitResult.Ignored));
            Assert.That(_presenter.ReceiveHit(bullet), Is.EqualTo(HitResult.Accepted));
            Assert.That(_presenter.ScreenCrackAmount, Is.EqualTo(0.7f));
            Assert.That(_tv.DamageState, Is.EqualTo(TelevisionDamageState.Intact));
            _presenter.Advance(5f);
            Assert.That(_material.GetFloat("_ScreenCrack"), Is.EqualTo(0.7f), "Glass holds until the loop resets.");
        }

        [Test]
        public void FiftyResetsDiscardOldEpochHitsAndClearAllTransientEffects()
        {
            for (int epoch = 0; epoch < 50; epoch++)
            {
                _presenter.NotifyCameraBullet(epoch);
                _vfx.NotifyShotFired();
                var reset = new LoopResetContext(epoch, epoch + 1);
                _presenter.Quiesce(reset);
                _presenter.Restore(reset);
                _presenter.Resume(reset);
                _presenter.NotifyCameraBullet(epoch);
                Assert.That(_presenter.ScreenCrackAmount, Is.Zero);
                Assert.That(_presenter.TitleAmount, Is.Zero);
                Assert.That(_presenter.CutInAmount, Is.Zero);
                Assert.That(_presenter.MuzzleLight.enabled, Is.False);
                Assert.That(_root.GetComponentsInChildren<Light>(true).Length, Is.EqualTo(1));
            }
            _presenter.NotifyCameraBullet(50);
            Assert.That(_presenter.ScreenCrackAmount, Is.EqualTo(0.7f));
        }

        [Test]
        public void FirstHeavyTelevisionHitShowsRealisationOnceAndKeepsGameplayRunning()
        {
            _tv.ReceiveHit(new HitInfo(ImpactKind.Light, "book", Vector3.zero, Vector3.forward, 1f));
            Assert.That(_presenter.CutInAmount, Is.Zero);
            _tv.ReceiveHit(new HitInfo(ImpactKind.Heavy, "baseball_bat", Vector3.zero, Vector3.forward, 1f));
            Assert.That(_presenter.CutInAmount, Is.EqualTo(1f));
            Assert.That(_flow.CurrentPhase, Is.EqualTo(GamePhase.FreeRoam));
            Assert.That(_facts.CaptureSnapshot().TelevisionDamage, Is.EqualTo(TelevisionDamageState.Cracked));
            _presenter.Advance(_settings.RealisationSeconds);
            Assert.That(_presenter.CutInAmount, Is.Zero);
            _tv.Restore(new LoopResetContext(0, 1));
            _tv.ReceiveHit(new HitInfo(ImpactKind.Heavy, "baseball_bat", Vector3.zero, Vector3.forward, 1f));
            Assert.That(_presenter.CutInAmount, Is.Zero, "Repeated notifications cannot retrigger the panel.");
        }

        [Test]
        public void GameTimeZeroFreezesCutInFlashAndTitle()
        {
            _tv.ReceiveHit(new HitInfo(ImpactKind.Heavy, "bat", Vector3.zero, Vector3.forward, 1f));
            _vfx.NotifyShotFired();
            _presenter.Advance(0f);
            _presenter.Advance(0f);
            Assert.That(_presenter.CutInAmount, Is.EqualTo(1f));
            Assert.That(_presenter.MuzzleLight.enabled, Is.True);
            Assert.That(_presenter.MuzzleLight.intensity, Is.EqualTo(_settings.MuzzleLightIntensity));
        }

        [Test]
        public void SemanticMuzzleLightUsesShotOriginAndExpiresWithoutDependingOnParticlePool()
        {
            Configure(); // Idempotent installation must not double-subscribe camera hits or shot events.
            _root.transform.position = new Vector3(3f, 1.4f, 0.8f);
            _vfx.NotifyShotFired();
            Assert.That(_presenter.MuzzleLight.enabled, Is.True);
            Assert.That(_presenter.MuzzleLight.transform.position, Is.EqualTo(_root.transform.position));
            _presenter.Advance(_settings.MuzzleLightSeconds + 0.01f);
            Assert.That(_presenter.MuzzleLight.enabled, Is.False);
        }

        [Test]
        public void EndingTransitionClearsDeathCutInAndCameraGlass()
        {
            _presenter.NotifyCameraBullet(0);
            _tv.ReceiveHit(new HitInfo(ImpactKind.Heavy, "bat", Vector3.zero, Vector3.forward, 1f));
            _tv.ReceiveHit(new HitInfo(ImpactKind.Heavy, "bat", Vector3.zero, Vector3.forward, 1f));
            _flow.CommitPending();
            Assert.That(_flow.CurrentPhase, Is.EqualTo(GamePhase.EndingE1));
            Assert.That(_presenter.CutInAmount, Is.Zero);
            Assert.That(_presenter.ScreenCrackAmount, Is.Zero);
            Assert.That(_tv.DamageState, Is.EqualTo(TelevisionDamageState.Destroyed));
        }

        [Test]
        public void ProductionArtAndShaderAreAvailable()
        {
            var config = Resources.Load<MvSignatureSettings>("MvSignatureSettings");
            Assert.That(config, Is.Not.Null);
            Assert.That(config.Title, Is.Not.Null);
            Assert.That(config.Realisation, Is.Not.Null);
            Assert.That(config.Glass, Is.Not.Null);
            Assert.That(config.FlashlightAir.shader.isSupported, Is.True);
            Assert.That(_material.shader.isSupported, Is.True);
        }
    }
}
