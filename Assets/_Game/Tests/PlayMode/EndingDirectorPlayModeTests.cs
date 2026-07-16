using System.Collections;
using ConGame.Bootstrap;
using ConGame.Domain.Flow;
using ConGame.Runtime.Flow;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ConGame.Tests.PlayMode
{
    public sealed class EndingDirectorPlayModeTests
    {
        [UnityTest]
        public IEnumerator EndingE1AdvancesToE2AfterWhiteNoiseBeat()
        {
            GameObject root = new GameObject("ending_root");
            try
            {
                GameFlowController flow = root.AddComponent<GameFlowController>();
                flow.Initialize(GamePhase.FreeRoam);
                EndingDirector ending = root.AddComponent<EndingDirector>();
                ending.enabled = false;
                ending.Configure(flow, null, null, null, null, null, null, null, null, null, string.Empty);
                ending.enabled = true;

                Assert.That(flow.Request(GamePhase.EndingE1, TransitionPriority.EndingCommitted, "tv"), Is.True);
                Assert.That(flow.CommitPending(), Is.True);
                Assert.That(ending.IsRunning, Is.True);

                yield return new WaitForSeconds(2.2f);
                Assert.That(flow.CurrentPhase, Is.EqualTo(GamePhase.EndingE2));
            }
            finally
            {
                Object.Destroy(root);
            }
        }
    }
}
