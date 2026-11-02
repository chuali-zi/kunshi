using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using ConGame.Bootstrap;
using ConGame.Domain.Choreography;
using ConGame.Domain.Counterplay;
using ConGame.Domain.Flow;
using ConGame.Domain.Facts;
using ConGame.Runtime.Flow;
using ConGame.Runtime.AI;
using ConGame.Runtime.Counterplay;
using ConGame.Runtime.Interaction;
using ConGame.Runtime.Loop;
using ConGame.Runtime.Player;
using ConGame.Runtime.Events;
using ConGame.Runtime.Facts;
using ConGame.Presentation.Choreography;
using ConGame.Presentation.M1;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ConGame.Tests.PlayMode
{
    public sealed class ProductionPresentationRepairTests
    {
        private Scene _loadedRoom;
        private bool _loadedByTest;

        [UnityTearDown]
        public IEnumerator UnloadRoomAfterTest()
        {
            if (_loadedByTest && _loadedRoom.IsValid() && _loadedRoom.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(_loadedRoom);
            }

            _loadedByTest = false;
            _loadedRoom = default;
        }

        [UnityTest]
        public IEnumerator RoomInitializationMountsPairedAndRuntimeAdapter()
        {
            yield return LoadProductionRoom();
            {
                GameObject root = FindGameplayRoot(_loadedRoom);
                RoomContext context = root.GetComponentInChildren<RoomContext>(true);
                PairedPerformancePlayer paired = root.GetComponentInChildren<PairedPerformancePlayer>(true);
                CounterplayPerformanceRuntimeAdapter adapter =
                    root.GetComponentInChildren<CounterplayPerformanceRuntimeAdapter>(true);

                Assert.That(context, Is.Not.Null);
                Assert.That(context.enabled, Is.True);
                Assert.That(paired, Is.Not.Null);
                Assert.That(adapter, Is.Not.Null);
                Assert.That(GetPrivateField<object>(adapter, "_performancePlayer"), Is.SameAs(paired));

                IDictionary animations = GetPrivateField<IDictionary>(paired, "_animations");
                Assert.That(animations, Is.Not.Null);
                Assert.That(animations.Count, Is.GreaterThanOrEqualTo(3),
                    "D08/D11/D21 must receive the authored player+killer animation pairs.");
            }
        }

        [UnityTest]
        public IEnumerator ProductionD21DoesNotOpenLootUntilFallSemanticEvent()
        {
            yield return LoadProductionRoom();
            {
                GameObject root = FindGameplayRoot(_loadedRoom);
                LoopDirector loop = root.GetComponentInChildren<LoopDirector>(true);
                KillerDirector killer = root.GetComponentInChildren<KillerDirector>(true);
                PlayerMotor player = root.GetComponentInChildren<PlayerMotor>(true);
                PairedPerformancePlayer paired = root.GetComponentInChildren<PairedPerformancePlayer>(true);
                StolenLootDropController loot = root.GetComponentInChildren<StolenLootDropController>(true);
                CounterplayPerformanceRuntimeAdapter adapter =
                    root.GetComponentInChildren<CounterplayPerformanceRuntimeAdapter>(true);

                Assert.That(loop, Is.Not.Null);
                Assert.That(killer, Is.Not.Null);
                Assert.That(player, Is.Not.Null);
                Assert.That(paired, Is.Not.Null);
                Assert.That(loot, Is.Not.Null);
                Assert.That(adapter, Is.Not.Null);

                loot.Restore(new LoopResetContext(loop.LoopEpoch, loop.LoopEpoch));
                root.GetComponentInChildren<GameFlowController>(true).Initialize(GamePhase.Breach);
                PlaceAtTrapContactPose(killer, player);
                killer.BeginEntry(loop.LoopEpoch, null);
                GameplayFactService facts = root.GetComponentInChildren<GameplayFactService>(true);
                facts.SetRoomLight(RoomLightState.PersistentBlack);
                facts.SetTrap(TrapState.BlanketArmed);
                facts.SetTrapPulled(true);
                root.GetComponentInChildren<GameplayEventHub>(true).EmitStimulus(KillerStimulusType.TrapPull, "blanket");
                Assert.That(paired.IsPlaying, Is.True,
                    "A confirmed pull during doorway entry must use the paired route before live hunt starts.");
                Assert.That(root.GetComponentInChildren<GameFlowController>(true).CurrentPhase,
                    Is.EqualTo(GamePhase.Counterplay));
                Assert.That(loot.State, Is.EqualTo(CounterplayLootWindowState.Inactive));

                yield return new WaitForSeconds(1.1f);
                Assert.That(loot.State, Is.EqualTo(CounterplayLootWindowState.Inactive),
                    "The 1.2s fall must finish before the D21 loot semantic event.");

                yield return new WaitForSeconds(0.2f);
                Assert.That(loot.State, Is.EqualTo(CounterplayLootWindowState.D21Choice));

                Animator killerAnimator = killer.GetComponentInChildren<Animator>(true);
                if (killerAnimator != null && killerAnimator.isHuman)
                    Assert.That(killerAnimator.GetBoneTransform(HumanBodyBones.Hips).position.y,
                        Is.LessThan(killer.transform.position.y + 0.7f), "The fallen visual must reach the floor.");
                GameObject pistol = GetPrivateField<GameObject>(loot, "pistolDrop");
                yield return new WaitForSeconds(.65f);
                ProductionPickupTestDriver.Approach(player, pistol.GetComponent<InteractableEntity>());
                Physics.SyncTransforms();
                Assert.That(player.GetComponent<PlayerInteractor>().TryInteract(), Is.True,
                    "Grab-only input must select the dropped pistol even with a usable door nearby.");
                yield return new WaitForSeconds(0.25f);
                Assert.That(loot.State, Is.EqualTo(CounterplayLootWindowState.D22AwaitMainShot));

                paired.CancelAllForLoop(loop.LoopEpoch);
                Assert.That(paired.IsPlaying, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator PairedAndGreyboxAreBothStoppedByLoopResetContract()
        {
            yield return LoadProductionRoom();
            {
                GameObject root = FindGameplayRoot(_loadedRoom);
                PairedPerformancePlayer paired = root.GetComponentInChildren<PairedPerformancePlayer>(true);
                M1GreyboxPerformancePlayer greybox =
                    root.GetComponentInChildren<M1GreyboxPerformancePlayer>(true);
                KillerDirector killer = root.GetComponentInChildren<KillerDirector>(true);
                PlayerMotor player = root.GetComponentInChildren<PlayerMotor>(true);
                LoopDirector loop = root.GetComponentInChildren<LoopDirector>(true);

                Assert.That(paired, Is.Not.Null);
                Assert.That(greybox, Is.Not.Null);
                Assert.That(killer, Is.Not.Null);
                Assert.That(player, Is.Not.Null);
                Assert.That(loop, Is.Not.Null);

                PlaceAtTrapContactPose(killer, player);
                CounterplayCatalogAsset catalog = GetPrivateField<CounterplayCatalogAsset>(killer, "catalog");
                CounterplayDefinitionAsset d21 = null;
                for (int i = 0; i < catalog.Definitions.Count; i++)
                {
                    if (catalog.Definitions[i] != null && catalog.Definitions[i].OutcomeId == "D21")
                    {
                        d21 = catalog.Definitions[i];
                        break;
                    }
                }

                Assert.That(d21, Is.Not.Null);
                PlaceAtTrapContactPose(killer, player);
                Assert.That(greybox.Play(d21, null), Is.True);
                Assert.That(greybox.IsPlaying, Is.True);
                PerformanceAnchorPair anchors =
                    PerformanceAnchorPair.CreateFacing(killer.transform, player.transform);
                ConfirmedPerformanceIntent intent = new ConfirmedPerformanceIntent(
                    "D21", loop.LoopEpoch, PerformanceConfirmationKind.TrapTrigger, in anchors);
                Assert.That(paired.TryLaunch(in intent), Is.True);
                Assert.That(paired.IsPlaying, Is.True);

                paired.CancelAllForLoop(loop.LoopEpoch + 1);
                greybox.CancelAllForLoop(loop.LoopEpoch + 1);
                yield return null;

                Assert.That(paired.IsPlaying, Is.False);
                Assert.That(paired.HasActiveAnimationGraph, Is.False);
                Assert.That(greybox.IsPlaying, Is.False);
                Assert.That(greybox.HasActiveAnimationGraph, Is.False);
            }
        }

        private IEnumerator LoadProductionRoom()
        {
            _loadedRoom = SceneManager.GetSceneByName("Room_M1");
            _loadedByTest = !_loadedRoom.IsValid() || !_loadedRoom.isLoaded;
            if (_loadedByTest)
            {
                yield return SceneManager.LoadSceneAsync("Room_M1", LoadSceneMode.Additive);
                _loadedRoom = SceneManager.GetSceneByName("Room_M1");
            }

            yield return null;
            yield return null;
        }

        private static GameObject FindGameplayRoot(Scene room)
        {
            foreach (GameObject candidate in room.GetRootGameObjects())
            {
                if (candidate.GetComponentInChildren<RoomContext>(true) != null)
                {
                    return candidate;
                }
            }

            return null;
        }

        private void PlaceAtTrapContactPose(KillerDirector killer, PlayerMotor player)
        {
            killer.gameObject.SetActive(true);
            var socket = System.Array.Find(FindGameplayRoot(_loadedRoom).GetComponentsInChildren<PlacementSocket>(true),
                candidate => candidate.name == "PRP_DoorTrapSocket");
            Assert.That(socket, Is.Not.Null);
            Vector3 killerPosition = socket.transform.position;
            killerPosition.y = .08f;
            Vector3 playerPosition = killerPosition - Vector3.forward * .7f;
            player.Teleport(playerPosition, Quaternion.identity);
            killer.transform.position = killerPosition;
            killer.transform.rotation = Quaternion.LookRotation(
                playerPosition - killer.transform.position, Vector3.up);
            player.transform.rotation = Quaternion.LookRotation(
                killer.transform.position - playerPosition, Vector3.up);
        }

        private static T GetPrivateField<T>(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            return (T)field.GetValue(target);
        }
    }
}
