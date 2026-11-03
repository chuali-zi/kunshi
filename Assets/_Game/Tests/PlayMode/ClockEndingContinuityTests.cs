using System.Collections;
using ConGame.Runtime.Interaction;
using ConGame.Runtime.Loop;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ConGame.Tests.PlayMode
{
    public sealed class ClockEndingContinuityTests
    {
        [UnityTest]
        public IEnumerator StuckHandDoesNotDriftAndEndingTicksSurviveUpdatesUntilReset()
        {
            var root = new GameObject("Clock continuity");
            try
            {
                var hand = new GameObject("SecondHand").transform;
                hand.SetParent(root.transform);
                Quaternion initial = Quaternion.Euler(0, 0, 37);
                hand.localRotation = initial;
                var clock = root.AddComponent<ClockTwitchBehaviour>();
                clock.Configure(hand, null, .05f);
                clock.CaptureInitialState();

                bool sawTwitch = false;
                float until = Time.time + .7f;
                while (Time.time < until)
                {
                    yield return null;
                    float angle = Quaternion.Angle(initial, hand.localRotation);
                    Assert.That(angle, Is.LessThanOrEqualTo(1.51f),
                        "Ordinary loops must twitch at one mark without accumulating real clock ticks.");
                    sawTwitch |= angle > 1f;
                }
                Assert.That(sawTwitch, Is.True);

                for (int tick = 1; tick <= 3; tick++)
                {
                    clock.AdvanceForEnding();
                    yield return new WaitForSeconds(.12f);
                    Assert.That(Quaternion.Angle(hand.localRotation,
                        initial * Quaternion.Euler(0, 0, -6 * tick)), Is.LessThan(.01f),
                        "The ending must advance the original hand and stop the stuck-clock animation.");
                }
                Assert.That(root.GetComponentsInChildren<Transform>().Length, Is.EqualTo(2),
                    "The ending must reuse the original hand.");

                var reset = new LoopResetContext(1, 2);
                clock.Quiesce(reset);
                clock.Restore(reset);
                clock.Resume(reset);
                Assert.That(Quaternion.Angle(initial, hand.localRotation), Is.LessThan(.01f));
                yield return new WaitForSeconds(.3f);
                Assert.That(Quaternion.Angle(initial, hand.localRotation), Is.LessThanOrEqualTo(1.51f));
            }
            finally
            {
                Object.Destroy(root);
            }
        }
    }
}
