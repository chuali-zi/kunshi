using ConGame.Domain.Facts;
using ConGame.Runtime.Player;
using UnityEngine;

namespace ConGame.Runtime.Facts
{

    [RequireComponent(typeof(Collider))]
    public sealed class PlayerZoneTrigger : MonoBehaviour
    {
        [SerializeField] private PlayerZone zone = PlayerZone.Open;
        [SerializeField] private GameplayFactService facts;

        public void Configure(PlayerZone targetZone, GameplayFactService factService)
        {
            zone = targetZone;
            facts = factService;
            Collider trigger = GetComponent<Collider>();
            trigger.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (facts != null && other.GetComponentInParent<PlayerMotor>() != null)
            {
                facts.SetPlayerZone(zone);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (facts != null && other.GetComponentInParent<PlayerMotor>() != null)
            {
                facts.SetPlayerZone(PlayerZone.Open);
            }
        }
    }
}
