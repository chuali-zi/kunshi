using System;
using System.Collections;
using ConGame.Bootstrap;
using ConGame.Domain.Facts;
using ConGame.Domain.Flow;
using ConGame.Runtime.AI;
using ConGame.Runtime.Ending;
using ConGame.Runtime.Facts;
using ConGame.Runtime.Flow;
using ConGame.Runtime.Input;
using ConGame.Runtime.Loop;
using ConGame.Runtime.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Utils;

namespace ConGame.Tests.PlayMode
{
    public sealed class EndingRepairPlayModeTests
    {
        [UnityTest]
        public IEnumerator PassOneUsesSameEpochResetAndKeepsTelevisionDestroyed()
        {
            GameObject root = new GameObject("ending_reset_test");
            try
            {
                GameFlowController flow = root.AddComponent<GameFlowController>();
                flow.Initialize(GamePhase.FreeRoam);
                LoopDirector loop = root.AddComponent<LoopDirector>();
                LoopResetRegistry registry = root.AddComponent<LoopResetRegistry>();
                registry.Configure(root.transform);

                GameplayFactService facts = root.AddComponent<GameplayFactService>();
                TelevisionController television = root.AddComponent<TelevisionController>();
                television.Configure(facts, null, flow);

                GameObject furniture = new GameObject("furniture");
                furniture.transform.SetParent(root.transform);
                furniture.transform.localPosition = new Vector3(1f, 0f, 0f);
                furniture.AddComponent<TransformResettable>();
                ResetContextProbe resetProbe = furniture.AddComponent<ResetContextProbe>();

                GameObject playerObject = new GameObject("player");
                playerObject.transform.SetParent(root.transform);
                CharacterController playerController = playerObject.AddComponent<CharacterController>();
                PlayerMotor player = playerObject.AddComponent<PlayerMotor>();
                player.Configure(playerController, null, null, facts, null);
                playerObject.transform.position = new Vector3(1f, 0f, 0f);

                EndingDirector ending = CreateEnding(root, flow, loop, registry, null, null, television, player);
                registry.CaptureInitialState();
                furniture.transform.localPosition = new Vector3(9f, 0f, 0f);
                playerObject.transform.position = new Vector3(8f, 0f, 0f);

                BeginEnding(flow);
                yield return WaitFor(() => flow.CurrentPhase == GamePhase.EndingE2, 3f);

                Assert.That(ending.PassResetCount, Is.EqualTo(1));
                Assert.That(loop.LoopEpoch, Is.EqualTo(0), "Ending presets must not advance persistent loop progress.");
                Assert.That(resetProbe.RestoreCount, Is.EqualTo(1));
                Assert.That(resetProbe.PreviousEpoch, Is.EqualTo(resetProbe.NextEpoch));
                Assert.That(
                    furniture.transform.localPosition,
                    Is.EqualTo(new Vector3(1f, 0f, 0f)).Using(Vector3ComparerWithEqualsOperator.Instance));
                Assert.That(television.DamageState, Is.EqualTo(TelevisionDamageState.Destroyed));
                Assert.That(facts.CaptureSnapshot().TelevisionDamage, Is.EqualTo(TelevisionDamageState.Destroyed));
                Assert.That(playerObject.transform.position.x, Is.EqualTo(8f).Within(0.01f),
                    "Pass1 restores the room but must preserve the player's television-side position.");
            }
            finally
            {
                UnityEngine.Object.Destroy(root);
            }
        }

