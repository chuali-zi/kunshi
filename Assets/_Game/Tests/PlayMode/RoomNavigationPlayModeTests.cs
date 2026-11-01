using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ConGame.Tests.PlayMode
{
    public sealed class RoomNavigationPlayModeTests
    {
        [UnityTest]
        public IEnumerator RoomGroundNavMeshConnectsEveryKillerTacticalAnchor()
        {
            Scene room = SceneManager.GetSceneByName("Room_M1");
            bool loadedByTest = !room.IsValid() || !room.isLoaded;
            if (loadedByTest)
            {
                yield return SceneManager.LoadSceneAsync("Room_M1", LoadSceneMode.Additive);
                room = SceneManager.GetSceneByName("Room_M1");
            }

            yield return null;
            Transform anchors = null;
            foreach (GameObject root in room.GetRootGameObjects())
            {
                foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
                {
                    if (candidate.name == "KillerTacticalAnchors")
                    {
                        anchors = candidate;
                        break;
                    }
                }
            }

            Assert.That(anchors, Is.Not.Null);
            Assert.That(anchors.childCount, Is.GreaterThanOrEqualTo(8));
            Assert.That(NavMesh.SamplePosition(
                new Vector3(2.18f, 0f, 0.55f), out NavMeshHit entry, 1f, NavMesh.AllAreas), Is.True);
            Assert.That(entry.position.y, Is.LessThan(0.2f), "Entry must sample the floor, not the ceiling.");

            foreach (Transform anchor in anchors)
            {
                Assert.That(NavMesh.SamplePosition(anchor.position, out NavMeshHit hit, 1f, NavMesh.AllAreas),
                    Is.True, anchor.name + " is outside baked navigation.");
                Assert.That(hit.position.y, Is.LessThan(0.25f), anchor.name + " sampled a furniture top.");
                NavMeshPath path = new NavMeshPath();
                Assert.That(NavMesh.CalculatePath(entry.position, hit.position, NavMesh.AllAreas, path),
                    Is.True, anchor.name + " could not calculate a path.");
                Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete),
                    anchor.name + " must be reachable from the doorway.");
            }

            if (loadedByTest && room.IsValid() && room.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(room);
            }
        }
    }
}