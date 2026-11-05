using System.Collections;
using ConGame.Domain.Facts;
using ConGame.Domain.Flow;
using ConGame.Domain.Interaction;
using ConGame.Runtime.AI;
using ConGame.Runtime.Config;
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
using UnityEngine.TestTools.Utils;

namespace ConGame.Tests.PlayMode
{
    public sealed class InteractionRepairPlayModeTests
    {
        [UnityTest]
        public IEnumerator GroundPlaceKeepsCarriedFurnitureStableInsteadOfThrowingIt()
        {
            GameObject root = new GameObject("ground-place");
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            GameObject chair = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                floor.transform.SetPositionAndRotation(new Vector3(0f, -0.05f, 1.5f), Quaternion.identity);
                floor.transform.localScale = new Vector3(5f, 0.1f, 5f);
                Transform socket = new GameObject("hand").transform;
                socket.SetParent(root.transform, false);
                GameplayFactService facts = root.AddComponent<GameplayFactService>();
                facts.CaptureInitialState();
                GameplayEventHub events = root.AddComponent<GameplayEventHub>();
                PlayerInteractor interactor = root.AddComponent<PlayerInteractor>();
                interactor.Configure(null, null, socket, null, facts, events, null);

                chair.transform.position = new Vector3(0f, 0.5f, 1f);
                Rigidbody body = chair.AddComponent<Rigidbody>();
                body.isKinematic = true;
                InteractableEntity entity = chair.AddComponent<InteractableEntity>();
                entity.Configure(null, "wood_chair");
                StatefulWorldItem stateful = chair.AddComponent<StatefulWorldItem>();
                stateful.Configure(body);
                chair.AddComponent<CarryCapability>();

                Assert.That(interactor.TryCarry(entity, body, 0.5f), Is.True);
                Assert.That(interactor.TryInteract(), Is.True);
                yield return new WaitForFixedUpdate();

                Assert.That(stateful.CurrentState, Is.EqualTo(ItemPhysicalState.Placed));
                Assert.That(body.isKinematic, Is.True);
                Assert.That(body.linearVelocity, Is.EqualTo(Vector3.zero));
            }
            finally
            {
                Object.Destroy(root);
                Object.Destroy(floor);
                Object.Destroy(chair);
            }
        }

        [UnityTest]
        public IEnumerator ClimbMovesToStandAnchorThenRestoresFreeMovement()
        {
            GameObject root = new GameObject("climb-player");
            GameObject anchorObject = new GameObject("stand-anchor");
            GameConfigAsset config = ScriptableObject.CreateInstance<GameConfigAsset>();
            try
            {
                CharacterController controller = root.AddComponent<CharacterController>();
                PlayerInputRouter input = root.AddComponent<PlayerInputRouter>();
                GameplayFactService facts = root.AddComponent<GameplayFactService>();
                facts.CaptureInitialState();
                PlayerMotor motor = root.AddComponent<PlayerMotor>();
                motor.Configure(controller, input, null, facts, config);
                PlayerInteractor interactor = root.AddComponent<PlayerInteractor>();
                interactor.Configure(input, null, root.transform, motor, facts, null, config);
                anchorObject.transform.SetPositionAndRotation(new Vector3(0f, 0.8f, 0.2f),
                                                               Quaternion.Euler(0f, 180f, 0f));

                Assert.That(interactor.TryClimb(anchorObject.transform, 0.1f), Is.True);
                Assert.That(interactor.IsMovementLocked, Is.True);
                yield return new WaitForSeconds(0.15f);

                // Normal movement resumes after arrival; this fixture has no landing surface,
                // so allow the small gravity step before this coroutine observes completion.
                Assert.That(Vector3.Distance(root.transform.position, anchorObject.transform.position), Is.LessThan(.02f));
                Assert.That(interactor.IsMovementLocked, Is.False);
                Assert.That(facts.CaptureSnapshot().PlayerPose, Is.EqualTo(PlayerPose.Standing));
            }
            finally
            {
                Object.Destroy(root);
                Object.Destroy(anchorObject);
                Object.Destroy(config);
            }
        }