        [UnityTest]
        public IEnumerator E2LeavesFourFullSecondsBeforeKillerEntry()
        {
            GameObject root = new GameObject("ending_delay_test");
            try
            {
                GameFlowController flow = root.AddComponent<GameFlowController>();
                flow.Initialize(GamePhase.FreeRoam);
                LoopResetRegistry registry = root.AddComponent<LoopResetRegistry>();
                registry.Configure(root.transform);
                PlayerInputRouter input = root.AddComponent<PlayerInputRouter>();

                GameObject killerObject = new GameObject("killer");
                killerObject.transform.SetParent(root.transform);
                KillerDirector killer = killerObject.AddComponent<KillerDirector>();
                killer.Configure(null, null, null, null, null);

                EndingDirector ending = CreateEnding(root, flow, null, registry, input, killer, null);
                registry.CaptureInitialState();
                BeginEnding(flow);
                yield return WaitFor(() => flow.CurrentPhase == GamePhase.EndingE2, 3f);

                Assert.That(input.GameplayEnabled, Is.True);
                Assert.That(ending.E2EntryStarted, Is.False);
                Assert.That(killer.State, Is.EqualTo(KillerState.Dormant));

                yield return new WaitForSeconds(3.8f);
                Assert.That(ending.E2EntryStarted, Is.False, "Killer entered before four seconds of free control elapsed.");
                Assert.That(killer.State, Is.EqualTo(KillerState.Dormant));

                yield return WaitFor(() => ending.E2EntryStarted, 0.5f);
                yield return WaitFor(() => !string.IsNullOrEmpty(killer.SelectedOutcomeId), 2.2f);
                Assert.That(killer.State, Is.Not.EqualTo(KillerState.Dormant));
                Assert.That(killer.SelectedOutcomeId, Is.EqualTo("D01"));
            }
            finally
            {
                UnityEngine.Object.Destroy(root);
            }
        }

        [UnityTest]
        public IEnumerator E3EnablesOnlyLookAndKeepsSystemPauseAvailable()
        {
            GameObject root = new GameObject("ending_input_test");
            try
            {
                GameFlowController flow = root.AddComponent<GameFlowController>();
                flow.Initialize(GamePhase.FreeRoam);
                LoopResetRegistry registry = root.AddComponent<LoopResetRegistry>();
                registry.Configure(root.transform);
                PlayerInputRouter input = root.AddComponent<PlayerInputRouter>();

                GameplayFactService facts = root.AddComponent<GameplayFactService>();
                TelevisionController television = root.AddComponent<TelevisionController>();
                television.Configure(facts, null, flow);

                EndingDirector ending = CreateEnding(root, flow, null, registry, input, null, television);
                registry.CaptureInitialState();
                BeginEnding(flow);
                yield return WaitFor(() => flow.CurrentPhase == GamePhase.EndingE3, 11f);

                Assert.That(ending.PassResetCount, Is.EqualTo(2));
                Assert.That(television.DamageState, Is.EqualTo(TelevisionDamageState.Destroyed));
                Assert.That(input.GameplayEnabled, Is.True);
                Assert.That(input.InputMode, Is.EqualTo(GameplayInputMode.LookOnly));
                Assert.That(input.SystemInputEnabled, Is.True);
                Assert.That(ending.LookOnlyInputRequested, Is.True);
            }
            finally
            {
                UnityEngine.Object.Destroy(root);
            }
        }

        private static EndingDirector CreateEnding(
            GameObject root,
            GameFlowController flow,
            LoopDirector loop,
            LoopResetRegistry registry,
            PlayerInputRouter input,
            KillerDirector killer,
            TelevisionController television,
            PlayerMotor player = null)
        {
            EndingDirector ending = root.AddComponent<EndingDirector>();
            ending.enabled = false;
            ending.Configure(
                flow,
                loop,
                registry,
                input,
                player,
                killer,
                television,
                null,
                null,
                null,
                string.Empty);
            ending.enabled = true;
            return ending;
        }

        private static void BeginEnding(GameFlowController flow)
        {
            Assert.That(flow.Request(GamePhase.EndingE1, TransitionPriority.EndingCommitted, "test television hit"), Is.True);
            Assert.That(flow.CommitPending(), Is.True);
        }

        private static IEnumerator WaitFor(Func<bool> predicate, float timeout)
        {
            float elapsed = 0f;
            while (!predicate() && elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            Assert.That(predicate(), Is.True, $"Condition was not met within {timeout:0.0}s.");
        }

        public sealed class ResetContextProbe : MonoBehaviour, ILoopResettable
        {
            public int ResetOrder => 101;
            public int RestoreCount { get; private set; }
            public int PreviousEpoch { get; private set; }
            public int NextEpoch { get; private set; }

            public void CaptureInitialState()
            {
            }

            public void Quiesce(in LoopResetContext context)
            {
            }

            public void Restore(in LoopResetContext context)
            {
                RestoreCount++;
                PreviousEpoch = context.PreviousEpoch;
                NextEpoch = context.NextEpoch;
            }

            public void Resume(in LoopResetContext context)
            {
            }
        }
    }
}
