using System.Collections;
using ConGame.Runtime.Facts;
using ConGame.Domain.Interaction;
using ConGame.Presentation.Animation;
using ConGame.Runtime.AI;
using ConGame.Runtime.Events;
using ConGame.Runtime.Interaction;
using ConGame.Runtime.Lighting;
using ConGame.Runtime.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ConGame.Tests.PlayMode
{
    public sealed class InteractionPresentationPlayModeTests
    {
        [UnityTest]
        public IEnumerator KillerWalksFromDoorSpawnBeforeEntrySelectionCanStart()
        {
            GameObject killerObject = new GameObject("killer");
            GameObject insideObject = new GameObject("entry-inside");
            try
            {
                killerObject.transform.position = new Vector3(2f, 0f, 0f);
                insideObject.transform.position = Vector3.zero;
                KillerDirector killer = killerObject.AddComponent<KillerDirector>();
                killer.Configure(null, null, null, null, null);
                killer.ConfigureEntry(insideObject.transform, 1f);
                killer.CaptureInitialState();

                killer.PrepareEntry(4, _ => { });
                yield return new WaitForSeconds(0.15f);
                Assert.That(killer.State, Is.EqualTo(KillerState.Perceiving));
                Assert.That(killer.SelectedOutcomeId, Is.Null.Or.Empty);
                Vector3 spawn = killer.transform.position;
                killer.BeginEntry(4, _ => { });

                yield return new WaitForSeconds(0.25f);
                Assert.That(killer.State, Is.EqualTo(KillerState.Navigating));
                Assert.That(killer.MotionSpeed, Is.GreaterThan(0.5f));
                Assert.That(Vector3.Distance(killer.transform.position, spawn), Is.GreaterThan(0.15f));
                Assert.That(killer.SelectedOutcomeId, Is.Null.Or.Empty);

                yield return new WaitForSeconds(1.9f);
                Assert.That(Vector3.Distance(killer.transform.position, insideObject.transform.position),
                    Is.LessThan(0.06f));
                Assert.That(killer.MotionSpeed, Is.Zero.Within(0.01f));
            }
            finally
            {
                Object.Destroy(killerObject);
                Object.Destroy(insideObject);
            }
        }

[UnityTest]
        public IEnumerator RoomM1WiresDoorEntryHandSocketAndImpactFeedback()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByName("Room_M1");
            bool loadedByTest = !scene.IsValid() || !scene.isLoaded;
            if (loadedByTest)
            {
                yield return UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("Room_M1",
                    UnityEngine.SceneManagement.LoadSceneMode.Additive);
                scene = UnityEngine.SceneManagement.SceneManager.GetSceneByName("Room_M1");
            }

            yield return null;
            GameObject[] roots = scene.GetRootGameObjects();
            KillerDirector killer = null;
            HumanoidHandSocketBinder binder = null;
            ConGame.Presentation.Interaction.ImpactFeedbackPresenter feedback = null;
            Transform socket = null;
            Transform bat = null;
            foreach (GameObject root in roots)
            {
                killer ??= root.GetComponentInChildren<KillerDirector>(true);
                binder ??= root.GetComponentInChildren<HumanoidHandSocketBinder>(true);
                feedback ??= root.GetComponentInChildren<ConGame.Presentation.Interaction.ImpactFeedbackPresenter>(true);
                foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
                {
                    if (candidate.name == "HandSocket_R") socket = candidate;
                    if (candidate.name == "PRP_BaseballBat") bat = candidate;
                }
            }

            Assert.That(killer, Is.Not.Null);
            var anchorField = typeof(KillerDirector).GetField("entryInsideTarget",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(anchorField.GetValue(killer), Is.Not.Null);
            Assert.That(binder, Is.Not.Null);
            Assert.That(socket, Is.Not.Null);
            Assert.That(socket.parent.name, Does.Contain("RightHand"));
            Assert.That(feedback, Is.Not.Null);
            Assert.That(bat, Is.Not.Null);
            Assert.That(bat.GetComponent<CapsuleCollider>().center.y, Is.EqualTo(0.275f).Within(0.001f));

            if (loadedByTest)
            {
                yield return UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(scene);
            }
        }


        [UnityTest]
        public IEnumerator HandSocketBinderParentsSocketToRightHandBone()
        {
            GameObject player = new GameObject("player");
            GameObject rightHand = new GameObject("mixamorig:RightHand");
            GameObject socket = new GameObject("HandSocket_R");
            try
            {
                rightHand.transform.SetParent(player.transform);
                rightHand.transform.localScale = Vector3.one * 100f;
                socket.transform.SetParent(player.transform);
                Animator animator = player.AddComponent<Animator>();
                HumanoidHandSocketBinder binder = player.AddComponent<HumanoidHandSocketBinder>();
                binder.Configure(animator, socket.transform, Vector3.zero, Quaternion.identity);

                Assert.That(binder.BindNow(), Is.True);
                Assert.That(socket.transform.parent, Is.EqualTo(rightHand.transform));
                Assert.That(socket.transform.localPosition, Is.EqualTo(Vector3.zero));
                Assert.That(Vector3.Distance(socket.transform.lossyScale, Vector3.one), Is.LessThan(0.001f));
            }
            finally
            {
                Object.Destroy(player);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator SwingReachesOverheadBreakableAndPrioritizesItOverWallAndTriggerClutter()
        {
            GameObject playerObject = new GameObject("player");
            GameObject weaponObject = new GameObject("baseball_bat");
            GameObject lightObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            GameObject wallObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            GameObject eventObject = new GameObject("events");
            GameObject clutterRoot = new GameObject("trigger-clutter");
            try
            {
                PlayerInteractor player = playerObject.AddComponent<PlayerInteractor>();
                playerObject.transform.rotation = Quaternion.identity;

                InteractableEntity entity = weaponObject.AddComponent<InteractableEntity>();
                entity.Configure(null, "baseball_bat");
                MeleeWeaponCapability melee = weaponObject.AddComponent<MeleeWeaponCapability>();

                lightObject.name = "ceiling-light";
                lightObject.transform.position = new Vector3(0f, 2.2f, 1.05f);
                lightObject.transform.localScale = Vector3.one * 0.45f;
                Light light = lightObject.AddComponent<Light>();
                BreakableLight breakable = lightObject.AddComponent<BreakableLight>();
                breakable.Configure(light);

                wallObject.name = "strike-wall";
                wallObject.transform.position = new Vector3(0f, 1.2f, 1.4f);
                wallObject.transform.localScale = new Vector3(2f, 2.4f, 0.2f);
                wallObject.AddComponent<CountingHitReceiver>();

                for (int i = 0; i < 40; i++)
                {
                    GameObject trigger = new GameObject($"trigger-{i}");
                    trigger.transform.SetParent(clutterRoot.transform);
                    trigger.transform.position = new Vector3(0f, 1f, 1f);
                    trigger.AddComponent<SphereCollider>().isTrigger = true;
                }

                GameplayEventHub events = eventObject.AddComponent<GameplayEventHub>();
                int impactCount = 0;
                events.ImpactResolved += _ => impactCount++;
                Physics.SyncTransforms();

                melee.Swing(player, null, events);
                yield return new WaitForSeconds(1.05f);

                Assert.That(breakable.IsBroken, Is.True,
                    "The fixed-camera melee volume must reach the visible ceiling-light mesh.");
                Assert.That(wallObject.GetComponent<CountingHitReceiver>().HitCount, Is.Zero,
                    "A generic strike wall must not steal a breakable target's hit.");
                Assert.That(impactCount, Is.EqualTo(1));
            }
            finally
            {
                Object.Destroy(playerObject);
                Object.Destroy(weaponObject);
                Object.Destroy(lightObject);
                Object.Destroy(wallObject);
                Object.Destroy(eventObject);
                Object.Destroy(clutterRoot);
            }
        }

        [UnityTest]
        public IEnumerator SwingResolvesNonConvexMeshColliderAndClearsSwinging()
        {
            GameObject playerObject = new GameObject("player");
            GameObject weaponObject = new GameObject("baseball_bat");
            GameObject meshObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            GameObject factsObject = new GameObject("facts");
            try
            {
                playerObject.transform.rotation = Quaternion.identity;
                meshObject.name = "non-convex-target";
                meshObject.transform.position = new Vector3(0f, 0.5f, 1f);
                meshObject.transform.localScale = Vector3.one * 2f;

                MeshCollider mesh = meshObject.GetComponent<MeshCollider>();
                Assert.That(mesh, Is.Not.Null);
                Assert.That(mesh.convex, Is.False);
                CountingHitReceiver receiver = meshObject.AddComponent<CountingHitReceiver>();

                PlayerInteractor player = playerObject.AddComponent<PlayerInteractor>();
                InteractableEntity entity = weaponObject.AddComponent<InteractableEntity>();
                entity.Configure(null, "baseball_bat");
                MeleeWeaponCapability melee = weaponObject.AddComponent<MeleeWeaponCapability>();
                GameplayFactService facts = factsObject.AddComponent<GameplayFactService>();
                facts.CaptureInitialState();

                Physics.SyncTransforms();
                melee.Swing(player, facts, null);
                yield return new WaitForSeconds(1.05f);

                Assert.That(receiver.HitCount, Is.EqualTo(1));
                Assert.That(facts.CaptureSnapshot().IsSwinging, Is.False);
            }
            finally
            {
                Object.Destroy(playerObject);
                Object.Destroy(weaponObject);
                Object.Destroy(meshObject);
                Object.Destroy(factsObject);
            }
        }
    }

    public sealed class CountingHitReceiver : MonoBehaviour, IHitReceiver
    {
        public int HitCount { get; private set; }

        public HitResult ReceiveHit(in HitInfo hit)
        {
            HitCount++;
            return HitResult.Accepted;
        }
    }
}
