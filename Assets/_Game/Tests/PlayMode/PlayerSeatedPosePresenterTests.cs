using System.Collections;
using System.IO;
using ConGame.Bootstrap;
using ConGame.Domain.Facts;
using ConGame.Domain.Flow;
using ConGame.Presentation.Animation;
using ConGame.Presentation.Camera;
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
        public IEnumerator MainShotKeepsIdlePlayerGroundedAndCutawayOnlyInFixedView()
        {
            CaptureSaveFiles();
            yield return LoadFreshRoomCopy();
            yield return null;
            yield return null;

            Camera camera = FindInRoom<Camera>();
            camera.aspect = 16f / 9f; // The deliverable shot is 1920 x 1080; Test Runner uses its own Game View size.
            Vector3 cameraPosition = camera.transform.position;
            Quaternion cameraRotation = camera.transform.rotation;
            yield return new WaitForSeconds(4.5f);

            var facts = FindInRoom<GameplayFactService>();
            var motor = FindInRoom<PlayerMotor>();
            var presenter = FindInRoom<PlayerSeatedPosePresenter>();
            var chair = FindInRoomTransform("FUR_WoodChair");
            AssertSeatedAtChair(facts, motor, presenter, chair);
            var animator = motor.GetComponentInChildren<Animator>();
            Assert.That(animator.GetBoneTransform(HumanBodyBones.Hips).position.y,
                Is.InRange(.58f, .7f), "The hips should meet the raised wooden seat.");
            foreach (var foot in new[] { HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot })
                Assert.That(animator.GetBoneTransform(foot).position.y, Is.InRange(.09f, .17f),
                    "The seated feet must meet the floor rather than hang above it.");
            AssertSolesOnFloor(motor);
            Assert.That(Vector3.Dot(motor.transform.forward, cameraPosition - motor.transform.position), Is.GreaterThan(0),
                "The seated man should face the audience.");
            Assert.That(Vector3.Distance(camera.transform.position, cameraPosition), Is.LessThan(.001f));
            Assert.That(Quaternion.Angle(camera.transform.rotation, cameraRotation), Is.LessThan(.01f));
            foreach (var point in new[] {
                new Vector3(-3f, 2.6f, .9f), new Vector3(3.1f, 1.18f, 1.9f),
                new Vector3(0, 3.1f, 2.595f), new Vector3(0, 0, -.25f) })
            {
                var viewport = camera.WorldToViewportPoint(point);
                Assert.That(viewport.x, Is.InRange(0f, 1f), "The stage edges must fit horizontally.");
                Assert.That(viewport.y, Is.InRange(0f, 1f), "The ceiling and foreground floor must fit vertically.");
                Assert.That(viewport.z, Is.GreaterThan(0));
            }

            // The distant stage lens must not displace the playable ending entry beyond the front wall.
            Assert.That(FindInRoomTransform("EndingObserverEntry").position.z, Is.GreaterThan(-2.6f));
            int cutawayMask = 1 << LayerMask.NameToLayer("MainShotCutaway");
            Assert.That(camera.cullingMask & cutawayMask, Is.Zero);
            var mode = FindInRoom<CameraModeController>();
            mode.SwitchToFirstPerson();
            Assert.That(camera.cullingMask & cutawayMask, Is.Not.Zero,
                "First person must restore the front wall and television.");
            mode.SwitchToFixed();
            Assert.That(camera.cullingMask & cutawayMask, Is.Zero);

            // Exercise the pose transition separately from device injection (covered by the input integration test).
            presenter.SendMessage("OnMeaningfulAction", SendMessageOptions.RequireReceiver);
            yield return new WaitForSeconds(.5f);
            Assert.That(presenter.IsAwakeningSeated, Is.False);
            AssertSolesOnFloor(motor);
        }

        private static void AssertSolesOnFloor(PlayerMotor motor)
        {
            foreach (var skin in motor.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var mesh = new Mesh();
                skin.BakeMesh(mesh, true);
                float soleY = float.PositiveInfinity;
                foreach (var vertex in mesh.vertices)
                    soleY = Mathf.Min(soleY, skin.transform.TransformPoint(vertex).y);
                Object.Destroy(mesh);
                Assert.That(soleY, Is.InRange(-.02f, .03f), "The actual shoe soles must meet the carpet.");
            }
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
