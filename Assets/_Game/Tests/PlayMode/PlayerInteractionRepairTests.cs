using System.Collections;
using System.Reflection;
using ConGame.Domain.Facts;
using ConGame.Domain.Interaction;
using ConGame.Runtime.AI;
using ConGame.Runtime.Facts;
using ConGame.Runtime.Interaction;
using ConGame.Runtime.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ConGame.Tests.PlayMode
{
    // These are deliberately small component-level regressions. They do not require a loaded room
    // and therefore remain useful while the room installer is being wired in parallel.
    public sealed class PlayerInteractionRepairTests
    {
        [Test]
        public void ConfigureDragMarksBlanketAsGroundDragAndKeepsConfiguredSpeed()
        {
            GameObject item = new GameObject("blanket");
            try
            {
                item.AddComponent<InteractableEntity>();
                item.AddComponent<Rigidbody>();
                item.AddComponent<StatefulWorldItem>();
                CarryCapability carry = item.AddComponent<CarryCapability>();

                carry.ConfigureDrag(0.35f);

                Assert.That(carry.IsGroundDrag, Is.True);
                Assert.That(carry.MovementSpeedMultiplier, Is.EqualTo(0.35f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(item);
            }
        }

        [Test]
        public void TrapOnlyPullsMatchingTypeAfterPlayerUseAndTriggerDoesNotAutoPull()
        {
            GameObject factsObject = new GameObject("facts");
            GameObject trapObject = new GameObject("rug trap");
            GameObject killerObject = new GameObject("killer");
            GameObject playerObject = new GameObject("player");
            try
            {
                GameplayFactService facts = factsObject.AddComponent<GameplayFactService>();
                facts.CaptureInitialState();

                TripTrapController trap = trapObject.AddComponent<TripTrapController>();
                trap.Configure(facts, null, TrapState.RugArmed, false);

                BoxCollider killerCollider = killerObject.AddComponent<BoxCollider>();
                killerObject.AddComponent<KillerDirector>();
                MethodInfo enter = typeof(TripTrapController).GetMethod(
                    "OnTriggerEnter", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(enter, Is.Not.Null);
                enter.Invoke(trap, new object[] { killerCollider });

                facts.SetTrap(TrapState.RugArmed);
                Assert.That(facts.CaptureSnapshot().TrapPulled, Is.False,
                    "entering the trap records presence but must not pull automatically");
                Assert.That(trap.Use(new InteractionContext(null, null)), Is.EqualTo(InteractionResult.Rejected),
                    "a trap cannot be pulled without a player interaction");

                PlayerInteractor player = playerObject.AddComponent<PlayerInteractor>();
                playerObject.transform.position = new Vector3(0f, 0f, -0.5f);
                trapObject.transform.position = Vector3.zero;
                Assert.That(trap.Use(new InteractionContext(player, null)), Is.EqualTo(InteractionResult.Accepted));
                Assert.That(facts.CaptureSnapshot().TrapPulled, Is.True);

                facts.SetTrap(TrapState.BlanketArmed);
                facts.SetTrapPulled(false);
                Assert.That(trap.CanUse(new InteractionContext(player, null)), Is.False,
                    "a rug controller must not consume a blanket armed state");
            }
            finally
            {
                Object.DestroyImmediate(playerObject);
                Object.DestroyImmediate(killerObject);
                Object.DestroyImmediate(trapObject);
                Object.DestroyImmediate(factsObject);
            }
        }

        [Test]
        public void ConfigureNormalCarryClearsGroundDragMode()
        {
            GameObject item = new GameObject("carry item");
            try
            {
                item.AddComponent<InteractableEntity>();
                item.AddComponent<Rigidbody>();
                item.AddComponent<StatefulWorldItem>();
                CarryCapability carry = item.AddComponent<CarryCapability>();

                carry.ConfigureDrag();
                carry.Configure(0.8f);

                Assert.That(carry.IsGroundDrag, Is.False);
                Assert.That(carry.MovementSpeedMultiplier, Is.EqualTo(0.8f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(item);
            }
        }

        [Test]
        public void ReapplyingControlModePreservesPendingHoldAndHandoffButModeChangeCancelsThem()
        {
            GameObject playerObject = new GameObject("player");
            GameObject itemObject = new GameObject("pickup");
            GameObject handObject = new GameObject("hand");
            HandoffSignalReceiver signals = playerObject.AddComponent<HandoffSignalReceiver>();
            playerObject.SetActive(false);
            PlayerInteractor player = playerObject.AddComponent<PlayerInteractor>();
            player.Configure(null, null, handObject.transform, null, null, null, null, signals);
            try
            {
                FieldInfo holdField = typeof(PlayerInteractor).GetField("_interactHold", BindingFlags.Instance | BindingFlags.NonPublic);
                FieldInfo holdKindField = typeof(PlayerInteractor).GetField("_pendingHoldKind", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(holdField, Is.Not.Null);
                Assert.That(holdKindField, Is.Not.Null);
                HoldDurationGate hold = new HoldDurationGate();
                hold.Begin(Time.unscaledTime, 0.35f);
                holdField.SetValue(player, hold);
                holdKindField.SetValue(player, System.Enum.Parse(holdKindField.FieldType, "UseOrPickUp"));
                playerObject.SetActive(true);

                player.SetControlMode(PlayerControlMode.Free);
                Assert.That(((HoldDurationGate)holdField.GetValue(player)).IsPending, Is.True,
                    "reapplying Free must leave a pending short/long Interact decision intact");

                player.SetControlMode(PlayerControlMode.Blocked);
                Assert.That(((HoldDurationGate)holdField.GetValue(player)).IsPending, Is.False,
                    "a real transition to Blocked cancels a pending hold");

                player.SetControlMode(PlayerControlMode.Free);
                itemObject.AddComponent<BoxCollider>();
                Rigidbody body = itemObject.AddComponent<Rigidbody>();
                body.isKinematic = true;
                InteractableEntity entity = itemObject.AddComponent<InteractableEntity>();
                entity.Configure(null, "pickup");
                StatefulWorldItem stateful = itemObject.AddComponent<StatefulWorldItem>();
                stateful.Configure(body);
                Assert.That(player.TryHold(entity, body), Is.True);
                Assert.That(player.IsHandoffPending, Is.True);

                player.SetControlMode(PlayerControlMode.Free);
                Assert.That(player.IsHandoffPending, Is.True,
                    "reapplying Free must not cancel a pending item handoff");

                player.SetControlMode(PlayerControlMode.Blocked);
                Assert.That(player.IsHandoffPending, Is.False,
                    "a real transition to Blocked cancels a pending handoff");
                Assert.That(player.HeldEntity, Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(itemObject);
                Object.DestroyImmediate(handObject);
                Object.DestroyImmediate(playerObject);
            }
        }

        [Test]
        public void TrapWithConfiguredSocketRequiresAnActualSocketOccupant()
        {
            GameObject factsObject = new GameObject("facts");
            GameObject trapObject = new GameObject("door trap");
            GameObject socketObject = new GameObject("PRP_DoorTrapSocket");
            GameObject killerObject = new GameObject("killer");
            GameObject playerObject = new GameObject("player");
            try
            {
                GameplayFactService facts = factsObject.AddComponent<GameplayFactService>();
                facts.CaptureInitialState();
                PlacementSocket socket = socketObject.AddComponent<PlacementSocket>();
                TripTrapController trap = trapObject.AddComponent<TripTrapController>();
                trap.Configure(facts, null, TrapState.RugArmed, false);
                trap.ConfigureSocket(socket);
                facts.SetTrap(TrapState.RugArmed);

                BoxCollider killerCollider = killerObject.AddComponent<BoxCollider>();
                killerObject.AddComponent<KillerDirector>();
                typeof(TripTrapController).GetMethod("OnTriggerEnter", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(trap, new object[] { killerCollider });
                PlayerInteractor player = playerObject.AddComponent<PlayerInteractor>();

                Assert.That(trap.Use(new InteractionContext(player, null)), Is.EqualTo(InteractionResult.Rejected));
                Assert.That(facts.CaptureSnapshot().TrapPulled, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(playerObject);
                Object.DestroyImmediate(killerObject);
                Object.DestroyImmediate(socketObject);
                Object.DestroyImmediate(trapObject);
                Object.DestroyImmediate(factsObject);
            }
        }

        [Test]
        public void ScannerUsesFacingAndDistanceBeforeCapabilityPriority()
        {
            GameObject playerObject = new GameObject("player");
            GameObject lamp = new GameObject("lamp");
            GameObject dresser = new GameObject("dresser");
            try
            {
                PlayerInteractor player = playerObject.AddComponent<PlayerInteractor>();
                InteractionScanner scanner = playerObject.AddComponent<InteractionScanner>();
                scanner.Configure(player, 2f, ~0);

                lamp.transform.position = new Vector3(0f, 1f, 1f);
                lamp.AddComponent<BoxCollider>();
                InteractableEntity lampEntity = lamp.AddComponent<InteractableEntity>();
                lampEntity.Configure(null, "lamp");
                UseCapability lampUse = lamp.AddComponent<UseCapability>();
                lampUse.Configure(lamp.AddComponent<RepairAlwaysUseHandler>());

                dresser.transform.position = new Vector3(0.8f, 1f, 0.2f);
                dresser.AddComponent<BoxCollider>();
                InteractableEntity dresserEntity = dresser.AddComponent<InteractableEntity>();
                dresserEntity.Configure(null, "dresser");
                Rigidbody body = dresser.AddComponent<Rigidbody>();
                body.isKinematic = true;
                dresser.AddComponent<StatefulWorldItem>();
                dresser.AddComponent<CarryCapability>();

                Assert.That(scanner.Refresh(), Is.EqualTo(lampEntity));
                playerObject.transform.forward = Vector3.right;
                Assert.That(scanner.Refresh(), Is.EqualTo(dresserEntity));
            }
            finally
            {
                Object.DestroyImmediate(dresser);
                Object.DestroyImmediate(lamp);
                Object.DestroyImmediate(playerObject);
            }
        }

        [Test]
        public void ScannerRanksNearbyFloorLootBeforeFartherTabletopItemWithoutExtendingRange()
        {
            var playerObject = new GameObject("player");
            var loot = new GameObject("floor loot");
            var tabletop = new GameObject("tabletop item");
            try
            {
                playerObject.transform.position = new Vector3(100f, .08f, 0f);
                var player = playerObject.AddComponent<PlayerInteractor>();
                var scanner = playerObject.AddComponent<InteractionScanner>();
                scanner.Configure(player, 1.2f, ~0);
                loot.transform.position = new Vector3(100f, .14f, .65f);
                tabletop.transform.position = new Vector3(100f, 1.08f, .85f);
                foreach (var target in new[] { loot, tabletop })
                {
                    target.AddComponent<BoxCollider>().size = Vector3.one * .06f;
                    target.AddComponent<Rigidbody>().isKinematic = true;
                    target.AddComponent<InteractableEntity>();
                    target.AddComponent<PickupCapability>();
                }
                Physics.SyncTransforms();
                Assert.That(scanner.RefreshForVerb(InteractionVerb.PickUp),
                    Is.SameAs(loot.GetComponent<InteractableEntity>()));

                // Planar ranking must not make floor objects outside the chest sphere reachable.
                loot.transform.position += Vector3.forward * .65f;
                Physics.SyncTransforms();
                Assert.That(scanner.RefreshForVerb(InteractionVerb.PickUp),
                    Is.SameAs(tabletop.GetComponent<InteractableEntity>()));
            }
            finally
            {
                Object.DestroyImmediate(tabletop);
                Object.DestroyImmediate(loot);
                Object.DestroyImmediate(playerObject);
            }
        }

        [Test]
        public void ScannerFiltersCandidatesWithNoExecutableCapability()
        {
            GameObject playerObject = new GameObject("player");
            GameObject blocked = new GameObject("blocked");
            try
            {
                PlayerInteractor player = playerObject.AddComponent<PlayerInteractor>();
                InteractionScanner scanner = playerObject.AddComponent<InteractionScanner>();
                scanner.Configure(player, 2f, ~0);
                blocked.transform.position = new Vector3(0f, 1f, 0.5f);
                blocked.AddComponent<BoxCollider>();
                InteractableEntity entity = blocked.AddComponent<InteractableEntity>();
                entity.Configure(null, "blocked");
                UseCapability use = blocked.AddComponent<UseCapability>();
                use.Configure(blocked.AddComponent<RepairNeverUseHandler>());

                Assert.That(scanner.Refresh(), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(blocked);
                Object.DestroyImmediate(playerObject);
            }
        }

        [Test]
        public void ScannerExposesArmedTrapWithinPullRangeButKeepsOrdinaryRadius()
        {
            GameObject factsObject = new GameObject("facts");
            GameObject playerObject = new GameObject("player");
            GameObject trapObject = new GameObject("armed rug");
            GameObject killerObject = new GameObject("killer");
            GameObject ordinaryObject = new GameObject("ordinary item");
            try
            {
                GameplayFactService facts = factsObject.AddComponent<GameplayFactService>();
                facts.CaptureInitialState();
                facts.SetTrap(TrapState.RugArmed);

                PlayerInteractor player = playerObject.AddComponent<PlayerInteractor>();
                playerObject.transform.forward = Vector3.forward;
                InteractionScanner scanner = playerObject.AddComponent<InteractionScanner>();
                scanner.Configure(player, 1.2f, ~0);
                player.Configure(null, scanner, playerObject.transform, null, facts, null, null);

                trapObject.transform.position = new Vector3(0f, 0f, 1.4f);
                BoxCollider trapCollider = trapObject.AddComponent<BoxCollider>();
                trapCollider.isTrigger = true;
                trapCollider.size = new Vector3(0.2f, 0.1f, 0.2f);
                InteractableEntity trapEntity = trapObject.AddComponent<InteractableEntity>();
                trapEntity.Configure(null, "rug");
                TripTrapController trap = trapObject.AddComponent<TripTrapController>();
                trap.Configure(facts, null, TrapState.RugArmed, false);
                trapObject.AddComponent<UseCapability>().Configure(trap);

                killerObject.AddComponent<KillerDirector>();
                Collider killerCollider = killerObject.AddComponent<BoxCollider>();
                typeof(TripTrapController).GetMethod("OnTriggerEnter", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(trap, new object[] { killerCollider });
                Physics.SyncTransforms();

                InteractionContext pullContext = new InteractionContext(player, trapEntity);
                Vector3 scanOrigin = playerObject.transform.position + Vector3.up;
                Assert.That(Vector3.Distance(scanOrigin, trapCollider.ClosestPoint(scanOrigin)), Is.GreaterThan(1.2f),
                    "the trap fixture must actually lie outside the ordinary scan sphere");
                Assert.That(trap.CanUse(pullContext), Is.True);
                playerObject.transform.position = new Vector3(0f, 0f, -0.31f);
                Physics.SyncTransforms();
                Assert.That(trap.CanUse(pullContext), Is.False, "an armed trap beyond 1.6m is not executable");
                playerObject.transform.position = Vector3.zero;
                playerObject.transform.forward = Vector3.back;
                Physics.SyncTransforms();
                Assert.That(trap.CanUse(pullContext), Is.False, "an armed trap behind the player is not executable");
                playerObject.transform.forward = Vector3.forward;
                Physics.SyncTransforms();

                Assert.That(scanner.RefreshForVerb(InteractionVerb.Use), Is.EqualTo(trapEntity));
                Assert.That(scanner.Refresh(), Is.EqualTo(trapEntity));
                Assert.That(player.TryInteract(), Is.True);
                Assert.That(facts.CaptureSnapshot().TrapPulled, Is.True,
                    "the interactor Use route must commit the pull from 1.4m");

                facts.SetTrap(TrapState.None);
                trap.Configure(facts, null, TrapState.RugArmed, true);
                Assert.That(trap.CanUse(pullContext), Is.True, "an unarmed rug can still be armed with Use");
                Assert.That(trap.CanPull(pullContext), Is.False,
                    "arming Use must not qualify as an extended-range pull");
                Physics.SyncTransforms();
                Assert.That(scanner.Refresh(), Is.Null,
                    "an unarmed rug outside the regular scan range must not be remotely armed");

                ordinaryObject.transform.position = new Vector3(0f, 1f, 1.5f);
                BoxCollider ordinaryCollider = ordinaryObject.AddComponent<BoxCollider>();
                ordinaryCollider.size = new Vector3(0.2f, 0.2f, 0.2f);
                InteractableEntity ordinary = ordinaryObject.AddComponent<InteractableEntity>();
                ordinary.Configure(null, "ordinary");
                ordinaryObject.AddComponent<UseCapability>().Configure(ordinaryObject.AddComponent<RepairAlwaysUseHandler>());
                trapObject.SetActive(false);
                Physics.SyncTransforms();

                Assert.That(scanner.Refresh(), Is.Null);
                ordinaryCollider.size = new Vector3(0.2f, 0.2f, 1.2f);
                Physics.SyncTransforms();
                Assert.That(scanner.Refresh(), Is.EqualTo(ordinary),
                    "an ordinary collider whose edge overlaps the configured scan sphere stays selectable");
            }
            finally
            {
                Object.DestroyImmediate(ordinaryObject);
                Object.DestroyImmediate(killerObject);
                Object.DestroyImmediate(trapObject);
                Object.DestroyImmediate(playerObject);
                Object.DestroyImmediate(factsObject);
            }
        }

        [Test]
        public void ScannerKeepsGroundBlanketAheadOfSlightlyOffsetDoor()
        {
            GameObject playerObject = new GameObject("player");
            GameObject blanket = new GameObject("placed blanket");
            GameObject door = new GameObject("door");
            try
            {
                playerObject.transform.position = new Vector3(1.65f, 0.08f, 0.20f);
                playerObject.transform.forward = Vector3.right;
                PlayerInteractor player = playerObject.AddComponent<PlayerInteractor>();
                InteractionScanner scanner = playerObject.AddComponent<InteractionScanner>();
                scanner.Configure(player, 1.5f, ~0);

                blanket.transform.position = new Vector3(2.55f, 0.08f, 0.20f);
                blanket.AddComponent<BoxCollider>();
                InteractableEntity blanketEntity = blanket.AddComponent<InteractableEntity>();
                blanketEntity.Configure(null, "blanket");
                UseCapability blanketUse = blanket.AddComponent<UseCapability>();
                blanketUse.Configure(blanket.AddComponent<RepairAlwaysUseHandler>());
                Rigidbody blanketBody = blanket.AddComponent<Rigidbody>();
                blanketBody.isKinematic = true;
                blanket.AddComponent<StatefulWorldItem>();
                CarryCapability blanketCarry = blanket.AddComponent<CarryCapability>();
                blanketCarry.ConfigureDrag();

                door.transform.position = new Vector3(2.70f, 1.05f, 0.55f);
                door.AddComponent<BoxCollider>();
                InteractableEntity doorEntity = door.AddComponent<InteractableEntity>();
                doorEntity.Configure(null, "room door");
                UseCapability doorUse = door.AddComponent<UseCapability>();
                doorUse.Configure(door.AddComponent<RepairAlwaysUseHandler>());

                Assert.That(scanner.Refresh(), Is.EqualTo(blanketEntity));
            }
            finally
            {
                Object.DestroyImmediate(door);
                Object.DestroyImmediate(blanket);
                Object.DestroyImmediate(playerObject);
            }
        }

        [Test]
        public void ScannerChoosesGroundBlanketOverCoLocatedBedClimb()
        {
            GameObject playerObject = new GameObject("player");
            GameObject bed = new GameObject("bed");
            GameObject blanket = new GameObject("blanket");
            GameObject anchor = new GameObject("bed stand anchor");
            try
            {
                playerObject.transform.position = new Vector3(1.3f, 0.08f, 1.05f);
                playerObject.transform.forward = Vector3.forward;
                PlayerInteractor player = playerObject.AddComponent<PlayerInteractor>();
                InteractionScanner scanner = playerObject.AddComponent<InteractionScanner>();
                scanner.Configure(player, 1.5f, ~0);

                bed.transform.position = new Vector3(1.3f, 0.30f, 2.1f);
                BoxCollider bedCollider = bed.AddComponent<BoxCollider>();
                bedCollider.size = new Vector3(2f, 0.6f, 1f);
                InteractableEntity bedEntity = bed.AddComponent<InteractableEntity>();
                bedEntity.Configure(null, "bed");
                anchor.transform.position = new Vector3(1.3f, 1.5f, 2.1f);
                ClimbCapability climb = bed.AddComponent<ClimbCapability>();
                climb.Configure(anchor.transform, 0.4f, false);

                blanket.transform.position = new Vector3(1.3f, 0.65f, 2.1f);
                BoxCollider blanketCollider = blanket.AddComponent<BoxCollider>();
                blanketCollider.size = new Vector3(1.2f, 0.12f, 0.86f);
                InteractableEntity blanketEntity = blanket.AddComponent<InteractableEntity>();
                blanketEntity.Configure(null, "blanket");
                Rigidbody blanketBody = blanket.AddComponent<Rigidbody>();
                blanketBody.isKinematic = true;
                blanket.AddComponent<StatefulWorldItem>();
                CarryCapability carry = blanket.AddComponent<CarryCapability>();
                carry.ConfigureDrag();

                Assert.That(scanner.Refresh(), Is.EqualTo(blanketEntity));
            }
            finally
            {
                Object.DestroyImmediate(anchor);
                Object.DestroyImmediate(blanket);
                Object.DestroyImmediate(bed);
                Object.DestroyImmediate(playerObject);
            }
        }

        [Test]
        public void BlanketUseRejectsMissingTrapContactInsteadOfUsingGlobalFact()
        {
            GameObject factsObject = new GameObject("facts");
            GameObject itemObject = new GameObject("blanket");
            GameObject socketObject = new GameObject("blanket socket");
            try
            {
                GameplayFactService facts = factsObject.AddComponent<GameplayFactService>();
                facts.CaptureInitialState();
                facts.SetTrap(TrapState.BlanketArmed);

                StatefulWorldItem item = itemObject.AddComponent<StatefulWorldItem>();
                PlacementSocket socket = socketObject.AddComponent<PlacementSocket>();
                FieldInfo currentSocket = typeof(StatefulWorldItem).GetField(
                    "<CurrentSocket>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(currentSocket, Is.Not.Null);
                currentSocket.SetValue(item, socket);

                BlanketTrapUseHandler handler = itemObject.AddComponent<BlanketTrapUseHandler>();
                handler.Configure(facts);

                Assert.That(handler.CanUse(new InteractionContext(null, null)), Is.False);
                Assert.That(handler.Use(new InteractionContext(null, null)), Is.EqualTo(InteractionResult.Rejected));
                Assert.That(facts.CaptureSnapshot().TrapPulled, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(socketObject);
                Object.DestroyImmediate(itemObject);
                Object.DestroyImmediate(factsObject);
            }
        }

        [UnityTest]
        public IEnumerator TrapTriggerTracksKillerPresenceWithoutAutoPulling()
        {
            GameObject factsObject = new GameObject("isolated facts");
            GameObject trapObject = new GameObject("isolated trap");
            GameObject killerObject = new GameObject("isolated killer");
            try
            {
                factsObject.transform.position = new Vector3(100f, 0f, 0f);
                trapObject.transform.position = new Vector3(100f, 0.08f, 0f);
                trapObject.transform.localScale = new Vector3(0.9f, 0.06f, 0.55f);
                killerObject.transform.position = new Vector3(98f, 0.08f, 0f);

                GameplayFactService facts = factsObject.AddComponent<GameplayFactService>();
                facts.CaptureInitialState();
                BoxCollider trigger = trapObject.AddComponent<BoxCollider>();
                trigger.isTrigger = true;
                trigger.size = Vector3.one;
                TripTrapController trap = trapObject.AddComponent<TripTrapController>();
                trap.Configure(facts, null, TrapState.RugArmed, false);

                killerObject.AddComponent<KillerDirector>();
                CapsuleCollider killerCollider = killerObject.AddComponent<CapsuleCollider>();
                killerCollider.radius = 0.42f;
                killerCollider.height = 1.8f;
                killerCollider.center = new Vector3(0f, 1f, 0f);

                yield return new WaitForFixedUpdate();
                Assert.That(trap.OccupantPresent, Is.False);
                killerObject.transform.position = trapObject.transform.position;
                yield return new WaitForFixedUpdate();
                Assert.That(trap.OccupantPresent, Is.True);
                Assert.That(facts.CaptureSnapshot().TrapPulled, Is.False);

                killerObject.transform.position = trapObject.transform.position + Vector3.left * 2f;
                yield return new WaitForFixedUpdate();
                Assert.That(trap.OccupantPresent, Is.False);
                Assert.That(facts.CaptureSnapshot().TrapPulled, Is.False);
            }
            finally
            {
                Object.Destroy(trapObject);
                Object.Destroy(killerObject);
                Object.Destroy(factsObject);
            }
        }
    }

    public sealed class RepairAlwaysUseHandler : MonoBehaviour, IUseHandler
    {
        public bool CanUse(in InteractionContext context) => true;
        public InteractionResult Use(in InteractionContext context) => InteractionResult.Accepted;
    }

    public sealed class RepairNeverUseHandler : MonoBehaviour, IUseHandler
    {
        public bool CanUse(in InteractionContext context) => false;
        public InteractionResult Use(in InteractionContext context) => InteractionResult.Rejected;
    }
}
