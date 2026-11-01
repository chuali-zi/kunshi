using ConGame.Domain.Facts;

namespace ConGame.Domain.Interaction
{
    public static class AmbushFactDerivation
    {
        public static bool IsAmbushing(PlayerZone zone, PlayerPose pose, bool moving)
        {
            if (moving)
            {
                return false;
            }

            bool cover = zone == PlayerZone.DoorBlind || zone == PlayerZone.SofaRear;
            bool low = pose == PlayerPose.Crouched || pose == PlayerPose.Hidden;
            return cover && low;
        }
    }
}
