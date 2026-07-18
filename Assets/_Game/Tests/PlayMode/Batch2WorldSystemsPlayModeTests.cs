using System.Collections;
using ConGame.Domain.Counterplay;
using ConGame.Domain.Facts;
using ConGame.Domain.Interaction;
using ConGame.Runtime.Counterplay;
using ConGame.Runtime.Events;
using ConGame.Runtime.Facts;
using ConGame.Runtime.Interaction;
using ConGame.Runtime.Loop;
using ConGame.Runtime.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ConGame.Tests.PlayMode
{
    public sealed class Batch2WorldSystemsPlayModeTests
    {
        [UnityTest]
        public IEnumerator TippingBookcaseSetsBookcaseDoorBlockForD13()
        {
            GameObject factsObject = new GameObject("facts");
            GameObject eventsObject = new GameObject("events");
            GameObject bookcase = new GameObject("bookcase");
            try
            {
                GameplayFactService facts = factsObject.AddComponent<GameplayFactService>();
                facts.CaptureInitialState();
                GameplayEventHub events = eventsObject.AddComponent<GameplayEventHub>();
                NoiseLevel? heard = null;
                events.NoiseEmitted += noise => heard = noise.Level;

                TipBookcaseUseHandler tip = bookcase.AddComponent<TipBookcaseUseHandler>();
                tip.Configure(facts, events, new Vector3(0f, 0f, 85f));
                tip.CaptureInitialState();

                Assert.That(tip.Use(default), Is.EqualTo(InteractionResult.Accepted));
                Assert.That(tip.IsTipped, Is.True);
                Assert.That(facts.CaptureSnapshot().DoorBlock, Is.EqualTo(DoorBlockState.Bookcase));
                Assert.That(facts.DoorBlockBreachExtraSeconds, Is.EqualTo(0f));
                Assert.That(heard, Is.EqualTo(NoiseLevel.Loud));
                Assert.That(tip.CanUse(default), Is.False);

                tip.Restore(new LoopResetContext(1, 2));
                Assert.That(tip.IsTipped, Is.False);
                yield return null;
            }
            finally
            {
                Object.Destroy(factsObject);
                Object.Destroy(eventsObject);
                Object.Destroy(bookcase);
            }
        }

        [UnityTest]
        public IEnumerator WindowHitMarksStrikingWindowForD15()
        {
            GameObject factsObject = new GameObject("facts");
            GameObject window = new GameObject("window");
            try
            {
                GameplayFactService facts = factsObject.AddComponent<GameplayFactService>();
                facts.CaptureInitialState();
                StrikeSurfaceHitReceiver receiver = window.AddComponent<StrikeSurfaceHitReceiver>();
                receiver.Configure(facts, false);

                Assert.That(
                    receiver.ReceiveHit(new HitInfo(ImpactKind.Heavy, "bat", Vector3.zero, Vector3.forward, 6f)),
                    Is.EqualTo(HitResult.Accepted));
                Assert.That(facts.CaptureSnapshot().IsStrikingWindow, Is.True);
                yield return null;
            }
            finally
            {
                Object.Destroy(factsObject);
                Object.Destroy(window);
            }
        }

        [UnityTest]
        public IEnumerator ThrowableThrowDoesNotEmitProjectileStimulusBeforeCollision()
        {
            GameObject factsObject = new GameObject("facts");
            GameObject eventsObject = new GameObject("events");
            GameObject player = new GameObject("player");
            GameObject hand = new GameObject("hand");
            GameObject book = new GameObject("book");
            try
            {
                hand.transform.SetParent(player.transform);
                CharacterController controller = player.AddComponent<CharacterController>();
                GameplayFactService facts = factsObject.AddComponent<GameplayFactService>();
                facts.CaptureInitialState();
                GameplayEventHub events = eventsObject.AddComponent<GameplayEventHub>();
                KillerStimulus? stimulus = null;
                events.KillerStimulusEmitted += value => stimulus = value;

                PlayerMotor motor = player.AddComponent<PlayerMotor>();
                motor.Configure(controller, null, null, facts, null);
                PlayerInteractor interactor = player.AddComponent<PlayerInteractor>();
                interactor.Configure(null, null, hand.transform, motor, facts, events, null);

                book.AddComponent<BoxCollider>();
                Rigidbody body = book.AddComponent<Rigidbody>();
                InteractableEntity entity = book.AddComponent<InteractableEntity>();
                entity.Configure(null, "books");
                StatefulWorldItem stateful = book.AddComponent<StatefulWorldItem>();
                stateful.Configure(body);
                stateful.CaptureInitialState();

                Assert.That(interactor.TryHold(entity, body), Is.True);
                interactor.ThrowHeld();
                Assert.That(interactor.HeldEntity, Is.Null);
                Assert.That(stimulus.HasValue, Is.False);
                Assert.That(stateful.CurrentState, Is.EqualTo(ItemPhysicalState.Flying));
                yield return null;
            }
            finally
            {
                Object.Destroy(factsObject);
                Object.Destroy(eventsObject);
                Object.Destroy(player);
                Object.Destroy(book);
            }
        }

        [UnityTest]
        public IEnumerator ImplementedBatch2EntryAndReactiveCounterplaysSelectExpectedOutcomes()
        {
            var catalog = ScriptableObject.CreateInstance<CounterplayCatalogAsset>();
            var definitions = new System.Collections.Generic.List<CounterplayDefinitionAsset>();
            foreach (CounterplaySeed seed in CounterplaySeedLibrary.All)
            {
                CounterplayDefinitionAsset definition = ScriptableObject.CreateInstance<CounterplayDefinitionAsset>();
                definition.Configure(seed.Id, seed.SortOrder, seed.TriggerMode, seed.Conditions, seed.Status);
                definitions.Add(definition);
            }

            catalog.Configure(definitions.ToArray());

            FactSnapshot bookcaseBlock = FactSnapshot.CreateBaseline();
            bookcaseBlock.DoorBlock = DoorBlockState.Bookcase;
            bookcaseBlock.HasMeaningfulInput = true;
            bookcaseBlock.IsAtAwakeningPoint = false;
            Assert.That(catalog.Select(CounterplayTriggerMode.Entry, bookcaseBlock)?.OutcomeId, Is.EqualTo("D13"));

            FactSnapshot window = FactSnapshot.CreateBaseline();
            window.IsStrikingWindow = true;
            window.HasMeaningfulInput = true;
            window.IsAtAwakeningPoint = false;
            Assert.That(catalog.Select(CounterplayTriggerMode.Entry, window)?.OutcomeId, Is.EqualTo("D15"));

            FactSnapshot projectile = FactSnapshot.CreateBaseline();
            projectile.IncomingProjectile = true;
            projectile.HasMeaningfulInput = true;
            projectile.IsAtAwakeningPoint = false;
            Assert.That(catalog.Select(CounterplayTriggerMode.Reactive, projectile)?.OutcomeId, Is.EqualTo("D10"));

            for (int i = 0; i < definitions.Count; i++)
            {
                Object.Destroy(definitions[i]);
            }

            Object.Destroy(catalog);
            yield return null;
        }
    }
}
