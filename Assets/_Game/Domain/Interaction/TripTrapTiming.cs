using ConGame.Domain.Facts;

namespace ConGame.Domain.Interaction
{
    public static class TripTrapTiming
    {
        public static bool CanPull(bool occupantPresent, TrapState armedState, bool alreadyPulled)
        {
            return occupantPresent && !alreadyPulled &&
                   (armedState == TrapState.BlanketArmed || armedState == TrapState.RugArmed);
        }

        public static bool ShouldAutoPull(bool occupantPresent, TrapState armedState, bool alreadyPulled)
        {
            return CanPull(occupantPresent, armedState, alreadyPulled);
        }
    }
}
