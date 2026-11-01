using System.Collections;
using ConGame.Bootstrap;
using ConGame.Domain.Persistence;
using ConGame.Runtime.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ConGame.Tests.PlayMode
{
    public sealed class MenuAndPausePlayModeTests
    {
        [UnityTest]
        public IEnumerator PauseKeepsSystemInputAliveAndRestoresRuntimeState()
        {
            float initialTimeScale = Time.timeScale;
            bool initialAudioPause = AudioListener.pause;
            GameObject root = new GameObject("Pause Test");
            PlayerInputRouter input = root.AddComponent<PlayerInputRouter>();
            PauseMenuController pause = root.AddComponent<PauseMenuController>();
            pause.Configure(input);

            yield return null;
            Assert.That(input.GameplayEnabled, Is.True);
            Assert.That(input.SystemInputEnabled, Is.True);

            pause.Pause();

            Assert.That(pause.IsPaused, Is.True);
            Assert.That(Time.timeScale, Is.EqualTo(0f));
            Assert.That(AudioListener.pause, Is.True);
            Assert.That(input.GameplayEnabled, Is.False);
            Assert.That(input.SystemInputEnabled, Is.True);

            pause.Resume();

            Assert.That(pause.IsPaused, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(initialTimeScale));
            Assert.That(AudioListener.pause, Is.EqualTo(initialAudioPause));
            Assert.That(input.GameplayEnabled, Is.True);

            Object.Destroy(root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PauseRestoresLookOnlyModeWithoutEnablingGameplayActions()
        {
            GameObject root = new GameObject("Look Only Pause Test");
            PlayerInputRouter input = root.AddComponent<PlayerInputRouter>();
            PauseMenuController pause = root.AddComponent<PauseMenuController>();
            pause.Configure(input);

            yield return null;
            input.SetLookOnlyEnabled(true);
            pause.Pause();
            pause.Resume();

            Assert.That(input.InputMode, Is.EqualTo(GameplayInputMode.LookOnly));
            Assert.That(input.GameplayEnabled, Is.True);

            Object.Destroy(root);
            yield return null;
        }

        [Test]
        public void ExistingOrFinishedProgressRequiresExplicitNewGameConfirmation()
        {
            PersistentProgressV1 fresh = PersistentProgressV1.CreateNew();
            Assert.That(SimpleMainMenu.RequiresNewGameConfirmation(fresh), Is.False);
            Assert.That(SimpleMainMenu.CanContinueProgress(fresh), Is.False);
            Assert.That(SimpleMainMenu.CanContinueProgress(new PersistentProgressV1 { loopIndex = 2 }), Is.True);
            Assert.That(SimpleMainMenu.CanContinueProgress(
                new PersistentProgressV1 { loopIndex = 4, endingCompleted = true }), Is.False);
            Assert.That(SimpleMainMenu.RequiresNewGameConfirmation(new PersistentProgressV1 { loopIndex = 2 }), Is.True);
            Assert.That(SimpleMainMenu.RequiresNewGameConfirmation(
                new PersistentProgressV1 { endingCompleted = true }), Is.True);
        }
    }
}
