namespace ConGame.Domain.Interaction
{
    public struct ClockTwitchState
    {
        public ClockTwitchState(float periodSeconds)
        {
            Period = periodSeconds <= 0f ? 0.5f : periodSeconds;
            Accumulated = 0f;
            Silenced = false;
        }

        public float Period { get; }
        public float Accumulated { get; private set; }
        public bool Silenced { get; private set; }

        public int ConsumeTicks(float deltaSeconds)
        {
            if (Silenced || deltaSeconds <= 0f)
            {
                return 0;
            }

            Accumulated += deltaSeconds;
            int ticks = 0;
            while (Accumulated >= Period)
            {
                Accumulated -= Period;
                ticks++;
            }

            return ticks;
        }

        public void Silence()
        {
            Silenced = true;
            Accumulated = 0f;
        }

        public void Reset()
        {
            Accumulated = 0f;
            Silenced = false;
        }
    }
}
