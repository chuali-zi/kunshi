using System;
using System.Collections;
using ConGame.Runtime.Counterplay;
using ConGame.Runtime.Loop;
using ConGame.Runtime.Player;
using UnityEngine;

namespace ConGame.Presentation.M1
{

    [DisallowMultipleComponent]
    public sealed class M1GreyboxPerformancePlayer : MonoBehaviour, IPerformancePlayer, ILoopResettable
    {
        [SerializeField]
        private Transform killer;
        [SerializeField]
        private PlayerMotor playerMotor;
        [SerializeField]
        private PlayerInteractor playerInteractor;

        private Coroutine _routine;
        private Action _completed;

        public int ResetOrder => 25;

        public bool IsPlaying { get; private set; }

        public M1GreyboxDeathStyle ActiveStyle { get; private set; }

        public void Configure(Transform killerTransform, PlayerMotor motor, PlayerInteractor interactor)
        {
            killer = killerTransform;
            playerMotor = motor;
            playerInteractor = interactor;
        }

        public bool Play(CounterplayDefinitionAsset definition, Action completed)
        {
            if (definition == null || definition.Performance is not M1GreyboxPlayableAsset performance ||
                killer == null || playerMotor == null)
            {
                return false;
            }

            CancelActive();
            ActiveStyle = performance.Style;
            _completed = completed;
            IsPlaying = true;
            _routine = StartCoroutine(RunPerformance(performance));
            return true;
        }

        public void CancelAllForLoop(int loopEpoch)
        {
            CancelActive();
        }

        public void CaptureInitialState()
        {
        }

        public void Quiesce(in LoopResetContext context)
        {
            CancelActive();
        }

        public void Restore(in LoopResetContext context)
        {
            IsPlaying = false;
        }

        public void Resume(in LoopResetContext context)
        {
        }

        private IEnumerator RunPerformance(M1GreyboxPlayableAsset performance)
        {
            Transform player = playerMotor.transform;
            Vector3 killerStart = killer.position;
            Quaternion killerStartRotation = killer.rotation;
            Quaternion playerStartRotation = player.rotation;
            float duration = performance.SequenceDuration;
            float approachDuration = duration * 0.62f;

            if (performance.Style == M1GreyboxDeathStyle.ArmedStandoff)
            {
                playerInteractor?.ForceDropHeld();
            }

            Vector3 target = GetKillerTarget(performance.Style, player);
            float elapsed = 0f;
            while (elapsed < approachDuration)
            {
                elapsed += Time.deltaTime;
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

            Quaternion impactRotation = playerStartRotation * GetImpactRotation(performance.Style);
            float impactDuration = Mathf.Max(0.15f, duration - approachDuration);
            elapsed = 0f;
            while (elapsed < impactDuration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / impactDuration);
                player.rotation =
                    Quaternion.Slerp(playerStartRotation, impactRotation, Mathf.SmoothStep(0f, 1f, progress));
                yield return null;
            }

            player.rotation = impactRotation;
            _routine = null;
            IsPlaying = false;
            Action callback = _completed;
            _completed = null;
            callback?.Invoke();
        }

        private static Vector3 GetKillerTarget(M1GreyboxDeathStyle style, Transform player)
        {
            switch (style)
            {
                case M1GreyboxDeathStyle.SofaRearShot:
                    return player.position + player.right * 0.8f + player.forward * 0.45f;
                case M1GreyboxDeathStyle.ArmedStandoff:
                    return player.position + player.forward * 2.15f;
                default:
                    return player.position + player.forward * 1.35f;
            }
        }

        private static Quaternion GetImpactRotation(M1GreyboxDeathStyle style)
        {
            switch (style)
            {
                case M1GreyboxDeathStyle.SofaRearShot:
                    return Quaternion.Euler(78f, 0f, 18f);
                case M1GreyboxDeathStyle.ArmedStandoff:
                    return Quaternion.Euler(0f, 0f, -82f);
                default:
                    return Quaternion.Euler(0f, 0f, 78f);
            }
        }

        private void CancelActive()
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }

            IsPlaying = false;
            _completed = null;
        }
    }
}
