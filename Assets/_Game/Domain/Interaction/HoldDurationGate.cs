namespace ConGame.Domain.Interaction
{
    public struct HoldDurationGate
    {
        public bool IsPending { get; private set; }
        public float StartedAt { get; private set; }
        public float RequiredSeconds { get; private set; }

        public void Begin(float now, float requiredSeconds)
        {
            IsPending = true;
            StartedAt = now;
            RequiredSeconds = requiredSeconds < 0f ? 0f : requiredSeconds;
        }

        public bool IsComplete(float now)
        {
            return IsPending && now - StartedAt >= RequiredSeconds;
        }

        public void Cancel()
        {
            IsPending = false;
            StartedAt = 0f;
            RequiredSeconds = 0f;
        }
    }
}
