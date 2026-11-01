using System.Collections;
using System.Collections.Generic;
using ConGame.Domain.Choreography;
using ConGame.Domain.Counterplay;
using ConGame.Domain.Facts;
using ConGame.Domain.Interaction;
using ConGame.Domain.Persistence;
using ConGame.Presentation.M1;
using ConGame.Runtime.AI;
using ConGame.Runtime.Counterplay;
using ConGame.Runtime.Events;
using ConGame.Runtime.Facts;
using ConGame.Runtime.Interaction;
using ConGame.Runtime.Loop;
using ConGame.Runtime.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ConGame.Tests.PlayMode
{
    public sealed class CounterplayRepairPlayModeTests
    {
        [UnityTest]
        public IEnumerator PerformanceLockBlocksReevaluationAndSynchronousSwitchKeepsNewestState()
        {
            GameObject root = new GameObject("performance_generation_test");
            CounterplayCatalogAsset catalog = ScriptableObject.CreateInstance<CounterplayCatalogAsset>();
            CounterplayDefinitionAsset d08 = ScriptableObject.CreateInstance<CounterplayDefinitionAsset>();
            CounterplayDefinitionAsset d22 = ScriptableObject.CreateInstance<CounterplayDefinitionAsset>();
            M1GreyboxPlayableAsset p08 = ScriptableObject.CreateInstance<M1GreyboxPlayableAsset>();
            M1GreyboxPlayableAsset p22 = ScriptableObject.CreateInstance<M1GreyboxPlayableAsset>();
            try
            {
                p08.Configure("D08", M1GreyboxDeathStyle.ArmedStandoff, 1f,
                    PerformanceHeldItemPolicy.Preserve, null, null);
                p22.Configure("D22", M1GreyboxDeathStyle.ArmedStandoff, 1f,
                    PerformanceHeldItemPolicy.Preserve, null, null);
                d08.Configure("D08", 100, CounterplayTriggerMode.Entry, new FactCondition[0],
                    ContentStatus.Implemented, p08);
                d22.Configure("D22", 1, CounterplayTriggerMode.Reactive,
                    new FactCondition[0], ContentStatus.Implemented, p22);
                catalog.Configure(new[] { d08, d22 });

                GameplayFactService facts = root.AddComponent<GameplayFactService>();
                facts.CaptureInitialState();
                SynchronousSwitchPerformancePlayer player = root.AddComponent<SynchronousSwitchPerformancePlayer>();
                KillerDirector killer = root.AddComponent<KillerDirector>();
                killer.Configure(null, null, facts, catalog, null, player);
                player.Director = killer;
                killer.BeginEntry(3, _ => { });
                yield return new WaitForSeconds(2.1f);

                Assert.That(killer.State, Is.EqualTo(KillerState.PerformanceLocked));
                Assert.That(killer.SelectedOutcomeId, Is.EqualTo("D08"));
                killer.HandleStimulus(new KillerStimulus(KillerStimulusType.Glare, "test"));
                yield return null;
                Assert.That(killer.SelectedOutcomeId, Is.EqualTo("D08"));

                player.SwitchOnD08 = true;
                Assert.That(killer.TryStartOutcome("D08"), Is.True);
                Assert.That(killer.SelectedOutcomeId, Is.EqualTo("D22"));
                Assert.That(killer.State, Is.EqualTo(KillerState.PerformanceLocked));
            }
            finally
            {
                Object.Destroy(root);
                Object.Destroy(catalog);
                Object.Destroy(d08);
                Object.Destroy(d22);
                Object.Destroy(p08);
                Object.Destroy(p22);
            }
        }

        [UnityTest]
        public IEnumerator LiveAttackCommitFreezesOutcomeBeforeReactiveStimuliCanReevaluate()
        {
            GameObject root = new GameObject("atomic-live-commit");
            GameObject player = new GameObject("player");
            GameObject killerObject = new GameObject("killer");
            GameObject eye = new GameObject("eye");
            CounterplayCatalogAsset catalog = ScriptableObject.CreateInstance<CounterplayCatalogAsset>();
            CounterplayDefinitionAsset d01 = ScriptableObject.CreateInstance<CounterplayDefinitionAsset>();
            CounterplayDefinitionAsset d22 = ScriptableObject.CreateInstance<CounterplayDefinitionAsset>();
            try
            {
                player.transform.SetParent(root.transform);
                killerObject.transform.SetParent(root.transform);
                eye.transform.SetParent(killerObject.transform, false);
                player.transform.position = new Vector3(0f, 0f, 3f);
                eye.transform.localPosition = new Vector3(0f, 1.55f, 0.15f);
                CapsuleCollider target = player.AddComponent<CapsuleCollider>();
                target.height = 2f;
                target.center = Vector3.up;

                d01.Configure("D01", 100,
                    CounterplayTriggerMode.Entry | CounterplayTriggerMode.Continuous,
                    new FactCondition[0], ContentStatus.Implemented, null);
                d22.Configure("D22", 1, CounterplayTriggerMode.Reactive,
                    new FactCondition[0], ContentStatus.Implemented, null);
                catalog.Configure(new[] { d01, d22 });

                GameplayFactService facts = root.AddComponent<GameplayFactService>();
                facts.CaptureInitialState();
                GameplayEventHub events = root.AddComponent<GameplayEventHub>();
                KillerTacticalController hunt = killerObject.AddComponent<KillerTacticalController>();
                hunt.Configure(null, player.transform, eye.transform, facts, events, null, new Transform[0]);
                KillerDirector killer = killerObject.AddComponent<KillerDirector>();
                killer.Configure(null, player.transform, facts, catalog, null);
                killer.ConfigureHunt(hunt);

                string committedOutcome = null;
                int shots = 0;
                killer.ShotFired += (_, __) => shots++;
                killer.BeginEntry(31, _ => { }, outcome =>
                {
                    committedOutcome = outcome;
                    killer.HandleStimulus(new KillerStimulus(KillerStimulusType.Glare, "same-frame"));
                    return true;
                });

                float timeout = Time.time + 4f;
                while (committedOutcome == null && Time.time < timeout)
                {
                    yield return null;
                }

                Assert.That(committedOutcome, Is.EqualTo("D01"));
                Assert.That(killer.SelectedOutcomeId, Is.EqualTo("D01"));
                Assert.That(killer.State, Is.EqualTo(KillerState.PerformanceLocked));
                Assert.That(killer.ConfirmAttackCommit(31, committedOutcome), Is.True);
                Assert.That(shots, Is.EqualTo(1));
            }
            finally
            {
                Object.Destroy(root);
                Object.Destroy(catalog);
                Object.Destroy(d01);
                Object.Destroy(d22);
            }
        }


        [UnityTest]
        public IEnumerator D08OnlyOpensPistolAfterSuccessAndTimesOutAsD08()
        {
            Fixture fixture = new Fixture();
            GameObject hand = new GameObject("hand");
            try
            {
                fixture.Controller.ConfigureWindowDurations(0.05f, 1f, 1f, 0.05f);
                fixture.Adapter.OnPerformanceStarted("D08", 7, PerformanceHeldItemPolicy.DropAtStart);
                Assert.That(fixture.Controller.State, Is.EqualTo(CounterplayLootWindowState.D08Struggle));
                Assert.That(fixture.Pistol.activeSelf, Is.False);
                Assert.That(fixture.Flashlight.activeSelf, Is.False);

                bool failureDeferred = fixture.Adapter.OnBranchResolved(
                    "D08", 7,
                    new PerformanceBranchDefinition("close_shot", false, string.Empty),
                    CounterplayNarrativeOutcome.ScriptedFailure);
                Assert.That(failureDeferred, Is.False);
                Assert.That(fixture.Controller.State, Is.EqualTo(CounterplayLootWindowState.Resolved));
                Assert.That(fixture.RequestedNext, Is.Null);

                fixture.Adapter.OnPerformanceStarted("D08", 8, PerformanceHeldItemPolicy.DropAtStart);
                bool successDeferred = fixture.Adapter.OnBranchResolved(
                    "D08", 8,
                    new PerformanceBranchDefinition("disarm_window", true, string.Empty),
                    CounterplayNarrativeOutcome.ScriptedFailure);
                Assert.That(successDeferred, Is.True);
                Assert.That(fixture.Controller.State, Is.EqualTo(CounterplayLootWindowState.D08LootWindow));
                Assert.That(fixture.Pistol.activeSelf, Is.True);
                Assert.That(fixture.Flashlight.activeSelf, Is.False);
                Assert.That(fixture.PistolState.BeginHeld(hand.transform, null), Is.True);
                Assert.That(fixture.Controller.State, Is.EqualTo(CounterplayLootWindowState.Resolved));
                Assert.That(fixture.RequestedNext, Is.EqualTo("D22"));

                fixture.ResetObservedRoutes();
                fixture.PistolState.MarkPlaced();
                fixture.Adapter.OnPerformanceStarted("D08", 9, PerformanceHeldItemPolicy.DropAtStart);
                fixture.Adapter.OnBranchResolved(
                    "D08", 9,
                    new PerformanceBranchDefinition("disarm_window", true, string.Empty),
                    CounterplayNarrativeOutcome.ScriptedFailure);
                yield return new WaitForSeconds(0.08f);
                Assert.That(fixture.Controller.State, Is.EqualTo(CounterplayLootWindowState.Resolved));
                Assert.That(fixture.CompletedOutcome, Is.EqualTo("D08"));
                Assert.That(fixture.RequestedNext, Is.Null);
            }
            finally
            {
                Object.Destroy(hand);
                fixture.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator D21RoutesRealPickupAndIlluminationStatesWithoutSelfChaining()
        {
            Fixture fixture = new Fixture();
            GameObject hand = new GameObject("hand");
            try
            {
                fixture.Adapter.OnPerformanceStarted("D21", 11, PerformanceHeldItemPolicy.DropAtStart);
                OpenD21Choice(fixture, 11);
                Assert.That(fixture.Controller.State, Is.EqualTo(CounterplayLootWindowState.D21Choice));
                Assert.That(fixture.Pistol.activeSelf, Is.True);
                Assert.That(fixture.Flashlight.activeSelf, Is.True);
                Assert.That(fixture.Backup.activeSelf, Is.False);

                Assert.That(fixture.FlashlightState.BeginHeld(hand.transform, null), Is.True);
                Assert.That(fixture.Controller.State, Is.EqualTo(CounterplayLootWindowState.D21FlashlightArmed));
                fixture.Controller.NotifyKillerIlluminated(
                    new FlashlightHit("flashlight", Vector3.zero, Vector3.forward, 0.25f));
                Assert.That(fixture.Controller.State, Is.EqualTo(CounterplayLootWindowState.Resolved));
                yield return FinishD21SemanticTail(fixture, 11);
                Assert.That(fixture.CompletedOutcome, Is.EqualTo("D21"));
                Assert.That(fixture.Loop.Progress.d21Seen, Is.True);
                Assert.That(fixture.RequestedNext, Is.Null);

                fixture.ResetObservedRoutes();
                fixture.FlashlightState.MarkPlaced();
                fixture.Adapter.OnPerformanceStarted("D21", 12, PerformanceHeldItemPolicy.DropAtStart);
                OpenD21Choice(fixture, 12);
                Assert.That(fixture.FlashlightState.BeginHeld(hand.transform, null), Is.True);
                fixture.Events.EmitStimulus(ConGame.Domain.Facts.KillerStimulusType.Glare, "mirror");
                Assert.That(fixture.Controller.State, Is.EqualTo(CounterplayLootWindowState.Resolved));
                Assert.That(fixture.RequestedNext, Is.EqualTo("D17"));

                fixture.ResetObservedRoutes();
                fixture.PistolState.MarkPlaced();
                fixture.Adapter.OnPerformanceStarted("D21", 13, PerformanceHeldItemPolicy.DropAtStart);
                OpenD21Choice(fixture, 13);
                Assert.That(fixture.PistolState.BeginHeld(hand.transform, null), Is.True);
                Assert.That(fixture.Controller.State, Is.EqualTo(CounterplayLootWindowState.Resolved));
                Assert.That(fixture.RequestedNext, Is.EqualTo("D22"));
            }
            finally
            {
                Object.Destroy(hand);
                fixture.Dispose();
            }

            yield return null;
        }

        private static IEnumerator FinishD21SemanticTail(Fixture fixture, int loopEpoch)
        {
            PairedPerformancePhaseMachine machine = new PairedPerformancePhaseMachine(
                PairedPerformanceCatalog.CreateD21());
            List<PairedPerformanceSemanticEvent> semanticEvents =
                new List<PairedPerformanceSemanticEvent>();

            machine.Start(loopEpoch, semanticEvents);
            semanticEvents.Clear();
            Assert.That(machine.TryJumpToStage("block-light", loopEpoch, semanticEvents), Is.True);
            semanticEvents.Clear();

            machine.Advance(3.01f, false, loopEpoch, semanticEvents);
            yield return null;
            machine.Advance(1.61f, false, loopEpoch, semanticEvents);
            yield return null;
            machine.Advance(0.51f, false, loopEpoch, semanticEvents);
            yield return null;

            Assert.That(machine.IsComplete, Is.True);
            Assert.That(HasSemanticEvent(semanticEvents, "D21.BackupPistol.Draw"), Is.True);
            Assert.That(HasSemanticEvent(semanticEvents, "D21.BackupPistol.Fire"), Is.True);
            Assert.That(HasSemanticEvent(semanticEvents, "D21.Completed"), Is.True);
            fixture.Adapter.RequestOutcomeCompletion("D21", loopEpoch, true);
            yield return null;
        }

        private static bool HasSemanticEvent(
            IReadOnlyList<PairedPerformanceSemanticEvent> events, string eventId)
        {
            for (int i = 0; i < events.Count; i++)
            {
                if (string.Equals(events[i].EventId, eventId, System.StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static void OpenD21Choice(Fixture fixture, int loopEpoch)
        {
            fixture.Controller.NotifyPairedSemanticEvent(new PairedPerformanceSemanticEvent(
                "D21.Pistol.Dropped", "D21", "item-drop",
                PairedContactState.Separated, loopEpoch));
            fixture.Controller.NotifyPairedSemanticEvent(new PairedPerformanceSemanticEvent(
                "D21.Flashlight.Dropped", "D21", "item-drop",
                PairedContactState.Separated, loopEpoch));
        }

        [Test]
        public void OldD21EventsCannotReopenHandInteractionAfterSwitchToD22OrReset()
        {
            Fixture fixture = new Fixture();
            try
            {
                fixture.Adapter.OnPerformanceStarted("D21", 17, PerformanceHeldItemPolicy.DropAtStart);
                OpenD21Choice(fixture, 17);
                fixture.Adapter.OnPerformanceStarted("D22", 17, PerformanceHeldItemPolicy.LockToPerformance);
                foreach (string eventId in new[] { "D21.Pair.Begin", "D21.Flashlight.Dislodged",
                             "D21.Flashlight.Recovery.Open", "D21.Pistol.Dropped", "D21.Flashlight.Dropped" })
                {
                    fixture.Controller.NotifyPairedSemanticEvent(new PairedPerformanceSemanticEvent(
                        eventId, "D21", "detach", PairedContactState.Separated, 17));
                }
                Assert.That(fixture.Controller.State, Is.EqualTo(CounterplayLootWindowState.D22AwaitMainShot));
                Assert.That(fixture.Controller.MainFirearm.RoundsRemaining, Is.EqualTo(1));

                fixture.Controller.Restore(new LoopResetContext(17, 18));
                fixture.Controller.NotifyPairedSemanticEvent(new PairedPerformanceSemanticEvent(
                    "D21.Flashlight.Recovery.Open", "D21", "detach", PairedContactState.Separated, 18));
                Assert.That(fixture.Controller.State, Is.EqualTo(CounterplayLootWindowState.Inactive));
                Assert.That(fixture.Flashlight.activeSelf, Is.False);
            }
            finally { fixture.Dispose(); }
        }

        [UnityTest]
        public IEnumerator D22ConsumesOneRoundDodgesThenMainPistolDryFireFails()
        {
            Fixture fixture = new Fixture();
            GameObject playerObject = new GameObject("player");
            GameObject hand = new GameObject("hand");
            try
            {
                PlayerInteractor player = playerObject.AddComponent<PlayerInteractor>();
                fixture.Controller.ConfigureWindowDurations(1f, 1f, 1f, 0.05f);
                fixture.Adapter.OnPerformanceStarted("D22", 21, PerformanceHeldItemPolicy.LockToPerformance);
                Assert.That(fixture.Controller.State, Is.EqualTo(CounterplayLootWindowState.D22AwaitMainShot));
                Assert.That(fixture.Controller.MainFirearm.RoundsRemaining, Is.EqualTo(1));

                Assert.That(fixture.PistolState.BeginHeld(hand.transform, null), Is.True);
                fixture.PistolEntity.SetHeld(true);
                Assert.That(fixture.Controller.MainFirearm.Use(new InteractionContext(player, fixture.PistolEntity)),
                    Is.EqualTo(InteractionResult.Accepted));
                Assert.That(fixture.Controller.MainFirearm.RoundsRemaining, Is.Zero);
                Assert.That(fixture.Controller.State, Is.EqualTo(CounterplayLootWindowState.D22Dodging));

                yield return new WaitForSeconds(0.08f);
                Assert.That(fixture.Controller.State, Is.EqualTo(CounterplayLootWindowState.D22AwaitMainDryFire));
                Assert.That(fixture.Backup.activeSelf, Is.False,
                    "The terminal backup pistol stays hidden on the floor; the character visual is drawn at the finale cue.");
                Assert.That(fixture.BackupState.CurrentState, Is.EqualTo(ItemPhysicalState.Scripted));

                Assert.That(fixture.Controller.MainFirearm.Use(new InteractionContext(player, fixture.PistolEntity)),
                    Is.EqualTo(InteractionResult.Accepted));
                Assert.That(fixture.Controller.State, Is.EqualTo(CounterplayLootWindowState.Resolved));
                Assert.That(fixture.CompletedOutcome, Is.EqualTo("D22"));
                Assert.That(fixture.RequestedNext, Is.Null);
            }
            finally
            {
                Object.Destroy(playerObject);
                Object.Destroy(hand);
                fixture.Dispose();
            }
        }

        private sealed class Fixture
        {
            public Fixture()
            {
                Root = new GameObject("counterplay_repair_fixture");
                Events = Root.AddComponent<GameplayEventHub>();
                Loop = Root.AddComponent<LoopDirector>();
                typeof(LoopDirector).GetField("_progress",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    ?.SetValue(Loop, PersistentProgressV1.CreateNew());
                Adapter = Root.AddComponent<CounterplayPerformanceRuntimeAdapter>();
                Adapter.Configure(null, Loop, null);
                Pistol = CreateItem("pistol_main", out InteractableEntity pistolEntity,
                    out StatefulWorldItem pistolState);
                PistolEntity = pistolEntity;
                PistolState = pistolState;
                Flashlight = CreateItem("flashlight", out InteractableEntity flashlightEntity,
                    out StatefulWorldItem flashlightState);
                FlashlightEntity = flashlightEntity;
                FlashlightState = flashlightState;
                Backup = CreateItem("pistol_backup", out InteractableEntity backupEntity,
                    out StatefulWorldItem backupState, ItemPhysicalState.Scripted);
                BackupEntity = backupEntity;
                BackupState = backupState;
                Controller = Root.AddComponent<StolenLootDropController>();
                Controller.Configure(Pistol, Flashlight, Adapter, Backup, Events);
                Controller.CaptureInitialState();
                Adapter.NextOutcomeRequested += OnNextOutcome;
                Adapter.OutcomeCompletionRequested += OnCompleted;
            }

            public GameObject Root { get; }
            public GameplayEventHub Events { get; }
            public LoopDirector Loop { get; }
            public CounterplayPerformanceRuntimeAdapter Adapter { get; }
            public StolenLootDropController Controller { get; }
            public GameObject Pistol { get; }
            public GameObject Flashlight { get; }
            public GameObject Backup { get; }
            public InteractableEntity PistolEntity { get; }
            public InteractableEntity FlashlightEntity { get; }
            public InteractableEntity BackupEntity { get; }
            public StatefulWorldItem PistolState { get; }
            public StatefulWorldItem FlashlightState { get; }
            public StatefulWorldItem BackupState { get; }
            public string RequestedNext { get; private set; }
            public string CompletedOutcome { get; private set; }

            public void ResetObservedRoutes()
            {
                RequestedNext = null;
                CompletedOutcome = null;
            }

            public void Dispose()
            {
                Adapter.NextOutcomeRequested -= OnNextOutcome;
                Adapter.OutcomeCompletionRequested -= OnCompleted;
                Object.Destroy(Root);
            }

            private GameObject CreateItem(
                string id,
                out InteractableEntity entity,
                out StatefulWorldItem stateful,
                ItemPhysicalState initialState = ItemPhysicalState.Placed)
            {
                GameObject item = new GameObject(id);
                item.transform.SetParent(Root.transform);
                item.AddComponent<BoxCollider>();
                Rigidbody body = item.AddComponent<Rigidbody>();
                body.isKinematic = true;
                entity = item.AddComponent<InteractableEntity>();
                entity.Configure(null, id);
                stateful = item.AddComponent<StatefulWorldItem>();
                stateful.Configure(body, initialState);
                stateful.CaptureInitialState();
                item.AddComponent<PickupCapability>();
                item.SetActive(false);
                return item;
            }

            private void OnNextOutcome(string outcomeId, int loopEpoch, string nextOutcomeId)
            {
                RequestedNext = nextOutcomeId;
            }

            private void OnCompleted(string outcomeId, int loopEpoch)
            {
                CompletedOutcome = outcomeId;
            }
        }
    }

    public sealed class SynchronousSwitchPerformancePlayer : MonoBehaviour, IPerformancePlayer
    {
        public KillerDirector Director { get; set; }
        public bool SwitchOnD08 { get; set; }

        public bool Play(CounterplayDefinitionAsset definition, System.Action completed)
        {
            if (SwitchOnD08 && definition != null && definition.OutcomeId == "D08")
            {
                SwitchOnD08 = false;
                Director.TryStartOutcome("D22");
            }

            return true;
        }

        public void CancelAllForLoop(int loopEpoch)
        {
        }
    }
}
