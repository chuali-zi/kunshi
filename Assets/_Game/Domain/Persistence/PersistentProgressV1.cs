using System;

namespace ConGame.Domain.Persistence
{

    [Serializable]
    public sealed class PersistentProgressV1
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public int loopIndex = 1;
        public int darkAdaptationXp;
        public bool d21Seen;
        public bool tvCrackEchoPending;
        public bool endingCompleted;

        public static PersistentProgressV1 CreateNew()
        {
            return new PersistentProgressV1();
        }

        public void Normalize()
        {
            schemaVersion = CurrentSchemaVersion;
            loopIndex = Math.Max(1, loopIndex);
            darkAdaptationXp = Math.Max(0, Math.Min(5, darkAdaptationXp));
        }

        public void AdvanceLoop()
        {
            loopIndex = Math.Max(1, loopIndex + 1);
        }
    }
}
