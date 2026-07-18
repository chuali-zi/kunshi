using System.Collections;
using ConGame.Domain.Facts;
using ConGame.Domain.Flow;
using ConGame.Domain.Interaction;
using ConGame.Runtime.AI;
using ConGame.Runtime.Events;
using ConGame.Runtime.Facts;
using ConGame.Runtime.Flow;
using ConGame.Runtime.Input;
using ConGame.Runtime.Interaction;
using ConGame.Runtime.Lighting;
using ConGame.Runtime.Loop;
using ConGame.Runtime.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ConGame.Tests.PlayMode
{
    public sealed class InteractionRepairPlayModeTests
    {
        [UnityTest]
        public IEnumerator BreachBlocksGameplayButCounterplayKeepsActionMapAndLocksMovement()
        {
            GameObject root = new GameObject("counterplay-control");
            try
            {
                GameplayFactService facts = root.AddComponent<GameplayFactService>();
                facts.CaptureInitialState();
                GameplayEventHub events = root.AddComponent<GameplayEventHub>();
                PlayerInputRouter input = root.AddComponent<PlayerInputRouter>();
                CharacterController controller = root.AddComponent<CharacterController>();
                PlayerMotor motor = root.AddComponent<PlayerMotor>();
                motor.Configure(controller, input, null, facts, null);
                PlayerInteractor interactor = root.AddComponent<PlayerInteractor>();
                interactor.Configure(input, null, root.transform, motor, facts, events, null);
                GameFlowController flow = root.AddComponent<GameFlowController>();
                LoopResetRegistry registry = root.AddComponent<LoopResetRegistry>();
                registry.Configure(root.transform);
                LoopDirector loop = root.AddComponent<LoopDirector>();
                loop.Configure(null, flow, registry, facts, events, input, motor, null);
                loop.Initialize();

                Assert.That(flow.Request(GamePhase.Breach, TransitionPriority.Normal, "test breach"), Is.True);
                Assert.That(flow.CommitPending(), Is.True);
                Assert.That(input.GameplayEnabled, Is.False);
                Assert.That(interactor.ControlMode, Is.EqualTo(PlayerControlMode.BreachBlocked));

                Assert.That(flow.CommitPending(), Is.True);
                Assert.That(flow.CurrentPhase, Is.EqualTo(GamePhase.Counterplay));
                Assert.That(input.GameplayEnabled, Is.True);
                Assert.That(interactor.ControlMode, Is.EqualTo(PlayerControlMode.Counterplay));
                Assert.That(interactor.IsMovementLocked, Is.True);
                Assert.That(interactor.CounterplayWindows, Is.EqualTo(CounterplayInputWindow.All));
                yield return null;
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        [UnityTest]
        public IEnumerator HeldUseWinsOverNearbyScanAndCounterplayCanRegrabDroppedItem()
        {
            GameObject player = new GameObject("player");
            GameObject hand = new GameObject("hand");
            GameObject held = new GameObject("held-use");
            GameObject nearby = new GameObject("nearby-use");
            try
            {
                hand.transform.SetParent(player.transform);
                hand.transform.localPosition = new Vector3(0f, 1f, 0.5f);
                CharacterController controller = player.AddComponent<CharacterController>();
                Rigidbody playerBody = player.AddComponent<Rigidbody>();
                playerBody.isKinematic = true;
                playerBody.useGravity = false;
                GameplayFactService facts = player.AddComponent<GameplayFactService>();
                facts.CaptureInitialState();
                PlayerMotor motor = player.AddComponent<PlayerMotor>();
                motor.Configure(controller, null, null, facts, null);
                PlayerInteractor interactor = player.AddComponent<PlayerInteractor>();
                InteractionScanner scanner = player.AddComponent<InteractionScanner>();
                scanner.Configure(interactor, 2f, ~0);
                interactor.Configure(null, scanner, hand.transform, motor, facts, null, null);

                held.AddComponent<BoxCollider>();
                Rigidbody heldBody = held.AddComponent<Rigidbody>();
                heldBody.useGravity = false;
                InteractableEntity heldEntity = held.AddComponent<InteractableEntity>();
                heldEntity.Configure(null, "held-use");
                StatefulWorldItem heldState = held.AddComponent<StatefulWorldItem>();
                heldState.Configure(heldBody);
                CountingUseHandler heldHandler = held.AddComponent<CountingUseHandler>();
                UseCapability heldUse = held.AddComponent<UseCapability>();
                heldUse.Configure(heldHandler);
                held.AddComponent<PickupCapability>();

                nearby.transform.position = new Vector3(0f, 1f, 1f);
                nearby.AddComponent<BoxCollider>();
                InteractableEntity nearbyEntity = nearby.AddComponent<InteractableEntity>();
                nearbyEntity.Configure(null, "nearby-use");
                CountingUseHandler nearbyHandler = nearby.AddComponent<CountingUseHandler>();
                UseCapability nearbyUse = nearby.AddComponent<UseCapability>();
                nearbyUse.Configure(nearbyHandler);

                Assert.That(interactor.TryHold(heldEntity, heldBody), Is.True);
                interactor.SetControlMode(PlayerControlMode.Counterplay, CounterplayInputWindow.All);
                Physics.SyncTransforms();
                Assert.That(interactor.TryInteract(), Is.True);
                Assert.That(heldHandler.UseCount, Is.EqualTo(1));
                Assert.That(nearbyHandler.UseCount, Is.Zero);

                interactor.ForceDropHeld();
                Physics.SyncTransforms();
                Assert.That(heldState.CurrentState, Is.EqualTo(ItemPhysicalState.Flying));
                Assert.That(interactor.TryInteract(), Is.True);
                Assert.That(interactor.HeldEntity, Is.EqualTo(heldEntity));
                yield return null;
            }
            finally
            {
                Object.Destroy(player);
                Object.Destroy(held);
                Object.Destroy(nearby);
            }
        }

        [UnityTest]
        public IEnumerator PickingPlacedItemReleasesSocketOccupantAndFacts()
        {
            GameObject player = new GameObject("player");
            GameObject hand = new GameObject("hand");
            GameObject item = new GameObject("chair");
            GameObject socketObject = new GameObject("door-socket");
            try
            {
                hand.transform.SetParent(player.transform);
                CharacterController controller = player.AddComponent<CharacterController>();
                GameplayFactService facts = player.AddComponent<GameplayFactService>();
                facts.CaptureInitialState();
                PlayerMotor motor = player.AddComponent<PlayerMotor>();
                motor.Configure(controller, null, null, facts, null);
                PlayerInteractor interactor = player.AddComponent<PlayerInteractor>();
                interactor.Configure(null, null, hand.transform, motor, facts, null, null);

                item.AddComponent<BoxCollider>();
                Rigidbody body = item.AddComponent<Rigidbody>();
                InteractableEntity entity = item.AddComponent<InteractableEntity>();
                entity.Configure(null, "chair");
                StatefulWorldItem stateful = item.AddComponent<StatefulWorldItem>();
                stateful.Configure(body);
                item.AddComponent<PickupCapability>();
                PlacementSocket socket = socketObject.AddComponent<PlacementSocket>();
                socket.Configure(
                    "door",
                    ItemCategory.None,
                    null,
                    facts,
                    DoorBlockState.Furniture,
                    TrapState.BlanketArmed);

                Assert.That(interactor.TryHold(entity, body), Is.True);
                Assert.That(interactor.TryPlaceHeld(socket), Is.True);
                Assert.That(socket.Occupant, Is.EqualTo(entity));
                Assert.That(stateful.CurrentSocket, Is.EqualTo(socket));
                Assert.That(facts.CaptureSnapshot().DoorBlock, Is.EqualTo(DoorBlockState.Furniture));
                Assert.That(facts.CaptureSnapshot().Trap, Is.EqualTo(TrapState.BlanketArmed));

                Assert.That(entity.TryExecute(interactor, InteractionVerb.PickUp), Is.True);
                Assert.That(socket.Occupant, Is.Null);
                Assert.That(stateful.CurrentSocket, Is.Null);
                Assert.That(facts.CaptureSnapshot().DoorBlock, Is.EqualTo(DoorBlockState.None));
                Assert.That(facts.CaptureSnapshot().Trap, Is.EqualTo(TrapState.None));
                yield return null;
            }
            finally
            {
                Object.Destroy(player);
                Object.Destroy(item);
                Object.Destroy(socketObject);
            }
        }

        [UnityTest]
        public IEnumerator FlyingItemBreaksCeilingLightOnFirstPhysicalImpactWithoutKillerStimulus()
        {
            GameObject item = new GameObject("thrown-book");
            GameObject hand = new GameObject("hand");
            GameObject ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
            GameObject eventsObject = new GameObject("events");
            try
            {
                item.transform.position = Vector3.zero;
                item.AddComponent<SphereCollider>().radius = 0.15f;
                Rigidbody body = item.AddComponent<Rigidbody>();
                body.useGravity = false;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                InteractableEntity entity = item.AddComponent<InteractableEntity>();
                entity.Configure(null, "book");
                StatefulWorldItem stateful = item.AddComponent<StatefulWorldItem>();
                stateful.Configure(body, ItemPhysicalState.Placed, ImpactKind.Projectile);

                ceiling.transform.position = new Vector3(0f, 0f, 1.2f);
                ceiling.transform.localScale = new Vector3(2f, 2f, 0.2f);
                Light light = ceiling.AddComponent<Light>();
                BreakableLight breakable = ceiling.AddComponent<BreakableLight>();
                breakable.Configure(light);

                GameplayEventHub events = eventsObject.AddComponent<GameplayEventHub>();
                bool emitted = false;
                events.KillerStimulusEmitted += _ => emitted = true;
                Assert.That(stateful.BeginHeld(hand.transform, null), Is.True);
                stateful.Release(Vector3.forward * 12f, Vector3.zero, events);

                float timeout = Time.time + 1f;
                while (!breakable.IsBroken && Time.time < timeout)
                {
                    yield return new WaitForFixedUpdate();
                }

                Assert.That(breakable.IsBroken, Is.True);
                Assert.That(stateful.CurrentState, Is.EqualTo(ItemPhysicalState.Flying));
                Assert.That(emitted, Is.False);
            }
            finally
            {
                Object.Destroy(item);
                Object.Destroy(hand);
                Object.Destroy(ceiling);
                Object.Destroy(eventsObject);
            }
        }

        [UnityTest]
        public IEnumerator IncomingProjectileStimulusRequiresActualKillerCollision()
        {
            GameObject item = new GameObject("thrown-object");
            GameObject hand = new GameObject("hand");
            GameObject killer = new GameObject("killer");
            GameObject eventsObject = new GameObject("events");
            try
            {
                item.AddComponent<SphereCollider>().radius = 0.15f;
                Rigidbody body = item.AddComponent<Rigidbody>();
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                InteractableEntity entity = item.AddComponent<InteractableEntity>();
                entity.Configure(null, "projectile");
                StatefulWorldItem stateful = item.AddComponent<StatefulWorldItem>();
                stateful.Configure(body);

                killer.transform.position = new Vector3(0f, 0f, 1.2f);
                killer.AddComponent<BoxCollider>().size = new Vector3(2f, 2f, 0.2f);
                killer.AddComponent<KillerDirector>();
                GameplayEventHub events = eventsObject.AddComponent<GameplayEventHub>();
                KillerStimulus? stimulus = null;
                events.KillerStimulusEmitted += value => stimulus = value;

                Assert.That(stateful.BeginHeld(hand.transform, null), Is.True);
                stateful.Release(Vector3.forward * 12f, Vector3.zero, events);
                float timeout = Time.time + 1f;
                while (!stimulus.HasValue && Time.time < timeout)
                {
                    yield return new WaitForFixedUpdate();
                }

                Assert.That(stimulus.HasValue, Is.True);
                Assert.That(stimulus.Value.Type, Is.EqualTo(KillerStimulusType.IncomingProjectile));
            }
            finally
            {
                Object.Destroy(item);
                Object.Destroy(hand);
                Object.Destroy(killer);
                Object.Destroy(eventsObject);
            }
        }

        [UnityTest]
        public IEnumerator OverlappingZonesFallBackToStillOccupiedZone()
        {
            GameObject factsObject = new GameObject("facts");
            GameObject player = new GameObject("player");
            GameObject zoneA = new GameObject("zone-a");
            GameObject zoneB = new GameObject("zone-b");
            try
            {
                GameplayFactService facts = factsObject.AddComponent<GameplayFactService>();
                facts.CaptureInitialState();
                CharacterController controller = player.AddComponent<CharacterController>();
                Rigidbody triggerBody = player.AddComponent<Rigidbody>();
                triggerBody.isKinematic = true;
                triggerBody.useGravity = false;
                PlayerMotor motor = player.AddComponent<PlayerMotor>();
                motor.Configure(controller, null, null, facts, null);
                player.transform.position = new Vector3(0f, 0f, -4f);

                BoxCollider colliderA = zoneA.AddComponent<BoxCollider>();
                colliderA.size = new Vector3(3f, 3f, 4f);
                PlayerZoneTrigger triggerA = zoneA.AddComponent<PlayerZoneTrigger>();
                triggerA.Configure(PlayerZone.SofaRear, facts);

                zoneB.transform.position = new Vector3(0f, 0f, 2f);
                BoxCollider colliderB = zoneB.AddComponent<BoxCollider>();
                colliderB.size = new Vector3(3f, 3f, 4f);
                PlayerZoneTrigger triggerB = zoneB.AddComponent<PlayerZoneTrigger>();
                triggerB.Configure(PlayerZone.Bed, facts);

                Physics.SyncTransforms();
                yield return new WaitForFixedUpdate();
                player.transform.position = new Vector3(0f, 0f, -1f);
                Physics.SyncTransforms();
                yield return new WaitForFixedUpdate();
                Assert.That(facts.CaptureSnapshot().PlayerZone, Is.EqualTo(PlayerZone.SofaRear));

                player.transform.position = new Vector3(0f, 0f, 1f);
                Physics.SyncTransforms();
                yield return new WaitForFixedUpdate();
                Assert.That(facts.CaptureSnapshot().PlayerZone, Is.EqualTo(PlayerZone.Bed));

                player.transform.position = new Vector3(0f, 0f, 3f);
                Physics.SyncTransforms();
                yield return new WaitForFixedUpdate();
                Assert.That(facts.CaptureSnapshot().PlayerZone, Is.EqualTo(PlayerZone.Bed));

                player.transform.position = new Vector3(0f, 0f, 5f);
                Physics.SyncTransforms();
                yield return new WaitForFixedUpdate();
                Assert.That(facts.CaptureSnapshot().PlayerZone, Is.EqualTo(PlayerZone.Open));
            }
            finally
            {
                Object.Destroy(factsObject);
                Object.Destroy(player);
                Object.Destroy(zoneA);
                Object.Destroy(zoneB);
            }
        }

        [UnityTest]
        public IEnumerator HideLocksMovementAndExitRestoresPoseZoneAndPosition()
        {
            GameObject player = new GameObject("player");
            GameObject hand = new GameObject("hand");
            GameObject bed = new GameObject("bed");
            GameObject anchor = new GameObject("hide-anchor");
            try
            {
                hand.transform.SetParent(player.transform);
                CharacterController controller = player.AddComponent<CharacterController>();
                GameplayFactService facts = player.AddComponent<GameplayFactService>();
                facts.CaptureInitialState();
                PlayerMotor motor = player.AddComponent<PlayerMotor>();
                motor.Configure(controller, null, null, facts, null);
                PlayerInteractor interactor = player.AddComponent<PlayerInteractor>();
                interactor.Configure(null, null, hand.transform, motor, facts, null, null);
                Vector3 start = new Vector3(2f, 0f, -1f);
                player.transform.position = start;
                anchor.transform.position = new Vector3(0f, 0f, 3f);

                InteractableEntity entity = bed.AddComponent<InteractableEntity>();
                entity.Configure(null, "bed");
                HideCapability hide = bed.AddComponent<HideCapability>();
                hide.Configure(anchor.transform, PlayerZone.UnderBed);

                Assert.That(entity.TryExecute(interactor, InteractionVerb.Hide), Is.True);
                Assert.That(interactor.IsHiding, Is.True);
                Assert.That(interactor.IsMovementLocked, Is.True);
                Assert.That(facts.CaptureSnapshot().PlayerPose, Is.EqualTo(PlayerPose.Hidden));
                Assert.That(facts.CaptureSnapshot().PlayerZone, Is.EqualTo(PlayerZone.UnderBed));

                interactor.ExitHide();
                Assert.That(interactor.IsHiding, Is.False);
                Assert.That(interactor.IsMovementLocked, Is.False);
                Assert.That(player.transform.position, Is.EqualTo(start));
                Assert.That(facts.CaptureSnapshot().PlayerPose, Is.EqualTo(PlayerPose.Standing));
                Assert.That(facts.CaptureSnapshot().PlayerZone, Is.EqualTo(PlayerZone.Awakening));
                yield return null;
            }
            finally
            {
                Object.Destroy(player);
                Object.Destroy(bed);
                Object.Destroy(anchor);
            }
        }

        [UnityTest]
        public IEnumerator FlashlightGlareExpiresAfterConfiguredDuration()
        {
            GameObject factsObject = new GameObject("facts");
            GameObject player = new GameObject("player");
            GameObject flashlight = new GameObject("flashlight");
            GameObject mirror = new GameObject("mirror");
            try
            {
                GameplayFactService facts = factsObject.AddComponent<GameplayFactService>();
                facts.CaptureInitialState();
                PlayerInteractor interactor = player.AddComponent<PlayerInteractor>();
                mirror.transform.position = new Vector3(0f, 1.4f, 1f);
                mirror.AddComponent<BoxCollider>();
                MirrorFlashlightReactive reactive = mirror.AddComponent<MirrorFlashlightReactive>();
                reactive.Configure(facts, null, 0.05f);
                FlashlightUseHandler handler = flashlight.AddComponent<FlashlightUseHandler>();
                handler.Configure(facts, null, 2f, 0.1f);

                Assert.That(
                    handler.Use(new InteractionContext(interactor, null)),
                    Is.EqualTo(InteractionResult.Accepted));
                Assert.That(facts.CaptureSnapshot().IsGlareActive, Is.True);
                yield return new WaitForSeconds(0.3f);
                yield return null;
                Assert.That(facts.CaptureSnapshot().IsGlareActive, Is.False);
            }
            finally
            {
                Object.Destroy(factsObject);
                Object.Destroy(player);
                Object.Destroy(flashlight);
                Object.Destroy(mirror);
            }
        }
    }

    public sealed class CountingUseHandler : MonoBehaviour, IUseHandler
    {
        public int UseCount { get; private set; }

        public bool CanUse(in InteractionContext context)
        {
            return true;
        }

        public InteractionResult Use(in InteractionContext context)
        {
            UseCount++;
            return InteractionResult.Accepted;
        }
    }
}
