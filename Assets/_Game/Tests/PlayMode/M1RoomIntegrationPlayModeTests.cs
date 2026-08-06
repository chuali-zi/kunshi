using System.Collections;
using ConGame.Bootstrap;
using ConGame.Domain.Facts;
using ConGame.Domain.Flow;
using ConGame.Runtime.AI;
using ConGame.Runtime.Counterplay;
using ConGame.Runtime.Facts;
using ConGame.Runtime.Flow;
using ConGame.Runtime.Loop;
using ConGame.Runtime.Interaction;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ConGame.Tests.PlayMode
{
    public sealed class M1RoomIntegrationPlayModeTests
    {
        [UnityTest]
        [Order(1000)]
        public IEnumerator RoomSceneRunsBreachIntoImplementedD01Performance()
        {
            Scene room = SceneManager.GetSceneByName("Room_M1");
            bool loadedByTest = !room.IsValid() || !room.isLoaded;
            if (loadedByTest)
            {
                yield return SceneManager.LoadSceneAsync("Room_M1", LoadSceneMode.Additive);
                room = SceneManager.GetSceneByName("Room_M1");
            }

            yield return null;
            yield return null;

            GameObject roomRoot = room.GetRootGameObjects()[0];
            RoomContext context = roomRoot.GetComponentInChildren<RoomContext>(true);
            GameFlowController flow = roomRoot.GetComponentInChildren<GameFlowController>(true);
            LoopDirector loop = roomRoot.GetComponentInChildren<LoopDirector>(true);
            RoomDoorBreachController breach = roomRoot.GetComponentInChildren<RoomDoorBreachController>(true);
            KillerDirector killer = roomRoot.GetComponentInChildren<KillerDirector>(true);
            GameplayFactService facts = roomRoot.GetComponentInChildren<GameplayFactService>(true);
            StolenLootDropController loot = roomRoot.GetComponentInChildren<StolenLootDropController>(true);

            Assert.That(context.enabled, Is.True);
            Assert.That(loot, Is.Not.Null);
            Assert.That(loot.MainFirearm, Is.Not.Null);
            Assert.That(killer.GetComponent<CounterplayFlashlightTarget>(), Is.Not.Null,
                "Inactive Killer must still receive the D21 flashlight target at scene startup.");
            Assert.That(killer.GetComponent<KillerHitReceiver>(), Is.Not.Null,
                "Killer must receive real melee and thrown-item hits.");
            Assert.That(killer.GetComponent<CapsuleCollider>(), Is.Not.Null,
                "Killer must expose a physical hurtbox for weapon feedback.");

            Transform sofa = null;
            Transform blanket = null;
            Transform chair = null;
            foreach (Transform candidate in roomRoot.GetComponentsInChildren<Transform>(true))
            {
                switch (candidate.name)
                {
                    case "FUR_Armchair":
                        sofa = candidate;
                        break;
                    case "PRP_Blanket":
                        blanket = candidate;
                        break;
                    case "FUR_WoodChair":
                        chair = candidate;
                        break;
                }
            }

            Assert.That(sofa, Is.Not.Null);
            Assert.That(sofa.position.x, Is.EqualTo(-1.25f).Within(0.01f));
            Assert.That(sofa.GetComponent<HideCapability>().HidePose, Is.EqualTo(PlayerPose.Crouched));
            Assert.That(blanket, Is.Not.Null);
            Assert.That(blanket.GetComponent<HideCapability>().RequiresPlacedItem, Is.True);
            Assert.That(chair, Is.Not.Null);
            Rigidbody chairBody = chair.GetComponent<Rigidbody>();
            Assert.That(chairBody.constraints,
                Is.EqualTo(RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ));
            Assert.That(chairBody.angularDamping, Is.EqualTo(8f).Within(0.01f));

            Camera fixedOutput = roomRoot.GetComponentInChildren<Camera>(true);
            Assert.That(fixedOutput, Is.Not.Null);
            Assert.That(fixedOutput.fieldOfView, Is.EqualTo(72f).Within(0.01f));
            Assert.That(fixedOutput.transform.position.z, Is.EqualTo(-2.58f).Within(0.01f));

            float awakenWait = 0f;
            while (flow.CurrentPhase == GamePhase.Awakening && awakenWait < 5.5f)
            {
                awakenWait += Time.deltaTime;
                yield return null;
            }

            Assert.That(flow.CurrentPhase, Is.EqualTo(GamePhase.FreeRoam));
            Assert.That(loop.EntryClock, Is.Not.Null);
            Assert.That(killer.State, Is.EqualTo(KillerState.Dormant));

            // Leave idle-awakening facts so Entry selects fallback D01 instead of D02.
            facts.SetMeaningfulInput(true);
            facts.SetPlayerZone(PlayerZone.Open);
            facts.SetAtAwakeningPoint(false);

            Assert.That(flow.Request(GamePhase.Breach, TransitionPriority.Normal, "M1 integration test"), Is.True);
            Assert.That(flow.CommitPending(), Is.True);
            Assert.That(breach.IsRunning, Is.True);

            yield return new WaitForSeconds(3.2f);

            Assert.That(flow.CurrentPhase, Is.EqualTo(GamePhase.Counterplay));
            Assert.That(breach.IsRunning, Is.False);
            Assert.That(killer.gameObject.activeSelf, Is.True);
            Assert.That(killer.SelectedOutcomeId, Is.EqualTo("D01"));
            Assert.That(killer.State, Is.EqualTo(KillerState.PerformanceLocked));

            Object.Destroy(roomRoot);
            yield return null;

            if (loadedByTest && room.IsValid() && room.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(room);
            }
        }
    }
}
