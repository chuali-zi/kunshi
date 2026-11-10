using System;
using ConGame.Runtime.Loop;
using UnityEngine;

namespace ConGame.Runtime.AI
{
    public enum RoomDoorBreachPhase
    {
        Idle = 0,
        KickWindup = 1,
        AwaitingContact = 2,
        DoorOpening = 3,
        Completed = 4
    }

    /// <summary>
    /// 踹门破门:KickWindup(晃动)→ KickContact(动画事件或确定性回退)→ DoorOpen。
    /// 门只在 KickContact 语义帧之后才打开;完成回调保持与 Breach 子段的既有契约
    /// (门破演出期间锁输入,回调后恢复自由移动)。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoomDoorBreachController : MonoBehaviour, ILoopResettable
    {
        private const float DefaultDuration = 0.8f;
        private const float DefaultOpenAngle = 95f;
        private const float ShakeAngle = 2.5f;
        private const float ShakeCycles = 3.5f;
        private const float MinOpeningSeconds = 0.05f;

        [SerializeField]
        private Transform door;
        [SerializeField, Min(0.01f)]
        private float duration = DefaultDuration;
        [SerializeField]
        private float openAngle = DefaultOpenAngle;
        [SerializeField]
        private BreachKickTimingSettings kickTiming = BreachKickTimingSettings.Default;

        private float _baseDuration = DefaultDuration;
        private Vector3 _initialLocalPosition;
        private Quaternion _initialLocalRotation;
        private Vector3 _initialLocalScale;
        private Action _completed;
        private float _elapsed;
        private float _contactElapsed = -1f;
        private int _loopEpoch;
        private bool _captured;

        public int ResetOrder => 20;

        public bool IsRunning { get; private set; }

        public RoomDoorBreachPhase Phase { get; private set; } = RoomDoorBreachPhase.Idle;

        public float Progress => duration <= 0f ? 1f : Mathf.Clamp01(_elapsed / duration);

        public float CurrentDuration => duration;

        public bool HasContactOccurred => _contactElapsed >= 0f;

        public event Action<int> KickWindupStarted;
        public event Action<int> KickContact;

        private void Awake()
        {
            // Scene deserialization does not call Configure. Reset must restore
            // the authored duration rather than the field initializer's default.
            _baseDuration = Mathf.Max(0.01f, duration);
        }

        public void Configure(Transform doorTransform, float breachDuration, float doorOpenAngle)
        {
            door = doorTransform;
            _baseDuration = Mathf.Max(0.01f, breachDuration);
            duration = _baseDuration;
            openAngle = doorOpenAngle;
            _captured = false;
        }

        public void ConfigureKickTiming(in BreachKickTimingSettings settings)
        {
            kickTiming = settings.Validated();
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
            _contactElapsed = -1f;
            IsRunning = door != null;

            if (door == null)
            {
                Complete();
                return;
            }

            ApplyInitialPose();
            SetPhase(RoomDoorBreachPhase.KickWindup);
            KickWindupStarted?.Invoke(loopEpoch);
        }

        /// <summary>
        /// 动画事件入口:AN_Killer_KickDoor 的接触帧由此驱动真实开门。
        /// 非运行中、epoch 不匹配或接触已发生时拒绝,保证接触只生效一次。
        /// </summary>
        public bool NotifyKickContact(int loopEpoch)
        {
            if (!IsRunning || loopEpoch != _loopEpoch || _contactElapsed >= 0f)
            {
                return false;
            }

            if (Phase != RoomDoorBreachPhase.KickWindup && Phase != RoomDoorBreachPhase.AwaitingContact)
            {
                return false;
            }

            EnterContact();
            return true;
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

            if (_contactElapsed < 0f)
            {
                if (_elapsed >= EffectiveContactFallbackSeconds())
                {
                    EnterContact();
                }
                else if (_elapsed >= EffectiveWindupSeconds() && Phase == RoomDoorBreachPhase.KickWindup)
                {
                    SetPhase(RoomDoorBreachPhase.AwaitingContact);
                }
            }

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
            _contactElapsed = -1f;
            duration = _baseDuration;
            SetPhase(RoomDoorBreachPhase.Idle);
        }

        public void Resume(in LoopResetContext context)
        {
        }

        private float EffectiveWindupSeconds()
        {
            return Mathf.Min(kickTiming.Validated().WindupSeconds, duration * 0.5f);
        }

        private float EffectiveContactFallbackSeconds()
        {
            BreachKickTimingSettings timing = kickTiming.Validated();
            return Mathf.Min(Mathf.Max(timing.ContactFallbackSeconds, EffectiveWindupSeconds()), duration);
        }

        private void EnterContact()
        {
            _contactElapsed = _elapsed;
            SetPhase(RoomDoorBreachPhase.DoorOpening);
            KickContact?.Invoke(_loopEpoch);
        }

        private void SetPhase(RoomDoorBreachPhase phase)
        {
            Phase = phase;
        }

        private void ApplyPose()
        {
            if (door == null || !_captured)
            {
                return;
            }

            float yaw;
            if (_contactElapsed < 0f)
            {
                float windupSeconds = EffectiveWindupSeconds();
                if (_elapsed < windupSeconds)
                {
                    float shakeProgress = windupSeconds <= 0f ? 1f : _elapsed / windupSeconds;
                    float damping = 1f - shakeProgress;
                    yaw = Mathf.Sin(shakeProgress * Mathf.PI * ShakeCycles) * ShakeAngle * damping;
                }
                else
                {
                    // 接触前保持门关闭:等待动画接触帧或回退计时。
                    yaw = 0f;
                }
            }
            else
            {
                float openingDuration = Mathf.Max(MinOpeningSeconds, duration - _contactElapsed);
                float openingProgress = Mathf.Clamp01((_elapsed - _contactElapsed) / openingDuration);
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

            SetPhase(RoomDoorBreachPhase.Completed);
            Action callback = _completed;
            _completed = null;
            callback?.Invoke();
        }

        private void Cancel()
        {
            IsRunning = false;
            _completed = null;
            _contactElapsed = -1f;
            SetPhase(RoomDoorBreachPhase.Idle);
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
