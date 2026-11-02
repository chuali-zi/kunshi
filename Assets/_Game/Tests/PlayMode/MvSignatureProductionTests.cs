using System.Collections;
using ConGame.Domain.Facts;
using ConGame.Domain.Flow;
using ConGame.Domain.Interaction;
using ConGame.Presentation.CRT;
using ConGame.Presentation.Lighting;
using ConGame.Presentation.Vfx;
using ConGame.Runtime.AI;
using ConGame.Runtime.Facts;
using ConGame.Runtime.Flow;
using ConGame.Runtime.Interaction;
using ConGame.Runtime.Lighting;
using ConGame.Runtime.Loop;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace ConGame.Tests.PlayMode
{
    public sealed class MvSignatureProductionTests
    {
        private Scene _room;

        [UnityTearDown]
        public IEnumerator Unload()
        {
            if (_room.IsValid() && _room.isLoaded) yield return SceneManager.UnloadSceneAsync(_room);
        }

        [UnityTest]
        public IEnumerator ProductionCompositorRendersAcrossTheWholeCameraTarget()
        {
            yield return SceneManager.LoadSceneAsync("Room_M1", LoadSceneMode.Additive);
            _room = SceneManager.GetSceneByName("Room_M1");
            yield return null;
            var root = _room.GetRootGameObjects()[0];
            var crt = root.GetComponentInChildren<CrtFullscreenPass>(true);
            var camera = root.GetComponentInChildren<Camera>(true);
            crt.SetBlackout(0f);
            crt.SendMessage("LateUpdate");
            crt.SetSignatureState(0f, 0f, 0f, 1f);
            var target = new RenderTexture(640, 360, 24);
            var pixels = new Texture2D(640, 360, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                target.Create();
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 640, 360), 0, 0);
                pixels.Apply();
                // Both ends of the diagonal white manga panel must render, including the old shadow-scissor area.
                Assert.That(pixels.GetPixel(100, 180).grayscale, Is.GreaterThan(0.7f));
                Assert.That(pixels.GetPixel(600, 180).grayscale, Is.GreaterThan(0.7f));
            }
            finally
            {
                RenderTexture.active = previous;
                target.Release();
                Object.Destroy(target);
                Object.Destroy(pixels);
            }
        }

        [UnityTest]
        public IEnumerator ProductionDarkEntryMuzzleAndTitleCleanUpOnRealReset()
        {
            yield return SceneManager.LoadSceneAsync("Room_M1", LoadSceneMode.Additive);
            _room = SceneManager.GetSceneByName("Room_M1");
            yield return null;
            var root = _room.GetRootGameObjects()[0];
            var presenter = root.GetComponentInChildren<MvSignaturePresenter>(true);
            var facts = root.GetComponentInChildren<GameplayFactService>(true);
            var flow = root.GetComponentInChildren<GameFlowController>(true);
            var loop = root.GetComponentInChildren<LoopDirector>(true);
            var killer = root.GetComponentInChildren<KillerDirector>(true);
            var vfx = root.GetComponentInChildren<SemanticVfxDirector>(true);
            Assert.That(presenter.Settings.Title, Is.Not.Null);
            var crt = root.GetComponentInChildren<CrtFullscreenPass>(true);
            Assert.That(crt.Material.GetTexture("_TitleTex"), Is.SameAs(presenter.Settings.Title));
            int epoch = loop.LoopEpoch;
            foreach (var light in root.GetComponentsInChildren<BreakableLight>(true))
                light.ReceiveHit(new HitInfo(ImpactKind.Heavy, "bat", light.transform.position, Vector3.up, 10f));
            yield return null;
            Assert.That(facts.CaptureSnapshot().RoomLight, Is.EqualTo(RoomLightState.PersistentBlack));
            Assert.That(presenter.DarkAmount, Is.EqualTo(1f));
            flow.Request(GamePhase.Breach, TransitionPriority.Normal, "signature acceptance");
            flow.CommitPending();
            float until = Time.time + 6f;
            while (!killer.IsLiveHuntActive && Time.time < until) yield return null;
            Assert.That(killer.IsLiveHuntActive, Is.True);
            Assert.That(killer.transform.position.x, Is.GreaterThan(1f), "Entry remains at the right-hand door.");
            var beam = killer.GetComponentInChildren<MvFlashlightBeam>(true);
            Assert.That(beam, Is.Not.Null);
            yield return null;
            Assert.That(beam.GetComponent<Light>().enabled, Is.True);
            Assert.That(beam.GetComponentInChildren<MeshRenderer>(true).enabled, Is.True);
            vfx.NotifyShotFired();
            Assert.That(presenter.MuzzleLight.enabled, Is.True);
            Assert.That(presenter.MuzzleLight.type, Is.EqualTo(LightType.Point));
            Assert.That(presenter.MuzzleLight.intensity, Is.GreaterThan(0f));
            presenter.ReceiveHit(new HitInfo(ImpactKind.Bullet, "pistol_main", Vector3.zero, Vector3.forward, 1f));
            loop.BeginConfirmedPerformance();
            loop.RequestKill("D12");
            flow.CommitPending();
            yield return new WaitForSeconds(0.4f);
            Assert.That(flow.CurrentPhase, Is.EqualTo(GamePhase.KillImpact));
            Assert.That(presenter.TitleAmount, Is.GreaterThan(0.9f));
            until = Time.time + 2f;
            while (loop.LoopEpoch == epoch && Time.time < until) yield return null;
            yield return null;
            Assert.That(loop.LoopEpoch, Is.EqualTo(epoch + 1));
            Assert.That(flow.CurrentPhase, Is.EqualTo(GamePhase.Awakening));
            Assert.That(presenter.TitleAmount, Is.Zero);
            Assert.That(presenter.ScreenCrackAmount, Is.Zero);
            Assert.That(presenter.CutInAmount, Is.Zero);
            Assert.That(presenter.DarkAmount, Is.Zero);
            Assert.That(presenter.MuzzleLight.enabled, Is.False);
            Assert.That(beam.GetComponent<Light>().enabled, Is.False);
            Assert.That(facts.CaptureSnapshot().RoomLight, Is.EqualTo(RoomLightState.Lit));
        }
    }
}
