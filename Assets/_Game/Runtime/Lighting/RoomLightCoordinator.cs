using ConGame.Domain.Facts;
using ConGame.Runtime.Facts;
using ConGame.Runtime.Loop;
using UnityEngine;

namespace ConGame.Runtime.Lighting
{
    [DisallowMultipleComponent]
    public sealed class RoomLightCoordinator : MonoBehaviour, ILoopResettable
    {
        [SerializeField] private BreakableLight ceilingLight;
        [SerializeField] private BreakableLight tableLight;
        [SerializeField] private GameplayFactService facts;

        public int ResetOrder => 59;
        public RoomLightState CurrentState { get; private set; } = RoomLightState.Lit;

        public void Configure(BreakableLight ceiling, BreakableLight table, GameplayFactService factService)
        {
            ceilingLight = ceiling;
            tableLight = table;
            facts = factService;
        }

        private void OnEnable()
        {
            if (ceilingLight != null)
            {
                ceilingLight.BrokenChanged += OnLightChanged;
            }

            if (tableLight != null)
            {
                tableLight.BrokenChanged += OnLightChanged;
            }
        }

        private void Start()
        {
            Recompute();
        }

        private void OnDisable()
        {
            if (ceilingLight != null)
            {
                ceilingLight.BrokenChanged -= OnLightChanged;
            }

            if (tableLight != null)
            {
                tableLight.BrokenChanged -= OnLightChanged;
            }
        }

        private void OnLightChanged(BreakableLight source, bool broken)
        {
            Recompute();
        }

        public RoomLightState Recompute()
        {
            if (ceilingLight == null || tableLight == null)
            {
                return CurrentState;
            }

            if (!ceilingLight.IsBroken)
            {
                CurrentState = RoomLightState.Lit;
            }
            else if (tableLight.IsBroken)
            {
                CurrentState = RoomLightState.PersistentBlack;
            }
            else if (!tableLight.IsPowered)
            {
                CurrentState = RoomLightState.RecoverableDark;
            }
            else
            {
                CurrentState = RoomLightState.Dim;
            }

            facts?.SetRoomLight(CurrentState);
            return CurrentState;
        }

        public void CaptureInitialState()
        {
            Recompute();
        }

        public void Quiesce(in LoopResetContext context)
        {
        }

        public void Restore(in LoopResetContext context)
        {
        }

        public void Resume(in LoopResetContext context)
        {
            Recompute();
        }
    }
}
