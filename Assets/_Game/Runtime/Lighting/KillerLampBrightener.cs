using ConGame.Domain.Facts;
using ConGame.Domain.Interaction;
using ConGame.Runtime.Facts;
using ConGame.Runtime.Interaction;
using UnityEngine;

namespace ConGame.Runtime.Lighting
{
    [DisallowMultipleComponent]
    public sealed class KillerLampBrightener : MonoBehaviour
    {
        [SerializeField] private GameplayFactService facts;
        [SerializeField] private BreakableLight tableLamp;
        [SerializeField] private Transform killer;
        [SerializeField] private float contactDistance = 1.8f;
        [SerializeField] private float interactionHeight = 1.1f;
        [SerializeField] private LayerMask obstructionMask = ~0;

        public void Configure(GameplayFactService factService, BreakableLight lamp)
        {
            facts = factService;
            tableLamp = lamp;
            killer = transform;
        }

        public void TryBrightenForRecoverableDark()
        {
            if (facts == null || tableLamp == null || killer == null ||
                tableLamp.IsBroken || !tableLamp.IsCableConnected || tableLamp.IsDraped)
            {
                return;
            }

            StatefulWorldItem item = tableLamp.GetComponent<StatefulWorldItem>();
            if (item != null && item.CurrentState == ItemPhysicalState.Held)
            {
                return;
            }

            if (Vector3.Distance(killer.position, tableLamp.transform.position) > contactDistance)
            {
                return;
            }

            if (!HasClearAccessToLamp())
            {
                return;
            }

            if (facts.CaptureSnapshot().RoomLight == RoomLightState.RecoverableDark)
            {
                tableLamp.SetSwitchOn(true);
            }
        }

        private bool HasClearAccessToLamp()
        {
            Vector3 origin = killer.position + Vector3.up * interactionHeight;
            Collider lampCollider = tableLamp.GetComponentInChildren<Collider>();
            Vector3 target = lampCollider == null
                ? tableLamp.transform.position
                : lampCollider.bounds.center;
            Vector3 delta = target - origin;
            float distance = delta.magnitude;
            if (distance <= 0.01f)
            {
                return true;
            }

            RaycastHit[] hits = Physics.RaycastAll(
                origin,
                delta / distance,
                distance,
                obstructionMask,
                QueryTriggerInteraction.Ignore);
            float nearestDistance = float.PositiveInfinity;
            Transform nearest = null;
            for (int i = 0; i < hits.Length; i++)
            {
                Transform candidate = hits[i].transform;
                if (candidate == null || candidate == killer || candidate.IsChildOf(killer))
                {
                    continue;
                }

                if (hits[i].distance < nearestDistance)
                {
                    nearestDistance = hits[i].distance;
                    nearest = candidate;
                }
            }

            return nearest == null || nearest == tableLamp.transform || nearest.IsChildOf(tableLamp.transform);
        }

        private void Update()
        {
            if (facts != null && tableLamp != null && facts.CaptureSnapshot().RoomLight == RoomLightState.RecoverableDark)
            {
                TryBrightenForRecoverableDark();
            }
        }
    }
}
