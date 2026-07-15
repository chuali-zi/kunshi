using ConGame.Domain.Facts;
using ConGame.Runtime.Facts;
using UnityEngine;

namespace ConGame.Runtime.Lighting
{

    [DisallowMultipleComponent]
    public sealed class RoomLightCoordinator : MonoBehaviour
    {
        [SerializeField] private BreakableLight ceilingLight;
        [SerializeField] private BreakableLight tableLight;
        [SerializeField] private GameplayFactService facts;

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

        private void Recompute()
        {
            if (facts == null || ceilingLight == null || tableLight == null)
            {
                return;
            }

            RoomLightState state;
            if (ceilingLight.IsBroken && tableLight.IsBroken)
            {
                state = RoomLightState.PersistentBlack;
            }
            else if (ceilingLight.IsBroken || tableLight.IsBroken)
            {
                state = RoomLightState.Dim;
            }
            else
            {
                state = RoomLightState.Lit;
            }

            facts.SetRoomLight(state);
        }
    }
}
