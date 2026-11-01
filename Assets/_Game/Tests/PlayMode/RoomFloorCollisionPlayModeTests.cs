using System.Collections;
using ConGame.Runtime.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ConGame.Tests.PlayMode
{
    public sealed class RoomFloorCollisionPlayModeTests
    {
        [UnityTest]
        public IEnumerator RoomFloorSupportsPlayerMovementFromSpawn()
        {
            Scene room = SceneManager.GetSceneByName("Room_M1");
            bool loadedByTest = !room.IsValid() || !room.isLoaded;
            if (loadedByTest)
            {
                yield return SceneManager.LoadSceneAsync("Room_M1", LoadSceneMode.Additive);
                room = SceneManager.GetSceneByName("Room_M1");
            }

            yield return null;

            GameObject roomRoot = room.GetRootGameObjects()[0];
            Transform floor = roomRoot.transform.Find("Environment/Floor");
            PlayerMotor motor = roomRoot.GetComponentInChildren<PlayerMotor>(true);
            CharacterController controller = motor.GetComponent<CharacterController>();

            Assert.That(floor, Is.Not.Null);
            BoxCollider floorCollider = floor.GetComponent<BoxCollider>();
            Assert.That(floorCollider, Is.Not.Null);
            Assert.That(floorCollider.enabled, Is.True, "The hidden greybox floor must keep collision enabled.");
            Assert.That(floorCollider.isTrigger, Is.False);

            Physics.SyncTransforms();
            bool groundedAtSpawn = Physics.Raycast(
                motor.transform.position + Vector3.up * 0.5f,
                Vector3.down,
                out RaycastHit hit,
                2f,
                ~0,
                QueryTriggerInteraction.Ignore);
            Assert.That(groundedAtSpawn, Is.True, "Player spawn must have solid floor below it.");
            Assert.That(hit.collider, Is.EqualTo(floorCollider));

            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Assert.That(controller.enabled, Is.False,
                "The production opener keeps the CharacterController disabled while seated.");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
            InputSystem.Update();
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
            yield return null;
            Assert.That(controller.enabled, Is.True,
                "A real first gameplay input must stand the player and restore the CharacterController.");

            Vector3 positionBeforeMove = motor.transform.position;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.S));
            InputSystem.Update();
            yield return new WaitForSeconds(0.25f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();

            Assert.That(Vector3.Distance(motor.transform.position, positionBeforeMove), Is.GreaterThan(0.1f),
                "Movement after standing should run through the production input and motor path.");

            Assert.That(motor.transform.position.y, Is.GreaterThanOrEqualTo(-0.01f),
                "Moving away from the spawn rug must not let the player fall through the room floor.");

            InputSystem.RemoveDevice(keyboard);

            if (loadedByTest)
            {
                yield return SceneManager.UnloadSceneAsync(room);
            }
        }
    }
}
