using System;

namespace ConGame.Domain.Loop
{

    public readonly struct DarkAdaptationResult
    {
        public DarkAdaptationResult(int experienceGained, float partialSeconds)
        {
            ExperienceGained = experienceGained;
            PartialSeconds = partialSeconds;
        }

        public int ExperienceGained { get; }

        public float PartialSeconds { get; }
    }

    public sealed class DarkAdaptationTracker
    {
        public const int MaxExperience = 5;
        public const float SecondsPerExperience = 5f;

        public DarkAdaptationTracker(int initialExperience = 0)
        {
            Experience = Math.Max(0, Math.Min(MaxExperience, initialExperience));
        }

        public int Experience { get; private set; }

        public float PartialSeconds { get; private set; }

        public DarkAdaptationResult Advance(bool isPersistentBlack, float deltaTime)
        {
            if (deltaTime < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            }

            if (!isPersistentBlack || Experience >= MaxExperience)
            {
                PartialSeconds = 0f;
                return new DarkAdaptationResult(0, PartialSeconds);
            }

            PartialSeconds += deltaTime;
            int gained = 0;
            while (PartialSeconds >= SecondsPerExperience && Experience < MaxExperience)
            {
                PartialSeconds -= SecondsPerExperience;
                Experience++;
                gained++;
            }

            if (Experience >= MaxExperience)
            {
                PartialSeconds = 0f;
            }

            return new DarkAdaptationResult(gained, PartialSeconds);
        }

        public void ResetPartial()
        {
            PartialSeconds = 0f;
        }
    }
}
