using System;
using ConGame.Domain.Interaction;

namespace ConGame.Domain.Facts
{

    public enum PlayerZone
    {
        Open = 0,
        Awakening = 1,
        SofaRear = 2,
        DoorBlind = 3,
        CameraBlind = 4,
        UnderBed = 5,
        Bed = 6,
        Other = 7
    }

    public enum PlayerPose
    {
        Sitting = 0,
        Standing = 1,
        Crouched = 2,
        Hidden = 3,
        Prone = 4,
        Kneeling = 5,
        Carrying = 6,
        Scripted = 7
    }

    public enum RoomLightState
    {
        Lit = 0,
        Dim = 1,
        RecoverableDark = 2,
        PersistentBlack = 3
    }

    public enum DoorBlockState
    {
        None = 0,
        Furniture = 1,
        Bookcase = 2
    }

    public enum TrapState
    {
        None = 0,
        RugArmed = 1,
        BlanketArmed = 2
    }

    public enum TelevisionDamageState
    {
        Intact = 0,
        Cracked = 1,
        Destroyed = 2
    }

    public enum FactKey
    {
        PlayerZone = 0,
        PlayerPose = 1,
        HeldItemCategories = 2,
        RoomLight = 3,
        DoorBlock = 4,
        Trap = 5,
        TrapPulled = 6,
        HasStolenPistol = 7,
        IsAmbushing = 8,
        IsMoving = 9,
        IsGlareActive = 10,
        IsStrikingWall = 11,
        IsStrikingWindow = 12,
        HasMeaningfulInput = 13,
        TelevisionDamage = 14,
        IsSwinging = 15,
        IsAtAwakeningPoint = 16,
        IncomingProjectile = 17
    }

    public enum FactOperator
    {
        Equal = 0,
        NotEqual = 1,
        True = 2,
        False = 3,
        HasAllFlags = 4,
        HasAnyFlags = 5
    }

    public enum KillerStimulusType
    {
        Entry = 0,
        IncomingProjectile = 1,
        TrapPull = 2,
        Glare = 3,
        TelevisionHeavyHit = 4,
        PlayerExposed = 5
    }

    [Serializable]
    public struct FactSnapshot
    {
        public PlayerZone PlayerZone;
        public PlayerPose PlayerPose;
        public ItemCategory HeldItemCategories;
        public RoomLightState RoomLight;
        public DoorBlockState DoorBlock;
        public TrapState Trap;
        public bool TrapPulled;
        public bool HasStolenPistol;
        public bool IsAmbushing;
        public bool IsMoving;
        public bool IsGlareActive;
        public bool IsStrikingWall;
        public bool IsStrikingWindow;
        public bool HasMeaningfulInput;
        public TelevisionDamageState TelevisionDamage;
        public bool IsSwinging;
        public bool IsAtAwakeningPoint;
        public bool IncomingProjectile;

        public int GetInt(FactKey key)
        {
            switch (key)
            {
                case FactKey.PlayerZone:
                    return (int)PlayerZone;
                case FactKey.PlayerPose:
                    return (int)PlayerPose;
                case FactKey.HeldItemCategories:
                    return (int)HeldItemCategories;
                case FactKey.RoomLight:
                    return (int)RoomLight;
                case FactKey.DoorBlock:
                    return (int)DoorBlock;
                case FactKey.Trap:
                    return (int)Trap;
                case FactKey.TrapPulled:
                    return TrapPulled ? 1 : 0;
                case FactKey.HasStolenPistol:
                    return HasStolenPistol ? 1 : 0;
                case FactKey.IsAmbushing:
                    return IsAmbushing ? 1 : 0;
                case FactKey.IsMoving:
                    return IsMoving ? 1 : 0;
                case FactKey.IsGlareActive:
                    return IsGlareActive ? 1 : 0;
                case FactKey.IsStrikingWall:
                    return IsStrikingWall ? 1 : 0;
                case FactKey.IsStrikingWindow:
                    return IsStrikingWindow ? 1 : 0;
                case FactKey.HasMeaningfulInput:
                    return HasMeaningfulInput ? 1 : 0;
                case FactKey.TelevisionDamage:
                    return (int)TelevisionDamage;
                case FactKey.IsSwinging:
                    return IsSwinging ? 1 : 0;
                case FactKey.IsAtAwakeningPoint:
                    return IsAtAwakeningPoint ? 1 : 0;
                case FactKey.IncomingProjectile:
                    return IncomingProjectile ? 1 : 0;
                default:
                    throw new ArgumentOutOfRangeException(nameof(key), key, null);
            }
        }

        public static FactSnapshot CreateBaseline()
        {
            return new FactSnapshot
            {
                PlayerZone = PlayerZone.Awakening,
                PlayerPose = PlayerPose.Standing,
                RoomLight = RoomLightState.Lit,
                DoorBlock = DoorBlockState.None,
                Trap = TrapState.None,
                TelevisionDamage = TelevisionDamageState.Intact,
                IsAtAwakeningPoint = true
            };
        }
    }

    [Serializable]
    public struct FactCondition
    {
        public FactCondition(FactKey key, FactOperator @operator, int expected)
        {
            Key = key;
            Operator = @operator;
            Expected = expected;
        }

        public FactKey Key;
        public FactOperator Operator;
        public int Expected;

        public bool Matches(in FactSnapshot snapshot)
        {
            int actual = snapshot.GetInt(Key);
            switch (Operator)
            {
                case FactOperator.Equal:
                    return actual == Expected;
                case FactOperator.NotEqual:
                    return actual != Expected;
                case FactOperator.True:
                    return actual != 0;
                case FactOperator.False:
                    return actual == 0;
                case FactOperator.HasAllFlags:
                    return (actual & Expected) == Expected;
                case FactOperator.HasAnyFlags:
                    return (actual & Expected) != 0;
                default:
                    return false;
            }
        }
    }

    public readonly struct KillerStimulus
    {
        public KillerStimulus(KillerStimulusType type, string sourceId = "")
        {
            Type = type;
            SourceId = sourceId ?? string.Empty;
        }

        public KillerStimulusType Type { get; }

        public string SourceId { get; }
    }
}
