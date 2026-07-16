using System;
using System.Collections;
using ConGame.Domain.Counterplay;
using ConGame.Runtime.Counterplay;
using ConGame.Runtime.Loop;
using ConGame.Runtime.Player;
using UnityEngine;

namespace ConGame.Presentation.M1
{
    [DisallowMultipleComponent]
    public sealed class M1GreyboxPerformancePlayer : MonoBehaviour, IPerformancePlayer, ILoopResettable,
        ICounterplayPerformanceHookTarget
    {
        [SerializeField] private Transform killer;
        [SerializeField] private PlayerMotor playerMotor;
        [SerializeField] private PlayerInteractor playerInteractor;

        private Coroutine _routine;
        private Action _completed;
        private ICounterplayPerformanceInput _performanceInput;
        private ICounterplayPerformanceProgress _progress;
        private QteSequenceCounter _qteCounter;
        private string _activeOutcomeId = string.Empty;
        private int _loopEpoch;
        private int _generation;

        public int ResetOrder => 25;
        public bool IsPlaying { get; private set; }
        public M1GreyboxDeathStyle ActiveStyle { get; private set; }
        public string ActiveOutcomeId => _activeOutcomeId;
        public int ActiveLoopEpoch => _loopEpoch;

        public void Configure(Transform killerTransform, PlayerMotor motor, PlayerInteractor interactor)
        {
            killer = killerTransform;
            playerMotor = motor;
            playerInteractor = interactor;
        }

        public void ConfigureHooks(
            ICounterplayPerformanceInput performanceInput,
            ICounterplayPerformanceProgress progress)
        {
            _performanceInput = performanceInput;
            _progress = progress;
        }

        public void PrepareLoopEpoch(int loopEpoch)
        {
            if (IsPlaying && _loopEpoch != loopEpoch)
            {
                CancelActive(true);
            }

            _loopEpoch = loopEpoch;
        }

        public bool Play(CounterplayDefinitionAsset definition, Action completed)
        {
            if (definition == null || definition.Performance is not M1GreyboxPlayableAsset performance ||
                killer == null || playerMotor == null)
            {
                return false;
            }

            CancelActive(true);
            _generation++;
            int generation = _generation;
            ActiveStyle = performance.Style;
            _activeOutcomeId = string.IsNullOrWhiteSpace(performance.OutcomeId)
                ? definition.OutcomeId
                : performance.OutcomeId;
            _completed = completed;
            float thresholdScale = _performanceInput == null ? 1f : _performanceInput.AccessibilityThresholdScale;
            _qteCounter = new QteSequenceCounter(performance.QteWindows, thresholdScale);
            IsPlaying = true;

            PerformanceHeldItemPolicy itemPolicy = performance.HeldItemPolicy;
            if (itemPolicy == PerformanceHeldItemPolicy.Preserve &&
                performance.Style == M1GreyboxDeathStyle.ArmedStandoff)
            {
                itemPolicy = PerformanceHeldItemPolicy.DropAtStart;
            }

            if (itemPolicy == PerformanceHeldItemPolicy.DropAtStart)
            {
                playerInteractor?.ForceDropHeld();
            }

            _progress?.OnPerformanceStarted(_activeOutcomeId, _loopEpoch, itemPolicy);
            _routine = StartCoroutine(RunPerformance(performance, generation));
            return true;
        }

        public void CancelAllForLoop(int loopEpoch)
        {
            if (!IsPlaying || _loopEpoch <= loopEpoch)
            {
                CancelActive(true);
            }

            _loopEpoch = Math.Max(_loopEpoch, loopEpoch + 1);
        }

        public void CaptureInitialState()
        {
        }

        public void Quiesce(in LoopResetContext context)
        {
            CancelAllForLoop(context.PreviousEpoch);
        }

        public void Restore(in LoopResetContext context)
        {
            IsPlaying = false;
            _activeOutcomeId = string.Empty;
            _qteCounter = null;
            _loopEpoch = context.NextEpoch;
        }

        public void Resume(in LoopResetContext context)
        {
        }

        private IEnumerator RunPerformance(M1GreyboxPlayableAsset performance, int generation)
        {
            Transform player = playerMotor.transform;
            Vector3 killerStart = killer.position;
            Quaternion killerStartRotation = killer.rotation;
            Quaternion playerStartRotation = player.rotation;
            float duration = Mathf.Max(performance.SequenceDuration, performance.TotalQteDuration);
            float approachDuration = duration * 0.62f;
            Vector3 target = GetKillerTarget(_activeOutcomeId, performance.Style, player);
            float elapsed = 0f;

            while (elapsed < approachDuration && generation == _generation)
            {
                float delta = Time.deltaTime;
                elapsed += delta;
                PublishQteProgress(delta);
                float progress = approachDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / approachDuration);
                float eased = Mathf.SmoothStep(0f, 1f, progress);
                killer.position = Vector3.Lerp(killerStart, target, eased);

                Vector3 lookDirection = player.position - killer.position;
                lookDirection.y = 0f;
                if (lookDirection.sqrMagnitude > 0.001f)
                {
                    Quaternion look = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
                    killer.rotation = Quaternion.Slerp(killerStartRotation, look, eased);
                }

                yield return null;
            }

            if (generation != _generation)
            {
                yield break;
            }

            Quaternion impactRotation = playerStartRotation * GetImpactRotation(_activeOutcomeId, performance.Style);
            float impactDuration = Mathf.Max(0.15f, duration - approachDuration);
            elapsed = 0f;
            while (elapsed < impactDuration && generation == _generation)
            {
                float delta = Time.deltaTime;
                elapsed += delta;
                PublishQteProgress(delta);
                float progress = Mathf.Clamp01(elapsed / impactDuration);
                player.rotation =
                    Quaternion.Slerp(playerStartRotation, impactRotation, Mathf.SmoothStep(0f, 1f, progress));
                yield return null;
            }

            if (generation != _generation)
            {
                yield break;
            }

            player.rotation = impactRotation;
            ResolveBranch(performance);
            _routine = null;
            IsPlaying = false;
            Action callback = _completed;
            _completed = null;
            callback?.Invoke();
        }

