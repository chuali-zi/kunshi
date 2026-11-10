using System.Collections;
using System.IO;
using ConGame.Bootstrap;
using ConGame.Domain.Facts;
using ConGame.Domain.Flow;
using ConGame.Domain.Interaction;
using ConGame.Presentation.Animation;
using ConGame.Presentation.Camera;
using ConGame.Presentation.Choreography;
using ConGame.Presentation.CRT;
using ConGame.Runtime.AI;
using ConGame.Runtime.Ending;
using ConGame.Runtime.Facts;
using ConGame.Runtime.Flow;
using ConGame.Runtime.Input;
using ConGame.Runtime.Interaction;
using ConGame.Runtime.Lighting;
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
    public sealed class PlayerSeatedPosePresenterTests : ProductionInputTestFixture
    {
        private const string RoomScenePath = "Assets/_Game/Scenes/Room_M1.unity";

        private Scene _room;
        private Keyboard _keyboard;
        private Mouse _mouse;
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

            if (_mouse != null)
            {
                InputSystem.RemoveDevice(_mouse);
                _mouse = null;
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
        public IEnumerator KeyboardWalksFromSeatedStartToBlanketAndLampThenRunsNaturalDeathAndReset()
        {
            CaptureSaveFiles();
            yield return LoadFreshRoomCopy();
            yield return null;
            yield return null;
            var motor = FindInRoom<PlayerMotor>();
            var player = FindInRoom<PlayerInteractor>();
            var presenter = FindInRoom<PlayerSeatedPosePresenter>();
            var facts = FindInRoom<GameplayFactService>();
            Assert.That(presenter.IsAwakeningSeated, Is.True);
            _keyboard = InputSystem.AddDevice<Keyboard>();

            // The first movement key must both stand the player and move them.
            Vector3 stand = presenter.StandingPosition;
            yield return HoldKeys(.25f, Key.S);
            Assert.That(presenter.IsAwakeningSeated, Is.False);
            Assert.That(facts.CaptureSnapshot().HasMeaningfulInput, Is.True);
            Assert.That(motor.transform.position.z, Is.LessThan(stand.z - .1f));
            yield return WalkTo(motor, new Vector3(0, 0, .2f));
            yield return WalkTo(motor, new Vector3(1.05f, 0, .2f));
            yield return WalkTo(motor, new Vector3(1.05f, 0, .65f));
            yield return HoldKeys(.5f, Key.E);
            Assert.That(player.HeldEntity, Is.Not.Null, "Walking to the bed must expose the real blanket.");
            Assert.That(player.HeldEntity.ItemId, Is.EqualTo("blanket"));

            // Put it down normally, then walk across the clear foreground to the lamp.
            yield return HoldKeys(.5f, Key.E);
            Assert.That(player.HeldEntity, Is.Null);
            yield return WalkTo(motor, new Vector3(1.05f, 0, .1f));
            yield return WalkTo(motor, new Vector3(-2.34f, 0, .1f));
            yield return WalkTo(motor, new Vector3(-2.34f, 0, .8f));
            var lamp = FindInRoomTransform("PRP_TableLamp").GetComponent<BreakableLight>();
            Assert.That(lamp.IsSwitchOn, Is.True);
            yield return HoldKeys(.1f, Key.E);
            Assert.That(lamp.IsSwitchOn, Is.False, "A short E press must use the lamp after walking to it.");
            Assert.That(player.HeldEntity, Is.Null, "A short lamp use must not pick it up.");
            // The loose bat can support the capsule by the lamp. Return to clear
            // floor before checking floor height, using movement and normal gravity.
            yield return WalkTo(motor, new Vector3(-2.34f, 0, .1f));
            yield return new WaitForSeconds(.4f);
            Assert.That(motor.transform.position.y, Is.InRange(-.01f, .1f));

            // Stay exposed and let the real 30-second entry clock, live hunt,
            // committed shot and reset run without phase/fact/kill shortcuts.
            yield return WalkTo(motor, new Vector3(0, 0, .1f));
            var flow = FindInRoom<GameFlowController>();
            var loop = FindInRoom<LoopDirector>();
            var camera = FindInRoom<Camera>();
            Vector3 cameraPosition = camera.transform.position;
            Quaternion cameraRotation = camera.transform.rotation;
            int epoch = loop.LoopEpoch;
            var sceneHandle = _room.handle;
            bool sawBreach = false, sawCounterplay = false, sawImpact = false, sawReset = false;
            float impactAt = -1f;
            flow.PhaseChanged += (_, phase, __) =>
            {
                if (phase == GamePhase.Breach) sawBreach = true;
                if (phase == GamePhase.Counterplay) sawCounterplay = true;
                if (phase == GamePhase.KillImpact) { sawImpact = true; impactAt = Time.time; }
                if (phase == GamePhase.LoopReset) sawReset = true;
            };
            float timeout = Time.time + 45f;
            while (loop.LoopEpoch == epoch && Time.time < timeout) yield return null;
            Assert.That(loop.LoopEpoch, Is.EqualTo(epoch + 1), "Natural entry and live hunt must finish the loop.");
            Assert.That(sawBreach && sawCounterplay && sawImpact && sawReset, Is.True);
            Assert.That(Time.time - impactAt, Is.GreaterThanOrEqualTo(.75f), "The shot impact must remain readable.");
            Assert.That(flow.CurrentPhase, Is.EqualTo(GamePhase.Awakening));
            Assert.That(_room.isLoaded, Is.True);
            Assert.That(_room.handle, Is.EqualTo(sceneHandle), "Death must reset the existing room without reloading.");
            Assert.That(motor.gameObject.scene.handle, Is.EqualTo(sceneHandle));
            AssertSeatedAtChair(facts, motor, presenter, FindInRoomTransform("FUR_WoodChair"));
            Assert.That(motor.GetComponent<CharacterController>().enabled, Is.False);
            Assert.That(lamp.IsSwitchOn, Is.True);
            Assert.That(player.HeldEntity, Is.Null);
            Assert.That(camera.transform.position, Is.EqualTo(cameraPosition));
            Assert.That(Quaternion.Angle(camera.transform.rotation, cameraRotation), Is.LessThan(.01f));
            yield return HoldKeys(.25f, Key.S);
            Assert.That(presenter.IsAwakeningSeated, Is.False, "The next loop must accept the same keyboard again.");
            Assert.That(motor.transform.position.z, Is.LessThan(presenter.StandingPosition.z - .1f));
        }

        [UnityTest]
        public IEnumerator KeyboardAndMouseReachTelevisionEndingFromSeatedStart()
        {
            CaptureSaveFiles();
            yield return LoadFreshRoomCopy();
            yield return null;
            yield return null;
            var motor = FindInRoom<PlayerMotor>();
            var player = FindInRoom<PlayerInteractor>();
            var television = FindInRoom<TelevisionController>();
            var flow = FindInRoom<GameFlowController>();
            var loop = FindInRoom<LoopDirector>();
            var ending = FindInRoom<EndingDirector>();
            var cameraMode = FindInRoom<CameraModeController>();
            var input = FindInRoom<PlayerInputRouter>();
            var clockHand = FindInRoomTransform("PRP_WallClock").Find("SecondHand");
            Assert.That(clockHand, Is.Not.Null);
            Quaternion clockMark = clockHand.localRotation;
            int epoch = loop.LoopEpoch;
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _mouse = InputSystem.AddDevice<Mouse>();

            // Every approach, pickup and hit uses ordinary production input.
            yield return WalkTo(motor, new Vector3(0, 0, .1f));
            yield return WalkTo(motor, new Vector3(-2.3f, 0, .1f));
            yield return WalkTo(motor, new Vector3(-2.3f, 0, .65f));
            yield return HoldKeys(.5f, Key.E);
            yield return new WaitForSeconds(.4f);
            Assert.That(player.HeldEntity, Is.Not.Null, "The room's bat must be reachable on foot.");
            Assert.That(player.HeldEntity.ItemId, Is.EqualTo("baseball_bat"));
            yield return WalkTo(motor, new Vector3(-2.3f, 0, .1f));
            yield return WalkTo(motor, new Vector3(0, 0, .1f));
            yield return WalkTo(motor, new Vector3(0, 0, -1.3f));
            yield return ClickAttack();
            yield return new WaitForSeconds(.9f);
            Assert.That(television.DamageState, Is.EqualTo(TelevisionDamageState.Cracked));
            Assert.That(cameraMode.IsFirstPerson, Is.False);
            yield return ClickAttack();

            float until = Time.time + 4f;
            while (flow.CurrentPhase != GamePhase.EndingE2 && Time.time < until) yield return null;
            Assert.That(flow.CurrentPhase, Is.EqualTo(GamePhase.EndingE2));
            Assert.That(television.DamageState, Is.EqualTo(TelevisionDamageState.Destroyed));
            Assert.That(cameraMode.IsFirstPerson, Is.True);
            Assert.That(ending.HasObserverHandoff, Is.True);
            Assert.That(ending.E2EntryStarted, Is.False);
            Assert.That(input.GameplayEnabled, Is.True);
            Vector3 observerStart = motor.transform.position;
            yield return HoldKeys(.2f, Key.W);
            Assert.That(Vector3.Distance(observerStart, motor.transform.position), Is.GreaterThan(.1f),
                "The observer must gain real movement control after the breakout.");

            until = Time.time + 25f;
            while (flow.CurrentPhase != GamePhase.EndingE3 && Time.time < until) yield return null;
            Assert.That(flow.CurrentPhase, Is.EqualTo(GamePhase.EndingE3));
            Assert.That(input.InputMode, Is.EqualTo(GameplayInputMode.LookOnly));
            Assert.That(ending.PassResetCount, Is.EqualTo(2));
            Vector3 seated = motor.transform.position;
            yield return HoldKeys(.2f, Key.W);
            Assert.That(motor.transform.position, Is.EqualTo(seated), "E3 must keep the observer seated.");

            until = Time.time + 12f;
            while (!ending.ClockAdvanced && Time.time < until) yield return null;
            Assert.That(ending.ClockAdvanced, Is.True);
            Assert.That(Quaternion.Angle(clockHand.localRotation,
                clockMark * Quaternion.Euler(0, 0, -6)), Is.LessThan(.01f),
                "E3 must release the actual stuck hand, starting with one real second.");
            Assert.That(FindInRoomTransform("EndingSecondHand"), Is.Null);
            until = Time.time + 12f;
            while (flow.CurrentPhase != GamePhase.Finished && Time.time < until) yield return null;
            Assert.That(flow.CurrentPhase, Is.EqualTo(GamePhase.Finished));
            Assert.That(ending.ShotFired, Is.True);
            Assert.That(loop.LoopEpoch, Is.EqualTo(epoch), "The ending must never run an ordinary death reset.");
            var saved = new JsonSaveStore().Load();
            Assert.That(saved.endingCompleted, Is.True);
            Assert.That(SimpleMainMenu.CanContinueProgress(saved), Is.False);
        }

        [UnityTest]
        public IEnumerator KeyboardReachesArmedD07ThenResetsTheHeldBatAndAcceptsNextLoopInput()
        {
            CaptureSaveFiles();
            Transform bat = null;
            Vector3 batStart = default, batReset = default;
            // Capture before Start/physics; a loose prop can settle or be bumped
            // on the approach, which is not the authored reset position.
            yield return LoadFreshRoomCopy(() =>
            {
                bat = FindInRoomTransform("PRP_BaseballBat");
                Assert.That(bat, Is.Not.Null);
                batStart = bat.position;
            });
            yield return null;
            yield return null;
            var motor = FindInRoom<PlayerMotor>();
            var player = FindInRoom<PlayerInteractor>();
            FindInRoom<LoopDirector>().LoopResetCompleted += (_, __) => batReset = bat.position;
            _keyboard = InputSystem.AddDevice<Keyboard>();

            yield return WalkTo(motor, new Vector3(0, 0, .1f));
            yield return WalkTo(motor, new Vector3(-2.3f, 0, .1f));
            yield return WalkTo(motor, new Vector3(-2.3f, 0, .65f));
            yield return HoldKeys(.5f, Key.E);
            yield return new WaitForSeconds(.4f);
            Assert.That(player.HeldEntity, Is.Not.Null);
            Assert.That(player.HeldEntity.ItemId, Is.EqualTo("baseball_bat"));
            yield return WalkTo(motor, new Vector3(-2.3f, 0, .1f));
            yield return WalkTo(motor, new Vector3(0, 0, .1f));

            yield return ObserveNaturalShotAndReset("D07");
            Assert.That(player.HeldEntity, Is.Null);
            Assert.That(Vector3.Distance(batReset, batStart), Is.LessThan(.01f),
                "Death must return the held weapon to its authored position.");
            yield return HoldKeys(.25f, Key.S);
            Assert.That(FindInRoom<PlayerSeatedPosePresenter>().IsAwakeningSeated, Is.False);
        }

        [UnityTest]
        public IEnumerator KeyboardShortStanceReleaseStillHidesAndSecondPressExits()
        {
            CaptureSaveFiles();
            yield return LoadFreshRoomCopy();
            yield return null;
            yield return null;
            var motor = FindInRoom<PlayerMotor>();
            var player = FindInRoom<PlayerInteractor>();
            var facts = FindInRoom<GameplayFactService>();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            yield return WalkTo(motor, new Vector3(0, 0, .1f));
            yield return HoldKeys(.15f, Key.D);
            Assert.That(motor.GetComponent<InteractionScanner>().RefreshForVerb(InteractionVerb.Hide), Is.Not.Null);
            Vector3 approach = motor.transform.position;
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.C));
            yield return new WaitForSeconds(.15f);
            Assert.That(player.IsHiding, Is.False, "A key-down must wait for the tap/hold decision.");
            Assert.That(facts.CaptureSnapshot().PlayerPose, Is.EqualTo(PlayerPose.Standing));
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            yield return null;
            yield return null;
            Assert.That(player.IsHiding, Is.True, "A short release must still enter the nearby bed hide.");
            Assert.That(facts.CaptureSnapshot().PlayerZone, Is.EqualTo(PlayerZone.UnderBed));
            Assert.That(facts.CaptureSnapshot().PlayerPose, Is.EqualTo(PlayerPose.Hidden));
            yield return HoldKeys(.1f, Key.C);
            Assert.That(player.IsHiding, Is.False);
            Assert.That(facts.CaptureSnapshot().PlayerPose, Is.EqualTo(PlayerPose.Standing));
            Assert.That(Vector3.Distance(motor.transform.position, approach), Is.LessThan(.03f));
        }

        [UnityTest]
        public IEnumerator KeyboardSurrendersToEnteredKillerAndNaturallyFinishesD06()
        {
            CaptureSaveFiles();
            yield return LoadFreshRoomCopy();
            yield return null;
            yield return null;
            var motor = FindInRoom<PlayerMotor>();
            var killer = FindInRoom<KillerDirector>();
            var input = FindInRoom<PlayerInputRouter>();
            var facts = FindInRoom<GameplayFactService>();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            yield return WalkTo(motor, new Vector3(0, 0, .1f));
            // Turn toward the right-hand door through ordinary movement.
            yield return HoldKeys(.15f, Key.D);
            float until = Time.time + 35f;
            while ((!killer.HasEnteredRoom || !input.GameplayEnabled) && Time.time < until)
                yield return null;
            Assert.That(killer.HasEnteredRoom && input.GameplayEnabled, Is.True);
            Assert.That(facts.CaptureSnapshot().PlayerPose, Is.EqualTo(PlayerPose.Standing));
            Assert.That(motor.GetComponent<InteractionScanner>().RefreshForVerb(InteractionVerb.Hide),
                Is.Not.Null, "This regression must retain the nearby competing bed hide action.");
            yield return HoldKeys(.75f, Key.C);
            Assert.That(facts.CaptureSnapshot().PlayerPose, Is.EqualTo(PlayerPose.Kneeling),
                "Holding C empty-handed toward the entered killer must surrender.");
            Assert.That(FindInRoom<PlayerInteractor>().HeldEntity, Is.Null);
            yield return ObserveNaturalShotAndReset("D06");
            yield return HoldKeys(.25f, Key.S);
            Assert.That(facts.CaptureSnapshot().PlayerPose, Is.EqualTo(PlayerPose.Standing));
        }

        [UnityTest]
        public IEnumerator KeyboardCrouchesThenPlaysDeadAndNaturallyFinishesD14()
        {
            CaptureSaveFiles();
            yield return LoadFreshRoomCopy();
            yield return null;
            yield return null;
            var motor = FindInRoom<PlayerMotor>();
            var facts = FindInRoom<GameplayFactService>();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            yield return WalkTo(motor, new Vector3(0, 0, .1f));
            yield return HoldKeys(.1f, Key.C);
            Assert.That(facts.CaptureSnapshot().PlayerPose, Is.EqualTo(PlayerPose.Crouched));
            yield return HoldKeys(.75f, Key.C);
            Assert.That(facts.CaptureSnapshot().PlayerPose, Is.EqualTo(PlayerPose.Prone),
                "Holding C from a crouch must play dead before entry.");
            yield return ObserveNaturalShotAndReset("D14");
            yield return HoldKeys(.25f, Key.S);
            Assert.That(facts.CaptureSnapshot().PlayerPose, Is.EqualTo(PlayerPose.Standing));
        }

        [UnityTest]
        public IEnumerator KeyboardWalksBehindSofaAndNaturallyFinishesStandingD03()
        {
            yield return WalkBehindSofa();
            var facts = FindInRoom<GameplayFactService>();
            Assert.That(facts.CaptureSnapshot().PlayerZone, Is.EqualTo(PlayerZone.SofaRear));
            Assert.That(facts.CaptureSnapshot().PlayerPose, Is.EqualTo(PlayerPose.Standing));
            yield return ObserveNaturalShotAndReset("D03", 60f);
            yield return HoldKeys(.25f, Key.S);
            Assert.That(facts.CaptureSnapshot().PlayerPose, Is.EqualTo(PlayerPose.Standing));
        }

        [UnityTest]
        public IEnumerator KeyboardHidesBehindSofaAndNaturallyFinishesCrouchedD03P()
        {
            yield return WalkBehindSofa();
            var facts = FindInRoom<GameplayFactService>();
            var player = FindInRoom<PlayerInteractor>();
            var motor = FindInRoom<PlayerMotor>();
            // Face and approach the backrest after walking around the furniture.
            yield return HoldKeys(.3f, Key.S);
            Assert.That(motor.GetComponent<InteractionScanner>().RefreshForVerb(InteractionVerb.Hide),
                Is.EqualTo(FindInRoomTransform("FUR_Armchair").GetComponent<InteractableEntity>()),
                $"The normal chest scanner must select the sofa before the stance tap; player at {motor.transform.position}, facing {motor.transform.forward}.");
            yield return HoldKeys(.1f, Key.C);
            Assert.That(player.IsHiding, Is.True, "Short C must use the real sofa's hide capability.");
            Assert.That(facts.CaptureSnapshot().PlayerZone, Is.EqualTo(PlayerZone.SofaRear));
            Assert.That(facts.CaptureSnapshot().PlayerPose, Is.EqualTo(PlayerPose.Crouched));
            var hunt = FindInRoom<KillerDirector>().TacticalController;
            bool searched = false;
            hunt.ModeChanged += mode => { if (mode == KillerTacticalMode.Searching) searched = true; };
            // Searching can revisit several authored anchors before discovery;
            // leave time for the complete impact hold and reset after the shot.
            yield return ObserveNaturalShotAndReset("D03P", 75f);
            Assert.That(searched, Is.True, "Concealed players must be found by searching, not ordinary vision.");
            Assert.That(player.IsHiding, Is.False);
            yield return HoldKeys(.25f, Key.S);
            Assert.That(facts.CaptureSnapshot().PlayerPose, Is.EqualTo(PlayerPose.Standing));
        }

        private IEnumerator WalkBehindSofa()
        {
            CaptureSaveFiles();
            yield return LoadFreshRoomCopy();
            yield return null;
            yield return null;
            var motor = FindInRoom<PlayerMotor>();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            yield return WalkTo(motor, new Vector3(0, 0, .1f));
            yield return WalkTo(motor, new Vector3(-.6f, 0, .1f));
            yield return WalkTo(motor, new Vector3(-.6f, 0, 2.25f));
            yield return WalkTo(motor, new Vector3(-1.4f, 0, 2.25f));
            yield return new WaitForFixedUpdate();
        }

        [UnityTest]
        public IEnumerator KeyboardLeavesChairAndNaturallyFinishesExposedD01WithoutExecutionSnap()
        {
            CaptureSaveFiles();
            yield return LoadFreshRoomCopy();
            yield return null;
            yield return null;
            var motor = FindInRoom<PlayerMotor>();
            var facts = FindInRoom<GameplayFactService>();
            var loop = FindInRoom<LoopDirector>();
            var paired = FindInRoom<PairedPerformancePlayer>();
            _keyboard = InputSystem.AddDevice<Keyboard>();

            yield return WalkTo(motor, new Vector3(0, 0, .1f));
            var snapshot = facts.CaptureSnapshot();
            Assert.That(snapshot.HasMeaningfulInput, Is.True);
            Assert.That(snapshot.IsAtAwakeningPoint, Is.False);
            Assert.That(snapshot.PlayerPose, Is.EqualTo(PlayerPose.Standing));
            Assert.That(FindInRoom<PlayerInteractor>().HeldEntity, Is.Null);
            Assert.That(loop.EntryClock.Deadline, Is.EqualTo(30f));
            Vector3 exposedPosition = motor.transform.position;
            FindInRoom<KillerDirector>().ConfirmedPerformanceLaunching += intent =>
            {
                Assert.That(intent.OutcomeId, Is.EqualTo("D01"));
                Assert.That(Vector3.Distance(motor.transform.position, exposedPosition), Is.LessThan(.02f),
                    "An ordinary live shot must preserve the player's exposed position.");
                Assert.That(paired.IsPlaying || paired.RootsLocked, Is.False,
                    "D01 must not attach the player to a paired execution anchor.");
            };

            yield return ObserveNaturalShotAndReset("D01");
            Assert.That(facts.CaptureSnapshot().HasMeaningfulInput, Is.False);
            yield return HoldKeys(.25f, Key.S);
            Assert.That(facts.CaptureSnapshot().PlayerPose, Is.EqualTo(PlayerPose.Standing));
            Assert.That(motor.transform.position.z,
                Is.LessThan(FindInRoom<PlayerSeatedPosePresenter>().StandingPosition.z - .1f));
        }

        [UnityTest]
        public IEnumerator IdleSeatedPlayerNaturallyFinishesD02AfterFortyFiveSeconds()
        {
            CaptureSaveFiles();
            yield return LoadFreshRoomCopy();
            yield return null;
            yield return null;
            var loop = FindInRoom<LoopDirector>();
            var flow = FindInRoom<GameFlowController>();
            var facts = FindInRoom<GameplayFactService>();
            int warnings = 0;
            float warningAt = -1f, entryAt = -1f;
            loop.EntryWarning += () => { warnings++; warningAt = loop.EntryClock.Elapsed; };
            flow.PhaseChanged += (_, phase, __) =>
            {
                if (phase != GamePhase.Breach) return;
                entryAt = loop.EntryClock.Elapsed;
                Assert.That(loop.EntryClock.Deadline, Is.EqualTo(45f));
                Assert.That(facts.CaptureSnapshot().HasMeaningfulInput, Is.False);
                Assert.That(facts.CaptureSnapshot().IsAtAwakeningPoint, Is.True);
                AssertSeatedAtChair(facts, FindInRoom<PlayerMotor>(),
                    FindInRoom<PlayerSeatedPosePresenter>(), FindInRoomTransform("FUR_WoodChair"));
            };

            // No synthetic input, timer changes or staged phases: the original
            // seated opening must itself remain a reachable death/reset route.
            yield return ObserveNaturalShotAndReset("D02", 60f);
            Assert.That(warnings, Is.EqualTo(1));
            Assert.That(warningAt, Is.InRange(44.5f, 44.5f + Time.maximumDeltaTime));
            Assert.That(entryAt, Is.InRange(45f, 45f + Time.maximumDeltaTime));
            Assert.That(facts.CaptureSnapshot().HasMeaningfulInput, Is.False);
            _keyboard = InputSystem.AddDevice<Keyboard>();
            yield return HoldKeys(.25f, Key.S);
            Assert.That(facts.CaptureSnapshot().PlayerPose, Is.EqualTo(PlayerPose.Standing));
        }

        [UnityTest]
        public IEnumerator KeyboardHidesUnderBedAndNaturallyFinishesPairedD11ThenResets()
        {
            CaptureSaveFiles();
            yield return LoadFreshRoomCopy();
            yield return null;
            yield return null;
            var motor = FindInRoom<PlayerMotor>();
            var player = FindInRoom<PlayerInteractor>();
            var facts = FindInRoom<GameplayFactService>();
            var killer = FindInRoom<KillerDirector>();
            var paired = FindInRoom<PairedPerformancePlayer>();
            var loop = FindInRoom<LoopDirector>();
            var flow = FindInRoom<GameFlowController>();
            var input = FindInRoom<PlayerInputRouter>();
            var camera = FindInRoom<Camera>();
            int epoch = loop.LoopEpoch, sceneHandle = _room.handle;
            Vector3 cameraPosition = camera.transform.position;
            Quaternion cameraRotation = camera.transform.rotation;
            int launches = 0;
            float impactAt = -1f;
            var beats = new System.Collections.Generic.List<string>();
            killer.ConfirmedPerformanceLaunching += intent =>
            {
                Assert.That(intent.OutcomeId, Is.EqualTo("D11"));
                Assert.That(paired.IsPlaying && paired.RootsLocked, Is.True);
                launches++;
            };
            paired.Events.SemanticEventEmitted += cue =>
            {
                Assert.That(cue.LoopEpoch, Is.EqualTo(epoch));
                if (cue.EventId == "D11.ApproachBed" || cue.EventId == "D11.GrabAnkle" ||
                    cue.EventId == "D11.DragOut" || cue.EventId == "D11.Execute") beats.Add(cue.EventId);
            };
            flow.PhaseChanged += (_, phase, __) =>
            {
                if (phase == GamePhase.KillImpact) impactAt = Time.time;
            };

            _keyboard = InputSystem.AddDevice<Keyboard>();
            yield return WalkTo(motor, new Vector3(0, 0, .1f));
            yield return HoldKeys(.15f, Key.D);
            yield return HoldKeys(.1f, Key.C);
            Assert.That(player.IsHiding, Is.True);
            Assert.That(facts.CaptureSnapshot().PlayerZone, Is.EqualTo(PlayerZone.UnderBed));
            Assert.That(facts.CaptureSnapshot().PlayerPose, Is.EqualTo(PlayerPose.Hidden));
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(facts.CaptureSnapshot().PlayerZone, Is.EqualTo(PlayerZone.UnderBed),
                "Overlapping bed-top triggers must not replace the active under-bed hide location.");
            bool sawLiveControl = false;
            float until = Time.time + 60f;
            while (loop.LoopEpoch == epoch && Time.time < until)
            {
                if (flow.CurrentPhase == GamePhase.Breach && killer.IsLiveHuntActive && input.GameplayEnabled)
                    sawLiveControl = true;
                yield return null;
            }
            Assert.That(sawLiveControl, Is.True);
            Assert.That(launches, Is.EqualTo(1), "The actual killer must reach the hidden player and start D11 once.");
            CollectionAssert.AreEqual(new[] { "D11.ApproachBed", "D11.GrabAnkle", "D11.DragOut", "D11.Execute" }, beats);
            Assert.That(impactAt, Is.GreaterThanOrEqualTo(0));
            Assert.That(Time.time - impactAt, Is.GreaterThanOrEqualTo(.75f));
            Assert.That(loop.LoopEpoch, Is.EqualTo(epoch + 1));
            Assert.That(loop.LastOutcomeId, Is.EqualTo("D11"));
            Assert.That(flow.CurrentPhase, Is.EqualTo(GamePhase.Awakening));
            Assert.That(_room.isLoaded && _room.handle == sceneHandle, Is.True);
            Assert.That(camera.transform.position, Is.EqualTo(cameraPosition));
            Assert.That(Quaternion.Angle(camera.transform.rotation, cameraRotation), Is.LessThan(.01f));
            Assert.That(paired.IsPlaying || paired.RootsLocked || paired.HasActiveAnimationGraph, Is.False);
            Assert.That(player.IsHiding, Is.False);
            AssertSeatedAtChair(facts, motor, FindInRoom<PlayerSeatedPosePresenter>(), FindInRoomTransform("FUR_WoodChair"));
            yield return HoldKeys(.25f, Key.S);
            Assert.That(facts.CaptureSnapshot().PlayerPose, Is.EqualTo(PlayerPose.Standing));
            Assert.That(motor.transform.position.z,
                Is.LessThan(FindInRoom<PlayerSeatedPosePresenter>().StandingPosition.z - .1f));
        }

        [UnityTest]
        public IEnumerator KeyboardHidesOnBedAndNaturallyFinishesD04ThenResetsBlanket()
        {
            CaptureSaveFiles();
            yield return LoadFreshRoomCopy();
            yield return null;
            yield return null;
            var motor = FindInRoom<PlayerMotor>();
            var player = FindInRoom<PlayerInteractor>();
            var facts = FindInRoom<GameplayFactService>();
            var blanket = FindInRoomTransform("PRP_Blanket").GetComponent<InteractableEntity>();
            _keyboard = InputSystem.AddDevice<Keyboard>();

            yield return WalkTo(motor, new Vector3(0, 0, .1f));
            yield return WalkTo(motor, new Vector3(1.05f, 0, .1f));
            yield return WalkTo(motor, new Vector3(1.05f, 0, .65f));
            Assert.That(motor.GetComponent<InteractionScanner>().RefreshForVerb(InteractionVerb.Hide),
                Is.EqualTo(blanket), "Approaching the blanket must offer its on-bed hide rather than the bed-bottom hide.");
            yield return HoldKeys(.1f, Key.C);
            Assert.That(player.IsHiding, Is.True);
            Assert.That(facts.CaptureSnapshot().PlayerZone, Is.EqualTo(PlayerZone.Bed));
            Assert.That(facts.CaptureSnapshot().PlayerPose, Is.EqualTo(PlayerPose.Hidden));
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(facts.CaptureSnapshot().PlayerZone, Is.EqualTo(PlayerZone.Bed));
            yield return ObserveNaturalShotAndReset("D04", 75f);
            Assert.That(player.IsHiding, Is.False);
            Assert.That(blanket.GetComponent<StatefulWorldItem>().CurrentState, Is.EqualTo(ItemPhysicalState.Placed));
            Assert.That(blanket.GetComponent<HideCapability>().isActiveAndEnabled, Is.True);
            yield return HoldKeys(.25f, Key.S);
            Assert.That(facts.CaptureSnapshot().PlayerPose, Is.EqualTo(PlayerPose.Standing));
        }

        [UnityTest]
        public IEnumerator KeyboardWalksToCameraBlindAndNaturallyFinishesD12WithScreenCrack()
        {
            CaptureSaveFiles();
            yield return LoadFreshRoomCopy();
            yield return null;
            yield return null;
            var motor = FindInRoom<PlayerMotor>();
            var facts = FindInRoom<GameplayFactService>();
            var signature = FindInRoom<MvSignaturePresenter>();
            var flow = FindInRoom<GameFlowController>();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            // Approach the bookcase's south side along the open floor.
            yield return WalkTo(motor, new Vector3(0, 0, .1f));
            yield return WalkTo(motor, new Vector3(0, 0, -.8f));
            yield return WalkTo(motor, new Vector3(-2.5f, 0, -.8f));
            yield return new WaitForFixedUpdate();
            Assert.That(facts.CaptureSnapshot().PlayerZone, Is.EqualTo(PlayerZone.CameraBlind));
            Assert.That(facts.CaptureSnapshot().PlayerPose, Is.EqualTo(PlayerPose.Standing));
            Assert.That(signature.ScreenCrackAmount, Is.Zero);
            bool sawCrack = false;
            flow.PhaseChanged += (_, phase, __) =>
            {
                if (phase != GamePhase.KillImpact) return;
                sawCrack = signature.ScreenCrackAmount > 0f;
                Assert.That(sawCrack, Is.True, "The real D12 bullet must fracture the viewer's screen.");
            };

            yield return ObserveNaturalShotAndReset("D12", 75f);
            Assert.That(sawCrack, Is.True);
            Assert.That(signature.ScreenCrackAmount, Is.Zero, "The next loop must clear the viewer's glass.");
            yield return HoldKeys(.25f, Key.S);
            Assert.That(facts.CaptureSnapshot().PlayerPose, Is.EqualTo(PlayerPose.Standing));
        }

        [UnityTest]
        public IEnumerator KeyboardCarriesChairToBlockDoorAndNaturallyFinishesD09ThenRestoresRoom()
        {
            CaptureSaveFiles();
            Transform chair = null;
            Vector3 chairStart = default, chairReset = default;
            Quaternion chairRotation = default, chairResetRotation = default;
            yield return LoadFreshRoomCopy(() =>
            {
                chair = FindInRoomTransform("FUR_WoodChair");
                chairStart = chair.position;
                chairRotation = chair.rotation;
            });
            yield return null;
            yield return null;
            var motor = FindInRoom<PlayerMotor>();
            var player = FindInRoom<PlayerInteractor>();
            var facts = FindInRoom<GameplayFactService>();
            var breach = FindInRoom<RoomDoorBreachController>();
            var socket = FindInRoomTransform("PRP_RoomDoor_Interact").GetComponent<PlacementSocket>();
            float baseDuration = breach.CurrentDuration;
            FindInRoom<LoopDirector>().LoopResetCompleted += (_, __) =>
            {
                chairReset = chair.position;
                chairResetRotation = chair.rotation;
            };
            _keyboard = InputSystem.AddDevice<Keyboard>();

            yield return WalkTo(motor, new Vector3(0, 0, .1f));
            yield return WalkTo(motor, new Vector3(-.35f, 0, .1f));
            yield return WalkTo(motor, new Vector3(-.35f, 0, 1.55f));
            Assert.That(motor.GetComponent<InteractionScanner>().RefreshForVerb(InteractionVerb.Carry),
                Is.EqualTo(chair.GetComponent<InteractableEntity>()));
            yield return HoldKeys(.5f, Key.E);
            yield return new WaitForSeconds(.4f);
            Assert.That(player.HeldEntity, Is.EqualTo(chair.GetComponent<InteractableEntity>()));
            Assert.That(facts.CaptureSnapshot().PlayerPose, Is.EqualTo(PlayerPose.Carrying));

            yield return WalkTo(motor, new Vector3(-.35f, 0, .1f));
            yield return WalkTo(motor, new Vector3(0, 0, .1f));
            yield return WalkTo(motor, new Vector3(2.4f, 0, .1f));
            yield return WalkTo(motor, new Vector3(2.4f, 0, 1.15f));
            Assert.That(motor.GetComponent<InteractionScanner>().RefreshForVerb(InteractionVerb.Place),
                Is.EqualTo(socket.GetComponent<InteractableEntity>()));
            yield return HoldKeys(.1f, Key.E);
            yield return new WaitForSeconds(.4f);
            Assert.That(player.HeldEntity, Is.Null);
            Assert.That(socket.Occupant, Is.EqualTo(chair.GetComponent<InteractableEntity>()));
            Assert.That(facts.CaptureSnapshot().DoorBlock, Is.EqualTo(DoorBlockState.Furniture));
            Assert.That(facts.DoorBlockBreachExtraSeconds, Is.EqualTo(3f));
            yield return WalkTo(motor, new Vector3(2.4f, 0, .1f));
            yield return WalkTo(motor, new Vector3(0, 0, .1f));
            bool sawExtendedBreach = false;
            breach.KickWindupStarted += _ =>
            {
                Assert.That(breach.CurrentDuration, Is.EqualTo(baseDuration + 3f).Within(.01f));
                sawExtendedBreach = true;
            };

            yield return ObserveNaturalShotAndReset("D09", 60f);
            Assert.That(sawExtendedBreach, Is.True);
            Assert.That(socket.Occupant, Is.Null);
            Assert.That(facts.CaptureSnapshot().DoorBlock, Is.EqualTo(DoorBlockState.None));
            Assert.That(facts.DoorBlockBreachExtraSeconds, Is.Zero);
            Assert.That(Vector3.Distance(chairReset, chairStart), Is.LessThan(.01f));
            Assert.That(Quaternion.Angle(chairResetRotation, chairRotation), Is.LessThan(.01f));
            Assert.That(breach.CurrentDuration, Is.EqualTo(baseDuration));
            yield return HoldKeys(.25f, Key.S);
            Assert.That(facts.CaptureSnapshot().PlayerPose, Is.EqualTo(PlayerPose.Standing));
        }

        private IEnumerator ObserveNaturalShotAndReset(string expectedOutcome, float timeoutSeconds = 45f)
        {
            var killer = FindInRoom<KillerDirector>();
            var flow = FindInRoom<GameFlowController>();
            var loop = FindInRoom<LoopDirector>();
            var input = FindInRoom<PlayerInputRouter>();
            var camera = FindInRoom<Camera>();
            int epoch = loop.LoopEpoch;
            int sceneHandle = _room.handle;
            Vector3 cameraPosition = camera.transform.position;
            Quaternion cameraRotation = camera.transform.rotation;
            string committed = null, fired = null;
            int shots = 0;
            float impactAt = -1f;
            killer.ConfirmedPerformanceLaunching += intent => committed = intent.OutcomeId;
            killer.ShotFired += (outcome, shotEpoch) =>
            {
                Assert.That(shotEpoch, Is.EqualTo(epoch));
                fired = outcome;
                shots++;
            };
            flow.PhaseChanged += (_, phase, __) =>
            {
                if (phase == GamePhase.KillImpact) impactAt = Time.time;
            };

            float until = Time.time + timeoutSeconds;
            bool sawLiveControl = false;
            while (loop.LoopEpoch == epoch && Time.time < until)
            {
                if (flow.CurrentPhase == GamePhase.Breach && killer.IsLiveHuntActive && input.GameplayEnabled)
                    sawLiveControl = true;
                yield return null;
            }
            Assert.That(sawLiveControl, Is.True, "Entry must release control for the live hunt.");
            Assert.That(committed, Is.EqualTo(expectedOutcome), "The actual attack must commit the intended branch.");
            Assert.That(fired, Is.EqualTo(expectedOutcome));
            Assert.That(shots, Is.EqualTo(1));
            Assert.That(impactAt, Is.GreaterThanOrEqualTo(0));
            Assert.That(Time.time - impactAt, Is.GreaterThanOrEqualTo(.75f));
            Assert.That(loop.LoopEpoch, Is.EqualTo(epoch + 1));
            Assert.That(loop.LastOutcomeId, Is.EqualTo(expectedOutcome));
            Assert.That(flow.CurrentPhase, Is.EqualTo(GamePhase.Awakening));
            Assert.That(_room.isLoaded && _room.handle == sceneHandle, Is.True);
            Assert.That(camera.transform.position, Is.EqualTo(cameraPosition));
            Assert.That(Quaternion.Angle(camera.transform.rotation, cameraRotation), Is.LessThan(.01f));
            AssertSeatedAtChair(FindInRoom<GameplayFactService>(), FindInRoom<PlayerMotor>(),
                FindInRoom<PlayerSeatedPosePresenter>(), FindInRoomTransform("FUR_WoodChair"));
        }

        private IEnumerator ClickAttack()
        {
            InputSystem.QueueStateEvent(_mouse, new MouseState().WithButton(MouseButton.Left));
            yield return null;
            InputSystem.QueueStateEvent(_mouse, new MouseState());
            yield return null;
        }

        private IEnumerator HoldKeys(float seconds, params Key[] keys)
        {
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(keys));
            yield return new WaitForSeconds(seconds);
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            yield return null;
            yield return null;
        }

        private IEnumerator WalkTo(PlayerMotor motor, Vector3 target)
        {
            float elapsed = 0f;
            while (elapsed < 3f)
            {
                Vector3 delta = target - motor.transform.position;
                delta.y = 0;
                if (delta.magnitude < .09f) break;
                Key key = Mathf.Abs(delta.x) > Mathf.Abs(delta.z)
                    ? (delta.x > 0 ? Key.D : Key.A)
                    : (delta.z > 0 ? Key.W : Key.S);
                InputSystem.QueueStateEvent(_keyboard, new KeyboardState(key));
                elapsed += Time.deltaTime;
                yield return null;
            }
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            yield return null;
            Vector3 remaining = target - motor.transform.position;
            remaining.y = 0;
            Assert.That(remaining.magnitude, Is.LessThan(.12f),
                $"Keyboard movement should reach {target}; stopped at {motor.transform.position}.");
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

        private IEnumerator LoadFreshRoomCopy(System.Action beforeStart = null)
        {
            Scene loaded = default;
            UnityEngine.Events.UnityAction<Scene, LoadSceneMode> onSceneLoaded = (scene, mode) =>
            {
                if (scene.path == RoomScenePath)
                {
                    loaded = scene;
                    _room = scene;
                    beforeStart?.Invoke();
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
