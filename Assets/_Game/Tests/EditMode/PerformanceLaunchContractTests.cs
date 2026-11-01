using ConGame.Runtime.AI;
using NUnit.Framework;
using UnityEngine;

namespace ConGame.Tests.EditMode
{
    public sealed class PerformanceLaunchContractTests
    {
        [Test]
        public void CreateFacingBuildsMutuallyFacingAnchorsAtCurrentPoses()
        {
            GameObject killerObject = new GameObject("Killer");
            GameObject playerObject = new GameObject("Player");
            try
            {
                killerObject.transform.SetPositionAndRotation(
                    new Vector3(1f, 0f, 1f), Quaternion.Euler(0f, 45f, 0f));
                playerObject.transform.SetPositionAndRotation(
                    new Vector3(1f, 0f, 4f), Quaternion.Euler(0f, 200f, 0f));

                PerformanceAnchorPair pair =
                    PerformanceAnchorPair.CreateFacing(killerObject.transform, playerObject.transform);

                Assert.That(pair.Killer.Position, Is.EqualTo(killerObject.transform.position));
                Assert.That(pair.Player.Position, Is.EqualTo(playerObject.transform.position));
                Assert.That(Quaternion.Angle(pair.Killer.Rotation, Quaternion.LookRotation(Vector3.forward)),
                    Is.LessThan(0.5f), "杀手锚点必须面向玩家");
                Assert.That(Quaternion.Angle(pair.Player.Rotation, Quaternion.LookRotation(Vector3.back)),
                    Is.LessThan(0.5f), "玩家锚点必须面向杀手");
                Assert.That(pair.MaxPositionError, Is.EqualTo(0.15f).Within(0.001f));
                Assert.That(pair.MaxAngleErrorDegrees, Is.EqualTo(10f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(killerObject);
                Object.DestroyImmediate(playerObject);
            }
        }

        [Test]
        public void LaunchToleranceRejectsExcessivePositionOrAngleError()
        {
            GameObject killerObject = new GameObject("Killer");
            GameObject playerObject = new GameObject("Player");
            try
            {
                killerObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                playerObject.transform.SetPositionAndRotation(
                    new Vector3(0f, 0f, 3f), Quaternion.identity);
                PerformanceAnchorPair pair =
                    PerformanceAnchorPair.CaptureCurrent(killerObject.transform, playerObject.transform);

                Assert.That(
                    pair.IsWithinLaunchTolerance(killerObject.transform, playerObject.transform),
                    Is.True, "确认当帧位姿必须在启动误差内");

                playerObject.transform.position = new Vector3(0.2f, 0f, 3f);
                Assert.That(
                    pair.IsWithinLaunchTolerance(killerObject.transform, playerObject.transform),
                    Is.False, "位移超过 0.15m 必须判定超差");

                playerObject.transform.position = new Vector3(0f, 0f, 3f);
                killerObject.transform.rotation = Quaternion.Euler(0f, 15f, 0f);
                Assert.That(
                    pair.IsWithinLaunchTolerance(killerObject.transform, playerObject.transform),
                    Is.False, "朝向超过 10° 必须判定超差");
            }
            finally
            {
                Object.DestroyImmediate(killerObject);
                Object.DestroyImmediate(playerObject);
            }
        }

        [Test]
        public void IntentValidityRequiresConfirmationKindAndOutcome()
        {
            PerformanceAnchorPair anchors = new PerformanceAnchorPair();
            ConfirmedPerformanceIntent valid =
                new ConfirmedPerformanceIntent("D01", 3, PerformanceConfirmationKind.AttackCommit, in anchors);
            ConfirmedPerformanceIntent noKind =
                new ConfirmedPerformanceIntent("D01", 3, PerformanceConfirmationKind.None, in anchors);
            ConfirmedPerformanceIntent noOutcome =
                new ConfirmedPerformanceIntent(" ", 3, PerformanceConfirmationKind.TrapTrigger, in anchors);

            Assert.That(valid.IsValid, Is.True);
            Assert.That(valid.LoopEpoch, Is.EqualTo(3));
            Assert.That(noKind.IsValid, Is.False);
            Assert.That(noOutcome.IsValid, Is.False);
        }
    }
}
