using System.Collections;
using ConGame.Domain.Counterplay;
using ConGame.Domain.Facts;
using ConGame.Domain.Interaction;
using ConGame.Runtime.Counterplay;
using ConGame.Runtime.Events;
using ConGame.Runtime.Facts;
using ConGame.Runtime.Interaction;
using ConGame.Runtime.Lighting;
using ConGame.Runtime.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ConGame.Tests.PlayMode
{
    public sealed class Batch3And4WorldSystemsPlayModeTests
    {
        [UnityTest]
        public IEnumerator PistolPickupSetsStolenFlagSelectableAsD22()
        {
            GameObject factsObject = new GameObject("facts");
            GameObject player = new GameObject("player");
            GameObject hand = new GameObject("hand");
            GameObject pistol = new GameObject("pistol");
            try
            {
                hand.transform.SetParent(player.transform);
                CharacterController controller = player.AddComponent<CharacterController>();
                GameplayFactService facts = factsObject.AddComponent<GameplayFactService>();
                facts.CaptureInitialState();
                PlayerMotor motor = player.AddComponent<PlayerMotor>();
                motor.Configure(controller, null, null, facts, null);
                PlayerInteractor interactor = player.AddComponent<PlayerInteractor>();
                interactor.Configure(null, null, hand.transform, motor, facts, null, null);

                pistol.AddComponent<BoxCollider>();
                Rigidbody body = pistol.AddComponent<Rigidbody>();
                InteractableEntity entity = pistol.AddComponent<InteractableEntity>();
                entity.Configure(null, "pistol_main");
                StatefulWorldItem stateful = pistol.AddComponent<StatefulWorldItem>();
                stateful.Configure(body);
                stateful.CaptureInitialState();

                Assert.That(interactor.TryHold(entity, body), Is.True);
                Assert.That(facts.CaptureSnapshot().HasStolenPistol, Is.True);

                CounterplayRule d22 = new CounterplayRule(
                    "D22", 300, CounterplayTriggerMode.Reactive | CounterplayTriggerMode.Continuous,
                    new[] { new FactCondition(FactKey.HasStolenPistol, FactOperator.True, 1) },
                    ContentStatus.Implemented);
                Assert.That(d22.Matches(CounterplayTriggerMode.Continuous, facts.CaptureSnapshot()), Is.True);
                yield return null;
            }
            finally
            {
                Object.Destroy(factsObject);
                Object.Destroy(player);
                Object.Destroy(pistol);
            }
        }

        [UnityTest]
        public IEnumerator FlashlightUseAndMirrorGlareActivateD17Fact()
        {
            GameObject factsObject = new GameObject("facts");
            GameObject eventsObject = new GameObject("events");
            GameObject player = new GameObject("player");
            GameObject flashlight = new GameObject("flashlight");
            GameObject mirror = new GameObject("mirror");
            try
            {
                GameplayFactService facts = factsObject.AddComponent<GameplayFactService>();
                facts.CaptureInitialState();
                GameplayEventHub events = eventsObject.AddComponent<GameplayEventHub>();
                KillerStimulus? stimulus = null;
                events.KillerStimulusEmitted += value => stimulus = value;

                PlayerInteractor interactor = player.AddComponent<PlayerInteractor>();
                mirror.transform.position = new Vector3(0f, 1.4f, 1f);
                mirror.AddComponent<BoxCollider>();
                MirrorFlashlightReactive reactive = mirror.AddComponent<MirrorFlashlightReactive>();
                reactive.Configure(facts, events);
                FlashlightUseHandler flashlightUse = flashlight.AddComponent<FlashlightUseHandler>();
                flashlightUse.Configure(facts, events);
                InteractionContext context = new InteractionContext(interactor, null);
                Assert.That(flashlightUse.Use(context), Is.EqualTo(InteractionResult.Accepted));
                Assert.That(facts.CaptureSnapshot().IsGlareActive, Is.True);
                Assert.That(stimulus.HasValue, Is.True);
                Assert.That(stimulus.Value.Type, Is.EqualTo(KillerStimulusType.Glare));

                facts.SetGlareActive(false);
                reactive.React(new FlashlightHit("flashlight", Vector3.zero, Vector3.forward, 0.2f));
                Assert.That(facts.CaptureSnapshot().IsGlareActive, Is.True);
                yield return null;
            }
            finally
            {
                Object.Destroy(factsObject);
                Object.Destroy(eventsObject);
                Object.Destroy(player);
                Object.Destroy(flashlight);
                Object.Destroy(mirror);
            }
        }

        [UnityTest]
        public IEnumerator BlanketArmThenPullWithPersistentBlackSelectsD21()
        {
            GameObject factsObject = new GameObject("facts");
            GameObject blanket = new GameObject("blanket");
            GameObject ceilingObject = new GameObject("ceiling");
            GameObject tableObject = new GameObject("table");
            GameObject coordinatorObject = new GameObject("coordinator");
            try
            {
                GameplayFactService facts = factsObject.AddComponent<GameplayFactService>();
                facts.CaptureInitialState();

                Light ceilingLight = ceilingObject.AddComponent<Light>();
                BreakableLight ceiling = ceilingObject.AddComponent<BreakableLight>();
                ceiling.Configure(ceilingLight);
                ceiling.CaptureInitialState();
                Light tableLight = tableObject.AddComponent<Light>();
                BreakableLight table = tableObject.AddComponent<BreakableLight>();
                table.Configure(tableLight);
                table.CaptureInitialState();
                RoomLightCoordinator coordinator = coordinatorObject.AddComponent<RoomLightCoordinator>();
                coordinator.Configure(ceiling, table, facts);
                ceiling.ReceiveHit(new HitInfo(ImpactKind.Heavy, "bat", Vector3.zero, Vector3.forward, 1f));
                table.ReceiveHit(new HitInfo(ImpactKind.Heavy, "bat", Vector3.zero, Vector3.forward, 1f));
                Assert.That(coordinator.Recompute(), Is.EqualTo(RoomLightState.PersistentBlack));

                BlanketTrapUseHandler trap = blanket.AddComponent<BlanketTrapUseHandler>();
                trap.Configure(facts);
                Rigidbody blanketBody = blanket.AddComponent<Rigidbody>();
                InteractableEntity blanketEntity = blanket.AddComponent<InteractableEntity>();
                blanketEntity.Configure(null, "blanket");
                StatefulWorldItem blanketState = blanket.AddComponent<StatefulWorldItem>();
                blanketState.Configure(blanketBody);
                GameObject socketObject = new GameObject("blanket-socket");
                socketObject.transform.SetParent(factsObject.transform);
                PlacementSocket socket = socketObject.AddComponent<PlacementSocket>();
                socket.Configure("blanket", ItemCategory.None, null, facts,
                    DoorBlockState.None, TrapState.BlanketArmed);
                Assert.That(socket.TryPlace(blanketEntity, blanketState), Is.True);
                Assert.That(facts.CaptureSnapshot().Trap, Is.EqualTo(TrapState.BlanketArmed));
                Assert.That(facts.CaptureSnapshot().TrapPulled, Is.False);
                Assert.That(trap.Use(default), Is.EqualTo(InteractionResult.Accepted));
                Assert.That(facts.CaptureSnapshot().TrapPulled, Is.True);

                CounterplayRule d21 = new CounterplayRule(
                    "D21", 200, CounterplayTriggerMode.Reactive,
                    new[]
                    {
                        new FactCondition(FactKey.Trap, FactOperator.Equal, (int)TrapState.BlanketArmed),
                        new FactCondition(FactKey.TrapPulled, FactOperator.True, 1),
                        new FactCondition(FactKey.RoomLight, FactOperator.Equal, (int)RoomLightState.PersistentBlack)
                    },
                    ContentStatus.Implemented);
                Assert.That(d21.Matches(CounterplayTriggerMode.Reactive, facts.CaptureSnapshot()), Is.True);
                yield return null;
            }
            finally
            {
                Object.Destroy(factsObject);
                Object.Destroy(blanket);
                Object.Destroy(ceilingObject);
                Object.Destroy(tableObject);
                Object.Destroy(coordinatorObject);
            }
        }

        [UnityTest]
        public IEnumerator CatalogSelectsBatch3And4Outcomes()
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

            FactSnapshot glare = FactSnapshot.CreateBaseline();
            glare.IsGlareActive = true;
            Assert.That(catalog.Select(CounterplayTriggerMode.Reactive, glare)?.OutcomeId, Is.EqualTo("D17"));

            FactSnapshot stolen = FactSnapshot.CreateBaseline();
            stolen.HasStolenPistol = true;
            Assert.That(catalog.Select(CounterplayTriggerMode.Continuous, stolen)?.OutcomeId, Is.EqualTo("D22"));

            FactSnapshot d21 = FactSnapshot.CreateBaseline();
            d21.Trap = TrapState.BlanketArmed;
            d21.TrapPulled = true;
            d21.RoomLight = RoomLightState.PersistentBlack;
            Assert.That(catalog.Select(CounterplayTriggerMode.Reactive, d21)?.OutcomeId, Is.EqualTo("D21"));

            for (int i = 0; i < definitions.Count; i++)
            {
                Object.Destroy(definitions[i]);
            }

            Object.Destroy(catalog);
            yield return null;
        }
    }
}
