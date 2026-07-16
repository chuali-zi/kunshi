using System.Collections;
using ConGame.Domain.Counterplay;
using ConGame.Domain.Facts;
using ConGame.Domain.Interaction;
using ConGame.Runtime.AI;
using ConGame.Runtime.Counterplay;
using ConGame.Runtime.Ending;
using ConGame.Runtime.Facts;
using ConGame.Runtime.Interaction;
using ConGame.Runtime.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ConGame.Tests.PlayMode
{
    public sealed class Batch5To7WorldSystemsPlayModeTests
    {
        [UnityTest]
        public IEnumerator StanceCycleReachesProneForD14()
        {
            GameObject factsObject = new GameObject("facts");
            GameObject player = new GameObject("player");
            try
            {
                GameplayFactService facts = factsObject.AddComponent<GameplayFactService>();
                facts.CaptureInitialState();
                PlayerInteractor interactor = player.AddComponent<PlayerInteractor>();
                interactor.Configure(null, null, null, null, facts, null, null);

                interactor.SetPose(PlayerPose.Standing);
                // Simulate stance cycle: Standing -> Crouched -> Prone
                interactor.SetPose(PlayerPose.Crouched);
                interactor.SetPose(PlayerPose.Prone);
                Assert.That(facts.CaptureSnapshot().PlayerPose, Is.EqualTo(PlayerPose.Prone));

                FactSnapshot snapshot = facts.CaptureSnapshot();
                snapshot.HasMeaningfulInput = true;
                snapshot.IsAtAwakeningPoint = false;
                CounterplayRule d14 = new CounterplayRule(
                    "D14", 1700, CounterplayTriggerMode.Entry,
                    new[] { new FactCondition(FactKey.PlayerPose, FactOperator.Equal, (int)PlayerPose.Prone) },
                    ContentStatus.Implemented);
                Assert.That(d14.Matches(CounterplayTriggerMode.Entry, snapshot), Is.True);
                yield return null;
            }
            finally
            {
                Object.Destroy(factsObject);
                Object.Destroy(player);
            }
        }

        [UnityTest]
        public IEnumerator KneelingPoseSelectsD06Continuous()
        {
            FactSnapshot snapshot = FactSnapshot.CreateBaseline();
            snapshot.PlayerPose = PlayerPose.Kneeling;
            snapshot.HasMeaningfulInput = true;
            snapshot.IsAtAwakeningPoint = false;
            CounterplayRule d06 = new CounterplayRule(
                "D06", 1900, CounterplayTriggerMode.Continuous,
                new[] { new FactCondition(FactKey.PlayerPose, FactOperator.Equal, (int)PlayerPose.Kneeling) },
                ContentStatus.Implemented);
            Assert.That(d06.Matches(CounterplayTriggerMode.Continuous, snapshot), Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator BedHideAndCameraBlindZonesMatchD04AndD12()
        {
            GameObject factsObject = new GameObject("facts");
            GameObject player = new GameObject("player");
            GameObject bedTop = new GameObject("bedTop");
            try
            {
                CharacterController controller = player.AddComponent<CharacterController>();
                GameplayFactService facts = factsObject.AddComponent<GameplayFactService>();
                facts.CaptureInitialState();
                PlayerMotor motor = player.AddComponent<PlayerMotor>();
                motor.Configure(controller, null, null, facts, null);
                PlayerInteractor interactor = player.AddComponent<PlayerInteractor>();
                interactor.Configure(null, null, null, motor, facts, null, null);

                GameObject anchor = new GameObject("anchor");
                anchor.transform.SetParent(bedTop.transform);
                HideCapability hide = bedTop.AddComponent<HideCapability>();
                hide.Configure(anchor.transform, PlayerZone.Bed);
                InteractionContext context = new InteractionContext(interactor, null);
                Assert.That(hide.Execute(context), Is.EqualTo(InteractionResult.Accepted));
                Assert.That(facts.CaptureSnapshot().PlayerZone, Is.EqualTo(PlayerZone.Bed));
                Assert.That(facts.CaptureSnapshot().PlayerPose, Is.EqualTo(PlayerPose.Hidden));

                FactSnapshot d04 = facts.CaptureSnapshot();
                d04.HasMeaningfulInput = true;
                d04.IsAtAwakeningPoint = false;
                CounterplayRule ruleD04 = new CounterplayRule(
                    "D04", 1300, CounterplayTriggerMode.Entry,
                    new[]
                    {
                        new FactCondition(FactKey.PlayerZone, FactOperator.Equal, (int)PlayerZone.Bed),
                        new FactCondition(FactKey.PlayerPose, FactOperator.Equal, (int)PlayerPose.Hidden)
                    },
                    ContentStatus.Implemented);
                Assert.That(ruleD04.Matches(CounterplayTriggerMode.Entry, d04), Is.True);

                facts.SetPlayerZone(PlayerZone.CameraBlind);
                FactSnapshot d12 = facts.CaptureSnapshot();
                d12.HasMeaningfulInput = true;
                d12.IsAtAwakeningPoint = false;
                CounterplayRule ruleD12 = new CounterplayRule(
                    "D12", 1600, CounterplayTriggerMode.Entry,
                    new[] { new FactCondition(FactKey.PlayerZone, FactOperator.Equal, (int)PlayerZone.CameraBlind) },
                    ContentStatus.Implemented);
                Assert.That(ruleD12.Matches(CounterplayTriggerMode.Entry, d12), Is.True);
                yield return null;
            }
            finally
            {
                Object.Destroy(factsObject);
                Object.Destroy(player);
                Object.Destroy(bedTop);
            }
        }

        [UnityTest]
        public IEnumerator IdleAtAwakeningSelectsD02()
        {
            FactSnapshot snapshot = FactSnapshot.CreateBaseline();
            snapshot.HasMeaningfulInput = false;
            snapshot.IsAtAwakeningPoint = true;
            CounterplayRule d02 = new CounterplayRule(
                "D02", 2200, CounterplayTriggerMode.Entry,
                new[]
                {
                    new FactCondition(FactKey.HasMeaningfulInput, FactOperator.False, 0),
                    new FactCondition(FactKey.IsAtAwakeningPoint, FactOperator.True, 1)
                },
                ContentStatus.Implemented);
            Assert.That(d02.Matches(CounterplayTriggerMode.Entry, snapshot), Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SofaRearCrouchedSelectsD03PAheadOfD03()
        {
            CounterplayEvaluator evaluator = new CounterplayEvaluator();
            var rules = new System.Collections.Generic.List<CounterplayRule>();
            foreach (CounterplaySeed seed in CounterplaySeedLibrary.All)
            {
                rules.Add(new CounterplayRule(seed.Id, seed.SortOrder, seed.TriggerMode, seed.Conditions, seed.Status));
            }

            FactSnapshot crouched = FactSnapshot.CreateBaseline();
            crouched.PlayerZone = PlayerZone.SofaRear;
            crouched.PlayerPose = PlayerPose.Crouched;
            crouched.HasMeaningfulInput = true;
            crouched.IsAtAwakeningPoint = false;
            Assert.That(evaluator.Select(rules, CounterplayTriggerMode.Entry, crouched)?.Id, Is.EqualTo("D03P"));

            FactSnapshot standing = crouched;
            standing.PlayerPose = PlayerPose.Standing;
            Assert.That(evaluator.Select(rules, CounterplayTriggerMode.Entry, standing)?.Id, Is.EqualTo("D03"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator AmbushInPersistentBlackSelectsD20OverD19()
        {
            GameObject root = new GameObject("ambush");
            try
            {
                GameplayFactService facts = root.AddComponent<GameplayFactService>();
                facts.CaptureInitialState();
                facts.SetRoomLight(RoomLightState.PersistentBlack);
                GameplayAffordanceHandler affordance = root.AddComponent<GameplayAffordanceHandler>();
                affordance.Configure(GameplayAffordanceKind.StartAmbush, facts);
                PlayerInteractor interactor = root.AddComponent<PlayerInteractor>();
                Assert.That(affordance.Use(new InteractionContext(interactor, null)), Is.EqualTo(InteractionResult.Accepted));
                Assert.That(facts.CaptureSnapshot().IsAmbushing, Is.True);

                CounterplayEvaluator evaluator = new CounterplayEvaluator();
                var rules = new System.Collections.Generic.List<CounterplayRule>();
                foreach (CounterplaySeed seed in CounterplaySeedLibrary.All)
                {
                    rules.Add(new CounterplayRule(seed.Id, seed.SortOrder, seed.TriggerMode, seed.Conditions, seed.Status));
                }

                FactSnapshot ambush = facts.CaptureSnapshot();
                ambush.HasMeaningfulInput = true;
                ambush.IsAtAwakeningPoint = false;
                Assert.That(evaluator.Select(rules, CounterplayTriggerMode.Entry, ambush)?.Id, Is.EqualTo("D20"));

                ambush.IsAmbushing = false;
                Assert.That(evaluator.Select(rules, CounterplayTriggerMode.Entry, ambush)?.Id, Is.EqualTo("D19"));
                yield return null;
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        [UnityTest]
        public IEnumerator RemoteUsePulsesTelevisionLightImpact()
        {
            GameObject tvObject = new GameObject("tv");
            GameObject remoteObject = new GameObject("remote");
            try
            {
                TelevisionController television = tvObject.AddComponent<TelevisionController>();
                bool pulsed = false;
                television.LightImpactPulse += () => pulsed = true;

                RemoteUseHandler remote = remoteObject.AddComponent<RemoteUseHandler>();
                remote.Configure(television);
                Assert.That(remote.Use(new InteractionContext(null, null)), Is.EqualTo(InteractionResult.Accepted));
                Assert.That(pulsed, Is.True);
                yield return null;
            }
            finally
            {
                Object.Destroy(tvObject);
                Object.Destroy(remoteObject);
            }
        }

        [UnityTest]
        public IEnumerator ScriptedBackupPistolIsExcludedFromOrdinaryScan()
        {
            GameObject player = new GameObject("player");
            GameObject backup = new GameObject("backup");
            try
            {
                player.AddComponent<CharacterController>();
                PlayerInteractor interactor = player.AddComponent<PlayerInteractor>();
                InteractionScanner scanner = player.AddComponent<InteractionScanner>();
                scanner.Configure(interactor, 2f, ~0);

                backup.transform.position = player.transform.position + Vector3.forward;
                backup.AddComponent<BoxCollider>();
                Rigidbody body = backup.AddComponent<Rigidbody>();
                InteractableEntity entity = backup.AddComponent<InteractableEntity>();
                entity.Configure(null, "pistol_backup");
                StatefulWorldItem stateful = backup.AddComponent<StatefulWorldItem>();
                stateful.Configure(body, ItemPhysicalState.Scripted);
                backup.AddComponent<InspectCapability>();

                Assert.That(stateful.AllowsOrdinaryInteraction, Is.False);
                Assert.That(scanner.Refresh(), Is.Null);
                yield return null;
            }
            finally
            {
                Object.Destroy(player);
                Object.Destroy(backup);
            }
        }
    }
}
