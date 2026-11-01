using System.Collections.Generic;
using ConGame.Domain.Interaction;
using UnityEngine;

namespace ConGame.Runtime.Interaction
{

    // Marker volume for the shard slow zone a fragile item leaves on break.
    // This component only owns the data and the trigger volume; the slow
    // itself is consumed by movement systems (PlayerMotor limp, killer
    // navigation avoidance) which are separate owners. Consumer contract:
    // while IsZoneActive, anything standing inside the trigger should apply
    // MovementMultiplier for LimpSeconds after leaving.
    [DisallowMultipleComponent]
    public sealed class ShardSlowZoneVolume : MonoBehaviour
    {
        private static readonly List<ShardSlowZoneVolume> Active = new List<ShardSlowZoneVolume>(4);

        [SerializeField] private SphereCollider triggerVolume;

        public static int ActiveCount => Active.Count;

        public static ShardSlowZoneVolume ActiveAt(int index)
        {
            return index >= 0 && index < Active.Count ? Active[index] : null;
        }

        public float Radius { get; private set; } = 0.45f;
        public float MovementMultiplier { get; private set; } = 0.55f;
        public float LimpSeconds { get; private set; } = 2f;
        public bool IsZoneActive { get; private set; }
        public Collider TriggerVolume => triggerVolume;

        public void Configure(in ShardSlowZoneSpec spec)
        {
            Radius = spec.Radius;
            MovementMultiplier = spec.MovementMultiplier;
            LimpSeconds = spec.LimpSeconds;
            EnsureTrigger();
            triggerVolume.radius = spec.Radius;
            triggerVolume.isTrigger = true;
            SetZoneActive(false);
        }

        public void SetZoneActive(bool active)
        {
            IsZoneActive = active;
            EnsureTrigger();
            triggerVolume.enabled = active;
            if (active)
            {
                if (!Active.Contains(this))
                {
                    Active.Add(this);
                }
            }
            else
            {
                Active.Remove(this);
            }
        }

        private void Awake()
        {
            EnsureTrigger();
            SetZoneActive(false);
        }

        private void OnDestroy()
        {
            Active.Remove(this);
        }

        private void EnsureTrigger()
        {
            if (triggerVolume == null)
            {
                triggerVolume = GetComponent<SphereCollider>();
            }

            if (triggerVolume == null)
            {
                triggerVolume = gameObject.AddComponent<SphereCollider>();
            }
        }
    }
}
