namespace ConGame.Domain.Interaction
{
    public static class SurrenderGate
    {
        public const float HoldSeconds = 0.6f;
        public const float MinFacingDot = 0.35f;

        public static bool CanSurrender(bool standing, bool emptyHanded, bool killerEntered, float facingDot)
        {
            return standing && emptyHanded && killerEntered && facingDot >= MinFacingDot;
        }
    }
}
