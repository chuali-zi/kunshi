using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using ConGame.Domain.Facts;
using ConGame.Domain.Flow;
using ConGame.Domain.Interaction;
using ConGame.Presentation.Choreography;
using ConGame.Presentation.Props;
using ConGame.Bootstrap;
using ConGame.Runtime.AI;
using ConGame.Runtime.Facts;
using ConGame.Runtime.Flow;
using ConGame.Runtime.Interaction;
using ConGame.Runtime.Lighting;
using ConGame.Runtime.Loop;
using ConGame.Runtime.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ConGame.Tests.PlayMode
{
    public sealed class ProductionD21FlashlightTests : ProductionInputTestFixture
    {
        private Scene _room;
        private bool _loadedByTest;
        private Keyboard _keyboard;

        [UnityTearDown]
        public IEnumerator UnloadRoom()
        {
            if (_loadedByTest && _room.IsValid() && _room.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(_room);
            }

            if (_keyboard != null)
            {
                InputSystem.RemoveDevice(_keyboard);
                _keyboard = null;
            }

            _room = default;
            _loadedByTest = false;
        }

        [UnityTest]
        public IEnumerator ProductionFlashlightChoiceRunsGrappleDropsAndRecoversTheLight()
        {
            yield return LoadRoom();

            GameFlowController flow = FindInRoom<GameFlowController>();
            LoopDirector loop = FindInRoom<LoopDirector>();
            PlayerMotor motor = FindInRoom<PlayerMotor>();
            PlayerInteractor player = FindInRoom<PlayerInteractor>();
            KillerDirector killer = FindInRoom<KillerDirector>();
            PairedPerformancePlayer paired = FindInRoom<PairedPerformancePlayer>();
            StolenLootDropController loot = FindInRoom<StolenLootDropController>();

            Assert.That(flow, Is.Not.Null);
            Assert.That(loop, Is.Not.Null);
            Assert.That(motor, Is.Not.Null);
            Assert.That(player, Is.Not.Null);
            Assert.That(killer, Is.Not.Null);
            Assert.That(paired, Is.Not.Null);
            Assert.That(loot, Is.Not.Null);

            GameplayFactService facts = FindInRoom<GameplayFactService>();
            InteractableEntity blanket = FindByName<InteractableEntity>("PRP_Blanket");
            PlacementSocket doorSocket = FindByName<PlacementSocket>("PRP_DoorTrapSocket");
            TripTrapController trap = doorSocket == null ? null : doorSocket.GetComponent<TripTrapController>();
            BreakableLight[] lamps = FindAllInRoom<BreakableLight>();
            BreakableLight tableLamp = FindLamp(lamps, "PRP_TableLamp");
            BreakableLight ceilingLamp = FindLamp(lamps, "PRP_CeilingLamp");
            Assert.That(facts, Is.Not.Null);
            Assert.That(blanket, Is.Not.Null);
            Assert.That(doorSocket, Is.Not.Null);
            Assert.That(trap, Is.Not.Null);
            Assert.That(tableLamp, Is.Not.Null);
            Assert.That(ceilingLamp, Is.Not.Null);

            flow.Initialize(GamePhase.Breach);
            FaceAndMove(motor, blanket.transform.position + Vector3.back * 0.65f, blanket.transform.position);
            Physics.SyncTransforms();
            yield return PressInteract(0.5f);
            Assert.That(player.HeldEntity, Is.SameAs(blanket));
            Vector3 midpoint = Vector3.Lerp(motor.transform.position, doorSocket.Anchor.position, 0.5f);
            motor.Teleport(midpoint, motor.transform.rotation);
            yield return null;
            FaceAndMove(motor, doorSocket.Anchor.position + Vector3.back * 0.8f, doorSocket.Anchor.position);
            Physics.SyncTransforms();
            Assert.That(player.GetComponent<InteractionScanner>().RefreshForVerb(InteractionVerb.Place), Is.Not.Null);
            yield return PressInteract(0.1f);
            yield return new WaitForSeconds(0.4f);
            Assert.That(doorSocket.Occupant, Is.SameAs(blanket));
            Assert.That(facts.CaptureSnapshot().Trap, Is.EqualTo(TrapState.BlanketArmed));

            HitInfo heavyHit = new HitInfo(ImpactKind.Heavy, "production D21 setup", Vector3.zero,
                Vector3.forward, 8f);
            Assert.That(tableLamp.ReceiveHit(heavyHit), Is.EqualTo(HitResult.Broke));
            Assert.That(ceilingLamp.ReceiveHit(heavyHit), Is.EqualTo(HitResult.Broke));
            Assert.That(facts.CaptureSnapshot().RoomLight, Is.EqualTo(RoomLightState.PersistentBlack));

            killer.gameObject.SetActive(true);
            killer.BeginEntry(loop.LoopEpoch, loop.RequestKill);
            killer.enabled = false;
            UnityEngine.AI.NavMeshAgent agent = killer.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null) agent.enabled = false;
            Vector3 trapPosition = trap.transform.position;
            killer.transform.position = trapPosition + Vector3.right * 2f;
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            killer.transform.position = trapPosition;
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(trap.OccupantPresent, Is.True);
            FaceAndMove(motor, trap.transform.position - trap.transform.forward * 0.7f, trap.transform.position);
            Physics.SyncTransforms();
            yield return null;
            Assert.That(player.GetComponent<InteractionScanner>().RefreshForVerb(InteractionVerb.Use), Is.Not.Null,
                "Player Interact should expose the armed trap pull action.");
            int startingEpoch = loop.LoopEpoch;
            bool confirmedD21Launch = false;
            killer.ConfirmedPerformanceLaunching += intent =>
            {
                if (intent.OutcomeId == "D21" && intent.LoopEpoch == startingEpoch &&
                    intent.Kind == PerformanceConfirmationKind.TrapTrigger)
                {
                    confirmedD21Launch = true;
                }
            };

            bool pairBegin = false;
            bool pairDominated = false;
            bool flashlightDislodged = false;
            bool recoveryOpened = false;
            bool blockLightBegan = false;
            bool blockLightEnded = false;
            bool backupDrawBegan = false;
            bool backupFireBegan = false;
            paired.Events.SemanticEventEmitted += semanticEvent =>
            {
                if (semanticEvent.LoopEpoch != startingEpoch || semanticEvent.OutcomeId != "D21") return;
                switch (semanticEvent.EventId)
                {
                    case "D21.Pair.Begin": pairBegin = true; break;
                    case "D21.Pair.Dominated": pairDominated = true; break;
                    case "D21.Flashlight.Dislodged": flashlightDislodged = true; break;
                    case "D21.Flashlight.Recovery.Open": recoveryOpened = true; break;
                    case "D21.BlockLight.Begin": blockLightBegan = true; break;
                    case "D21.BlockLight.End": blockLightEnded = true; break;
                    case "D21.BackupPistol.Draw": backupDrawBegan = true; break;
                    case "D21.BackupPistol.Fire": backupFireBegan = true; break;
                }
            };

            yield return PressInteract(0.1f);
            Assert.That(facts.CaptureSnapshot().TrapPulled, Is.True);
            Assert.That(confirmedD21Launch, Is.True,
                "The production trap pull should launch D21 through the confirmed-contact path.");
            Assert.That(paired.IsPlaying, Is.True,
                "The real trap pull should start D21 through its confirmed paired launcher.");
            Assert.That(loot.State, Is.EqualTo(CounterplayLootWindowState.Inactive),
                "Loot stays unavailable during the authored fall beat.");

            float wait = 0f;
            while (loot.State != CounterplayLootWindowState.D21Choice && wait < 3f)
            {
                wait += Time.deltaTime;
                yield return null;
            }
            Assert.That(loot.State, Is.EqualTo(CounterplayLootWindowState.D21Choice));

            GameObject flashlight = GetPrivateField<GameObject>(loot, "flashlightDrop");
            Assert.That(flashlight, Is.Not.Null);
            StatefulWorldItem flashlightState = flashlight.GetComponent<StatefulWorldItem>();
            DroppedFlashlightPresenter flashlightPresentation = flashlight.GetComponent<DroppedFlashlightPresenter>();
            Assert.That(flashlightState.CurrentState, Is.EqualTo(ItemPhysicalState.Flying));
            Assert.That(flashlightState.Body.isKinematic, Is.False);
            Assert.That(flashlightState.Body.angularVelocity.sqrMagnitude, Is.GreaterThan(0.01f));
            Assert.That(flashlightPresentation, Is.Not.Null);
            yield return null;
            Assert.That(flashlightPresentation.BeamLight.enabled, Is.True,
                "The dropped flashlight remains lit while the player chooses a prop.");
            Assert.That(loot.State, Is.EqualTo(CounterplayLootWindowState.D21Choice),
                "The rolling beam must not complete the counterplay on its own.");
            yield return new WaitForSeconds(0.65f);
            InteractableEntity flashlightEntity = flashlight.GetComponent<InteractableEntity>();
            Assert.That(flashlightEntity, Is.Not.Null);
            ProductionPickupTestDriver.Approach(motor, flashlightEntity);
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            Assert.That(player.GetComponent<InteractionScanner>().RefreshForVerb(InteractionVerb.PickUp),
                Is.SameAs(flashlightEntity), "The actual dropped flashlight should be pickup-scannable.");
            yield return PressInteract(0.12f);
            Assert.That(player.HeldEntity, Is.SameAs(flashlightEntity));
            Assert.That(loot.State, Is.EqualTo(CounterplayLootWindowState.D21Grappling));
            Assert.That(player.CounterplayWindows.HasFlag(CounterplayInputWindow.Qte), Is.True);

            // Deliver twelve real E press edges over the three authored one-second
            // struggle windows. Each E goes through PlayerInputRouter and the runtime
            // adapter's QTE pulse; no phase-machine shortcuts are used in this test.
            for (int i = 0; i < 12; i++)
            {
                yield return PressInteract(0.045f);
                yield return new WaitForSeconds(0.205f);
            }

            wait = 0f;
            while (loot.State != CounterplayLootWindowState.D21FlashlightRecovery && wait < 3f)
            {
                wait += Time.deltaTime;
                yield return null;
            }
            Assert.That(pairBegin, Is.True);
            Assert.That(pairDominated, Is.True,
                "Actual E pulses should win the three QTE windows in the dark grapple.");
            Assert.That(flashlightDislodged, Is.True,
                "The grapple should explicitly knock the held flashlight back to the floor.");
            Assert.That(recoveryOpened, Is.True);
            Assert.That(loot.State, Is.EqualTo(CounterplayLootWindowState.D21FlashlightRecovery));
            Assert.That(player.HeldEntity, Is.Not.SameAs(flashlightEntity),
                "The detach beat must not automatically give the flashlight to the player.");
            Assert.That(flashlightEntity.IsHeld, Is.False);

            yield return new WaitForSeconds(.65f);
            ProductionPickupTestDriver.Approach(motor, flashlightEntity);
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            Assert.That(player.GetComponent<InteractionScanner>().RefreshForVerb(InteractionVerb.PickUp),
                Is.SameAs(flashlightEntity), "The dislodged light should remain a real pickup after detach.");
            yield return PressInteract(0.12f);
            Assert.That(player.HeldEntity, Is.SameAs(flashlightEntity));
            Assert.That(loot.State, Is.EqualTo(CounterplayLootWindowState.D21FlashlightArmed));

            Vector3 toKiller = killer.transform.position - motor.transform.position;
            toKiller.y = 0f;
            Vector3 aimPosition = killer.transform.position - toKiller.normalized * 2f;
            aimPosition.y = motor.transform.position.y;
            motor.Teleport(aimPosition, Quaternion.LookRotation(toKiller.normalized, Vector3.up));
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            yield return PressInteract(0.12f);
            Assert.That(loot.State, Is.EqualTo(CounterplayLootWindowState.Resolved),
                "A real held-flashlight Use ray hitting the killer should release the block-light stage.");

            wait = 0f;
            while (!blockLightEnded && wait < 4.5f)
            {
                wait += Time.deltaTime;
                yield return null;
            }
            Assert.That(blockLightBegan, Is.True);
            Assert.That(blockLightEnded, Is.True,
                "The three-second block-light phase should resume after actual illumination.");

            wait = 0f;
            while (!backupFireBegan && wait < 5f)
            {
                wait += Time.deltaTime;
                yield return null;
            }
            Assert.That(backupDrawBegan, Is.True);
            Assert.That(backupFireBegan, Is.True,
                "The resumed performance should continue through backup draw and fire.");

            wait = 0f;
            while (loop.LoopEpoch == startingEpoch && wait < 3f)
            {
                wait += Time.deltaTime;
                yield return null;
            }
            Assert.That(loop.LoopEpoch, Is.GreaterThan(startingEpoch),
                "D21 should reach its normal terminal kill and loop reset.");
        }

        private IEnumerator LoadRoom()
        {
            _room = SceneManager.GetSceneByName("Room_M1");
            _loadedByTest = !_room.IsValid() || !_room.isLoaded;
            if (_loadedByTest)
            {
                yield return SceneManager.LoadSceneAsync("Room_M1", LoadSceneMode.Additive);
                _room = SceneManager.GetSceneByName("Room_M1");
            }

            yield return null;
            yield return null;
            _keyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.LeftShift));
            InputSystem.Update();
            yield return null;
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            InputSystem.Update();
            yield return new WaitForSeconds(0.3f);
        }

        private T FindInRoom<T>() where T : Component
        {
            foreach (GameObject root in _room.GetRootGameObjects())
            {
                T component = root.GetComponentInChildren<T>(true);
                if (component != null) return component;
            }

            return null;
        }

        private T FindByName<T>(string objectName) where T : Component
        {
            foreach (GameObject root in _room.GetRootGameObjects())
            {
                T[] components = root.GetComponentsInChildren<T>(true);
                foreach (T component in components)
                {
                    if (component.gameObject.name == objectName) return component;
                }
            }

            return null;
        }

        private T[] FindAllInRoom<T>() where T : Component
        {
            var components = new List<T>();
            foreach (GameObject root in _room.GetRootGameObjects())
            {
                components.AddRange(root.GetComponentsInChildren<T>(true));
            }

            return components.ToArray();
        }

        private static BreakableLight FindLamp(BreakableLight[] lamps, string objectName)
        {
            foreach (BreakableLight lamp in lamps)
            {
                if (lamp != null && lamp.gameObject.name == objectName) return lamp;
            }

            return null;
        }

        private void FaceAndMove(PlayerMotor motor, Vector3 position, Vector3 target)
        {
            position.y = 0.08f;
            Vector3 direction = target - position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f) direction = Vector3.forward;
            motor.Teleport(position, Quaternion.LookRotation(direction.normalized, Vector3.up));
        }

        private IEnumerator PressInteract(float holdSeconds)
        {
            if (_keyboard == null) _keyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.E));
            InputSystem.Update();
            yield return new WaitForSeconds(holdSeconds);
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            InputSystem.Update();
            yield return null;
        }

        private static T GetPrivateField<T>(object target, string name)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            return (T)field.GetValue(target);
        }
    }
}
