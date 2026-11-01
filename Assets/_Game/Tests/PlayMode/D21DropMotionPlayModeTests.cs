using System.Collections;
using ConGame.Domain.Interaction;
using ConGame.Presentation.Props;
using ConGame.Runtime.Facts;
using ConGame.Runtime.Events;
using ConGame.Runtime.Interaction;
using ConGame.Runtime.Loop;
using ConGame.Runtime.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ConGame.Tests.PlayMode
{
    public sealed class D21DropMotionPlayModeTests
    {
        [UnityTest]
        public IEnumerator ScriptedDropCannotDamageAReceiverButAPlayerThrowCan()
        {
            var prop = new GameObject("DroppedProp");
            var wall = new GameObject("ImpactTarget");
            var hand = new GameObject("Hand");
            var eventsObject = new GameObject("Events");
            try
            {
                wall.transform.position = new Vector3(0f, 1f, 1.2f);
                wall.AddComponent<BoxCollider>().size = new Vector3(2f, 2f, 0.2f);
                var receiver = wall.AddComponent<D21DropImpactReceiver>();
                prop.AddComponent<SphereCollider>().radius = 0.1f;
                var body = prop.AddComponent<Rigidbody>();
                body.isKinematic = true;
                var item = prop.AddComponent<StatefulWorldItem>();
                item.Configure(body);
                Assert.That(item.TossIntoWorld(Vector3.up, Quaternion.identity,
                    Vector3.forward * 5f, Vector3.zero), Is.True);
                bool touched = false;
                prop.AddComponent<D21DropContactProbe>().Touched = () => touched = true;
                float timeout = Time.time + 1f;
                while (!touched && Time.time < timeout) yield return new WaitForFixedUpdate();
                Assert.That(touched, Is.True, "The passive drop must still physically collide.");
                Assert.That(receiver.Hits, Is.Zero, "Passive collision cannot count as a player attack.");

                hand.transform.position = Vector3.up;
                Assert.That(item.BeginHeld(hand.transform, null), Is.True);
                var events = eventsObject.AddComponent<GameplayEventHub>();
                item.Release(Vector3.forward * 5f, Vector3.zero, events);
                timeout = Time.time + 1f;
                while (receiver.Hits == 0 && Time.time < timeout) yield return new WaitForFixedUpdate();
                Assert.That(receiver.Hits, Is.EqualTo(1), "An intentional throw still delivers its first impact.");
            }
            finally
            {
                Object.Destroy(prop); Object.Destroy(wall); Object.Destroy(hand); Object.Destroy(eventsObject);
            }
        }

        [UnityTest]
        public IEnumerator TossRollsLitPropPickupStopsMotionAndResetExtinguishesBeam()
        {
            var root = new GameObject("D21DropMotionTest");
            var itemObject = new GameObject("flashlight");
            try
            {
                var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.transform.SetParent(root.transform, false);
                floor.transform.position = new Vector3(0f, -0.1f, 0f);
                floor.transform.localScale = new Vector3(10f, 0.2f, 10f);
                var playerObject = new GameObject("player");
                playerObject.transform.SetParent(root.transform, false);
                var player = playerObject.AddComponent<PlayerInteractor>();
                var facts = playerObject.AddComponent<GameplayFactService>();
                facts.CaptureInitialState();
                var hand = new GameObject("hand").transform;
                hand.SetParent(playerObject.transform, false);
                hand.localPosition = Vector3.up;
                itemObject.transform.SetParent(root.transform, false);
                itemObject.transform.localPosition = new Vector3(2f, 0.5f, 0f);
                itemObject.AddComponent<BoxCollider>().size = new Vector3(0.08f, 0.08f, 0.2f);
                var body = itemObject.AddComponent<Rigidbody>();
                body.isKinematic = true;
                var item = itemObject.AddComponent<StatefulWorldItem>();
                item.Configure(body);
                var lightObject = new GameObject("beam");
                lightObject.transform.SetParent(itemObject.transform, false);
                var beam = lightObject.AddComponent<Light>();
                beam.type = LightType.Spot;
                var presentation = itemObject.AddComponent<DroppedFlashlightPresenter>();
                presentation.Configure(item, player, beam);
                item.CaptureInitialState();
                presentation.CaptureInitialState();

                Vector3 launchPosition = new Vector3(0f, 0.7f, 0f);
                Assert.That(item.TossIntoWorld(launchPosition, Quaternion.identity,
                    Vector3.forward, Vector3.up * 4f), Is.True);
                Quaternion startRotation = item.transform.rotation;
                yield return new WaitForSeconds(0.15f);
                Assert.That(Vector3.Distance(item.transform.position, launchPosition), Is.GreaterThan(0.05f));
                Assert.That(Quaternion.Angle(item.transform.rotation, startRotation), Is.GreaterThan(5f));
                Assert.That(beam.enabled, Is.True);
                Assert.That(Vector3.Angle(beam.transform.forward, item.transform.forward), Is.LessThan(0.1f));
                yield return new WaitForSeconds(0.85f);
                Assert.That(item.GetComponent<Collider>().bounds.min.y, Is.GreaterThan(-0.02f),
                    "The flashlight should land on the floor instead of passing through it.");

                Assert.That(item.BeginHeld(hand, null), Is.True);
                yield return null;
                Assert.That(body.isKinematic, Is.True);
                Assert.That(beam.enabled, Is.True);
                Assert.That(Vector3.Angle(beam.transform.forward, player.AimForward), Is.LessThan(0.1f));
                Assert.That(Vector3.Distance(beam.transform.position,
                    player.transform.position + Vector3.up * 1.4f), Is.LessThan(0.001f));
                Assert.That(facts.CaptureSnapshot().IsGlareActive, Is.False,
                    "Visible sweeping light cannot award the authoritative Use-ray hit.");
                Assert.That(item.TossIntoWorld(launchPosition, Quaternion.identity,
                    Vector3.forward, Vector3.zero), Is.False,
                    "A scripted drop cannot take an item back from the player's hand.");

                var context = new LoopResetContext(1, 2);
                presentation.Quiesce(context);
                item.Quiesce(context);
                item.Restore(context);
                presentation.Restore(context);
                item.Resume(context);
                presentation.Resume(context);
                Assert.That(item.CurrentState, Is.EqualTo(ItemPhysicalState.Placed));
                Assert.That(item.transform.parent, Is.SameAs(root.transform));
                Assert.That(item.transform.localPosition, Is.EqualTo(new Vector3(2f, 0.5f, 0f)));
                Assert.That(body.isKinematic, Is.True);
                Assert.That(beam.enabled, Is.False);
            }
            finally
            {
                Object.Destroy(itemObject);
                Object.Destroy(root);
            }
        }
    }

    public sealed class D21DropImpactReceiver : MonoBehaviour, IHitReceiver
    {
        public int Hits { get; private set; }
        public HitResult ReceiveHit(in HitInfo hit) { Hits++; return HitResult.Accepted; }
    }

    public sealed class D21DropContactProbe : MonoBehaviour
    {
        public System.Action Touched;
        private void OnCollisionEnter(Collision collision) => Touched?.Invoke();
    }
}
