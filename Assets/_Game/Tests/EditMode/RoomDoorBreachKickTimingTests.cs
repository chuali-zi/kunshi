using ConGame.Runtime.AI;
using ConGame.Runtime.Loop;
using NUnit.Framework;
using UnityEngine;

namespace ConGame.Tests.EditMode
{
    public sealed class RoomDoorBreachKickTimingTests
    {
        [Test]
        public void DoorStaysClosedUntilKickContact()
        {
            CreateRig(out GameObject root, out Transform hinge, out RoomDoorBreachController breach,
                out Quaternion initialRotation);
            try
            {
                breach.Configure(hinge, 1f, 100f);
                breach.CaptureInitialState();
                int windups = 0;
                int contacts = 0;
                breach.KickWindupStarted += epoch => windups += epoch == 1 ? 1 : 0;
                breach.KickContact += epoch => contacts += epoch == 1 ? 1 : 0;

                breach.Begin(1, null);
                Assert.That(breach.Phase, Is.EqualTo(RoomDoorBreachPhase.KickWindup));
                Assert.That(windups, Is.EqualTo(1));

                // 前摇结束后、接触回退(默认 0.4s)到达前,门必须保持关闭。
                breach.Advance(0.35f);
                Assert.That(breach.Phase, Is.EqualTo(RoomDoorBreachPhase.AwaitingContact));
                Assert.That(Quaternion.Angle(initialRotation, hinge.localRotation), Is.LessThan(0.01f));
                Assert.That(contacts, Is.EqualTo(0));

                breach.Advance(0.1f);
                Assert.That(breach.Phase, Is.EqualTo(RoomDoorBreachPhase.DoorOpening));
                Assert.That(contacts, Is.EqualTo(1), "无动画标记时回退计时必须触发一次 KickContact");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DeterministicFallbackCompletesWithFullyOpenDoor()
        {
            CreateRig(out GameObject root, out Transform hinge, out RoomDoorBreachController breach,
                out Quaternion initialRotation);
            try
            {
                breach.Configure(hinge, 0.5f, 100f);
                breach.CaptureInitialState();
                int completions = 0;
                breach.Begin(3, () => completions++);

                breach.Advance(0.1f);
                Assert.That(breach.IsRunning, Is.True);
                Assert.That(Quaternion.Angle(initialRotation, hinge.localRotation),
                    Is.GreaterThan(0.1f), "前摇晃动应仍然可见");

                breach.Advance(1f);
                breach.Advance(1f);
                Assert.That(breach.IsRunning, Is.False);
                Assert.That(breach.Phase, Is.EqualTo(RoomDoorBreachPhase.Completed));
                Assert.That(completions, Is.EqualTo(1));
                Assert.That(Quaternion.Angle(initialRotation, hinge.localRotation),
                    Is.EqualTo(100f).Within(0.1f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void AnimationContactEventDrivesDoorOpenOnlyOnce()
        {
            CreateRig(out GameObject root, out Transform hinge, out RoomDoorBreachController breach,
                out Quaternion initialRotation);
            try
            {
                breach.Configure(hinge, 1f, 100f);
                breach.CaptureInitialState();
                int contacts = 0;
                breach.KickContact += _ => contacts++;
                breach.Begin(1, null);

                breach.Advance(0.1f);
                Assert.That(breach.NotifyKickContact(2), Is.False, "epoch 不匹配必须拒绝");
                Assert.That(breach.NotifyKickContact(1), Is.True);
                Assert.That(breach.Phase, Is.EqualTo(RoomDoorBreachPhase.DoorOpening));
                Assert.That(breach.NotifyKickContact(1), Is.False, "接触语义只生效一次");
                Assert.That(contacts, Is.EqualTo(1));

                // 接触之后门立即开始打开(区别于接触前保持关闭)。
                breach.Advance(0.3f);
                Assert.That(Quaternion.Angle(initialRotation, hinge.localRotation),
                    Is.GreaterThan(5f), "KickContact 语义帧之后门必须开始打开");

                breach.Advance(1f);
                Assert.That(breach.IsRunning, Is.False);
                Assert.That(contacts, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void LoopResetRestoresDoorPoseAndIdlePhase()
        {
            CreateRig(out GameObject root, out Transform hinge, out RoomDoorBreachController breach,
                out Quaternion initialRotation);
            try
            {
                breach.Configure(hinge, 0.5f, 100f);
                breach.CaptureInitialState();
                breach.Begin(3, null);
                breach.Advance(1f);
                breach.Advance(1f);

                LoopResetContext reset = new LoopResetContext(3, 4);
                breach.Quiesce(reset);
                breach.Restore(reset);

                Assert.That(breach.Phase, Is.EqualTo(RoomDoorBreachPhase.Idle));
                Assert.That(breach.HasContactOccurred, Is.False);
                Assert.That(Quaternion.Angle(initialRotation, hinge.localRotation),
                    Is.LessThan(0.01f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void CreateRig(
            out GameObject root,
            out Transform hinge,
            out RoomDoorBreachController breach,
            out Quaternion initialRotation)
        {
            root = new GameObject("Breach EditMode Test");
            GameObject hingeObject = new GameObject("Door Hinge");
            hingeObject.transform.SetParent(root.transform, false);
            hinge = hingeObject.transform;
            initialRotation = Quaternion.Euler(0f, 12f, 0f);
            hinge.localRotation = initialRotation;
            breach = root.AddComponent<RoomDoorBreachController>();
        }
    }
}
