namespace ConGame.Runtime.Ending
{

    public enum EndingStageId
    {
        None = 0,
        E2FirstPersonTakeover = 1,
        E2FreeControl = 2,
        E2HuntToDeath = 3,
        E2Fall = 4,
        E2Blackout = 5,
        E3SitChair = 6,
        E3KillerApproach = 7,
        E3ClockTick = 8,
        E3BackupShot = 9,
        E3Title = 10
    }

    public enum EndingSequenceState
    {
        Idle = 0,
        Running = 1,
        Completed = 2,
        Cancelled = 3
    }

    public enum EndingStageEventKind
    {
        Enter = 0,
        Exit = 1
    }

    public enum EndingStageExitReason
    {
        Completed = 0,
        Timeout = 1,
        Cancelled = 2
    }
}
