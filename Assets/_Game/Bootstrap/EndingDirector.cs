using System;
using System.Collections;
using ConGame.Domain.Facts;
using ConGame.Domain.Flow;
using ConGame.Presentation.Animation;
using ConGame.Presentation.Audio;
using ConGame.Presentation.Camera;
using ConGame.Presentation.CRT;
using ConGame.Runtime.AI;
using ConGame.Runtime.Ending;
using ConGame.Runtime.Flow;
using ConGame.Runtime.Input;
using ConGame.Runtime.Interaction;
using ConGame.Runtime.Loop;
using ConGame.Runtime.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ConGame.Bootstrap
{
    public enum EndingKillerStage
    {
        Dormant = 0,
        WalkingIn = 1,
        Standing = 2,
        Aiming = 3,
        ShotFired = 4
    }

    [DisallowMultipleComponent]
    public sealed class EndingDirector : MonoBehaviour
    {
        private const float E1WhiteNoiseSeconds = 0.2f;
        private const float E1BlackoutSeconds = 1.5f;

        [SerializeField] private GameFlowController flow;
        [SerializeField] private LoopDirector loop;
        [SerializeField] private LoopResetRegistry resetRegistry;
        [SerializeField] private PlayerInputRouter input;
        [SerializeField] private PlayerMotor player;
        [SerializeField] private KillerDirector killer;
        [SerializeField] private TelevisionController television;
        [SerializeField] private CameraModeController cameraMode;
        [SerializeField] private FirstPersonLookController firstPersonLook;
        [SerializeField] private CrtFullscreenPass crtPass;
        [SerializeField] private Transform endingChairAnchor;
        [SerializeField] private Transform endingKillerAnchor;
        [SerializeField] private EndingSequenceSettingsAsset endingSettingsAsset;
        [SerializeField] private EndingObserverHandoff observerHandoff;
        [SerializeField] private string menuSceneName = "MainMenu";

        private Coroutine _routine;
        private bool _showTitleCard;
        private bool _e2KillReceived;
        private bool _e2EntryStarted;
        private bool _lookOnlyInputRequested;
        private string _e2OutcomeId;
        private EndingSequenceSettings _runSettings = EndingSequenceSettings.Default;
        private Transform _fallenView;
        private Vector3 _viewRestPosition;
        private Quaternion _viewRestRotation;

        public event Action ClockAdvancedEvent;
        public event Action EndingShotFired;

        public bool IsRunning => _routine != null;
        public bool ShowTitleCard => _showTitleCard;
        public bool E2EntryStarted => _e2EntryStarted;
        public bool LookOnlyInputRequested => _lookOnlyInputRequested;
        public bool ClockAdvanced { get; private set; }
        public bool ShotFired { get; private set; }
        public int PassResetCount { get; private set; }
        public EndingKillerStage KillerStage { get; private set; }
        public bool HasObserverHandoff => observerHandoff != null && observerHandoff.HasObserverClone;
        public EndingSequenceSettingsAsset SettingsAsset => endingSettingsAsset;

        private EndingSequenceSettings EndingSettings => _runSettings;

        public void Configure(
            GameFlowController flowController,
            LoopDirector loopDirector,
            LoopResetRegistry registry,
            PlayerInputRouter inputRouter,
            PlayerMotor playerMotor,
            KillerDirector killerDirector,
            TelevisionController televisionController,
            CameraModeController cameraController,
            Transform chairAnchor,
            Transform killerAnchor,
            string menuScene = "MainMenu",
            FirstPersonLookController lookController = null,
            CrtFullscreenPass fullscreenPass = null)
        {
            Unsubscribe();
            flow = flowController;
            loop = loopDirector;
            resetRegistry = registry;
            input = inputRouter;
            player = playerMotor;
            killer = killerDirector;
            television = televisionController;
            cameraMode = cameraController;
            firstPersonLook = lookController;
            crtPass = fullscreenPass;
            endingChairAnchor = chairAnchor;
            endingKillerAnchor = killerAnchor;
            menuSceneName = menuScene;
            if (isActiveAndEnabled)
            {
                Subscribe();
            }
        }

        public void ConfigurePresentation(
            EndingSequenceSettingsAsset settingsAsset,
            Transform screenAnchor,
            Transform originalActorVisual,
            Transform exitAnchor)
        {
            endingSettingsAsset = settingsAsset;
            if (observerHandoff == null)
            {
                observerHandoff = GetComponent<EndingObserverHandoff>();
                if (observerHandoff == null)
                {
                    observerHandoff = gameObject.AddComponent<EndingObserverHandoff>();
                }
            }

            observerHandoff.Configure(
                player,
                endingSettingsAsset,
                screenAnchor,
                originalActorVisual,
                exitAnchor);
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();

            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }
            RestoreViewHeight();
            observerHandoff?.ResetHandoff();
        }

        private void Subscribe()
        {
            if (flow != null)
            {
                flow.PhaseChanged -= OnPhaseChanged;
                flow.PhaseChanged += OnPhaseChanged;
            }

            if (loop != null)
            {
                loop.EndingFightCompleted -= OnEndingFightCompleted;
                loop.EndingFightCompleted += OnEndingFightCompleted;
            }
        }

        private void Unsubscribe()
        {
            if (flow != null)
            {
                flow.PhaseChanged -= OnPhaseChanged;
            }

            if (loop != null)
            {
                loop.EndingFightCompleted -= OnEndingFightCompleted;
            }
        }

        private void OnPhaseChanged(GamePhase previous, GamePhase current, string reason)
        {
            if (current == GamePhase.EndingE1 && _routine == null)
            {
                _routine = StartCoroutine(RunEnding());
            }
        }

        private void OnEndingFightCompleted(string outcomeId)
        {
            if (flow != null && flow.CurrentPhase != GamePhase.EndingE2)
            {
                return;
            }

            _e2KillReceived = true;
            _e2OutcomeId = IsEndingOutcome(outcomeId) ? outcomeId : "D01";
        }

        private IEnumerator RunEnding()
        {
            _runSettings = endingSettingsAsset == null
                ? EndingSequenceSettings.Default : endingSettingsAsset.CreateSettings();
            ResetPublicState();
            LockPlayer();
            observerHandoff?.CaptureOriginalPose();
            int epoch = loop == null ? 0 : loop.LoopEpoch;
            killer?.SetEndingMode(false);
            killer?.Quiesce(new LoopResetContext(epoch, epoch));

            // E1 owns the win immediately, so no counterplay or ordinary loop reset can resume.
            television?.SetEndingDestroyed();
            crtPass?.Pulse(1f);
            crtPass?.SetBlackout(0f);
            yield return new WaitForSeconds(E1WhiteNoiseSeconds);
            crtPass?.SetBlackout(1f);
            yield return new WaitForSeconds(E1BlackoutSeconds);

            Vector3 previousPlayerPosition = player == null ? Vector3.zero : player.transform.position;
            Quaternion previousPlayerRotation = player == null ? Quaternion.identity : player.transform.rotation;
            RestoreEndingPass(epoch, false);
            cameraMode?.SwitchToFirstPerson();
            firstPersonLook?.ResetLook();
            firstPersonLook?.ClearYawLimit();
            bool handedOff = observerHandoff != null && observerHandoff.BeginObserverTakeover();
            if (!handedOff && player != null)
            {
                // Preserve legacy scenes that have no presentation anchors.
                player.Teleport(previousPlayerPosition, previousPlayerRotation);
            }
            player?.SetMovementReference(firstPersonLook == null ? null : firstPersonLook.ViewTransform, true);
            observerHandoff?.BeginExitMotion();
            if (handedOff)
            {
                firstPersonLook?.LookAt(observerHandoff.CapturedPlayerPosition + Vector3.up * 1.2f);
                UnityEngine.Object.FindAnyObjectByType<RoomDoorBreachController>()?.Begin(epoch, null, 0.45f);
            }
            else if (firstPersonLook != null && television != null)
            {
                firstPersonLook.LookAt(television.transform.position);
            }
            if (firstPersonLook != null)
            {
                firstPersonLook.LookEnabled = true;
            }

            if (handedOff) yield return new WaitForSeconds(EndingSettings.E2TakeoverSeconds);
            crtPass?.SetBlackout(0f);
            input?.SetGameplayEnabled(true);
            player?.SetInputLocked(false);
            flow?.Request(GamePhase.EndingE2, TransitionPriority.Normal, "Ending pass one completed.");
            flow?.CommitPending();

            yield return new WaitForSeconds(EndingSettings.E2FreeControlSeconds);
            observerHandoff?.CompleteHandoff();
            _e2EntryStarted = true;
            killer?.SetEndingMode(true);
            if (killer == null)
            {
                OnEndingFightCompleted("D01");
            }
            else
            {
                killer.gameObject.SetActive(true);
                killer.BeginEntry(epoch, NotifyEndingFightCompleted);
            }

            float e2Elapsed = 0f;
            while (!_e2KillReceived && e2Elapsed < EndingSettings.E2FightTimeoutSeconds)
            {
                e2Elapsed += Time.deltaTime;
                yield return null;
            }

            if (!_e2KillReceived)
            {
                killer?.ForceImmediateKill("D01");
                float wait = 0f;
                while (!_e2KillReceived && wait < 0.5f)
                {
                    wait += Time.deltaTime;
                    yield return null;
                }

                if (!_e2KillReceived)
                {
                    OnEndingFightCompleted("D01");
                }
            }

            killer?.SetEndingMode(false);
            killer?.Quiesce(new LoopResetContext(epoch, epoch));
            LockPlayer();
            if (firstPersonLook != null)
            {
                firstPersonLook.LookEnabled = false;
            }

            yield return FallToGround();
            crtPass?.SetBlackout(1f);
            yield return new WaitForSeconds(EndingSettings.E2BlackoutSeconds);
            RestoreViewHeight();

            flow?.Request(
                GamePhase.EndingE3,
                TransitionPriority.Normal,
                string.IsNullOrEmpty(_e2OutcomeId) ? "First-person death completed." : _e2OutcomeId);
            flow?.CommitPending();

            RestoreEndingPass(epoch, true);
            cameraMode?.SwitchToFirstPerson();
            player?.SetMovementReference(firstPersonLook == null ? null : firstPersonLook.ViewTransform, true);
            player?.SetInputLocked(true);
            input?.SetLookOnlyEnabled(true);
            _lookOnlyInputRequested = true;
            if (firstPersonLook != null)
            {
                firstPersonLook.LookEnabled = true;
                firstPersonLook.ResetLook();
                if (endingKillerAnchor != null)
                {
                    firstPersonLook.LookAt(endingKillerAnchor.position);
                }
                firstPersonLook.SetYawLimit(EndingSettings.E3YawLimitDegrees);
            }

            crtPass?.SetBlackout(0f);
            yield return RunFinalKillerBlocking();

            for (int tick = 0; tick < EndingSettings.E3ClockTickCount; tick++)
            {
                AdvanceClock();
                yield return new WaitForSeconds(EndingSettings.E3ClockTickSeconds);
            }
            FireEndingShot();
            crtPass?.SetBlackout(1f);
            yield return new WaitForSeconds(EndingSettings.E3ShotSilenceSeconds);

            loop?.MarkEndingCompleted();
            _showTitleCard = true;
            _lookOnlyInputRequested = false;
            input?.SetGameplayEnabled(false);
            firstPersonLook?.ResetLook();
            if (firstPersonLook != null)
            {
                firstPersonLook.ClearYawLimit();
                firstPersonLook.LookEnabled = false;
            }

            crtPass?.SetTitleAmount(1f);
            crtPass?.SetBlackout(0.92f);
            flow?.Request(GamePhase.Finished, TransitionPriority.Normal, "The clock advanced and the final shot landed.");
            flow?.CommitPending();

            yield return new WaitForSeconds(EndingSettings.E3TitleHoldSeconds);
            _showTitleCard = false;
            crtPass?.SetTitleAmount(0f);
            crtPass?.SetBlackout(0f);
            Time.timeScale = 1f;
            if (!string.IsNullOrWhiteSpace(menuSceneName))
            {
                observerHandoff?.CompleteHandoff();
                SceneManager.LoadScene(menuSceneName, LoadSceneMode.Single);
            }

            _routine = null;
        }

        private IEnumerator FallToGround()
        {
            if (player == null)
            {
                yield return new WaitForSeconds(EndingSettings.E2FallSeconds);
                yield break;
            }

            Transform view = firstPersonLook == null ? null : firstPersonLook.ViewTransform;
            Vector3 startPosition = view == null ? Vector3.zero : view.localPosition;
            Quaternion startRotation = view == null ? Quaternion.identity : view.localRotation;
            _fallenView = view;
            _viewRestPosition = startPosition;
            _viewRestRotation = startRotation;
            EndingSequenceSettings settings = EndingSettings;
            float elapsed = 0f;
            while (elapsed < settings.E2FallSeconds)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, settings.E2FallSeconds));
                EndingFallPose pose = EndingFallMotion.Sample(t, settings);
                if (view != null)
                {
                    view.localPosition = startPosition + Vector3.down * pose.HeightDropMeters;
                    view.localRotation = startRotation * Quaternion.Euler(
                        pose.PitchDownDegrees, 0f, pose.RollDegrees);
                }
                yield return null;
            }
        }

        private void RestoreViewHeight()
        {
            if (_fallenView == null) return;
            _fallenView.localPosition = _viewRestPosition;
            _fallenView.localRotation = _viewRestRotation;
            _fallenView = null;
        }

        private void OnGUI()
        {
            if (!_showTitleCard)
            {
                return;
            }

            GUIStyle style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.Clamp(Screen.height / 18, 28, 64),
                fontStyle = FontStyle.Normal
            };
            style.normal.textColor = new Color(0.92f, 0.92f, 0.9f, 1f);
            GUI.Label(new Rect(0f, 0f, Screen.width, Screen.height), "困室", style);
        }

        private IEnumerator RunFinalKillerBlocking()
        {
            RoomDoorBreachController endingDoor = UnityEngine.Object.FindAnyObjectByType<RoomDoorBreachController>();
            if (endingDoor != null)
            {
                endingDoor.Begin(loop == null ? 0 : loop.LoopEpoch, null, 0.45f);
                while (endingDoor.IsRunning)
                {
                    yield return null;
                }
            }

            KillerStage = EndingKillerStage.WalkingIn;
            if (killer != null)
            {
                CharacterAnimatorDriver driver = killer.GetComponent<CharacterAnimatorDriver>();
                killer.gameObject.SetActive(true);
                Vector3 startPosition = killer.transform.position;
                Quaternion startRotation = killer.transform.rotation;
                Vector3 targetPosition = endingKillerAnchor == null
                    ? startPosition
                    : endingKillerAnchor.position;
                Quaternion targetRotation = endingKillerAnchor == null
                    ? startRotation
                    : endingKillerAnchor.rotation;
                float approachSeconds = Mathf.Max(0.01f, EndingSettings.E3KillerApproachSeconds);
                driver?.SetScriptedMotionSpeed(Vector3.Distance(startPosition, targetPosition) / approachSeconds);
                float elapsed = 0f;
                while (elapsed < approachSeconds)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.SmoothStep(0f, 1f, elapsed / approachSeconds);
                    killer.transform.SetPositionAndRotation(
                        Vector3.Lerp(startPosition, targetPosition, t),
                        Quaternion.Slerp(startRotation, targetRotation, t));
                    yield return null;
                }
                driver?.SetScriptedMotionSpeed(0f);
            }
            else
            {
                yield return new WaitForSeconds(EndingSettings.E3KillerApproachSeconds);
            }

            KillerStage = EndingKillerStage.Standing;
            yield return new WaitForSeconds(EndingSettings.E3SitChairSeconds);

            KillerStage = EndingKillerStage.Aiming;
            if (killer != null && player != null)
            {
                killer.GetComponent<CharacterAnimatorDriver>()?.PlayAimFire();
                Quaternion startRotation = killer.transform.rotation;
                Vector3 direction = player.transform.position - killer.transform.position;
                Quaternion targetRotation = direction.sqrMagnitude < 0.0001f
                    ? startRotation
                    : Quaternion.LookRotation(direction.normalized, Vector3.up);
                float elapsed = 0f;
                while (elapsed < EndingSettings.E3SitChairSeconds)
                {
                    elapsed += Time.deltaTime;
                    killer.transform.rotation = Quaternion.Slerp(
                        startRotation,
                        targetRotation,
                        Mathf.SmoothStep(0f, 1f, elapsed / Mathf.Max(0.01f, EndingSettings.E3SitChairSeconds)));
                    yield return null;
                }
            }
            else
            {
                yield return new WaitForSeconds(EndingSettings.E3SitChairSeconds);
            }
        }

        private void RestoreEndingPass(int epoch, bool finalPass)
        {
            if (resetRegistry != null)
            {
                resetRegistry.ResetAll(epoch, epoch);
                PassResetCount++;
            }

            television?.SetEndingDestroyed();
            killer?.SetEndingMode(false);
            killer?.Quiesce(new LoopResetContext(epoch, epoch));
            LockPlayer();

            if (finalPass && player != null && endingChairAnchor != null)
            {
                Transform chair = FindSceneTransform("FUR_WoodChair");
                Vector3 position = endingChairAnchor.position;
                if (chair != null)
                {
                    position.x = chair.position.x;
                    position.z = chair.position.z;
                }

                Quaternion rotation = endingChairAnchor.rotation;
                if (endingKillerAnchor != null)
                {
                    Vector3 direction = endingKillerAnchor.position - position;
                    direction.y = 0f;
                    if (direction.sqrMagnitude > 0.0001f)
                    {
                        rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
                    }
                }

                player.Teleport(position, rotation);
                player.GetComponent<PlayerInteractor>()?.SetPose(PlayerPose.Sitting);
            }
        }

        private void AdvanceClock()
        {
            bool firstTick = !ClockAdvanced;
            ClockAdvanced = true;
            Transform wallClock = FindSceneTransform("WallClock") ?? FindSceneTransform("PRP_WallClock");
            ClockTwitchBehaviour clock = wallClock == null ? null : wallClock.GetComponent<ClockTwitchBehaviour>();
            if (clock != null)
            {
                clock.AdvanceForEnding();
            }
            else
            {
                // Compatibility for legacy scenes without the clock controller.
                Transform secondHand = wallClock == null ? null : FindChild(wallClock, "SecondHand");
                if (wallClock != null && secondHand == null)
                {
                    GameObject hand = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    hand.name = "SecondHand";
                    hand.transform.SetParent(wallClock, false);
                    hand.transform.localPosition = new Vector3(0f, 0.18f, -0.03f);
                    hand.transform.localScale = new Vector3(0.015f, 0.36f, 0.015f);
                    Destroy(hand.GetComponent<Collider>());
                    secondHand = hand.transform;
                }
                if (secondHand != null)
                    secondHand.localRotation *= Quaternion.Euler(0f, 0f, -6f);
            }

            if (firstTick) ClockAdvancedEvent?.Invoke();
        }

        private void FireEndingShot()
        {
            ShotFired = true;
            KillerStage = EndingKillerStage.ShotFired;
            // 已在瞄准阶段进入 AimFire；这里保留枪响事件契约，状态禁止自重入。
            killer?.GetComponent<CharacterAnimatorDriver>()?.PlayAimFire();
            crtPass?.Pulse(1f);
            UnityEngine.Object.FindAnyObjectByType<SemanticAudioDirector>()?.Play(AudioCueId.Gunshot);
            EndingShotFired?.Invoke();
        }

        private void NotifyEndingFightCompleted(string outcomeId)
        {
            if (loop == null)
            {
                OnEndingFightCompleted(outcomeId);
            }
            else
            {
                loop.NotifyEndingFightCompleted(outcomeId);
            }
        }

        private void ResetPublicState()
        {
            _showTitleCard = false;
            _e2KillReceived = false;
            _e2EntryStarted = false;
            _lookOnlyInputRequested = false;
            _e2OutcomeId = string.Empty;
            ClockAdvanced = false;
            ShotFired = false;
            PassResetCount = 0;
            KillerStage = EndingKillerStage.Dormant;
        }

        private void LockPlayer()
        {
            input?.SetGameplayEnabled(false);
            player?.SetInputLocked(true);
        }

        private static bool IsEndingOutcome(string outcomeId)
        {
            return string.Equals(outcomeId, "D01", StringComparison.Ordinal) ||
                   string.Equals(outcomeId, "D07", StringComparison.Ordinal) ||
                   string.Equals(outcomeId, "D10", StringComparison.Ordinal);
        }

        private static Transform FindChild(Transform parent, string childName)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (string.Equals(child.name, childName, StringComparison.Ordinal))
                {
                    return child;
                }

                Transform nested = FindChild(child, childName);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }

        private static Transform FindSceneTransform(string objectName)
        {
            Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i] != null && transforms[i].gameObject.scene.IsValid() &&
                    string.Equals(transforms[i].name, objectName, StringComparison.Ordinal))
                {
                    return transforms[i];
                }
            }

            return null;
        }
    }
}
