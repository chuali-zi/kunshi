using System.Collections.Generic;
using ConGame.Domain.Facts;
using ConGame.Runtime.Player;
using UnityEngine;

namespace ConGame.Runtime.Facts
{

    [RequireComponent(typeof(Collider))]
    public sealed class PlayerZoneTrigger : MonoBehaviour
    {
        private static readonly Dictionary<GameplayFactService, List<PlayerZoneTrigger>> ActiveZones =
            new Dictionary<GameplayFactService, List<PlayerZoneTrigger>>();

        [SerializeField] private PlayerZone zone = PlayerZone.Open;
        [SerializeField] private GameplayFactService facts;
        private readonly HashSet<int> _playerColliders = new HashSet<int>();

        public void Configure(PlayerZone targetZone, GameplayFactService factService)
        {
            zone = targetZone;
            facts = factService;
            Collider trigger = GetComponent<Collider>();
            trigger.isTrigger = true;
        }

        public static void ClearOccupancy(GameplayFactService factService)
        {
            if (factService == null || !ActiveZones.TryGetValue(factService, out List<PlayerZoneTrigger> zones))
            {
                return;
            }

            for (int i = 0; i < zones.Count; i++)
            {
                if (zones[i] != null)
                {
                    zones[i]._playerColliders.Clear();
                }
            }

            ActiveZones.Remove(factService);
            factService.SetPlayerZone(PlayerZone.Open);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (facts == null || other.GetComponentInParent<PlayerMotor>() == null ||
                !_playerColliders.Add(other.GetInstanceID()) || _playerColliders.Count != 1)
            {
                return;
            }

            RegisterActiveZone();
        }

        private void OnTriggerExit(Collider other)
        {
            if (facts == null || other.GetComponentInParent<PlayerMotor>() == null ||
                !_playerColliders.Remove(other.GetInstanceID()) || _playerColliders.Count != 0)
            {
                return;
            }

            UnregisterActiveZone();
        }

        private void OnDisable()
        {
            _playerColliders.Clear();
            UnregisterActiveZone();
        }

        private void RegisterActiveZone()
        {
            if (!ActiveZones.TryGetValue(facts, out List<PlayerZoneTrigger> zones))
            {
                zones = new List<PlayerZoneTrigger>();
                ActiveZones.Add(facts, zones);
            }

            zones.Remove(this);
            zones.Add(this);
            facts.SetPlayerZone(zone);
        }

        private void UnregisterActiveZone()
        {
            if (facts == null || !ActiveZones.TryGetValue(facts, out List<PlayerZoneTrigger> zones))
            {
                return;
            }

            zones.Remove(this);
            for (int i = zones.Count - 1; i >= 0; i--)
            {
                PlayerZoneTrigger active = zones[i];
                if (active != null && active.isActiveAndEnabled && active._playerColliders.Count > 0)
                {
                    facts.SetPlayerZone(active.zone);
                    return;
                }

                zones.RemoveAt(i);
            }

            ActiveZones.Remove(facts);
            facts.SetPlayerZone(PlayerZone.Open);
        }
    }
}
