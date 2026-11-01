using ConGame.Domain.Interaction;
using ConGame.Runtime.Loop;
using UnityEngine;

namespace ConGame.Runtime.Interaction
{
    [DisallowMultipleComponent]
    public sealed class ClockTwitchBehaviour : MonoBehaviour, ILoopResettable
    {
        [SerializeField] private Transform secondHand;
        [SerializeField] private StatefulWorldItem clockItem;
        [SerializeField] private float periodSeconds = 0.5f;
        [SerializeField] private float twitchDegrees = 1.5f;

        private ClockTwitchState _state;
        private Quaternion _initialLocalRotation;
        private bool _captured;

        public int ResetOrder => 76;
        public bool IsSilenced => _state.Silenced;

        public void Configure(Transform hand, StatefulWorldItem item, float period = 0.5f)
        {
            secondHand = hand;
            clockItem = item;
            periodSeconds = period;
            _state = new ClockTwitchState(periodSeconds);
        }

        private void Update()
        {
            if (clockItem != null && clockItem.CurrentState == ItemPhysicalState.Broken)
            {
                _state.Silence();
                return;
            }

            int ticks = _state.ConsumeTicks(Time.deltaTime);
            if (secondHand == null)
            {
                return;
            }

            for (int i = 0; i < ticks; i++)
            {
                secondHand.localRotation *= Quaternion.Euler(0f, 0f, -twitchDegrees);
            }
        }

        public void CaptureInitialState()
        {
            _state = new ClockTwitchState(periodSeconds <= 0f ? 0.5f : periodSeconds);
            if (secondHand != null)
            {
                _initialLocalRotation = secondHand.localRotation;
            }

            _captured = true;
        }

        public void Quiesce(in LoopResetContext context)
        {
        }

        public void Restore(in LoopResetContext context)
        {
            if (!_captured)
            {
                CaptureInitialState();
            }

            _state.Reset();
            if (secondHand != null)
            {
                secondHand.localRotation = _initialLocalRotation;
            }
        }

        public void Resume(in LoopResetContext context)
        {
        }
    }
}
