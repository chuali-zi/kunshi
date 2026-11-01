using System.Collections;
using System.Collections.Generic;
using ConGame.Domain.Flow;
using ConGame.Runtime.AI;
using ConGame.Runtime.Events;
using ConGame.Runtime.Facts;
using ConGame.Runtime.Flow;
using ConGame.Runtime.Input;
using ConGame.Runtime.Loop;
using ConGame.Runtime.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ConGame.Tests.PlayMode
{
    public sealed class KillerHuntFlowPlayModeTests
    {
        [UnityTest]
        public IEnumerator ClearShotUsesBreachHuntThenCounterplayImpactAndDelayedReset()
        {
            GameObject root = new GameObject("hunt-flow");
            GameObject player = new GameObject("player");
            GameObject killerObject = new GameObject("killer");
            GameObject eye = new GameObject("eye");
            try
            {
                player.transform.SetParent(root.transform);
                killerObject.transform.SetParent(root.transform);
                eye.transform.SetParent(killerObject.transform, false);
                player.transform.position = new Vector3(0f, 0f, 3f);
                killerObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                eye.transform.localPosition = new Vector3(0f, 1.55f, 0.15f);
                CapsuleCollider targetCollider = player.AddComponent<CapsuleCollider>();
                targetCollider.height = 2f;
                targetCollider.center = Vector3.up;

                GameplayFactService facts = root.AddComponent<GameplayFactService>();
                facts.CaptureInitialState();
                GameplayEventHub events = root.AddComponent<GameplayEventHub>();
                PlayerInputRouter input = player.AddComponent<PlayerInputRouter>();
                CharacterController controller = player.AddComponent<CharacterController>();
                PlayerMotor motor = player.AddComponent<PlayerMotor>();
                motor.Configure(controller, input, null, facts, null);
                PlayerInteractor interactor = player.AddComponent<PlayerInteractor>();
                interactor.Configure(input, null, player.transform, motor, facts, events, null);

                killerObject.AddComponent<KillerPerceptionSensor>();
                KillerTacticalController hunt = killerObject.AddComponent<KillerTacticalController>();
                hunt.Configure(null, player.transform, eye.transform, facts, events, null,
                    new Transform[0]);
                KillerDirector killer = killerObject.AddComponent<KillerDirector>();
                killer.Configure(null, player.transform, facts, null, null);
                killer.ConfigureHunt(hunt);

                GameFlowController flow = root.AddComponent<GameFlowController>();
                LoopResetRegistry registry = root.AddComponent<LoopResetRegistry>();
                registry.Configure(root.transform);
                LoopDirector loop = root.AddComponent<LoopDirector>();
                loop.Configure(null, flow, registry, facts, events, input, motor, killer);
                registry.CaptureInitialState();
                loop.Initialize();

                List<GamePhase> phases = new List<GamePhase>();
                float killImpactAt = -1f;
                float resetAt = -1f;
                flow.PhaseChanged += (_, current, __) =>
                {
                    phases.Add(current);
                    if (current == GamePhase.KillImpact)
                    {
                        killImpactAt = Time.realtimeSinceStartup;
                    }
                };
                int shots = 0;
                killer.ShotFired += (_, __) => shots++;
                bool resetCompleted = false;
                loop.LoopResetCompleted += (_, __) =>
                {
                    resetAt = Time.realtimeSinceStartup;
                    resetCompleted = true;
                };

                Assert.That(flow.Request(GamePhase.Breach, TransitionPriority.Normal, "hunt test"), Is.True);
                Assert.That(flow.CommitPending(), Is.True);
                Assert.That(flow.CurrentPhase, Is.EqualTo(GamePhase.Breach));
                Assert.That(interactor.ControlMode, Is.EqualTo(PlayerControlMode.Free));
                Assert.That(interactor.IsMovementLocked, Is.False);

                float timeout = Time.time + 6f;
                while (!resetCompleted && Time.time < timeout)
                {
                    yield return null;
                }

                Assert.That(resetCompleted, Is.True);
                Assert.That(shots, Is.EqualTo(1));
                Assert.That(phases, Does.Contain(GamePhase.Counterplay));
                Assert.That(phases, Does.Contain(GamePhase.KillImpact));
                Assert.That(phases, Does.Contain(GamePhase.LoopReset));
                Assert.That(killImpactAt, Is.GreaterThanOrEqualTo(0f));
                Assert.That(resetAt - killImpactAt, Is.GreaterThanOrEqualTo(0.75f),
                    "HitFall/KillImpact must remain readable before reset begins.");
                Assert.That(flow.CurrentPhase, Is.EqualTo(GamePhase.Awakening));
                Assert.That(killer.State, Is.EqualTo(KillerState.Dormant));
                Assert.That(hunt.Mode, Is.EqualTo(KillerTacticalMode.Inactive));
            }
            finally
            {
                Object.Destroy(root);
            }
        }
    }
}