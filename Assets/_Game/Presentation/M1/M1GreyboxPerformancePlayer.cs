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

            int expectedCancelGeneration = _generation + 1;
            CancelActive(true);
            if (_generation != expectedCancelGeneration || IsPlaying)
            {
                return true;
            }

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
                if (generation != _generation)
                {
                    return true;
                }
            }

            _progress?.OnPerformanceStarted(_activeOutcomeId, _loopEpoch, itemPolicy);
            if (generation != _generation)
            {
                return true;
            }

            _routine = StartCoroutine(RunPerformance(performance, generation));
            return true;
        }

        public void CancelAllForLoop(int loopEpoch)
        {
            if (!IsPlaying || _loopEpoch <= loopEpoch)
            {
                CancelActive(true);
            }

            _loopEpoch = Math.Max(_loopEpoch, loopEpoch);
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
            OutcomeBeatProfile profile = GetOutcomeProfile(_activeOutcomeId);
            float approachDuration = duration * profile.ApproachFraction;
            float holdDuration = duration * profile.HoldFraction;
            Vector3 target = player.position + player.right * profile.LateralOffset +
                             player.forward * profile.ForwardOffset;
            float elapsed = 0f;

            while (elapsed < approachDuration && generation == _generation)
            {
                float delta = Time.deltaTime;
                elapsed += delta;
                PublishQteProgress(delta);
                if (generation != _generation)
                {
                    yield break;
                }

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

            elapsed = 0f;
            while (elapsed < holdDuration && generation == _generation)
            {
                float delta = Time.deltaTime;
                elapsed += delta;
                PublishQteProgress(delta);
                if (generation != _generation)
                {
                    yield break;
                }

                float pulse = Mathf.Sin(elapsed * profile.HoldPulseSpeed) * profile.HoldPulseDistance;
                killer.position = target + killer.right * pulse;
                yield return null;
            }

            Quaternion impactRotation = playerStartRotation * Quaternion.Euler(profile.ImpactEuler);
            float impactDuration = Mathf.Max(0.15f, duration - approachDuration - holdDuration);
            elapsed = 0f;
            while (elapsed < impactDuration && generation == _generation)
            {
                float delta = Time.deltaTime;
                elapsed += delta;
                PublishQteProgress(delta);
                if (generation != _generation)
                {
                    yield break;
                }

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
            bool completionDeferred = ResolveBranch(performance);
            if (generation != _generation)
            {
                yield break;
            }

            _routine = null;
            if (completionDeferred)
            {
                yield break;
            }

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

        private bool ResolveBranch(M1GreyboxPlayableAsset performance)
        {
            if (performance == null || _qteCounter == null)
            {
                return false;
            }

            bool mechanicalSuccess = _qteCounter.AllWindowsSucceeded;
            if (performance.TryResolveBranch(mechanicalSuccess, out PerformanceBranchDefinition branch))
            {
                return _progress != null && _progress.OnBranchResolved(
                    _activeOutcomeId,
                    _loopEpoch,
                    branch,
                    CounterplayNarrativeOutcome.ScriptedFailure);
            }

            return false;
        }

        private static OutcomeBeatProfile GetOutcomeProfile(string outcomeId)
        {
            switch (outcomeId)
            {
                case "D01": return new OutcomeBeatProfile(-0.10f, 1.05f, 0.46f, 0.08f, 58f, -4f, -74f, 6f, 0.018f);
                case "D02": return new OutcomeBeatProfile(0.38f, 1.18f, 0.52f, 0.06f, 12f, 18f, 78f, 7f, 0.025f);
                case "D03": return new OutcomeBeatProfile(0.82f, 0.48f, 0.42f, 0.18f, 78f, -12f, 22f, 4f, 0.012f);
                case "D03P": return new OutcomeBeatProfile(-0.76f, 0.62f, 0.68f, 0.20f, 84f, 16f, -18f, 2f, 0.035f);
                case "D04": return new OutcomeBeatProfile(-0.55f, 1.32f, 0.55f, 0.07f, 0f, -22f, -82f, 8f, 0.020f);
                case "D05": return new OutcomeBeatProfile(0.18f, 0.88f, 0.38f, 0.15f, 66f, 8f, 64f, 5f, 0.030f);
                case "D06": return new OutcomeBeatProfile(0f, 1.52f, 0.64f, 0.12f, 88f, 0f, -8f, 3f, 0.014f);
                case "D07": return new OutcomeBeatProfile(-0.34f, 2.05f, 0.35f, 0.22f, 4f, 10f, -86f, 9f, 0.022f);
                case "D08": return new OutcomeBeatProfile(0.22f, 1.72f, 0.40f, 0.16f, 0f, -14f, 82f, 10f, 0.028f);
                case "D09": return new OutcomeBeatProfile(0.62f, 1.06f, 0.58f, 0.05f, 74f, 24f, 36f, 6f, 0.016f);
                case "D10": return new OutcomeBeatProfile(-0.92f, 1.42f, 0.44f, 0.11f, 52f, -28f, -58f, 11f, 0.034f);
                case "D11": return new OutcomeBeatProfile(0.46f, 0.72f, 0.49f, 0.14f, 82f, 6f, 12f, 4f, 0.021f);
                case "D12": return new OutcomeBeatProfile(-0.24f, 1.24f, 0.61f, 0.04f, 18f, 32f, -80f, 12f, 0.019f);
                case "D13": return new OutcomeBeatProfile(0.70f, 1.58f, 0.36f, 0.19f, 62f, -18f, 48f, 5f, 0.032f);
                case "D14": return new OutcomeBeatProfile(-0.66f, 0.96f, 0.57f, 0.09f, 86f, 14f, -30f, 8f, 0.024f);
                case "D15": return new OutcomeBeatProfile(0.12f, 1.38f, 0.47f, 0.13f, 34f, -34f, 72f, 7f, 0.027f);
                case "D16": return new OutcomeBeatProfile(0.96f, 1.84f, 0.53f, 0.10f, 6f, 38f, 84f, 9f, 0.015f);
                case "D17": return new OutcomeBeatProfile(-1.02f, 1.12f, 0.41f, 0.17f, 70f, -6f, -44f, 13f, 0.038f);
                case "D18": return new OutcomeBeatProfile(0.30f, 0.82f, 0.63f, 0.03f, 80f, 26f, 28f, 6f, 0.011f);
                case "D19": return new OutcomeBeatProfile(-0.44f, 1.66f, 0.34f, 0.24f, 24f, -40f, -68f, 4f, 0.029f);
                case "D20": return new OutcomeBeatProfile(0.56f, 1.28f, 0.59f, 0.08f, 64f, 12f, 56f, 10f, 0.023f);
                case "D21": return new OutcomeBeatProfile(-0.18f, 2.18f, 0.28f, 0.30f, 8f, -8f, -88f, 3f, 0.040f);
                case "D22": return new OutcomeBeatProfile(0.26f, 2.32f, 0.24f, 0.34f, 2f, 20f, 86f, 14f, 0.045f);
                default: return new OutcomeBeatProfile(0f, 1.1f, 0.55f, 0.08f, 70f, 0f, 70f, 6f, 0.02f);
            }
        }

        private readonly struct OutcomeBeatProfile
        {
            public OutcomeBeatProfile(
                float lateralOffset,
                float forwardOffset,
                float approachFraction,
                float holdFraction,
                float impactPitch,
                float impactYaw,
                float impactRoll,
                float holdPulseSpeed,
                float holdPulseDistance)
            {
                LateralOffset = lateralOffset;
                ForwardOffset = forwardOffset;
                ApproachFraction = approachFraction;
                HoldFraction = holdFraction;
                ImpactEuler = new Vector3(impactPitch, impactYaw, impactRoll);
                HoldPulseSpeed = holdPulseSpeed;
                HoldPulseDistance = holdPulseDistance;
            }

            public float LateralOffset { get; }
            public float ForwardOffset { get; }
            public float ApproachFraction { get; }
            public float HoldFraction { get; }
            public Vector3 ImpactEuler { get; }
            public float HoldPulseSpeed { get; }
            public float HoldPulseDistance { get; }
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
