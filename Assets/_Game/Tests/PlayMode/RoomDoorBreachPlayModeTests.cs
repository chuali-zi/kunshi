using System.Collections;
using ConGame.Runtime.AI;
using ConGame.Runtime.Loop;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ConGame.Tests.PlayMode
{
    public sealed class RoomDoorBreachPlayModeTests
    {
        [UnityTest]
        public IEnumerator BreachCompletesOnceAndLoopResetRestoresDoor()
        {
            GameObject root = new GameObject("Breach Test");
            GameObject hinge = new GameObject("Door Hinge");
            hinge.transform.SetParent(root.transform, false);
            Quaternion initialRotation = Quaternion.Euler(0f, 12f, 0f);
            hinge.transform.localRotation = initialRotation;

            RoomDoorBreachController breach = root.AddComponent<RoomDoorBreachController>();
            breach.Configure(hinge.transform, 0.5f, 100f);
            breach.CaptureInitialState();

            int completions = 0;
            breach.Begin(3, () => completions++);
            breach.Advance(0.1f);

            Assert.That(breach.IsRunning, Is.True);
            Assert.That(Quaternion.Angle(initialRotation, hinge.transform.localRotation), Is.GreaterThan(0.1f));

            breach.Advance(1f);
            breach.Advance(1f);

            Assert.That(breach.IsRunning, Is.False);
            Assert.That(completions, Is.EqualTo(1));
            Assert.That(Quaternion.Angle(initialRotation, hinge.transform.localRotation),
                        Is.EqualTo(100f).Within(0.1f));

            LoopResetContext reset = new LoopResetContext(3, 4);
            breach.Quiesce(reset);
            breach.Restore(reset);

            Assert.That(Quaternion.Angle(initialRotation, hinge.transform.localRotation), Is.LessThan(0.01f));

            Object.Destroy(root);
            yield return null;
        }
    }
}
