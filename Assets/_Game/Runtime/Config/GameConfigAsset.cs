using ConGame.Domain.Loop;
using UnityEngine;

namespace ConGame.Runtime.Config
{

    [CreateAssetMenu(menuName = "ConGame/Config/Game Config", fileName = "CFG_Game")]
    public sealed class GameConfigAsset : ScriptableObject
    {
        [Header("Entry clock")]
        [SerializeField, Min(1f)]
        private float idleEntrySeconds = 45f;
        [SerializeField, Min(1f)]
        private float activeEntrySeconds = 30f;
        [SerializeField, Min(0.1f)]
        private float loudNoiseDelaySeconds = 8f;
        [SerializeField, Min(0f)]
        private float warningLeadSeconds = 0.5f;

        [Header("Player")]
        [SerializeField, Min(0.1f)]
        private float walkSpeed = 2.8f;
        [SerializeField, Min(0.1f)]
        private float sprintSpeed = 4.5f;
        [SerializeField, Min(1f)]
        private float turnSpeedDegrees = 720f;
        [SerializeField, Min(0.1f)]
        private float interactionRadius = 1.2f;
        [SerializeField, Min(0.1f)]
        private float throwSpeed = 8f;

        [Header("M1 pacing")]
        [SerializeField, Min(0.1f)]
        private float killerCounterplayDelay = 2.5f;
        [SerializeField, Min(0.1f)]
        private float breachDuration = 0.9f;
        [SerializeField, Range(45f, 140f)]
        private float breachOpenAngle = 105f;

        public float WalkSpeed => walkSpeed;
        public float SprintSpeed => sprintSpeed;
        public float TurnSpeedDegrees => turnSpeedDegrees;
        public float InteractionRadius => interactionRadius;
        public float ThrowSpeed => throwSpeed;

        public float BreachDuration => breachDuration;
        public float BreachOpenAngle => breachOpenAngle;
        public float KillerCounterplayDelay => killerCounterplayDelay;

        public EntryClockSettings CreateEntryClockSettings()
        {
            return new EntryClockSettings(idleEntrySeconds, activeEntrySeconds, loudNoiseDelaySeconds,
                                          warningLeadSeconds);
        }
    }
}
