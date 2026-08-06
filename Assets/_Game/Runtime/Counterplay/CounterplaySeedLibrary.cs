using System.Collections.Generic;
using ConGame.Domain.Counterplay;
using ConGame.Domain.Facts;
using ConGame.Domain.Interaction;

namespace ConGame.Runtime.Counterplay
{

    public readonly struct CounterplaySeed
    {
        public CounterplaySeed(
            string id,
            int order,
            CounterplayTriggerMode mode,
            ContentStatus status,
            params FactCondition[] conditions)
        {
            Id = id;
            SortOrder = order;
            TriggerMode = mode;
            Status = status;
            Conditions = conditions;
        }

        public string Id { get; }
        public int SortOrder { get; }
        public CounterplayTriggerMode TriggerMode { get; }
        public ContentStatus Status { get; }
        public FactCondition[] Conditions { get; }
    }

    public static class CounterplaySeedLibrary
    {
        private static FactCondition Eq(FactKey key, int value) => new FactCondition(key, FactOperator.Equal, value);
        private static FactCondition IsTrue(FactKey key) => new FactCondition(key, FactOperator.True, 1);
        private static FactCondition IsFalse(FactKey key) => new FactCondition(key, FactOperator.False, 0);
        private static FactCondition Has(FactKey key, int flags) => new FactCondition(key, FactOperator.HasAllFlags, flags);

        public static IReadOnlyList<CounterplaySeed> All { get; } = new[]
        {
            new CounterplaySeed("D21", 200, CounterplayTriggerMode.Reactive, ContentStatus.Implemented,
                Eq(FactKey.Trap, (int)TrapState.BlanketArmed), IsTrue(FactKey.TrapPulled), Eq(FactKey.RoomLight, (int)RoomLightState.PersistentBlack)),
            new CounterplaySeed("D22", 300, CounterplayTriggerMode.Reactive | CounterplayTriggerMode.Continuous, ContentStatus.Implemented,
                IsTrue(FactKey.HasStolenPistol)),
            new CounterplaySeed("D08", 400, CounterplayTriggerMode.Reactive, ContentStatus.Implemented,
                Eq(FactKey.PlayerZone, (int)PlayerZone.DoorBlind), Has(FactKey.HeldItemCategories, (int)ItemCategory.Weapon), IsTrue(FactKey.IsSwinging)),
            new CounterplaySeed("D20", 500, CounterplayTriggerMode.Entry | CounterplayTriggerMode.Continuous, ContentStatus.Implemented,
                Eq(FactKey.RoomLight, (int)RoomLightState.PersistentBlack), IsTrue(FactKey.IsAmbushing)),
            new CounterplaySeed("D19", 600, CounterplayTriggerMode.Entry | CounterplayTriggerMode.Continuous, ContentStatus.Implemented,
                Eq(FactKey.RoomLight, (int)RoomLightState.PersistentBlack)),
            new CounterplaySeed("D18", 700, CounterplayTriggerMode.Entry, ContentStatus.Implemented,
                Eq(FactKey.RoomLight, (int)RoomLightState.RecoverableDark)),
            new CounterplaySeed("D16", 800, CounterplayTriggerMode.Reactive, ContentStatus.Implemented,
                Eq(FactKey.Trap, (int)TrapState.RugArmed), IsTrue(FactKey.TrapPulled)),
            new CounterplaySeed("D17", 900, CounterplayTriggerMode.Reactive, ContentStatus.Implemented,
                IsTrue(FactKey.IsGlareActive)),
            new CounterplaySeed("D09", 1000, CounterplayTriggerMode.Entry, ContentStatus.Implemented,
                Eq(FactKey.DoorBlock, (int)DoorBlockState.Furniture)),
            new CounterplaySeed("D13", 1100, CounterplayTriggerMode.Entry, ContentStatus.Implemented,
                Eq(FactKey.DoorBlock, (int)DoorBlockState.Bookcase)),
            new CounterplaySeed("D11", 1200, CounterplayTriggerMode.Entry, ContentStatus.Implemented,
                Eq(FactKey.PlayerZone, (int)PlayerZone.UnderBed)),
            new CounterplaySeed("D04", 1300, CounterplayTriggerMode.Entry, ContentStatus.Implemented,
                Eq(FactKey.PlayerZone, (int)PlayerZone.Bed), Eq(FactKey.PlayerPose, (int)PlayerPose.Hidden)),
            new CounterplaySeed("D03P", 1400, CounterplayTriggerMode.Entry | CounterplayTriggerMode.Continuous, ContentStatus.Implemented,
                Eq(FactKey.PlayerZone, (int)PlayerZone.SofaRear), Eq(FactKey.PlayerPose, (int)PlayerPose.Crouched)),
            new CounterplaySeed("D03", 1500, CounterplayTriggerMode.Entry, ContentStatus.Implemented,
                Eq(FactKey.PlayerZone, (int)PlayerZone.SofaRear), Eq(FactKey.PlayerPose, (int)PlayerPose.Standing)),
            new CounterplaySeed("D12", 1600, CounterplayTriggerMode.Entry, ContentStatus.Implemented,
                Eq(FactKey.PlayerZone, (int)PlayerZone.CameraBlind)),
            new CounterplaySeed("D14", 1700, CounterplayTriggerMode.Entry, ContentStatus.Implemented,
                Eq(FactKey.PlayerPose, (int)PlayerPose.Prone)),
            new CounterplaySeed("D10", 1800, CounterplayTriggerMode.Reactive, ContentStatus.Implemented,
                IsTrue(FactKey.IncomingProjectile)),
            new CounterplaySeed("D06", 1900, CounterplayTriggerMode.Continuous, ContentStatus.Implemented,
                Eq(FactKey.PlayerPose, (int)PlayerPose.Kneeling)),
            new CounterplaySeed("D07", 2000, CounterplayTriggerMode.Entry | CounterplayTriggerMode.Continuous, ContentStatus.Implemented,
                Has(FactKey.HeldItemCategories, (int)ItemCategory.Weapon)),
            new CounterplaySeed("D05", 2100, CounterplayTriggerMode.Entry | CounterplayTriggerMode.Continuous, ContentStatus.Implemented,
                IsTrue(FactKey.IsStrikingWall)),
            new CounterplaySeed("D15", 2101, CounterplayTriggerMode.Entry | CounterplayTriggerMode.Continuous, ContentStatus.Implemented,
                IsTrue(FactKey.IsStrikingWindow)),
            new CounterplaySeed("D02", 2200, CounterplayTriggerMode.Entry, ContentStatus.Implemented,
                IsFalse(FactKey.HasMeaningfulInput), IsTrue(FactKey.IsAtAwakeningPoint)),
            new CounterplaySeed("D01", 2300, CounterplayTriggerMode.Fallback, ContentStatus.Implemented)
        };
    }
}
