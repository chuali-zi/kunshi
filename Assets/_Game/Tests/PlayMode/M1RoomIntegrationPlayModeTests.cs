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
using ConGame.Runtime.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ConGame.Tests.PlayMode
{
    public sealed class M1RoomIntegrationPlayModeTests
    {
        private Scene _ownedRoom;

        [UnityTearDown]
        public IEnumerator UnloadOwnedRoom()
        {
            if (_ownedRoom.IsValid() && _ownedRoom.isLoaded)
                yield return SceneManager.UnloadSceneAsync(_ownedRoom);
            _ownedRoom = default;
        }

        [UnityTest]
        [Order(1000)]
        public IEnumerator RoomSceneRunsBreachIntoInteractiveHuntWithoutForcedExecution()
        {
            Scene room = SceneManager.GetSceneByName("Room_M1");
            bool loadedByTest = !room.IsValid() || !room.isLoaded;
            if (loadedByTest)
            {
                yield return SceneManager.LoadSceneAsync("Room_M1", LoadSceneMode.Additive);
                room = SceneManager.GetSceneByName("Room_M1");
                _ownedRoom = room;
            }

            yield return null;
            yield return null;

            GameObject roomRoot = room.GetRootGameObjects()[0];
            RoomContext context = roomRoot.GetComponentInChildren<RoomContext>(true);
            GameFlowController flow = roomRoot.GetComponentInChildren<GameFlowController>(true);
            LoopDirector loop = roomRoot.GetComponentInChildren<LoopDirector>(true);
            RoomDoorBreachController breach = roomRoot.GetComponentInChildren<RoomDoorBreachController>(true);
            KillerDirector killer = roomRoot.GetComponentInChildren<KillerDirector>(true);
            KillerTacticalController hunt = roomRoot.GetComponentInChildren<KillerTacticalController>(true);
            GameplayFactService facts = roomRoot.GetComponentInChildren<GameplayFactService>(true);
            PlayerInteractor interactor = roomRoot.GetComponentInChildren<PlayerInteractor>(true);
            PlayerNoiseEmitter noise = roomRoot.GetComponentInChildren<PlayerNoiseEmitter>(true);
            StolenLootDropController loot = roomRoot.GetComponentInChildren<StolenLootDropController>(true);

            Assert.That(context.enabled, Is.True);
            Assert.That(hunt, Is.Not.Null);
            Assert.That(noise, Is.Not.Null);
            Assert.That(killer.TacticalController, Is.SameAs(hunt));
            Assert.That(killer.transform.Find("KillerViewOrigin"), Is.Not.Null);
            Assert.That(loot, Is.Not.Null);
            Assert.That(loot.MainFirearm, Is.Not.Null);
            Assert.That(killer.GetComponent<CounterplayFlashlightTarget>(), Is.Not.Null);
            Assert.That(killer.GetComponent<KillerHitReceiver>(), Is.Not.Null);
            Assert.That(killer.GetComponent<CapsuleCollider>(), Is.Not.Null);

            Transform sofa = null;
            Transform blanket = null;
            Transform chair = null;
            Transform tacticalAnchors = null;
            foreach (Transform candidate in roomRoot.GetComponentsInChildren<Transform>(true))
            {
                switch (candidate.name)
                {
                    case "FUR_Armchair": sofa = candidate; break;
                    case "PRP_Blanket": blanket = candidate; break;
                    case "FUR_WoodChair": chair = candidate; break;
                    case "KillerTacticalAnchors": tacticalAnchors = candidate; break;
                }
            }

            Assert.That(sofa, Is.Not.Null);
            Assert.That(sofa.position.x, Is.EqualTo(-1.8f).Within(0.01f));
            Assert.That(sofa.position.z, Is.EqualTo(1.5f).Within(0.01f));
            Assert.That(sofa.GetComponent<HideCapability>().HidePose, Is.EqualTo(PlayerPose.Crouched));
            Assert.That(blanket, Is.Not.Null);
            Assert.That(blanket.GetComponent<HideCapability>().RequiresPlacedItem, Is.True);
            Assert.That(chair, Is.Not.Null);
            Assert.That(tacticalAnchors, Is.Not.Null);
            Assert.That(tacticalAnchors.childCount, Is.GreaterThanOrEqualTo(8));
            Rigidbody chairBody = chair.GetComponent<Rigidbody>();
            Assert.That(chairBody.constraints,
                Is.EqualTo(RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ));
            Assert.That(chairBody.angularDamping, Is.EqualTo(8f).Within(0.01f));

            Camera fixedOutput = roomRoot.GetComponentInChildren<Camera>(true);
            Assert.That(fixedOutput, Is.Not.Null);
            Vector3 cameraPosition = fixedOutput.transform.position;
            Quaternion cameraRotation = fixedOutput.transform.rotation;

            float awakenWait = 0f;
            while (flow.CurrentPhase == GamePhase.Awakening && awakenWait < 5.5f)
            {
                awakenWait += Time.deltaTime;
                yield return null;
            }

            Assert.That(flow.CurrentPhase, Is.EqualTo(GamePhase.FreeRoam));
            Assert.That(loop.EntryClock, Is.Not.Null);
            Assert.That(killer.State, Is.EqualTo(KillerState.Dormant));

            facts.SetMeaningfulInput(true);
            facts.SetPlayerZone(PlayerZone.UnderBed);
            facts.SetPlayerPose(PlayerPose.Hidden);
            facts.SetAtAwakeningPoint(false);

            Assert.That(flow.Request(GamePhase.Breach, TransitionPriority.Normal, "M1 live hunt test"), Is.True);
            Assert.That(flow.CommitPending(), Is.True);
            Assert.That(breach.IsRunning, Is.True);

            float huntWait = 0f;
            while (!killer.IsLiveHuntActive && huntWait < 5.5f)
            {
                huntWait += Time.deltaTime;
                yield return null;
            }

            Assert.That(flow.CurrentPhase, Is.EqualTo(GamePhase.Breach),
                "The room must remain playable until AI earns a clear shot.");
            Assert.That(breach.IsRunning, Is.False);
            Assert.That(killer.gameObject.activeSelf, Is.True);
            Assert.That(killer.IsLiveHuntActive, Is.True);
            Assert.That(killer.State, Is.Not.EqualTo(KillerState.PerformanceLocked));
            Assert.That(interactor.ControlMode, Is.EqualTo(PlayerControlMode.Free));
            Assert.That(interactor.IsMovementLocked, Is.False);
            Assert.That(fixedOutput.transform.position, Is.EqualTo(cameraPosition));
            Assert.That(fixedOutput.transform.rotation, Is.EqualTo(cameraRotation));

            Object.Destroy(roomRoot);
            yield return null;

            if (loadedByTest && room.IsValid() && room.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(room);
            }
        }
    }
}
