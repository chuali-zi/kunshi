using System.Collections;
using ConGame.Bootstrap;
using ConGame.Domain.Facts;
using ConGame.Domain.Flow;
using ConGame.Runtime.AI;
using ConGame.Runtime.Facts;
using ConGame.Runtime.Flow;
using ConGame.Runtime.Loop;
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

            Assert.That(context.enabled, Is.True);

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

            yield return new WaitForSeconds(1.05f);

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
