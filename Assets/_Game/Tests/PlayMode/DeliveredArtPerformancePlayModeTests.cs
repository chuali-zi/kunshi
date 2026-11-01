using System.Collections;
using ConGame.Domain.Counterplay;
using ConGame.Domain.Interaction;
using ConGame.Presentation.M1;
using ConGame.Runtime.Counterplay;
using ConGame.Runtime.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ConGame.Tests.PlayMode
{
    public sealed class DeliveredArtPerformancePlayModeTests
    {
        [UnityTest]
        public IEnumerator PerformanceDrivesBothAnimatorsFromOnePlayableGraphAndCancelsCleanly()
        {
            GameObject host = new GameObject("performance_host");
            GameObject playerObject = new GameObject("player");
            GameObject killerObject = new GameObject("killer");
            M1GreyboxPlayableAsset performance = ScriptableObject.CreateInstance<M1GreyboxPlayableAsset>();
            CounterplayDefinitionAsset definition = ScriptableObject.CreateInstance<CounterplayDefinitionAsset>();
            AnimationClip playerClip = new AnimationClip();
            AnimationClip killerClip = new AnimationClip();
            try
            {
                PlayerMotor motor = playerObject.AddComponent<PlayerMotor>();
                playerObject.AddComponent<Animator>();
                killerObject.AddComponent<Animator>();
                performance.Configure("D01", M1GreyboxDeathStyle.DefaultShot, 0.5f,
                    PerformanceHeldItemPolicy.Preserve, null, null);
                performance.ConfigureAnimations(playerClip, killerClip);
                definition.Configure("D01", 0, CounterplayTriggerMode.Entry, null,
                    ContentStatus.Implemented, performance);

                M1GreyboxPerformancePlayer player = host.AddComponent<M1GreyboxPerformancePlayer>();
                player.Configure(killerObject.transform, motor, null);
                Assert.That(player.Play(definition, null), Is.True);
                yield return null;

                Assert.That(player.IsPlaying, Is.True);
                Assert.That(player.HasActiveAnimationGraph, Is.True);
                Assert.That(player.ActiveAnimationOutputCount, Is.EqualTo(2));

                player.CancelAllForLoop(0);
                yield return null;
                Assert.That(player.IsPlaying, Is.False);
                Assert.That(player.HasActiveAnimationGraph, Is.False);

                Assert.That(player.Play(definition, null), Is.True);
                Assert.That(player.HasActiveAnimationGraph, Is.True);
                host.SetActive(false);
                yield return null;
                Assert.That(player.IsPlaying, Is.False);
                Assert.That(player.HasActiveAnimationGraph, Is.False,
                    "Disabling or unloading the host must release its playable graph without a loop reset.");
            }
            finally
            {
                Object.Destroy(host);
                Object.Destroy(playerObject);
                Object.Destroy(killerObject);
                Object.Destroy(performance);
                Object.Destroy(definition);
                Object.Destroy(playerClip);
                Object.Destroy(killerClip);
            }
        }
    }
}