        [UnityTest]
        public IEnumerator DoorBreachBrieflyBlocksThenLiveHuntRestoresFullPlayerControl()
        {
            GameObject root = new GameObject("live-hunt-control");
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

                Assert.That(flow.CurrentPhase, Is.EqualTo(GamePhase.Breach));
                Assert.That(input.GameplayEnabled, Is.True,
                    "No authored door controller means the breach completes immediately.");
                Assert.That(interactor.ControlMode, Is.EqualTo(PlayerControlMode.Free));
                Assert.That(interactor.IsMovementLocked, Is.False);
                Assert.That(flow.CommitPending(), Is.False,
                    "Live hunt stays in Breach until the killer earns a firing commit.");
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
        public IEnumerator HideLocationSurvivesZoneCallbacksUntilExitThenOrdinaryZonesResume()
        {
            GameObject player = new GameObject("player");
            GameObject zone = new GameObject("bed-top-zone");
            GameObject anchor = new GameObject("under-bed-anchor");
            try
            {
                var controller = player.AddComponent<CharacterController>();
                var body = player.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.useGravity = false;
                var facts = player.AddComponent<GameplayFactService>();
                facts.CaptureInitialState();
                var motor = player.AddComponent<PlayerMotor>();
                motor.Configure(controller, null, null, facts, null);
                var interactor = player.AddComponent<PlayerInteractor>();
                interactor.Configure(null, null, null, motor, facts, null, null);
                Vector3 start = new Vector3(0, 0, -4);
                player.transform.position = start;
                anchor.transform.position = new Vector3(0, 0, 3);
                zone.transform.position = anchor.transform.position;
                zone.AddComponent<BoxCollider>().size = new Vector3(3, 3, 4);
                zone.AddComponent<PlayerZoneTrigger>().Configure(PlayerZone.Bed, facts);
                Physics.SyncTransforms();
                yield return new WaitForFixedUpdate();

                interactor.EnterHide(anchor.transform, PlayerZone.UnderBed);
                Physics.SyncTransforms();
                yield return new WaitForFixedUpdate();
                Assert.That(facts.CaptureSnapshot().PlayerZone, Is.EqualTo(PlayerZone.UnderBed),
                    "Entering a physical bed-top trigger must preserve the chosen hide location.");
                zone.SetActive(false);
                yield return new WaitForFixedUpdate();
                Assert.That(facts.CaptureSnapshot().PlayerZone, Is.EqualTo(PlayerZone.UnderBed),
                    "Unregistering the last physical zone must not clear an active hide location.");
                zone.SetActive(true);
                Physics.SyncTransforms();
                yield return new WaitForFixedUpdate();
                Assert.That(facts.CaptureSnapshot().PlayerZone, Is.EqualTo(PlayerZone.UnderBed));

                interactor.ExitHide();
                Assert.That(player.transform.position, Is.EqualTo(start));
                Assert.That(interactor.IsHiding, Is.False);
                Physics.SyncTransforms();
                yield return new WaitForFixedUpdate();
                player.transform.position = anchor.transform.position;
                Physics.SyncTransforms();
                yield return new WaitForFixedUpdate();
                Assert.That(facts.CaptureSnapshot().PlayerZone, Is.EqualTo(PlayerZone.Bed));
                player.transform.position = start;
                Physics.SyncTransforms();
                yield return new WaitForFixedUpdate();
                Assert.That(facts.CaptureSnapshot().PlayerZone, Is.EqualTo(PlayerZone.Open));
            }
            finally
            {
                Object.Destroy(player);
                Object.Destroy(zone);
                Object.Destroy(anchor);
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
        public IEnumerator SofaHideUsesConfiguredCrouchedPose()
        {
            GameObject player = new GameObject("player");
            GameObject sofa = new GameObject("sofa");
            GameObject anchor = new GameObject("sofa-hide-anchor");
            try
            {
                CharacterController controller = player.AddComponent<CharacterController>();
                GameplayFactService facts = player.AddComponent<GameplayFactService>();
                facts.CaptureInitialState();
                PlayerMotor motor = player.AddComponent<PlayerMotor>();
                motor.Configure(controller, null, null, facts, null);
                PlayerInteractor interactor = player.AddComponent<PlayerInteractor>();
                interactor.Configure(null, null, null, motor, facts, null, null);

                InteractableEntity entity = sofa.AddComponent<InteractableEntity>();
                entity.Configure(null, "sofa");
                HideCapability hide = sofa.AddComponent<HideCapability>();
                hide.Configure(anchor.transform, PlayerZone.SofaRear, PlayerPose.Crouched);

                Assert.That(entity.TryExecute(interactor, InteractionVerb.Hide), Is.True);
                Assert.That(interactor.IsHiding, Is.True);
                Assert.That(interactor.IsMovementLocked, Is.True);
                Assert.That(facts.CaptureSnapshot().PlayerZone, Is.EqualTo(PlayerZone.SofaRear));
                Assert.That(facts.CaptureSnapshot().PlayerPose, Is.EqualTo(PlayerPose.Crouched));
                yield return null;
            }
            finally
            {
                Object.Destroy(player);
                Object.Destroy(sofa);
                Object.Destroy(anchor);
            }
        }

        [UnityTest]
        public IEnumerator KillerHitReceiverPlaysNonLethalRecoil()
        {
            GameObject killer = new GameObject("killer");
            GameObject model = new GameObject("model");
            try
            {
                model.transform.SetParent(killer.transform, false);
                KillerHitReceiver receiver = killer.AddComponent<KillerHitReceiver>();
                receiver.Configure(model.transform);
                Vector3 rest = model.transform.localPosition;

                HitResult result = receiver.ReceiveHit(
                    new HitInfo(ImpactKind.Heavy, "baseball_bat", Vector3.zero, Vector3.forward, 6f));

                Assert.That(result, Is.EqualTo(HitResult.Accepted));
                Assert.That(receiver.IsReacting, Is.True);
                Assert.That(receiver.LastHitSourceId, Is.EqualTo("baseball_bat"));
                yield return null;
                Assert.That(model.transform.localPosition, Is.Not.EqualTo(rest));

                yield return new WaitForSeconds(0.35f);
                Assert.That(receiver.IsReacting, Is.False);
                Assert.That(model.transform.localPosition, Is.EqualTo(rest));
            }
            finally
            {
                Object.Destroy(killer);
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
