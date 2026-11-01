using System.Collections;
using ConGame.Domain.Facts;
using ConGame.Domain.Interaction;
using ConGame.Runtime.AI;
using ConGame.Runtime.Events;
using ConGame.Runtime.Facts;
using ConGame.Runtime.Interaction;
using ConGame.Runtime.Loop;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ConGame.Tests.PlayMode
{
    public sealed class KillerHuntPlayModeTests
    {
        [UnityTest]
        public IEnumerator OcclusionCancelsAimAndPreventsShotCommit()
        {
            Fixture fixture = new Fixture();
            try
            {
                fixture.Hunt.BeginHunt(7, "D01");
                yield return WaitForMode(fixture.Hunt, KillerTacticalMode.Aiming, 2f);
                Assert.That(fixture.Hunt.Mode, Is.EqualTo(KillerTacticalMode.Aiming));

                fixture.CreateOccluder();
                yield return new WaitForSeconds(1f);

                Assert.That(fixture.CommitCount, Is.Zero);
                Assert.That(fixture.Hunt.AimProgress, Is.Zero.Within(0.001f));
                Assert.That(fixture.Hunt.Mode,
                    Is.EqualTo(KillerTacticalMode.Investigating).Or.EqualTo(KillerTacticalMode.Searching));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator SustainedClearSightCommitsOnceThenResolvesOnlyAfterConfirmation()
        {
            Fixture fixture = new Fixture();
            try
            {
                fixture.Hunt.BeginHunt(9, "D01");
                float timeout = Time.time + 3f;
                while (fixture.CommitCount == 0 && Time.time < timeout)
                {
                    yield return null;
                }

                Assert.That(fixture.CommitCount, Is.EqualTo(1));
                Assert.That(fixture.Hunt.Mode, Is.EqualTo(KillerTacticalMode.Committing));
                Assert.That(fixture.ShotCount, Is.Zero);
                Assert.That(fixture.ResolveCount, Is.Zero);

                Assert.That(fixture.Hunt.ConfirmAttackCommit(9), Is.True);
                Assert.That(fixture.Hunt.ConfirmAttackCommit(9), Is.False);
                Assert.That(fixture.ShotCount, Is.EqualTo(1));
                yield return new WaitForSeconds(0.35f);
                Assert.That(fixture.ResolveCount, Is.EqualTo(1));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator NoiseBehindOcclusionUpdatesLastKnownPositionWithoutSeeingPlayer()
        {
            Fixture fixture = new Fixture();
            try
            {
                fixture.CreateOccluder();
                fixture.Hunt.BeginHunt(11, "D01");
                Vector3 source = new Vector3(-2f, 0f, 1f);
                fixture.Events.EmitNoise(NoiseLevel.Loud, source, "thrown_book");
                yield return null;

                Assert.That(fixture.Hunt.HasVisualContact, Is.False);
                Assert.That(fixture.Hunt.Mode, Is.EqualTo(KillerTacticalMode.Investigating));
                Assert.That(Vector3.Distance(fixture.Hunt.LastKnownPosition, source), Is.LessThan(0.001f));
                Assert.That(fixture.CommitCount, Is.Zero);
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator HiddenPlayerIsNotAcquiredByOrdinaryVision()
        {
            Fixture fixture = new Fixture();
            try
            {
                fixture.Facts.SetPlayerPose(PlayerPose.Hidden);
                fixture.Hunt.BeginHunt(12, "D03P");
                yield return new WaitForSeconds(1.25f);

                Assert.That(fixture.Hunt.HasVisualContact, Is.False);
                Assert.That(fixture.CommitCount, Is.Zero);
                Assert.That(fixture.Hunt.Mode,
                    Is.EqualTo(KillerTacticalMode.Observing).Or.EqualTo(KillerTacticalMode.Searching));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator NonLethalHitCancelsAimAndForcesRecovery()
        {
            Fixture fixture = new Fixture();
            try
            {
                fixture.Hunt.BeginHunt(13, "D01");
                yield return WaitForMode(fixture.Hunt, KillerTacticalMode.Aiming, 2f);
                Assert.That(fixture.Hunt.Mode, Is.EqualTo(KillerTacticalMode.Aiming));

                fixture.Hunt.NotifyHit(new HitInfo(
                    ImpactKind.Heavy, "baseball_bat", fixture.Killer.transform.position,
                    Vector3.forward, 6f));

                Assert.That(fixture.Hunt.Mode, Is.EqualTo(KillerTacticalMode.Recovering));
                Assert.That(fixture.Hunt.AimProgress, Is.Zero.Within(0.001f));
                yield return new WaitForSeconds(0.25f);
                Assert.That(fixture.CommitCount, Is.Zero);
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator LoopResetCancelsAimMemoryAndLateCallbacks()
        {
            Fixture fixture = new Fixture();
            try
            {
                fixture.Hunt.BeginHunt(20, "D01");
                yield return WaitForMode(fixture.Hunt, KillerTacticalMode.Aiming, 2f);
                Assert.That(fixture.Hunt.Mode, Is.EqualTo(KillerTacticalMode.Aiming));

                fixture.Hunt.Quiesce(new LoopResetContext(20, 21));
                fixture.Hunt.Restore(new LoopResetContext(20, 21));
                fixture.Hunt.Resume(new LoopResetContext(20, 21));
                yield return new WaitForSeconds(1f);

                Assert.That(fixture.Hunt.Mode, Is.EqualTo(KillerTacticalMode.Inactive));
                Assert.That(fixture.Hunt.HasLastKnownPosition, Is.False);
                Assert.That(fixture.CommitCount, Is.Zero);
                Assert.That(fixture.ResolveCount, Is.Zero);
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator MovementNoiseDoesNotCancelAnEstablishedAim()
        {
            Fixture fixture = new Fixture();
            try
            {
                fixture.Hunt.BeginHunt(21, "D01");
                yield return WaitForMode(fixture.Hunt, KillerTacticalMode.Aiming, 2f);
                Assert.That(fixture.Hunt.Mode, Is.EqualTo(KillerTacticalMode.Aiming));

                float timeout = Time.time + 1.2f;
                while (fixture.CommitCount == 0 && Time.time < timeout)
                {
                    fixture.Events.EmitNoise(NoiseLevel.Normal, fixture.Player.transform.position, "moving_player");
                    yield return new WaitForSeconds(0.08f);
                }

                Assert.That(fixture.CommitCount, Is.EqualTo(1));
                Assert.That(fixture.Hunt.Mode, Is.EqualTo(KillerTacticalMode.Committing));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator HiddenPlayerIsFoundOnlyAfterCloseAuthoredAnchorSearch()
        {
            Fixture fixture = new Fixture();
            try
            {
                fixture.Player.transform.position = new Vector3(0f, 0f, 0.75f);
                Physics.SyncTransforms();
                fixture.Facts.SetPlayerPose(PlayerPose.Hidden);
                fixture.Hunt.BeginHunt(22, "D11");
                yield return new WaitForSeconds(0.75f);
                Assert.That(fixture.CommitCount, Is.Zero, "Ordinary sight must not reveal a hidden player.");

                float timeout = Time.time + 3.5f;
                while (fixture.CommitCount == 0 && Time.time < timeout)
                {
                    yield return null;
                }

                Assert.That(fixture.CommitCount, Is.EqualTo(1));
                Assert.That(fixture.Hunt.Mode, Is.EqualTo(KillerTacticalMode.Committing));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator RepeatedBatHitsCannotPermanentlyStunlockTheKiller()
        {
            Fixture fixture = new Fixture();
            try
            {
                fixture.Hunt.BeginHunt(23, "D01");
                yield return WaitForMode(fixture.Hunt, KillerTacticalMode.Aiming, 2f);
                HitInfo hit = new HitInfo(
                    ImpactKind.Heavy, "baseball_bat", fixture.Killer.transform.position,
                    Vector3.forward, 6f);

                fixture.Hunt.NotifyHit(hit);
                yield return new WaitForSeconds(0.15f);
                fixture.Hunt.NotifyHit(hit);
                yield return new WaitForSeconds(0.15f);

                float timeout = Time.time + 3f;
                while (fixture.CommitCount == 0 && Time.time < timeout)
                {
                    fixture.Hunt.NotifyHit(hit);
                    yield return new WaitForSeconds(0.2f);
                }

                Assert.That(fixture.CommitCount, Is.EqualTo(1),
                    "The bat may create an escape window but must not provide an infinite stun lock.");
            }
            finally
            {
                fixture.Dispose();
            }
        }




        private static IEnumerator WaitForMode(KillerTacticalController hunt, KillerTacticalMode mode, float seconds)
        {
            float timeout = Time.time + seconds;
            while (hunt.Mode != mode && Time.time < timeout)
            {
                yield return null;
            }
        }

        private sealed class Fixture
        {
            public readonly GameObject Root;
            public readonly GameObject Killer;
            public readonly GameObject Player;
            public readonly GameplayFactService Facts;
            public readonly GameplayEventHub Events;
            public readonly KillerTacticalController Hunt;

            private readonly GameObject _eye;
            private readonly GameObject _anchor;
            private GameObject _occluder;

            public int CommitCount { get; private set; }
            public int ShotCount { get; private set; }
            public int ResolveCount { get; private set; }

            public Fixture()
            {
                Root = new GameObject("hunt-fixture");
                Killer = new GameObject("killer");
                Player = new GameObject("player");
                _eye = new GameObject("eye");
                _anchor = new GameObject("standoff-anchor");

                Killer.transform.SetParent(Root.transform);
                Player.transform.SetParent(Root.transform);
                _eye.transform.SetParent(Killer.transform, false);
                _anchor.transform.SetParent(Root.transform);
                Killer.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                Player.transform.position = new Vector3(0f, 0f, 3f);
                _eye.transform.localPosition = new Vector3(0f, 1.55f, 0.15f);
                _anchor.transform.position = Vector3.zero;

                CapsuleCollider playerCollider = Player.AddComponent<CapsuleCollider>();
                playerCollider.height = 2f;
                playerCollider.center = Vector3.up;

                Facts = Root.AddComponent<GameplayFactService>();
                Facts.CaptureInitialState();
                Facts.SetPlayerPose(PlayerPose.Standing);
                Events = Root.AddComponent<GameplayEventHub>();
                Hunt = Killer.AddComponent<KillerTacticalController>();
                Hunt.Configure(null, Player.transform, _eye.transform, Facts, Events, null,
                    new[] { _anchor.transform });
                Hunt.AttackCommitRequested += (_, __) =>
                {
                    CommitCount++;
                    return true;
                };
                Hunt.ShotFired += (_, __) => ShotCount++;
                Hunt.ShotResolved += (_, __) => ResolveCount++;
            }

            public void CreateOccluder()
            {
                if (_occluder != null)
                {
                    return;
                }

                _occluder = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _occluder.name = "occluder";
                _occluder.transform.SetParent(Root.transform);
                _occluder.transform.position = new Vector3(0f, 1.1f, 1.5f);
                _occluder.transform.localScale = new Vector3(2f, 2.4f, 0.2f);
                Physics.SyncTransforms();
            }

            public void Dispose()
            {
                Object.Destroy(Root);
            }
        }
    }
}
