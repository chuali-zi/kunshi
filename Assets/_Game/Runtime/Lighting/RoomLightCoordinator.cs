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
            UnbindEvents();
            ceilingLight = ceiling;
            tableLight = table;
            facts = factService;
            if (isActiveAndEnabled)
            {
                BindEvents();
            }
        }

        private void OnEnable()
        {
            BindEvents();
        }

        private void BindEvents()
        {
            UnbindEvents();
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
            UnbindEvents();
        }

        private void UnbindEvents()
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
            // RecoverableDark means the killer can physically reach and turn the
            // table lamp back on. A carried/unplugged lamp is not that state;
            // keep it out of the D18 candidate instead of advertising a remote
            // repair that cannot happen.
            else if (!tableLight.IsPowered && tableLight.IsCableConnected && !tableLight.IsDraped)
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
