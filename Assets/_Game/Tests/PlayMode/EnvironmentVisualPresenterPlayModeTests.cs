using System.Collections;
using ConGame.Domain.Facts;
using ConGame.Domain.Interaction;
using ConGame.Presentation.CRT;
using ConGame.Presentation.Interaction;
using ConGame.Presentation.Lighting;
using ConGame.Runtime.Ending;
using ConGame.Runtime.Events;
using ConGame.Runtime.Interaction;
using ConGame.Runtime.Lighting;
using ConGame.Runtime.Loop;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ConGame.Tests.PlayMode
{
    public sealed class EnvironmentVisualPresenterPlayModeTests
    {
        [UnityTest]
        public IEnumerator TelevisionDamageActivatesMatchingVisualAndLoopResetRestoresIntact()
        {
            GameObject root = new GameObject("Television Presenter Test");
            LoopResetRegistry registry = root.AddComponent<LoopResetRegistry>();
            TelevisionController television = root.AddComponent<TelevisionController>();
            CrtStatePresenter presenter = root.AddComponent<CrtStatePresenter>();
            GameObject intact = CreateVisual("Intact", root.transform);
            GameObject cracked = CreateVisual("Cracked", root.transform);
            GameObject destroyed = CreateVisual("Destroyed", root.transform);

            television.Configure(null, null, null);
            presenter.Configure(television, null, null, null, intact, cracked, destroyed);
            registry.Configure(root.transform);
            registry.CaptureInitialState();

            AssertVisuals(intact, cracked, destroyed, true, false, false);

            HitInfo hit = new HitInfo(ImpactKind.Heavy, "baseball_bat", Vector3.zero, Vector3.forward, 6f);
            Assert.That(television.ReceiveHit(hit), Is.EqualTo(HitResult.Accepted));
            AssertVisuals(intact, cracked, destroyed, false, true, false);

            Assert.That(television.ReceiveHit(hit), Is.EqualTo(HitResult.EndingTriggered));
            AssertVisuals(intact, cracked, destroyed, false, false, true);

            registry.ResetAll(1, 2);
            Assert.That(television.DamageState, Is.EqualTo(TelevisionDamageState.Intact));
            AssertVisuals(intact, cracked, destroyed, true, false, false);

            Object.Destroy(root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator BreakableLightDamageActivatesBrokenVisualAndLoopResetRestoresIntact()
        {
            GameObject root = new GameObject("Light Presenter Test");
            LoopResetRegistry registry = root.AddComponent<LoopResetRegistry>();
            Light targetLight = root.AddComponent<Light>();
            BreakableLight lightState = root.AddComponent<BreakableLight>();
            BreakableLightPresenter presenter = root.AddComponent<BreakableLightPresenter>();
            GameObject intact = CreateVisual("Intact", root.transform);
            GameObject broken = CreateVisual("Broken", root.transform);

            lightState.Configure(targetLight);
            presenter.Configure(lightState, intact, broken);
            registry.Configure(root.transform);
            registry.CaptureInitialState();

            Assert.That(intact.activeSelf, Is.True);
            Assert.That(broken.activeSelf, Is.False);
            Assert.That(targetLight.enabled, Is.True);

            HitInfo hit = new HitInfo(ImpactKind.Heavy, "baseball_bat", Vector3.zero, Vector3.forward, 6f);
            Assert.That(lightState.ReceiveHit(hit), Is.EqualTo(HitResult.Broke));
            Assert.That(intact.activeSelf, Is.False);
            Assert.That(broken.activeSelf, Is.True);
            Assert.That(targetLight.enabled, Is.False);

            registry.ResetAll(1, 2);
            Assert.That(lightState.IsBroken, Is.False);
            Assert.That(intact.activeSelf, Is.True);
            Assert.That(broken.activeSelf, Is.False);
            Assert.That(targetLight.enabled, Is.True);

            Object.Destroy(root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator WorldItemBrokenStateSwitchesVisualAndLoopResetRestoresIntact()
        {
            GameObject root = new GameObject("World Item Presenter Test");
            LoopResetRegistry registry = root.AddComponent<LoopResetRegistry>();
            StatefulWorldItem item = root.AddComponent<StatefulWorldItem>();
            WorldItemVisualPresenter presenter = root.AddComponent<WorldItemVisualPresenter>();
            GameObject intact = CreateVisual("Intact", root.transform);
            GameObject broken = CreateVisual("Broken", root.transform);

            item.Configure(null);
            presenter.Configure(item, intact, broken);
            registry.Configure(root.transform);
            registry.CaptureInitialState();

            Assert.That(intact.activeSelf, Is.True);
            Assert.That(broken.activeSelf, Is.False);

            item.MarkBroken();
            Assert.That(intact.activeSelf, Is.False);
            Assert.That(broken.activeSelf, Is.True);

            registry.ResetAll(1, 2);
            Assert.That(item.CurrentState, Is.EqualTo(ItemPhysicalState.Placed));
            Assert.That(intact.activeSelf, Is.True);
            Assert.That(broken.activeSelf, Is.False);

            Object.Destroy(root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ImpactDamageAdvancesVisualPairsAndLoopResetRestoresThem()
        {
            GameObject root = new GameObject("Impact Damage Presenter Test");
            LoopResetRegistry registry = root.AddComponent<LoopResetRegistry>();
            GameplayEventHub events = root.AddComponent<GameplayEventHub>();
            StrikeSurfaceHitReceiver receiver = root.AddComponent<StrikeSurfaceHitReceiver>();
            ImpactDamageVisualPresenter presenter = root.AddComponent<ImpactDamageVisualPresenter>();
            GameObject[] intact =
            {
                CreateVisual("Intact 1", root.transform),
                CreateVisual("Intact 2", root.transform)
            };
            GameObject[] broken =
            {
                CreateVisual("Broken 1", root.transform),
                CreateVisual("Broken 2", root.transform)
            };

            presenter.Configure(events, receiver, 2, intact, broken);
            registry.Configure(root.transform);
            registry.CaptureInitialState();

            HitInfo hit = new HitInfo(ImpactKind.Heavy, "baseball_bat", Vector3.zero, Vector3.forward, 6f);
            events.EmitImpact(hit, HitResult.Accepted, receiver);
            Assert.That(presenter.Damage, Is.EqualTo(1));
            Assert.That(intact[0].activeSelf, Is.False);
            Assert.That(broken[0].activeSelf, Is.True);
            Assert.That(intact[1].activeSelf, Is.True);
            Assert.That(broken[1].activeSelf, Is.False);

            events.EmitImpact(hit, HitResult.Accepted, receiver);
            Assert.That(presenter.Damage, Is.EqualTo(2));
            Assert.That(intact[1].activeSelf, Is.False);
            Assert.That(broken[1].activeSelf, Is.True);

            registry.ResetAll(1, 2);
            Assert.That(presenter.Damage, Is.Zero);
            Assert.That(intact[0].activeSelf, Is.True);
            Assert.That(intact[1].activeSelf, Is.True);
            Assert.That(broken[0].activeSelf, Is.False);
            Assert.That(broken[1].activeSelf, Is.False);

            Object.Destroy(root);
            yield return null;
        }

        private static GameObject CreateVisual(string name, Transform parent)
        {
            GameObject visual = new GameObject(name);
            visual.transform.SetParent(parent);
            return visual;
        }

        private static void AssertVisuals(
            GameObject intact,
            GameObject cracked,
            GameObject destroyed,
            bool intactActive,
            bool crackedActive,
            bool destroyedActive)
        {
            Assert.That(intact.activeSelf, Is.EqualTo(intactActive));
            Assert.That(cracked.activeSelf, Is.EqualTo(crackedActive));
            Assert.That(destroyed.activeSelf, Is.EqualTo(destroyedActive));
        }
    }
}
