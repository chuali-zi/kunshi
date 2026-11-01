using System.Collections;
using System.Reflection;
using ConGame.Bootstrap;
using ConGame.Domain.Facts;
using ConGame.Domain.Flow;
using ConGame.Domain.Interaction;
using ConGame.Presentation.Choreography;
using ConGame.Runtime.AI;
using ConGame.Runtime.Counterplay;
using ConGame.Runtime.Ending;
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
    public sealed class ProductionGameplayReachabilityTests
    {
        private Scene _loadedRoom;
        private bool _loadedByTest;
        private Keyboard _testKeyboard;

        [UnityTearDown]
        public IEnumerator UnloadRoomAfterTest()
        {
            if (_loadedByTest && _loadedRoom.IsValid() && _loadedRoom.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(_loadedRoom);
            }

            _loadedByTest = false;
            _loadedRoom = default;
            if (_testKeyboard != null)
            {
                InputSystem.RemoveDevice(_testKeyboard);
                _testKeyboard = null;
            }
        }

        [UnityTest]
        public IEnumerator ProductionBlanketChainCanReachD21ChoiceAndD22()
        {
            yield return LoadProductionRoom();
            Assert.That(FindInRoom<RoomContext>(_loadedRoom), Is.Not.Null);

            GameFlowController flow = FindInRoom<GameFlowController>(_loadedRoom);
            LoopDirector loop = FindInRoom<LoopDirector>(_loadedRoom);
            GameplayFactService facts = FindInRoom<GameplayFactService>(_loadedRoom);
            PlayerMotor motor = FindInRoom<PlayerMotor>(_loadedRoom);
            PlayerInteractor player = FindInRoom<PlayerInteractor>(_loadedRoom);
            KillerDirector killer = FindInRoom<KillerDirector>(_loadedRoom);
            TelevisionController television = FindInRoom<TelevisionController>(_loadedRoom);
            StolenLootDropController loot = FindInRoom<StolenLootDropController>(_loadedRoom);
            Transform killerBackupVisual = FindByName<Transform>(_loadedRoom, "PRP_PistolBackup_Holstered");
            BreakableLight[] lamps = FindAllInRoom<BreakableLight>(_loadedRoom);
            InteractableEntity blanket = FindByName<InteractableEntity>(_loadedRoom, "PRP_Blanket");
            PlacementSocket doorSocket = FindByName<PlacementSocket>(_loadedRoom, "PRP_DoorTrapSocket");
            TripTrapController trap = doorSocket == null ? null : doorSocket.GetComponent<TripTrapController>();

            Assert.That(flow, Is.Not.Null);
            Assert.That(loop, Is.Not.Null);
            Assert.That(facts, Is.Not.Null);
            Assert.That(motor, Is.Not.Null);
            Assert.That(player, Is.Not.Null);
            Assert.That(killer, Is.Not.Null);
            Assert.That(television, Is.Not.Null);
            Assert.That(loot, Is.Not.Null);
            Assert.That(killerBackupVisual, Is.Not.Null,
                "The production killer rig should include its holstered backup pistol visual.");

            int playerD22ShotCues = 0;
            int terminalD22ShotCues = 0;
            int d22BackupDrawCues = 0;
            Transform terminalShotSource = null;
            loot.D22ShotCueRequested += (terminalShot, source, epoch) =>
            {
                if (terminalShot)
                {
                    terminalD22ShotCues++;
                    terminalShotSource = source;
                }
                else
                {
                    playerD22ShotCues++;
                }
            };
            loot.D22BackupVisualRequested += (visible, epoch) =>
            {
                if (visible)
                {
                    d22BackupDrawCues++;
                }
            };
            Assert.That(blanket, Is.Not.Null);
            Assert.That(doorSocket, Is.Not.Null);
            Assert.That(trap, Is.Not.Null);

            BreakableLight tableLamp = FindLamp(lamps, "PRP_TableLamp");
            BreakableLight ceilingLamp = FindLamp(lamps, "PRP_CeilingLamp");
            Assert.That(tableLamp, Is.Not.Null);
            Assert.That(ceilingLamp, Is.Not.Null);

            // Start at doorway entry. Set up the actual object chain before producing
            // PersistentBlack through the production light hit receivers.
            flow.Initialize(GamePhase.Breach);

            // Pick up the real blanket through its production scanner and capability,
            // carry it across the room, then place it through the door socket affordance.
            FaceAndMove(motor, player.transform, blanket.transform.position + Vector3.back * 0.65f,
                blanket.transform.position);
            Physics.SyncTransforms();
            yield return PressInteract(player, 0.5f);
            Assert.That(player.HeldEntity, Is.SameAs(blanket));
            Assert.That(blanket.GetComponent<StatefulWorldItem>().CurrentState, Is.EqualTo(ItemPhysicalState.Held));
            Assert.That(player.HeldMovementMultiplier, Is.EqualTo(0.5f).Within(0.01f),
                "The blanket must use the production ground-drag carry capability.");

            Vector3 midpoint = Vector3.Lerp(player.transform.position, doorSocket.Anchor.position, 0.5f);
            motor.Teleport(midpoint, player.transform.rotation);
            yield return null;
            FaceAndMove(motor, player.transform, doorSocket.Anchor.position + Vector3.back * 0.8f,
                doorSocket.Anchor.position);
            Physics.SyncTransforms();
            Assert.That(player.GetComponent<InteractionScanner>().RefreshForVerb(InteractionVerb.Place),
                Is.Not.Null, "The real door placement socket should be scanner-visible.");
            yield return PressInteract(player, 0.1f);
            yield return new WaitForSeconds(0.4f);
            Assert.That(doorSocket.Occupant, Is.SameAs(blanket));
            Assert.That(facts.CaptureSnapshot().Trap, Is.EqualTo(TrapState.BlanketArmed));
            Assert.That(facts.CaptureSnapshot().TrapPulled, Is.False);

            HitInfo heavyHit = new HitInfo(ImpactKind.Heavy, "test_heavy", Vector3.zero, Vector3.forward, 8f);
            Assert.That(tableLamp.ReceiveHit(heavyHit), Is.EqualTo(HitResult.Broke));
            Assert.That(ceilingLamp.ReceiveHit(heavyHit), Is.EqualTo(HitResult.Broke));
            Assert.That(facts.CaptureSnapshot().RoomLight, Is.EqualTo(RoomLightState.PersistentBlack));

            // Let the killer's actual collider enter the production trigger volume.
            // The position move is only test setup; occupancy is produced by PhysX.
            killer.gameObject.SetActive(true);
            killer.BeginEntry(loop.LoopEpoch, loop.RequestKill);
            // Keep the authored capsule in the test-selected approach path while
            // leaving its colliders and the production trigger callbacks enabled.
            killer.enabled = false;
            UnityEngine.AI.NavMeshAgent agent = killer.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null)
            {
                agent.enabled = false;
            }
            Vector3 trapPosition = trap.transform.position;
            killer.transform.position = trapPosition + Vector3.right * 2f;
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            killer.transform.position = trapPosition;
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(trap.OccupantPresent, Is.True,
                "D21 pull must be enabled by the killer's collider entering the placed blanket trigger.");
            Assert.That(facts.CaptureSnapshot().TrapPulled, Is.False,
                "Stepping onto the trap only creates a pull opportunity; player input commits the pull.");

            FaceAndMove(motor, player.transform, trap.transform.position - trap.transform.forward * 0.7f,
                trap.transform.position);
            Physics.SyncTransforms();
            yield return null;
            InteractableEntity pullTarget = player.GetComponent<InteractionScanner>()
                .RefreshForVerb(InteractionVerb.Use);
            Assert.That(pullTarget, Is.Not.Null,
                "The scanner should expose either the placed blanket or socket pull affordance.");
            float fallStartedAt = -1f;
            killer.ConfirmedPerformanceLaunching += intent =>
            {
                if (intent.OutcomeId == "D21") fallStartedAt = Time.time;
            };
            yield return PressInteract(player, 0.1f);
            Assert.That(facts.CaptureSnapshot().TrapPulled, Is.True);
            PairedPerformancePlayer paired = FindInRoom<PairedPerformancePlayer>(_loadedRoom);
            Assert.That(paired, Is.Not.Null);
            Assert.That(paired.IsPlaying, Is.True,
                "A production TrapPull stimulus must start the paired D21 performance.");
            Assert.That(loot.State, Is.EqualTo(CounterplayLootWindowState.Inactive));

            Assert.That(fallStartedAt, Is.GreaterThanOrEqualTo(0f));
            // The key press itself consumes frames; measure from the confirmed
            // launch rather than adding another 1.1s after that input completes.
            while (Time.time - fallStartedAt < 0.8f) yield return null;
            Assert.That(loot.State, Is.EqualTo(CounterplayLootWindowState.Inactive),
                "The pickup choice must remain closed until the D21 fall semantic event.");
            while (loot.State == CounterplayLootWindowState.Inactive && Time.time - fallStartedAt < 2f)
                yield return null;
            Assert.That(loot.State, Is.EqualTo(CounterplayLootWindowState.D21Choice));
            Assert.That(Time.time - fallStartedAt, Is.GreaterThanOrEqualTo(1.15f),
                "The authored 1.2s fall must not be skipped (one-frame scheduling tolerance).");

            GameObject pistol = GetPrivateField<GameObject>(loot, "pistolDrop");
            Assert.That(pistol, Is.Not.Null);
            FaceAndMove(motor, player.transform, pistol.transform.position + Vector3.left * 0.65f,
                pistol.transform.position);
            Physics.SyncTransforms();
            yield return PressInteract(player, 0.1f);
            yield return new WaitForSeconds(0.3f);
            Assert.That(loot.State, Is.EqualTo(CounterplayLootWindowState.D22AwaitMainShot));

            int d22Epoch = loop.LoopEpoch;
            yield return new WaitForSeconds(2.2f);
            Assert.That(flow.CurrentPhase, Is.EqualTo(GamePhase.Counterplay),
                "The 1.8s D22 approach performance must not end the encounter before the player's shot window.");
            Assert.That(loop.LoopEpoch, Is.EqualTo(d22Epoch));
            Assert.That(loot.State, Is.EqualTo(CounterplayLootWindowState.D22AwaitMainShot));
            Assert.That(player.HeldEntity, Is.Not.Null);
            Assert.That(player.HeldEntity.ItemId, Is.EqualTo("pistol_main"),
                "The real D21 pistol pickup should remain held for D22 interaction.");
            Assert.That(loot.MainFirearm.RoundsRemaining, Is.EqualTo(1));

            yield return PressInteract(player, 0.1f);
            Assert.That(loot.MainFirearm.RoundsRemaining, Is.Zero,
                "Counterplay Interact should fire the held main pistol through its Use capability.");
            Assert.That(playerD22ShotCues, Is.EqualTo(1),
                "The main pistol's actual FirearmUseHandler shot should publish one audiovisual cue.");
            Assert.That(loot.State, Is.EqualTo(CounterplayLootWindowState.D22Dodging));
            yield return new WaitForSeconds(0.5f);
            Assert.That(loot.State, Is.EqualTo(CounterplayLootWindowState.D22AwaitMainDryFire));
            GameObject backup = GetPrivateField<GameObject>(loot, "backupPistolDrop");
            Assert.That(backup.activeSelf, Is.False,
                "The world backup pistol must stay hidden until the terminal draw beat attaches the character visual.");

            yield return PressInteract(player, 0.1f);
            Assert.That(loot.State, Is.EqualTo(CounterplayLootWindowState.Resolved),
                "The second Interact should produce the empty main-pistol click and close D22.");
            Assert.That(flow.CurrentPhase, Is.EqualTo(GamePhase.Counterplay),
                "The draw/fire finale must play before the terminal kill transition.");

            float drawWait = 0f;
            while (d22BackupDrawCues == 0 && drawWait < 1.1f)
            {
                drawWait += Time.deltaTime;
                yield return null;
            }
            Assert.That(d22BackupDrawCues, Is.EqualTo(1),
                "The authored backup draw point should trigger one character-weapon visual handoff.");
            Assert.That(backup.activeSelf, Is.False);
            Assert.That(killerBackupVisual.gameObject.activeSelf, Is.True);
            Assert.That(killerBackupVisual.parent.name, Does.Contain("RightHand"),
                "The backup visual should be reparented into the animated firing hand at the draw cue.");

            float terminalShotWait = 0f;
            while (terminalD22ShotCues == 0 && terminalShotWait < 1.1f)
            {
                terminalShotWait += Time.deltaTime;
                yield return null;
            }
            Assert.That(terminalD22ShotCues, Is.EqualTo(1),
                "The authored backup fire point should publish one terminal shot cue.");
            Assert.That(terminalShotSource, Is.Not.Null);
            Assert.That(terminalShotSource.name, Does.Contain("PistolBackup"));

            float killWait = 0f;
            while (flow.CurrentPhase != GamePhase.KillImpact && killWait < 1.1f)
            {
                killWait += Time.deltaTime;
                yield return null;
            }
            Assert.That(flow.CurrentPhase, Is.EqualTo(GamePhase.KillImpact));
            float resetWait = 0f;
            while (loop.LoopEpoch == d22Epoch && resetWait < 2f)
            {
                resetWait += Time.deltaTime;
                yield return null;
            }
            Assert.That(loop.LoopEpoch, Is.GreaterThan(d22Epoch),
                "The real killer callback should still finish D22 through the normal loop reset.");
        }

        [UnityTest]
        public IEnumerator ProductionD22WindowTimeoutStillFinishesAfterGreyboxPerformance()
        {
            yield return LoadProductionRoom();
            Assert.That(FindInRoom<RoomContext>(_loadedRoom), Is.Not.Null);
            GameFlowController flow = FindInRoom<GameFlowController>(_loadedRoom);
            LoopDirector loop = FindInRoom<LoopDirector>(_loadedRoom);
            KillerDirector killer = FindInRoom<KillerDirector>(_loadedRoom);
            StolenLootDropController loot = FindInRoom<StolenLootDropController>(_loadedRoom);
            PlayerMotor motor = FindInRoom<PlayerMotor>(_loadedRoom);
            Assert.That(flow, Is.Not.Null);
            Assert.That(loop, Is.Not.Null);
            Assert.That(killer, Is.Not.Null);
            Assert.That(loot, Is.Not.Null);
            Assert.That(motor, Is.Not.Null);

            flow.Initialize(GamePhase.Breach);
            Assert.That(flow.Request(GamePhase.Counterplay, TransitionPriority.Normal, "D22 timeout reachability test"),
                Is.True);
            Assert.That(flow.CommitPending(), Is.True);
            int startingEpoch = loop.LoopEpoch;
            killer.gameObject.SetActive(true);
            killer.enabled = true;
            killer.BeginEntry(startingEpoch, loop.RequestKill);
            Assert.That(killer.TryStartOutcome("D22"), Is.True);
            Assert.That(loot.State, Is.EqualTo(CounterplayLootWindowState.D22AwaitMainShot));

            yield return new WaitForSeconds(2.2f);
            Assert.That(flow.CurrentPhase, Is.EqualTo(GamePhase.Counterplay),
                "D22 must remain available after its approach animation ends.");
            Assert.That(loop.LoopEpoch, Is.EqualTo(startingEpoch));
            Assert.That(loot.State, Is.EqualTo(CounterplayLootWindowState.D22AwaitMainShot));
            Assert.That(Vector3.Dot(motor.transform.up, Vector3.up), Is.GreaterThan(0.98f),
                "The D22 wait-for-shot beat must leave the player standing upright.");

            int terminalD22ShotCues = 0;
            loot.D22ShotCueRequested += (terminalShot, source, epoch) =>
            {
                if (terminalShot)
                {
                    terminalD22ShotCues++;
                }
            };
            float timeoutWait = 0f;
            while (loop.LoopEpoch == startingEpoch && timeoutWait < 17f)
            {
                timeoutWait += Time.deltaTime;
                yield return null;
            }
            Assert.That(loop.LoopEpoch, Is.GreaterThan(startingEpoch),
                "The production 12s D22 loot window timeout must still resolve the kill and loop reset.");
            Assert.That(terminalD22ShotCues, Is.EqualTo(1),
                "The production timeout must use the same single backup-fire finale as the empty-click branch.");
        }

        [UnityTest]
        public IEnumerator ProductionKillerOnlyRestoresUnbrokenLampFromClearReachablePosition()
        {
            yield return LoadProductionRoom();
            Assert.That(FindInRoom<RoomContext>(_loadedRoom), Is.Not.Null);

            GameplayFactService facts = FindInRoom<GameplayFactService>(_loadedRoom);
            PlayerInteractor player = FindInRoom<PlayerInteractor>(_loadedRoom);
            PlayerMotor motor = FindInRoom<PlayerMotor>(_loadedRoom);
            KillerDirector killer = FindInRoom<KillerDirector>(_loadedRoom);
            BreakableLight[] lamps = FindAllInRoom<BreakableLight>(_loadedRoom);
            BreakableLight tableLamp = FindLamp(lamps, "PRP_TableLamp");
            BreakableLight ceilingLamp = FindLamp(lamps, "PRP_CeilingLamp");
            Assert.That(facts, Is.Not.Null);
            Assert.That(player, Is.Not.Null);
            Assert.That(motor, Is.Not.Null);
            Assert.That(killer, Is.Not.Null);
            Assert.That(tableLamp, Is.Not.Null);
            Assert.That(ceilingLamp, Is.Not.Null);

            HitInfo heavyHit = new HitInfo(ImpactKind.Heavy, "test_heavy", Vector3.zero, Vector3.forward, 8f);
            Assert.That(ceilingLamp.ReceiveHit(heavyHit), Is.EqualTo(HitResult.Broke));
            Assert.That(tableLamp.IsBroken, Is.False);
            FaceAndMove(motor, player.transform, tableLamp.transform.position + Vector3.back * 0.7f,
                tableLamp.transform.position);
            Physics.SyncTransforms();
            InteractableEntity lampEntity = tableLamp.GetComponent<InteractableEntity>();
            Assert.That(lampEntity, Is.Not.Null);
            Assert.That(player.GetComponent<InteractionScanner>().RefreshForVerb(InteractionVerb.Use),
                Is.SameAs(lampEntity));
            yield return PressInteract(player, 0.1f);
            Assert.That(tableLamp.IsSwitchOn, Is.False);
            Assert.That(facts.CaptureSnapshot().RoomLight, Is.EqualTo(RoomLightState.RecoverableDark));

            KillerLampBrightener brightener = killer.GetComponent<KillerLampBrightener>();
            Assert.That(brightener, Is.Not.Null);
            Vector3 lampPosition = tableLamp.transform.position;
            killer.gameObject.SetActive(true);
            Vector3 reachableLampPosition = lampPosition + Vector3.back * 0.6f;
            reachableLampPosition.y = 0.08f;
            killer.transform.position = reachableLampPosition;
            UnityEngine.AI.NavMeshAgent agent = killer.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null)
            {
                agent.enabled = false;
            }
            Physics.SyncTransforms();
            yield return null;
            yield return null;
            Assert.That(tableLamp.IsSwitchOn, Is.True,
                "The production killer update should restore the lamp after reaching its clear interaction range.");
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
            // Leave the production opening chair via real input before positioning
            // the player for each encounter fixture.
            _testKeyboard ??= InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(_testKeyboard, new KeyboardState(Key.LeftShift));
            InputSystem.Update();
            yield return null;
            InputSystem.QueueStateEvent(_testKeyboard, new KeyboardState());
            InputSystem.Update();
            yield return new WaitForSeconds(0.3f);
        }

        private static T FindInRoom<T>(Scene room) where T : Component
        {
            foreach (GameObject candidate in room.GetRootGameObjects())
            {
                T match = candidate.GetComponentInChildren<T>(true);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static T[] FindAllInRoom<T>(Scene room) where T : Component
        {
            System.Collections.Generic.List<T> result = new System.Collections.Generic.List<T>();
            foreach (GameObject candidate in room.GetRootGameObjects())
            {
                result.AddRange(candidate.GetComponentsInChildren<T>(true));
            }

            return result.ToArray();
        }

        private static T FindByName<T>(Scene room, string objectName) where T : Component
        {
            T[] matches = FindAllInRoom<T>(room);
            for (int i = 0; i < matches.Length; i++)
            {
                if (matches[i] != null && matches[i].gameObject.name == objectName)
                {
                    return matches[i];
                }
            }

            return null;
        }

        private static BreakableLight FindLamp(BreakableLight[] lamps, string objectName)
        {
            for (int i = 0; i < lamps.Length; i++)
            {
                if (lamps[i] != null && lamps[i].gameObject.name == objectName)
                {
                    return lamps[i];
                }
            }

            return null;
        }

        private static void FaceAndMove(PlayerMotor motor, Transform player, Vector3 position, Vector3 target)
        {
            position.y = 0.08f;
            Vector3 direction = target - position;
            direction.y = 0f;
            Quaternion rotation = direction.sqrMagnitude < 0.001f
                ? player.rotation
                : Quaternion.LookRotation(direction.normalized, Vector3.up);
            motor.Teleport(position, rotation);
        }

        private IEnumerator PressInteract(PlayerInteractor player, float holdSeconds)
        {
            if (_testKeyboard == null)
            {
                _testKeyboard = InputSystem.AddDevice<Keyboard>();
            }

            InputSystem.QueueStateEvent(_testKeyboard, new KeyboardState(Key.E));
            InputSystem.Update();
            yield return new WaitForSeconds(holdSeconds);
            InputSystem.QueueStateEvent(_testKeyboard, new KeyboardState());
            InputSystem.Update();
            yield return null;
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
