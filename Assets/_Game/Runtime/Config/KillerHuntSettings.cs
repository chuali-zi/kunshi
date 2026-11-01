using System;
using UnityEngine;

namespace ConGame.Runtime.Config
{
    [Serializable]
    public struct KillerHuntSettings
    {
        [Range(30f, 180f)] public float ViewAngle;
        [Min(0.5f)] public float LitViewDistance;
        [Min(0.25f)] public float DarkViewDistance;
        [Min(0.25f)] public float HiddenSearchRevealDistance;
        [Min(0f)] public float DetectionSeconds;
        [Min(0.05f)] public float AimSeconds;
        [Min(0.5f)] public float ShootRange;
        [Min(0.5f)] public float StandoffDistance;
        [Min(0.1f)] public float SearchSeconds;
        [Min(0f)] public float InvestigatePauseSeconds;
        [Min(0.5f)] public float NormalHearingRadius;
        [Min(0.5f)] public float LoudHearingRadius;
        [Min(0.1f)] public float PatrolSpeed;
        [Min(0.1f)] public float PursuitSpeed;
        [Min(0.1f)] public float SprintInterruptSpeed;
        [Min(0.01f)] public float ShotImpactDelay;
        [Min(0.05f)] public float HitRecoverySeconds;
        [Min(0.1f)] public float RepathSeconds;

        public static KillerHuntSettings Default
        {
            get
            {
                return new KillerHuntSettings
                {
                    ViewAngle = 120f,
                    LitViewDistance = 12f,
                    DarkViewDistance = 1.5f,
                    HiddenSearchRevealDistance = 1.35f,
                    DetectionSeconds = 0.3f,
                    AimSeconds = 0.7f,
                    ShootRange = 4.25f,
                    StandoffDistance = 3f,
                    SearchSeconds = 8f,
                    InvestigatePauseSeconds = 1.2f,
                    NormalHearingRadius = 12f,
                    LoudHearingRadius = 30f,
                    PatrolSpeed = 1.25f,
                    PursuitSpeed = 2.25f,
                    SprintInterruptSpeed = 5.5f,
                    ShotImpactDelay = 0.18f,
                    HitRecoverySeconds = 0.55f,
                    RepathSeconds = 0.45f
                };
            }
        }

        public KillerHuntSettings Validated()
        {
            KillerHuntSettings fallback = Default;
            ViewAngle = ViewAngle <= 0f ? fallback.ViewAngle : Mathf.Clamp(ViewAngle, 30f, 180f);
            LitViewDistance = LitViewDistance <= 0f ? fallback.LitViewDistance : LitViewDistance;
            DarkViewDistance = DarkViewDistance <= 0f ? fallback.DarkViewDistance : DarkViewDistance;
            HiddenSearchRevealDistance = HiddenSearchRevealDistance <= 0f
                ? fallback.HiddenSearchRevealDistance
                : HiddenSearchRevealDistance;
            DetectionSeconds = DetectionSeconds <= 0f ? fallback.DetectionSeconds : DetectionSeconds;
            AimSeconds = AimSeconds <= 0f ? fallback.AimSeconds : AimSeconds;
            ShootRange = ShootRange <= 0f ? fallback.ShootRange : ShootRange;
            StandoffDistance = StandoffDistance <= 0f ? fallback.StandoffDistance : StandoffDistance;
            SearchSeconds = SearchSeconds <= 0f ? fallback.SearchSeconds : SearchSeconds;
            InvestigatePauseSeconds = InvestigatePauseSeconds <= 0f
                ? fallback.InvestigatePauseSeconds
                : InvestigatePauseSeconds;
            NormalHearingRadius = NormalHearingRadius <= 0f
                ? fallback.NormalHearingRadius
                : NormalHearingRadius;
            LoudHearingRadius = LoudHearingRadius <= 0f ? fallback.LoudHearingRadius : LoudHearingRadius;
            PatrolSpeed = PatrolSpeed <= 0f ? fallback.PatrolSpeed : PatrolSpeed;
            PursuitSpeed = PursuitSpeed <= 0f ? fallback.PursuitSpeed : PursuitSpeed;
            SprintInterruptSpeed = SprintInterruptSpeed <= 0f
                ? fallback.SprintInterruptSpeed
                : SprintInterruptSpeed;
            ShotImpactDelay = ShotImpactDelay <= 0f ? fallback.ShotImpactDelay : ShotImpactDelay;
            HitRecoverySeconds = HitRecoverySeconds <= 0f
                ? fallback.HitRecoverySeconds
                : HitRecoverySeconds;
            RepathSeconds = RepathSeconds <= 0f ? fallback.RepathSeconds : RepathSeconds;
            return this;
        }
    }
}