        private void PublishQteProgress(float deltaTime)
        {
            if (_qteCounter == null || _qteCounter.IsComplete)
            {
                return;
            }

            bool pressed = _performanceInput != null && _performanceInput.InteractIsPressed;
            QteSequenceSnapshot snapshot = _qteCounter.Advance(Mathf.Max(0f, deltaTime), pressed);
            _progress?.OnQteProgress(_activeOutcomeId, _loopEpoch, snapshot);
        }

        private void ResolveBranch(M1GreyboxPlayableAsset performance)
        {
            if (performance == null || _qteCounter == null)
            {
                return;
            }

            bool mechanicalSuccess = _qteCounter.AllWindowsSucceeded;
            if (performance.TryResolveBranch(mechanicalSuccess, out PerformanceBranchDefinition branch))
            {
                _progress?.OnBranchResolved(
                    _activeOutcomeId,
                    _loopEpoch,
                    branch,
                    CounterplayNarrativeOutcome.ScriptedFailure);
            }
        }

        private static Vector3 GetKillerTarget(
            string outcomeId,
            M1GreyboxDeathStyle style,
            Transform player)
        {
            switch (style)
            {
                case M1GreyboxDeathStyle.SofaRearShot:
                    return player.position + player.right * 0.8f + player.forward * 0.45f;
                case M1GreyboxDeathStyle.ArmedStandoff:
                    return player.position + player.forward * 2.15f;
                default:
                    int ordinal = GetOutcomeOrdinal(outcomeId);
                    float angle = ((ordinal % 7) - 3) * 11f;
                    float distance = 1.05f + (ordinal % 4) * 0.22f;
                    Vector3 offset = Quaternion.Euler(0f, angle, 0f) * player.forward * distance;
                    return player.position + offset;
            }
        }

        private static Quaternion GetImpactRotation(string outcomeId, M1GreyboxDeathStyle style)
        {
            switch (style)
            {
                case M1GreyboxDeathStyle.SofaRearShot:
                    return Quaternion.Euler(78f, 0f, 18f);
                case M1GreyboxDeathStyle.ArmedStandoff:
                    return Quaternion.Euler(0f, 0f, -82f);
                default:
                    int ordinal = GetOutcomeOrdinal(outcomeId);
                    float pitch = ordinal % 3 == 0 ? 72f : 0f;
                    float roll = ordinal % 2 == 0 ? -76f : 76f;
                    return Quaternion.Euler(pitch, 0f, roll);
            }
        }

        private static int GetOutcomeOrdinal(string outcomeId)
        {
            if (string.IsNullOrEmpty(outcomeId))
            {
                return 1;
            }

            int value = 0;
            for (int i = 0; i < outcomeId.Length; i++)
            {
                char character = outcomeId[i];
                if (character >= '0' && character <= '9')
                {
                    value = value * 10 + character - '0';
                }
            }

            return Math.Max(1, value);
        }

        private void CancelActive(bool notifyProgress = false)
        {
            bool wasPlaying = IsPlaying;
            string cancelledOutcomeId = _activeOutcomeId;
            int cancelledEpoch = _loopEpoch;
            _generation++;
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }

            IsPlaying = false;
            _completed = null;
            _qteCounter = null;
            _activeOutcomeId = string.Empty;
            if (notifyProgress && wasPlaying)
            {
                _progress?.OnPerformanceCancelled(cancelledOutcomeId, cancelledEpoch);
            }
        }
    }
}
