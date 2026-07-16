using System;
using ConGame.Runtime.Loop;
using UnityEngine;

namespace ConGame.Runtime.AI
{

    [DisallowMultipleComponent]
    public sealed class RoomDoorBreachController : MonoBehaviour, ILoopResettable
    {
        private const float DefaultDuration = 0.8f;
        private const float DefaultOpenAngle = 95f;
        private const float ShakeDuration = 0.2f;
        private const float ShakeAngle = 2.5f;
        private const float ShakeCycles = 3.5f;

        [SerializeField]
        private Transform door;
        [SerializeField, Min(0.01f)]
        private float duration = DefaultDuration;
        [SerializeField]
        private float openAngle = DefaultOpenAngle;

        private float _baseDuration = DefaultDuration;
        private Vector3 _initialLocalPosition;
        private Quaternion _initialLocalRotation;
        private Vector3 _initialLocalScale;
        private Action _completed;
        private float _elapsed;
        private int _loopEpoch;
        private bool _captured;

        public int ResetOrder => 20;

        public bool IsRunning { get; private set; }

        public float Progress => duration <= 0f ? 1f : Mathf.Clamp01(_elapsed / duration);

        public float CurrentDuration => duration;

        public void Configure(Transform doorTransform, float breachDuration, float doorOpenAngle)
        {
            door = doorTransform;
            _baseDuration = Mathf.Max(0.01f, breachDuration);
            duration = _baseDuration;
            openAngle = doorOpenAngle;
            _captured = false;
        }

        public void Begin(int loopEpoch, Action completed)
        {
            Begin(loopEpoch, completed, _baseDuration);
        }

        public void Begin(int loopEpoch, Action completed, float durationOverride)
        {
            EnsureInitialStateCaptured();
            Cancel();

            duration = Mathf.Max(0.01f, durationOverride);
            _loopEpoch = loopEpoch;
            _completed = completed;
            _elapsed = 0f;
            IsRunning = door != null;

            if (door == null)
            {
                Complete();
                return;
            }

            ApplyInitialPose();
        }

        private void Update()
        {
            Advance(Time.deltaTime);
        }

        public void Advance(float deltaTime)
        {
            if (!IsRunning || deltaTime <= 0f)
            {
                return;
            }

            _elapsed = Mathf.Min(_elapsed + deltaTime, duration);
            ApplyPose();

            if (_elapsed >= duration)
            {
                Complete();
            }
        }

        public void CaptureInitialState()
        {
            if (door == null)
            {
                _captured = false;
                return;
            }

            _initialLocalPosition = door.localPosition;
            _initialLocalRotation = door.localRotation;
            _initialLocalScale = door.localScale;
            _captured = true;
        }

        public void Quiesce(in LoopResetContext context)
        {
            Cancel();
            _loopEpoch = context.NextEpoch;
        }

        public void Restore(in LoopResetContext context)
        {
            ApplyInitialPose();
            _elapsed = 0f;
            duration = _baseDuration;
        }

        public void Resume(in LoopResetContext context)
        {
        }

        private void ApplyPose()
        {
            if (door == null || !_captured)
            {
                return;
            }

            float actualShakeDuration = Mathf.Min(ShakeDuration, duration * 0.4f);
            float yaw;
            if (_elapsed < actualShakeDuration)
            {
                float shakeProgress = actualShakeDuration <= 0f ? 1f : _elapsed / actualShakeDuration;
                float damping = 1f - shakeProgress;
                yaw = Mathf.Sin(shakeProgress * Mathf.PI * ShakeCycles) * ShakeAngle * damping;
            }
            else
            {
                float openingDuration = duration - actualShakeDuration;
                float openingProgress =
                    openingDuration <= 0f ? 1f : Mathf.Clamp01((_elapsed - actualShakeDuration) / openingDuration);
                yaw = Mathf.SmoothStep(0f, openAngle, openingProgress);
            }

            door.localPosition = _initialLocalPosition;
            door.localRotation = _initialLocalRotation * Quaternion.Euler(0f, yaw, 0f);
            door.localScale = _initialLocalScale;
        }

        private void ApplyInitialPose()
        {
            if (door == null || !_captured)
            {
                return;
            }

            door.localPosition = _initialLocalPosition;
            door.localRotation = _initialLocalRotation;
            door.localScale = _initialLocalScale;
        }

        private void Complete()
        {
            if (!IsRunning && _completed == null)
            {
                return;
            }

            IsRunning = false;
            _elapsed = duration;

            if (door != null && _captured)
            {
                door.localPosition = _initialLocalPosition;
                door.localRotation = _initialLocalRotation * Quaternion.Euler(0f, openAngle, 0f);
                door.localScale = _initialLocalScale;
            }

            Action callback = _completed;
            _completed = null;
            callback?.Invoke();
        }

        private void Cancel()
        {
            IsRunning = false;
            _completed = null;
        }

        private void EnsureInitialStateCaptured()
        {
            if (!_captured)
            {
                CaptureInitialState();
            }
        }
    }
}
