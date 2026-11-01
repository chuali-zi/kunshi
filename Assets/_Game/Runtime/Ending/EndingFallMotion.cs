using System;

namespace ConGame.Runtime.Ending
{

    /// <summary>E2 倒地采样结果:pitch 下压、视点高度下降、轻微 roll。</summary>
    public readonly struct EndingFallPose
    {
        public EndingFallPose(float pitchDownDegrees, float heightDropMeters, float rollDegrees)
        {
            PitchDownDegrees = pitchDownDegrees;
            HeightDropMeters = heightDropMeters;
            RollDegrees = rollDegrees;
        }

        public float PitchDownDegrees { get; }

        public float HeightDropMeters { get; }

        public float RollDegrees { get; }
    }

    /// <summary>
    /// E2 第一人称倒地的程序化相机曲线。纯 C# 采样,避免剧烈滚转/晕动:
    /// roll 只在倒地中段达到受限峰值并回收,pitch 与高度单调收敛。
    /// </summary>
    public static class EndingFallMotion
    {
        public static EndingFallPose Sample(float t, EndingSequenceSettings settings)
        {
            EndingSequenceSettings s = settings == null
                ? EndingSequenceSettings.Default
                : settings.CloneNormalized();
            float clamped = Clamp01(t);

            float pitch = s.FallPitchDegrees * SmoothStep(clamped);
            // 高度按加速下落收敛(t^1.6),末段不弹跳。
            float height = s.FallHeightDropMeters * (float)Math.Pow(clamped, 1.6);
            // roll 中段峰值 0.75 倍 + 末端 0.25 倍残量,总量永不超过 FallRollDegrees。
            float roll = s.FallRollDegrees *
                         ((float)Math.Sin(Math.PI * clamped) * 0.75f + SmoothStep(clamped) * 0.25f);
            return new EndingFallPose(pitch, height, roll);
        }

        private static float SmoothStep(float t)
        {
            return t * t * (3f - 2f * t);
        }

        private static float Clamp01(float value)
        {
            if (value < 0f)
            {
                return 0f;
            }

            return value > 1f ? 1f : value;
        }
    }
}
