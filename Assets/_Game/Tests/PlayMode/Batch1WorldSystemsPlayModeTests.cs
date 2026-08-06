using System.Collections;
using ConGame.Domain.Counterplay;
using ConGame.Domain.Facts;
using ConGame.Domain.Interaction;
using ConGame.Runtime.AI;
using ConGame.Runtime.Counterplay;
using ConGame.Runtime.Data;
using ConGame.Runtime.Facts;
using ConGame.Runtime.Interaction;
using ConGame.Runtime.Lighting;
using ConGame.Runtime.Loop;
using ConGame.Runtime.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ConGame.Tests.PlayMode
{
    public sealed class Batch1WorldSystemsPlayModeTests
    {
        [UnityTest]
        public IEnumerator PlacingChairOnDoorSocketSetsFurnitureBlockAndThreeSecondBreachExtra()
        {
            GameObject factsObject = new GameObject("facts");
            GameObject player = new GameObject("player");
            GameObject hand = new GameObject("hand");
            GameObject chair = new GameObject("chair");
            GameObject door = new GameObject("door");
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

                chair.AddComponent<BoxCollider>();
                Rigidbody body = chair.AddComponent<Rigidbody>();
                InteractableEntity entity = chair.AddComponent<InteractableEntity>();
                entity.Configure(null, "wood_chair");
                StatefulWorldItem stateful = chair.AddComponent<StatefulWorldItem>();
                stateful.Configure(body);
                stateful.CaptureInitialState();

                PlacementSocket socket = door.AddComponent<PlacementSocket>();
                socket.Configure("door_block", ItemCategory.None, null, facts, DoorBlockState.Furniture);

                Assert.That(interactor.TryCarry(entity, body, 0.5f), Is.True);
                Assert.That(interactor.TryPlaceHeld(socket), Is.True);
                Assert.That(facts.CaptureSnapshot().DoorBlock, Is.EqualTo(DoorBlockState.Furniture));
                Assert.That(facts.DoorBlockBreachExtraSeconds, Is.EqualTo(3f).Within(0.01f));

                RoomDoorBreachController breach = door.AddComponent<RoomDoorBreachController>();
                GameObject hinge = new GameObject("hinge");
                hinge.transform.SetParent(door.transform, false);
                breach.Configure(hinge.transform, 0.9f, 100f);
                breach.CaptureInitialState();
                breach.Begin(1, null, 0.9f + facts.DoorBlockBreachExtraSeconds);
                Assert.That(breach.CurrentDuration, Is.EqualTo(3.9f).Within(0.01f));
                yield return null;
            }
            finally
            {
                Object.Destroy(factsObject);
                Object.Destroy(player);
                Object.Destroy(chair);
                Object.Destroy(door);
            }
        }

        [UnityTest]
        public IEnumerator PlacingDresserOnDoorSocketSetsFiveSecondBreachExtra()
        {
            GameObject factsObject = new GameObject("facts");
            GameObject player = new GameObject("player");
            GameObject hand = new GameObject("hand");
            GameObject dresser = new GameObject("dresser");
            GameObject door = new GameObject("door");
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

                dresser.AddComponent<BoxCollider>();
                Rigidbody body = dresser.AddComponent<Rigidbody>();
                InteractableEntity entity = dresser.AddComponent<InteractableEntity>();
                entity.Configure(null, "dresser");
                StatefulWorldItem stateful = dresser.AddComponent<StatefulWorldItem>();
                stateful.Configure(body);
                stateful.CaptureInitialState();

                PlacementSocket socket = door.AddComponent<PlacementSocket>();
                socket.Configure("door_block", ItemCategory.None, null, facts, DoorBlockState.Furniture);

                Assert.That(interactor.TryCarry(entity, body, 0.33f), Is.True);
                Assert.That(interactor.TryPlaceHeld(socket), Is.True);
                Assert.That(facts.DoorBlockBreachExtraSeconds, Is.EqualTo(5f).Within(0.01f));
                yield return null;
            }
            finally
            {
                Object.Destroy(factsObject);
                Object.Destroy(player);
                Object.Destroy(dresser);
                Object.Destroy(door);
            }
        }

        [UnityTest]
        public IEnumerator DresserCannotBeThrownThroughGlobalThrowInput()
        {
            GameObject player = new GameObject("player");
            GameObject hand = new GameObject("hand");
            GameObject dresser = new GameObject("dresser");
            ItemDefinitionAsset definition = ScriptableObject.CreateInstance<ItemDefinitionAsset>();
            try
            {
                hand.transform.SetParent(player.transform);
                CharacterController controller = player.AddComponent<CharacterController>();
                PlayerMotor motor = player.AddComponent<PlayerMotor>();
                motor.Configure(controller, null, null, null, null);
                PlayerInteractor interactor = player.AddComponent<PlayerInteractor>();
                interactor.Configure(null, null, hand.transform, motor, null, null, null);

                definition.Configure("dresser", "dresser", InteractionVerb.Carry,
                    ItemCategory.Heavy | ItemCategory.DoorBlocker, ContentStatus.Implemented);
                Rigidbody body = dresser.AddComponent<Rigidbody>();
                InteractableEntity entity = dresser.AddComponent<InteractableEntity>();
                entity.Configure(definition);
                StatefulWorldItem stateful = dresser.AddComponent<StatefulWorldItem>();
                stateful.Configure(body);
                stateful.CaptureInitialState();

                Assert.That(interactor.TryCarry(entity, body, 0.33f), Is.True);
                interactor.ThrowHeld();

                Assert.That(interactor.HeldEntity, Is.SameAs(entity));
                Assert.That(body.isKinematic, Is.True);
                yield return null;
            }
            finally
            {
                Object.Destroy(player);
                Object.Destroy(dresser);
                Object.Destroy(definition);
            }
        }

        [UnityTest]
        public IEnumerator BedHideSetsUnderBedZoneForD11()
        {
            GameObject factsObject = new GameObject("facts");
            GameObject player = new GameObject("player");
            GameObject bed = new GameObject("bed");
            GameObject anchor = new GameObject("anchor");
            try
            {
                CharacterController controller = player.AddComponent<CharacterController>();
                GameplayFactService facts = factsObject.AddComponent<GameplayFactService>();
                facts.CaptureInitialState();
                PlayerMotor motor = player.AddComponent<PlayerMotor>();
                motor.Configure(controller, null, null, facts, null);
                PlayerInteractor interactor = player.AddComponent<PlayerInteractor>();
                interactor.Configure(null, null, null, motor, facts, null, null);

                InteractableEntity entity = bed.AddComponent<InteractableEntity>();
                entity.Configure(null, "bed");
                HideCapability hide = bed.AddComponent<HideCapability>();
                hide.Configure(anchor.transform, PlayerZone.UnderBed);

                Assert.That(entity.TryExecute(interactor, InteractionVerb.Hide), Is.True);
                Assert.That(facts.CaptureSnapshot().PlayerZone, Is.EqualTo(PlayerZone.UnderBed));
                Assert.That(facts.CaptureSnapshot().PlayerPose, Is.EqualTo(PlayerPose.Hidden));
                yield return null;
            }
            finally
            {
                Object.Destroy(factsObject);
                Object.Destroy(player);
                Object.Destroy(bed);
                Object.Destroy(anchor);
            }
        }

        [UnityTest]
        public IEnumerator RugAndBlanketAffordancesArmTrapFacts()
        {
            GameObject factsObject = new GameObject("facts");
            GameObject rug = new GameObject("rug");
            GameObject blanket = new GameObject("blanket");
            GameObject blanketSocketObject = new GameObject("blanket-socket");
            try
            {
                GameplayFactService facts = factsObject.AddComponent<GameplayFactService>();
                facts.CaptureInitialState();

                GameplayAffordanceHandler rugAffordance = rug.AddComponent<GameplayAffordanceHandler>();
                rugAffordance.Configure(GameplayAffordanceKind.PullRugTrap, facts);
                Assert.That(rugAffordance.Use(default), Is.EqualTo(InteractionResult.Accepted));
                Assert.That(facts.CaptureSnapshot().Trap, Is.EqualTo(TrapState.RugArmed));
                Assert.That(facts.CaptureSnapshot().TrapPulled, Is.True);

                BlanketTrapUseHandler blanketTrap = blanket.AddComponent<BlanketTrapUseHandler>();
                blanketTrap.Configure(facts);
                Rigidbody blanketBody = blanket.AddComponent<Rigidbody>();
                InteractableEntity blanketEntity = blanket.AddComponent<InteractableEntity>();
                blanketEntity.Configure(null, "blanket");
                StatefulWorldItem blanketState = blanket.AddComponent<StatefulWorldItem>();
                blanketState.Configure(blanketBody);
                PlacementSocket blanketSocket = blanketSocketObject.AddComponent<PlacementSocket>();
                blanketSocket.Configure("blanket", ItemCategory.None, null, facts,
                    DoorBlockState.None, TrapState.BlanketArmed);
                Assert.That(blanketSocket.TryPlace(blanketEntity, blanketState), Is.True);
                Assert.That(facts.CaptureSnapshot().Trap, Is.EqualTo(TrapState.BlanketArmed));
                Assert.That(facts.CaptureSnapshot().TrapPulled, Is.False);
                Assert.That(blanketTrap.Use(default), Is.EqualTo(InteractionResult.Accepted));
                Assert.That(facts.CaptureSnapshot().TrapPulled, Is.True);
                yield return null;
            }
            finally
            {
                Object.Destroy(factsObject);
                Object.Destroy(rug);
                Object.Destroy(blanket);
                Object.Destroy(blanketSocketObject);
            }
        }

        [UnityTest]
        public IEnumerator WallHitReceiverMarksStrikingWallForD05()
        {
            GameObject factsObject = new GameObject("facts");
            GameObject wall = new GameObject("wall");
            try
            {
                GameplayFactService facts = factsObject.AddComponent<GameplayFactService>();
                facts.CaptureInitialState();
                StrikeSurfaceHitReceiver receiver = wall.AddComponent<StrikeSurfaceHitReceiver>();
                receiver.Configure(facts, true);

                HitResult result = receiver.ReceiveHit(new HitInfo(ImpactKind.Heavy, "wood_chair", Vector3.zero,
                                                                   Vector3.forward, 6f));
                Assert.That(result, Is.EqualTo(HitResult.Accepted));
                Assert.That(facts.CaptureSnapshot().IsStrikingWall, Is.True);
                yield return null;
            }
            finally
            {
                Object.Destroy(factsObject);
                Object.Destroy(wall);
            }
        }

        [UnityTest]
        public IEnumerator BreakingBothLightsYieldsPersistentBlackSelectableAsD19()
        {
            GameObject factsObject = new GameObject("facts");
            GameObject ceilingObject = new GameObject("ceiling");
            GameObject tableObject = new GameObject("table");
            GameObject coordinatorObject = new GameObject("coordinator");
            try
            {
                GameplayFactService facts = factsObject.AddComponent<GameplayFactService>();
                facts.CaptureInitialState();

                Light ceilingUnityLight = ceilingObject.AddComponent<Light>();
                BreakableLight ceiling = ceilingObject.AddComponent<BreakableLight>();
                ceiling.Configure(ceilingUnityLight);
                ceiling.CaptureInitialState();

                Light tableUnityLight = tableObject.AddComponent<Light>();
                BreakableLight table = tableObject.AddComponent<BreakableLight>();
                table.Configure(tableUnityLight);
                table.CaptureInitialState();

                RoomLightCoordinator coordinator = coordinatorObject.AddComponent<RoomLightCoordinator>();
                coordinator.Configure(ceiling, table, facts);
                ceiling.ReceiveHit(new HitInfo(ImpactKind.Heavy, "bat", Vector3.zero, Vector3.forward, 1f));
                table.ReceiveHit(new HitInfo(ImpactKind.Heavy, "bat", Vector3.zero, Vector3.forward, 1f));
                Assert.That(coordinator.Recompute(), Is.EqualTo(RoomLightState.PersistentBlack));

                CounterplayRule d19 = new CounterplayRule(
                    "D19", 600, CounterplayTriggerMode.Entry | CounterplayTriggerMode.Continuous,
                    new[] { new FactCondition(FactKey.RoomLight, FactOperator.Equal, (int)RoomLightState.PersistentBlack) },
                    ContentStatus.Implemented);
                Assert.That(d19.Matches(CounterplayTriggerMode.Entry, facts.CaptureSnapshot()), Is.True);
                yield return null;
            }
            finally
            {
                Object.Destroy(factsObject);
                Object.Destroy(ceilingObject);
                Object.Destroy(tableObject);
                Object.Destroy(coordinatorObject);
            }
        }

        [UnityTest]
        public IEnumerator ImplementedBatch1EntryCounterplaysSelectExpectedOutcomes()
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

            FactSnapshot underBed = FactSnapshot.CreateBaseline();
            underBed.PlayerZone = PlayerZone.UnderBed;
            underBed.HasMeaningfulInput = true;
            underBed.IsAtAwakeningPoint = false;
            Assert.That(catalog.Select(CounterplayTriggerMode.Entry, underBed)?.OutcomeId, Is.EqualTo("D11"));

            FactSnapshot doorBlock = FactSnapshot.CreateBaseline();
            doorBlock.DoorBlock = DoorBlockState.Furniture;
            doorBlock.HasMeaningfulInput = true;
            doorBlock.IsAtAwakeningPoint = false;
            Assert.That(catalog.Select(CounterplayTriggerMode.Entry, doorBlock)?.OutcomeId, Is.EqualTo("D09"));

            FactSnapshot rug = FactSnapshot.CreateBaseline();
            rug.Trap = TrapState.RugArmed;
            rug.TrapPulled = true;
            rug.HasMeaningfulInput = true;
            rug.IsAtAwakeningPoint = false;
            Assert.That(catalog.Select(CounterplayTriggerMode.Entry, rug)?.OutcomeId, Is.EqualTo("D01"));
            Assert.That(catalog.Select(CounterplayTriggerMode.Reactive, rug)?.OutcomeId, Is.EqualTo("D16"));

            FactSnapshot strike = FactSnapshot.CreateBaseline();
            strike.IsStrikingWall = true;
            strike.HasMeaningfulInput = true;
            strike.IsAtAwakeningPoint = false;
            Assert.That(catalog.Select(CounterplayTriggerMode.Entry, strike)?.OutcomeId, Is.EqualTo("D05"));

            for (int i = 0; i < definitions.Count; i++)
            {
                Object.Destroy(definitions[i]);
            }

            Object.Destroy(catalog);
            yield return null;
        }
    }
}
