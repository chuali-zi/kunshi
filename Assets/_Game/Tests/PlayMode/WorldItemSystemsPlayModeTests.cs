using System.Collections;
using ConGame.Domain.Facts;
using ConGame.Domain.Interaction;
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
    public sealed class WorldItemSystemsPlayModeTests
    {
        [UnityTest]
        public IEnumerator HeldAndFlyingStateProtectsPlayerCollisionThenResetRestoresItem()
        {
            GameObject root = new GameObject("root");
            GameObject player = new GameObject("player");
            GameObject socket = new GameObject("hand");
            GameObject item = new GameObject("item");
            try
            {
                socket.transform.SetParent(player.transform);
                BoxCollider playerCollider = player.AddComponent<BoxCollider>();
                BoxCollider itemCollider = item.AddComponent<BoxCollider>();
                Rigidbody body = item.AddComponent<Rigidbody>();
                InteractableEntity entity = item.AddComponent<InteractableEntity>();
                entity.Configure(null, "test_item");
                StatefulWorldItem stateful = item.AddComponent<StatefulWorldItem>();
                stateful.Configure(body);
                item.transform.SetParent(root.transform);
                item.transform.localPosition = new Vector3(1f, 2f, 3f);
                stateful.CaptureInitialState();

                Assert.That(stateful.BeginHeld(socket.transform, new Collider[] { playerCollider }), Is.True);
                Assert.That(stateful.CurrentState, Is.EqualTo(ItemPhysicalState.Held));
                Assert.That(Physics.GetIgnoreCollision(itemCollider, playerCollider), Is.True);

                stateful.Release(Vector3.forward * 8f, Vector3.zero);
                Assert.That(stateful.CurrentState, Is.EqualTo(ItemPhysicalState.Flying));
                Assert.That(Physics.GetIgnoreCollision(itemCollider, playerCollider), Is.True);

                yield return new WaitForFixedUpdate();
                Assert.That(Physics.GetIgnoreCollision(itemCollider, playerCollider), Is.False);

                LoopResetContext context = new LoopResetContext(1, 2);
                stateful.Quiesce(context);
                stateful.Restore(context);
                stateful.Resume(context);
                Assert.That(stateful.CurrentState, Is.EqualTo(ItemPhysicalState.Placed));
                Assert.That(item.transform.parent, Is.EqualTo(root.transform));
                Assert.That(item.transform.localPosition, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            }
            finally
            {
                Object.Destroy(root);
                Object.Destroy(player);
                Object.Destroy(item);
            }
        }

        [UnityTest]
        public IEnumerator CarryMultiplierAndPlacementReturnPlayerToNormalMovement()
        {
            GameObject player = new GameObject("player");
            GameObject hand = new GameObject("hand");
            GameObject item = new GameObject("dresser");
            GameObject socketObject = new GameObject("placement");
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
                entity.Configure(null, "dresser");
                StatefulWorldItem stateful = item.AddComponent<StatefulWorldItem>();
                stateful.Configure(body);
                stateful.CaptureInitialState();

                PlacementSocket placement = socketObject.AddComponent<PlacementSocket>();
                placement.Configure("door", ItemCategory.None);

                Assert.That(interactor.TryCarry(entity, body, 0.33f), Is.True);
                Assert.That(motor.MovementSpeedMultiplier, Is.EqualTo(0.33f).Within(0.001f));
                Assert.That(facts.CaptureSnapshot().PlayerPose, Is.EqualTo(PlayerPose.Carrying));
                Assert.That(interactor.TryPlaceHeld(placement), Is.True);
                Assert.That(motor.MovementSpeedMultiplier, Is.EqualTo(1f));
                Assert.That(stateful.CurrentState, Is.EqualTo(ItemPhysicalState.Placed));
                Assert.That(placement.Occupant, Is.EqualTo(entity));
                Assert.That(facts.CaptureSnapshot().PlayerPose, Is.EqualTo(PlayerPose.Standing));
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
        public IEnumerator LightCoordinatorExposesAllFourGameplayStatesAndResets()
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
                Assert.That(coordinator.Recompute(), Is.EqualTo(RoomLightState.Lit));

                ceiling.ReceiveHit(new HitInfo(ImpactKind.Heavy, "bat", Vector3.zero, Vector3.forward, 1f));
                Assert.That(coordinator.Recompute(), Is.EqualTo(RoomLightState.Dim));

                table.SetPowered(false);
                Assert.That(coordinator.Recompute(), Is.EqualTo(RoomLightState.RecoverableDark));

                table.ReceiveHit(new HitInfo(ImpactKind.Heavy, "bat", Vector3.zero, Vector3.forward, 1f));
                Assert.That(coordinator.Recompute(), Is.EqualTo(RoomLightState.PersistentBlack));

                LoopResetContext context = new LoopResetContext(1, 2);
                ceiling.Restore(context);
                table.Restore(context);
                coordinator.Resume(context);
                Assert.That(coordinator.CurrentState, Is.EqualTo(RoomLightState.Lit));
                Assert.That(facts.CaptureSnapshot().RoomLight, Is.EqualTo(RoomLightState.Lit));
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
    }
}
