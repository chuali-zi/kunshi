using System.Collections;
using System.IO;
using ConGame.Bootstrap;
using ConGame.Domain.Facts;
using ConGame.Domain.Flow;
using ConGame.Presentation.Animation;
using ConGame.Runtime.Facts;
using ConGame.Runtime.Flow;
using ConGame.Runtime.Input;
using ConGame.Runtime.Loop;
using ConGame.Runtime.Persistence;
using ConGame.Runtime.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ConGame.Tests.PlayMode
{
    public sealed class PlayerSeatedPosePresenterTests
    {
        private const string RoomScenePath = "Assets/_Game/Scenes/Room_M1.unity";

        private Scene _room;
        private Keyboard _keyboard;
        private string[] _savePaths;
        private byte[][] _savedFileContents;

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            RestoreSaveFiles();
            if (_keyboard != null)
            {
                InputSystem.RemoveDevice(_keyboard);
                _keyboard = null;
            }

            if (_room.IsValid() && _room.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(_room);
            }

            _room = default;
        }

        [UnityTest]
        public IEnumerator RoomOpensSeatedWaitsWithoutInputThenStandsOnFirstActionAndResetsToChair()
        {
            CaptureSaveFiles();
            yield return LoadFreshRoomCopy();
            yield return null;
            yield return null;

            LoopDirector loop = FindInRoom<LoopDirector>();
            GameFlowController flow = FindInRoom<GameFlowController>();
            GameplayFactService facts = FindInRoom<GameplayFactService>();
            PlayerMotor motor = FindInRoom<PlayerMotor>();
            PlayerInputRouter input = FindInRoom<PlayerInputRouter>();
            PlayerSeatedPosePresenter presenter = FindInRoom<PlayerSeatedPosePresenter>();
            Transform chair = FindInRoomTransform("FUR_WoodChair");
            CharacterController characterController = motor.GetComponent<CharacterController>();

            Assert.That(loop, Is.Not.Null);
            Assert.That(flow, Is.Not.Null);
            Assert.That(facts, Is.Not.Null);
            Assert.That(input, Is.Not.Null);
            Assert.That(presenter, Is.Not.Null);
            Assert.That(chair, Is.Not.Null);
            AssertSeatedAtChair(facts, motor, presenter, chair);

            float noInputElapsed = 0f;
            while (noInputElapsed < 4.5f)
            {
                noInputElapsed += Time.deltaTime;
                yield return null;
            }

            Assert.That(flow.CurrentPhase, Is.EqualTo(GamePhase.FreeRoam),
                "The no-input awakening should finish on its authored timer.");
            AssertSeatedAtChair(facts, motor, presenter, chair);

            _keyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.E));
            InputSystem.Update();
            yield return null;
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            InputSystem.Update();
            yield return null;

            Assert.That(facts.CaptureSnapshot().HasMeaningfulInput, Is.True,
                "The first E press should reach the normal gameplay input route.");
            Assert.That(facts.CaptureSnapshot().PlayerPose, Is.Not.EqualTo(PlayerPose.Sitting));
            Assert.That(presenter.IsAwakeningSeated, Is.False);
            Assert.That(motor.transform.position.x, Is.EqualTo(presenter.StandingPosition.x).Within(0.01f));
            Assert.That(motor.transform.position.y, Is.EqualTo(presenter.StandingPosition.y).Within(0.02f));
            Assert.That(motor.transform.position.z, Is.EqualTo(presenter.StandingPosition.z).Within(0.01f));
            Assert.That(characterController.enabled, Is.True);

            yield return new WaitForSeconds(0.3f);

            flow = FindInRoom<GameFlowController>();
            if (flow.CurrentPhase == GamePhase.FreeRoam)
            {
                Assert.That(flow.Request(GamePhase.Breach, TransitionPriority.Normal, "seated pose reset test"), Is.True);
                Assert.That(flow.CommitPending(), Is.True);
            }

            if (flow.CurrentPhase == GamePhase.Breach)
            {
                Assert.That(flow.Request(GamePhase.Counterplay, TransitionPriority.Normal, "seated pose reset test"), Is.True);
                Assert.That(flow.CommitPending(), Is.True);
            }

            Assert.That(flow.CurrentPhase, Is.EqualTo(GamePhase.Counterplay));
            int previousEpoch = loop.LoopEpoch;
            loop.RequestKill("D01");
            Assert.That(flow.CommitPending(), Is.True);

            float resetElapsed = 0f;
            while (loop.LoopEpoch == previousEpoch && resetElapsed < 3f)
            {
                resetElapsed += Time.deltaTime;
                yield return null;
            }

            Assert.That(loop.LoopEpoch, Is.GreaterThan(previousEpoch),
                "A completed counterplay result should run the real loop reset.");
            AssertSeatedAtChair(facts, motor, presenter, chair);
            Assert.That(characterController.enabled, Is.False);
        }

        private IEnumerator LoadFreshRoomCopy()
        {
            Scene loaded = default;
            UnityEngine.Events.UnityAction<Scene, LoadSceneMode> onSceneLoaded = (scene, mode) =>
            {
                if (scene.path == RoomScenePath)
                {
                    loaded = scene;
                }
            };

            SceneManager.sceneLoaded += onSceneLoaded;
            yield return SceneManager.LoadSceneAsync(RoomScenePath, LoadSceneMode.Additive);
            SceneManager.sceneLoaded -= onSceneLoaded;
            _room = loaded;
            Assert.That(_room.IsValid() && _room.isLoaded, Is.True,
                "The test should exercise a fresh production Room_M1 instance.");
        }

        private T FindInRoom<T>() where T : Component
        {
            foreach (GameObject root in _room.GetRootGameObjects())
            {
                T result = root.GetComponentInChildren<T>(true);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        private Transform FindInRoomTransform(string objectName)
        {
            foreach (GameObject root in _room.GetRootGameObjects())
            {
                foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
                {
                    if (candidate.name == objectName)
                    {
                        return candidate;
                    }
                }
            }

            return null;
        }

        private static void AssertSeatedAtChair(
            GameplayFactService facts,
            PlayerMotor motor,
            PlayerSeatedPosePresenter presenter,
            Transform chair)
        {
            Assert.That(facts.CaptureSnapshot().PlayerPose, Is.EqualTo(PlayerPose.Sitting));
            Assert.That(presenter.IsAwakeningSeated, Is.True);
            Assert.That(motor.transform.position.x, Is.EqualTo(chair.position.x).Within(0.01f));
            Assert.That(motor.transform.position.y, Is.EqualTo(presenter.StandingPosition.y).Within(0.02f));
            Assert.That(motor.transform.position.z, Is.EqualTo(chair.position.z).Within(0.01f));
            Assert.That(motor.GetComponent<CharacterController>().enabled, Is.False);
        }

        private void CaptureSaveFiles()
        {
            string savePath = new JsonSaveStore().SavePath;
            _savePaths = new[] { savePath, savePath + ".bak", savePath + ".tmp" };
            _savedFileContents = new byte[_savePaths.Length][];
            for (int i = 0; i < _savePaths.Length; i++)
            {
                if (File.Exists(_savePaths[i]))
                {
                    _savedFileContents[i] = File.ReadAllBytes(_savePaths[i]);
                }
            }
        }

        private void RestoreSaveFiles()
        {
            if (_savePaths == null || _savedFileContents == null)
            {
                return;
            }

            for (int i = 0; i < _savePaths.Length; i++)
            {
                if (_savedFileContents[i] == null)
                {
                    if (File.Exists(_savePaths[i]))
                    {
                        File.Delete(_savePaths[i]);
                    }
                }
                else
                {
                    File.WriteAllBytes(_savePaths[i], _savedFileContents[i]);
                }
            }

            _savePaths = null;
            _savedFileContents = null;
        }
    }
}
