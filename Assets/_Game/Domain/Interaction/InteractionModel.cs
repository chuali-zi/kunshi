using System;

namespace ConGame.Domain.Interaction
{

    [Flags]
    public enum InteractionVerb
    {
        None = 0,
        Inspect = 1 << 0,
        PickUp = 1 << 1,
        Use = 1 << 2,
        Throw = 1 << 3,
        Swing = 1 << 4,
        Carry = 1 << 5,
        Hide = 1 << 6,
        Place = 1 << 7
    }

    [Flags]
    public enum ItemCategory
    {
        None = 0,
        Small = 1 << 0,
        Heavy = 1 << 1,
        Weapon = 1 << 2,
        Throwable = 1 << 3,
        LightSource = 1 << 4,
        DoorBlocker = 1 << 5,
        TripTrap = 1 << 6,
        Firearm = 1 << 7,
        EndingTool = 1 << 8
    }

    public enum ImpactKind
    {
        Light = 0,
        Heavy = 1,
        Projectile = 2,
        Bullet = 3
    }

    public enum ContentStatus
    {
        Specified = 0,
        Stub = 1,
        Implemented = 2
    }
}
