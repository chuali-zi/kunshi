using System.Collections;
using ConGame.Domain.Facts;
using ConGame.Domain.Flow;
using ConGame.Domain.Interaction;
using ConGame.Runtime.Ending;
using ConGame.Runtime.Events;
using ConGame.Runtime.Facts;
using ConGame.Runtime.Flow;
using ConGame.Runtime.Interaction;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ConGame.Tests.PlayMode
{

    public sealed class TelevisionFlowPlayModeTests
    {
        [UnityTest]
        public IEnumerator SecondHeavyHitBeatsDeathRequest()
        {
            GameObject root = new GameObject("TV Test");
            GameplayFactService facts = root.AddComponent<GameplayFactService>();
            GameplayEventHub events = root.AddComponent<GameplayEventHub>();
            GameFlowController flow = root.AddComponent<GameFlowController>();
            TelevisionController television = root.AddComponent<TelevisionController>();

            facts.CaptureInitialState();
            flow.Initialize(GamePhase.FreeRoam);
            television.Configure(facts, events, flow);
            television.CaptureInitialState();

            HitInfo hit = new HitInfo(ImpactKind.Heavy, "baseball_bat", Vector3.zero, Vector3.forward, 6f);
            Assert.That(television.ReceiveHit(hit), Is.EqualTo(HitResult.Accepted));
            Assert.That(television.DamageState, Is.EqualTo(TelevisionDamageState.Cracked));

            flow.Request(GamePhase.KillImpact, TransitionPriority.Death, "shot");
            Assert.That(television.ReceiveHit(hit), Is.EqualTo(HitResult.EndingTriggered));
            flow.CommitPending();

            Assert.That(flow.CurrentPhase, Is.EqualTo(GamePhase.EndingE1));
            Object.Destroy(root);
            yield return null;
        }
    }
}
