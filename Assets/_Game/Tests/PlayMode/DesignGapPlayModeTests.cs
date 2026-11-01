using System.Collections;
using ConGame.Domain.Facts;
using ConGame.Domain.Interaction;
using ConGame.Runtime.AI;
using ConGame.Runtime.Events;
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
    public sealed class DesignGapPlayModeTests
    {
        [UnityTest]
        public IEnumerator StuckDrawerOpensOnSecondUse()
        {
            GameObject drawer = new GameObject("drawer");
            try
            {
                OpenableDrawer openable = drawer.AddComponent<OpenableDrawer>();
                openable.Configure(2);
                openable.CaptureInitialState();
                Assert.That(openable.Use(default), Is.EqualTo(InteractionResult.Accepted));
                Assert.That(openable.IsOpen, Is.False);
                Assert.That(openable.Use(default), Is.EqualTo(InteractionResult.Accepted));
                Assert.That(openable.IsOpen, Is.True);
                openable.Restore(new LoopResetContext(1, 2));
                Assert.That(openable.IsOpen, Is.False);
                yield return null;
            }
            finally
            {
                Object.Destroy(drawer);
            }
        }

        [UnityTest]
        public IEnumerator DoorUseTogglesOpenUnlessBlocked()
        {
            GameObject hinge = new GameObject("hinge");
            GameObject factsObject = new GameObject("facts");
            GameObject door = new GameObject("door");
            try
            {
                GameplayFactService facts = factsObject.AddComponent<GameplayFactService>();
                facts.CaptureInitialState();
                hinge.transform.SetParent(door.transform, false);
                RoomDoorUseHandler handler = door.AddComponent<RoomDoorUseHandler>();
                handler.Configure(hinge.transform, facts, 90f);
                handler.CaptureInitialState();
                Assert.That(handler.Use(default), Is.EqualTo(InteractionResult.Accepted));
                Assert.That(handler.IsOpen, Is.True);
                facts.SetDoorBlock(DoorBlockState.Furniture);
                Assert.That(handler.CanUse(default), Is.False);
                yield return null;
            }
            finally
            {
                Object.Destroy(door);
                Object.Destroy(factsObject);
            }
        }

        [UnityTest]
        public IEnumerator LampUnplugsWhenHeld()
        {
            GameObject lamp = new GameObject("lamp");
            GameObject socket = new GameObject("hand");
            try
            {
                Light unityLight = lamp.AddComponent<Light>();
                BreakableLight breakable = lamp.AddComponent<BreakableLight>();
                breakable.Configure(unityLight);
                Rigidbody body = lamp.AddComponent<Rigidbody>();
                StatefulWorldItem item = lamp.AddComponent<StatefulWorldItem>();
                item.Configure(body);
                LampUnplugOnHold unplug = lamp.AddComponent<LampUnplugOnHold>();
                unplug.Configure(breakable, item);
                Assert.That(breakable.IsPowered, Is.True);
                Assert.That(item.BeginHeld(socket.transform, new Collider[0]), Is.True);
                Assert.That(breakable.IsPowered, Is.False);
                Assert.That(breakable.IsCableConnected, Is.False);
                breakable.SetSwitchOn(true);
                Assert.That(breakable.IsPowered, Is.False);
                breakable.Restore(new LoopResetContext(1, 1));
                Assert.That(breakable.IsCableConnected, Is.True);
                Assert.That(breakable.IsPowered, Is.True);
                yield return null;
            }
            finally
            {
                Object.Destroy(lamp);
                Object.Destroy(socket);
            }
        }

        [UnityTest]
        public IEnumerator TripTrapPullsOnlyWhenKillerOccupies()
        {
            GameObject factsObject = new GameObject("facts");
            GameObject eventsObject = new GameObject("events");
            GameObject rug = new GameObject("rug");
            GameObject killer = new GameObject("killer");
            GameObject player = new GameObject("player");
            try
            {
                GameplayFactService facts = factsObject.AddComponent<GameplayFactService>();
                facts.CaptureInitialState();
                GameplayEventHub events = eventsObject.AddComponent<GameplayEventHub>();
                BoxCollider trigger = rug.AddComponent<BoxCollider>();
                trigger.isTrigger = true;
                trigger.size = Vector3.one * 2f;
                TripTrapController trap = rug.AddComponent<TripTrapController>();
                trap.Configure(facts, events, TrapState.RugArmed, true);
                Assert.That(trap.Use(default), Is.EqualTo(InteractionResult.Accepted));
                Assert.That(facts.CaptureSnapshot().TrapPulled, Is.False);

                killer.AddComponent<KillerDirector>();
                killer.AddComponent<BoxCollider>();
                killer.transform.position = rug.transform.position;
                Physics.SyncTransforms();
                trap.SendMessage("OnTriggerEnter", killer.GetComponent<Collider>(), SendMessageOptions.DontRequireReceiver);
                Assert.That(facts.CaptureSnapshot().TrapPulled, Is.False);
                PlayerInteractor interactor = player.AddComponent<PlayerInteractor>();
                player.transform.position = rug.transform.position - Vector3.forward;
                Assert.That(trap.Use(new InteractionContext(interactor, null)), Is.EqualTo(InteractionResult.Accepted));
                Assert.That(facts.CaptureSnapshot().TrapPulled, Is.True);
                trap.SendMessage("OnTriggerExit", killer.GetComponent<Collider>(), SendMessageOptions.DontRequireReceiver);
                facts.SetTrapPulled(false);
                Assert.That(trap.Use(new InteractionContext(interactor, null)), Is.EqualTo(InteractionResult.Rejected));
                yield return null;
            }
            finally
            {
                Object.Destroy(factsObject);
                Object.Destroy(eventsObject);
                Object.Destroy(rug);
                Object.Destroy(killer);
                Object.Destroy(player);
            }
        }

        [UnityTest]
        public IEnumerator AmbushDriverWritesFactFromPoseAndZone()
        {
            GameObject root = new GameObject("ambush-driver");
            try
            {
                GameplayFactService facts = root.AddComponent<GameplayFactService>();
                facts.CaptureInitialState();
                facts.SetRoomLight(RoomLightState.PersistentBlack);
                facts.SetPlayerZone(PlayerZone.DoorBlind);
                facts.SetPlayerPose(PlayerPose.Crouched);
                facts.SetMoving(false);
                AmbushFactDriver driver = root.AddComponent<AmbushFactDriver>();
                driver.Configure(facts);
                yield return null;
                Assert.That(facts.CaptureSnapshot().IsAmbushing, Is.True);
                facts.SetMoving(true);
                yield return null;
                Assert.That(facts.CaptureSnapshot().IsAmbushing, Is.False);
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        [UnityTest]
        public IEnumerator WindowFifthHitRevealsBrickThenCountsAsWall()
        {
            GameObject factsObject = new GameObject("facts");
            GameObject window = new GameObject("window");
            GameObject brick = new GameObject("brick");
            try
            {
                GameplayFactService facts = factsObject.AddComponent<GameplayFactService>();
                facts.CaptureInitialState();
                brick.SetActive(false);
                StrikeSurfaceHitReceiver receiver = window.AddComponent<StrikeSurfaceHitReceiver>();
                receiver.Configure(facts, false, 5, brick);
                HitInfo hit = new HitInfo(ImpactKind.Heavy, "bat", Vector3.zero, Vector3.forward, 6f);
                for (int i = 0; i < 4; i++)
                {
                    receiver.ReceiveHit(hit);
                }

                Assert.That(receiver.BrickRevealed, Is.False);
                receiver.ReceiveHit(hit);
                Assert.That(receiver.BrickRevealed, Is.True);
                Assert.That(brick.activeSelf, Is.True);
                facts.SetStrikingWindow(false);
                facts.SetStrikingWall(false);
                receiver.ReceiveHit(hit);
                Assert.That(facts.CaptureSnapshot().IsStrikingWall, Is.True);
                yield return null;
            }
            finally
            {
                Object.Destroy(factsObject);
                Object.Destroy(window);
                Object.Destroy(brick);
            }
        }

        [UnityTest]
        public IEnumerator BookcaseAwayFromDoorDoesNotSetBookcaseBlock()
        {
            GameObject factsObject = new GameObject("facts");
            GameObject bookcase = new GameObject("bookcase");
            GameObject door = new GameObject("door");
            GameObject eventsObject = new GameObject("events");
            try
            {
                GameplayFactService facts = factsObject.AddComponent<GameplayFactService>();
                facts.CaptureInitialState();
                GameplayEventHub events = eventsObject.AddComponent<GameplayEventHub>();
                NoiseLevel? lastNoise = null;
                events.NoiseEmitted += noise => lastNoise = noise.Level;
                bookcase.transform.position = new Vector3(-2.65f, 0f, 1.65f);
                door.transform.position = new Vector3(2.7f, 0f, 1.45f);
                TipBookcaseUseHandler tip = bookcase.AddComponent<TipBookcaseUseHandler>();
                tip.Configure(facts, events, new Vector3(0f, 0f, 85f), door.transform, Vector3.left);
                tip.CaptureInitialState();
                Assert.That(tip.Use(default), Is.EqualTo(InteractionResult.Accepted));
                Assert.That(lastNoise, Is.Null);
                Assert.That(facts.CaptureSnapshot().DoorBlock, Is.EqualTo(DoorBlockState.None));
                yield return new WaitForSeconds(0.4f);
                Assert.That(lastNoise, Is.EqualTo(NoiseLevel.Loud));
            }
            finally
            {
                Object.Destroy(factsObject);
                Object.Destroy(bookcase);
                Object.Destroy(door);
                Object.Destroy(eventsObject);
            }
        }

        [UnityTest]
        public IEnumerator ShardZoneAppliesLimpMultiplier()
        {
            GameObject player = new GameObject("player");
            GameObject zoneObject = new GameObject("shards");
            try
            {
                CharacterController controller = player.AddComponent<CharacterController>();
                PlayerMotor motor = player.AddComponent<PlayerMotor>();
                motor.Configure(controller, null, null, null, null);
                ShardSlowZoneVolume zone = zoneObject.AddComponent<ShardSlowZoneVolume>();
                zone.Configure(new ShardSlowZoneSpec(1f, 0.55f, 2f));
                zone.SetZoneActive(true);
                zoneObject.transform.position = player.transform.position;
                motor.ApplyLimp(0.55f, 2f);
                Assert.That(motor.MovementSpeedMultiplier, Is.EqualTo(1f).Within(0.01f));
                yield return null;
            }
            finally
            {
                Object.Destroy(player);
                Object.Destroy(zoneObject);
            }
        }
    }
}
