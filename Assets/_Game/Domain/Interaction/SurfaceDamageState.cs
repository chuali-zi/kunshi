namespace ConGame.Domain.Interaction
{
    public struct SurfaceDamageState
    {
        public SurfaceDamageState(int maxHits, bool revealBrickAtCap = false)
        {
            MaxHits = maxHits < 1 ? 1 : maxHits;
            Hits = 0;
            RevealBrickAtCap = revealBrickAtCap;
            BrickRevealed = false;
        }

        public int MaxHits { get; }
        public int Hits { get; private set; }
        public bool RevealBrickAtCap { get; }
        public bool BrickRevealed { get; private set; }
        public bool IsCapped => Hits >= MaxHits;

        public bool RegisterHit()
        {
            if (IsCapped)
            {
                return false;
            }

            Hits++;
            if (RevealBrickAtCap && IsCapped)
            {
                BrickRevealed = true;
            }

            return true;
        }

        public void Reset()
        {
            Hits = 0;
            BrickRevealed = false;
        }
    }
}
