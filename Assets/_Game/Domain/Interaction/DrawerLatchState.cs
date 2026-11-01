namespace ConGame.Domain.Interaction
{
    public struct DrawerLatchState
    {
        public DrawerLatchState(int requiredPulls)
        {
            RequiredPulls = requiredPulls < 1 ? 1 : requiredPulls;
            Pulls = 0;
            IsOpen = false;
        }

        public int RequiredPulls { get; }
        public int Pulls { get; private set; }
        public bool IsOpen { get; private set; }

        public bool TryPull()
        {
            if (IsOpen)
            {
                return false;
            }

            Pulls++;
            if (Pulls < RequiredPulls)
            {
                return false;
            }

            IsOpen = true;
            return true;
        }

        public void Reset()
        {
            Pulls = 0;
            IsOpen = false;
        }
    }
}
