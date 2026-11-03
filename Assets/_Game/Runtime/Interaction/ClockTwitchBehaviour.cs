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
        private bool _twitchDisplaced;
        private int _endingTicks;

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
            if (_endingTicks > 0) return;

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
                _twitchDisplaced = !_twitchDisplaced;
            }
            secondHand.localRotation = _initialLocalRotation * Quaternion.Euler(
                0f, 0f, _twitchDisplaced ? -twitchDegrees : 0f);
        }

        // E3 releases the same stuck hand; regular twitch updates must no longer
        // pull it back to the loop's mark after this first real tick.
        public void AdvanceForEnding()
        {
            if (!_captured) CaptureInitialState();
            _endingTicks++;
            if (secondHand != null)
                secondHand.localRotation = _initialLocalRotation * Quaternion.Euler(0f, 0f, -6f * _endingTicks);
        }

        public void CaptureInitialState()
        {
            _state = new ClockTwitchState(periodSeconds <= 0f ? 0.5f : periodSeconds);
            if (secondHand != null)
            {
                _initialLocalRotation = secondHand.localRotation;
            }

            _captured = true;
            _twitchDisplaced = false;
            _endingTicks = 0;
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
            _twitchDisplaced = false;
            _endingTicks = 0;
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
