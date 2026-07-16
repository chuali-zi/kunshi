using System.Collections;
using ConGame.Domain.Counterplay;
using ConGame.Domain.Flow;
using ConGame.Presentation.Camera;
using ConGame.Presentation.CRT;
using ConGame.Runtime.AI;
using ConGame.Runtime.Ending;
using ConGame.Runtime.Flow;
using ConGame.Runtime.Input;
using ConGame.Runtime.Loop;
using ConGame.Runtime.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ConGame.Bootstrap
{
    [DisallowMultipleComponent]
    public sealed class EndingDirector : MonoBehaviour
    {
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
        [SerializeField] private string menuSceneName = "MainMenu";

        private Coroutine _routine;
        private QteSequenceCounter _e2Qte;
        private bool _showTitleCard;
        private bool _e2KillReceived;
        private string _e2OutcomeId;

        public bool IsRunning => _routine != null;
        public bool ShowTitleCard => _showTitleCard;

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
        }

        private void Subscribe()
        {
            if (flow != null)
            {
                flow.PhaseChanged -= OnPhaseChanged;
                flow.PhaseChanged += OnPhaseChanged;
            }

            if (input != null)
            {
                input.InteractPressed -= OnInteractPressed;
                input.InteractPressed += OnInteractPressed;
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

            if (input != null)
            {
                input.InteractPressed -= OnInteractPressed;
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

        private void OnInteractPressed()
        {
            if (_e2Qte == null || _e2Qte.IsComplete || flow == null || flow.CurrentPhase != GamePhase.EndingE2)
            {
                return;
            }

            _e2Qte.Advance(0f, true);
        }

        private void OnEndingFightCompleted(string outcomeId)
        {
            _e2KillReceived = true;
            _e2OutcomeId = outcomeId;
        }

        private IEnumerator RunEnding()
        {
            _showTitleCard = false;
            _e2KillReceived = false;
            LockPlayer();
            int epoch = loop == null ? 0 : loop.LoopEpoch;
            killer?.SetEndingMode(false);
            killer?.Quiesce(new LoopResetContext(epoch, epoch));

            // E1: white-noise shatter beat via CRT, then FP.
            crtPass?.Pulse(1f);
            crtPass?.SetBlackout(0.35f);
            television?.SetEndingDestroyed();
            yield return new WaitForSeconds(0.85f);
            crtPass?.SetBlackout(0.8f);
            yield return new WaitForSeconds(0.65f);
            crtPass?.SetBlackout(0f);

            cameraMode?.SwitchToFirstPerson();
            firstPersonLook?.ResetLook();
            if (firstPersonLook != null)
            {
                firstPersonLook.LookEnabled = true;
            }

            input?.SetGameplayEnabled(true);
            player?.SetInputLocked(false);
            flow.Request(GamePhase.EndingE2, TransitionPriority.Normal, "Ending pass one completed.");
            flow.CommitPending();

            // E2: FP fight — ending-filtered counterplays, plus mash windows as struggle feel.
            killer?.SetEndingMode(true);
            if (killer != null)
            {
                killer.gameObject.SetActive(true);
                killer.BeginEntry(epoch, outcome => loop?.NotifyEndingFightCompleted(outcome));
            }

            _e2Qte = new QteSequenceCounter(
                new[]
                {
                    new QteWindowDefinition(1.2f, 4), new QteWindowDefinition(1.2f, 5),
                    new QteWindowDefinition(1.2f, 5)
                },
                1f);

            float e2Elapsed = 0f;
            const float e2Timeout = 8f;
            while (!_e2KillReceived && e2Elapsed < e2Timeout)
            {
                float delta = Time.deltaTime;
                e2Elapsed += delta;
                _e2Qte?.Advance(delta, false);
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
            }

            _e2Qte = null;
            killer?.SetEndingMode(false);
            LockPlayer();
            if (firstPersonLook != null)
            {
                firstPersonLook.LookEnabled = false;
            }

            yield return FallToGround();
            crtPass?.SetBlackout(1f);
            yield return new WaitForSeconds(2.5f);

            flow.Request(GamePhase.EndingE3, TransitionPriority.Normal,
                         string.IsNullOrEmpty(_e2OutcomeId) ? "First-person death completed." : _e2OutcomeId);
            flow.CommitPending();

            ApplyEndingPreset(true);
            cameraMode?.SwitchToFirstPerson();
            input?.SetGameplayEnabled(true);
            player?.SetInputLocked(true);
            if (firstPersonLook != null)
            {
                firstPersonLook.LookEnabled = true;
                firstPersonLook.ResetLook();
            }

            if (killer != null)
            {
                killer.gameObject.SetActive(true);
                if (endingKillerAnchor != null)
                {
                    killer.transform.SetPositionAndRotation(endingKillerAnchor.position, endingKillerAnchor.rotation);
                }
            }

            crtPass?.SetBlackout(0.15f);
            yield return new WaitForSeconds(1.2f);
            crtPass?.Pulse(1f);
            yield return new WaitForSeconds(0.35f);
            crtPass?.SetBlackout(1f);
            yield return new WaitForSeconds(0.8f);

            loop?.MarkEndingCompleted();
            _showTitleCard = true;
            crtPass?.SetTitleAmount(1f);
            crtPass?.SetBlackout(0.92f);
            flow.Request(GamePhase.Finished, TransitionPriority.Normal, "The clock advanced and the final shot landed.");
            flow.CommitPending();

            yield return new WaitForSeconds(4f);
            _showTitleCard = false;
            crtPass?.SetTitleAmount(0f);
            crtPass?.SetBlackout(0f);
            Time.timeScale = 1f;
            if (!string.IsNullOrWhiteSpace(menuSceneName))
            {
                SceneManager.LoadScene(menuSceneName, LoadSceneMode.Single);
            }

            _routine = null;
        }

        private IEnumerator FallToGround()
        {
            if (player == null)
            {
                yield break;
            }

            Transform target = player.transform;
            Quaternion start = target.rotation;
            Quaternion end = start * Quaternion.Euler(78f, 0f, 20f);
            float elapsed = 0f;
            while (elapsed < 1.2f)
            {
                elapsed += Time.deltaTime;
                target.rotation = Quaternion.Slerp(start, end, Mathf.SmoothStep(0f, 1f, elapsed / 1.2f));
                yield return null;
            }
        }

        private void ApplyEndingPreset(bool finalPass)
        {
            television?.SetEndingDestroyed();

            if (finalPass && player != null && endingChairAnchor != null)
            {
                player.Teleport(endingChairAnchor.position, endingChairAnchor.rotation);
            }
        }

        private void LockPlayer()
        {
            input?.SetGameplayEnabled(false);
            player?.SetInputLocked(true);
        }
    }
}
