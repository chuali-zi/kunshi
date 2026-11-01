using System;

namespace ConGame.Runtime.Ending
{

    /// <summary>结局 E2/E3 每拍时长与倒地曲线参数。纯数据,不依赖 UnityEngine。</summary>
    [Serializable]
    public sealed class EndingSequenceSettings
    {
        public float E2TakeoverSeconds = 0.1f;
        public float E2FreeControlSeconds = 4f;
        public float E2FightTimeoutSeconds = 8f;
        public float E2FallSeconds = 1.2f;
        public float E2BlackoutSeconds = 3f;

        public float FallPitchDegrees = 68f;
        public float FallHeightDropMeters = 1.35f;
        public float FallRollDegrees = 9f;

        public float E3SitChairSeconds = 0.4f;
        public float E3KillerApproachSeconds = 1.7f;
        public float E3ClockTickSeconds = 1.25f;
        public int E3ClockTickCount = 3;
        public float E3ShotSilenceSeconds = 5f;
        public float E3TitleHoldSeconds = 4f;
        public float E3YawLimitDegrees = 55f;

        public static EndingSequenceSettings Default => new EndingSequenceSettings();

        public EndingSequenceSettings CloneNormalized()
        {
            EndingSequenceSettings copy = (EndingSequenceSettings)MemberwiseClone();
            copy.Normalize();
            return copy;
        }

        public void Normalize()
        {
            E2TakeoverSeconds = ClampNonNegative(E2TakeoverSeconds);
            E2FreeControlSeconds = ClampNonNegative(E2FreeControlSeconds);
            E2FightTimeoutSeconds = ClampNonNegative(E2FightTimeoutSeconds);
            E2FallSeconds = ClampNonNegative(E2FallSeconds);
            E2BlackoutSeconds = ClampNonNegative(E2BlackoutSeconds);
            FallPitchDegrees = Clamp(FallPitchDegrees, 0f, 85f);
            FallHeightDropMeters = ClampNonNegative(FallHeightDropMeters);
            FallRollDegrees = Clamp(FallRollDegrees, 0f, 15f);
            E3SitChairSeconds = ClampNonNegative(E3SitChairSeconds);
            E3KillerApproachSeconds = ClampNonNegative(E3KillerApproachSeconds);
            E3ClockTickSeconds = ClampNonNegative(E3ClockTickSeconds);
            E3ClockTickCount = E3ClockTickCount < 0 ? 0 : E3ClockTickCount;
            E3ShotSilenceSeconds = ClampNonNegative(E3ShotSilenceSeconds);
            E3TitleHoldSeconds = ClampNonNegative(E3TitleHoldSeconds);
            E3YawLimitDegrees = Clamp(E3YawLimitDegrees, 0f, 180f);
        }

        private static float ClampNonNegative(float value)
        {
            return value < 0f ? 0f : value;
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }
    }
}
