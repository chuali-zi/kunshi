namespace ConGame.Runtime.Ending
{

    public readonly struct EndingStageDefinition
    {
        public EndingStageDefinition(
            EndingStageId id,
            float durationSeconds,
            bool waitForExternalCompletion = false,
            float timeoutSeconds = 0f)
        {
            Id = id;
            DurationSeconds = durationSeconds < 0f ? 0f : durationSeconds;
            WaitForExternalCompletion = waitForExternalCompletion;
            TimeoutSeconds = timeoutSeconds < 0f ? 0f : timeoutSeconds;
        }

        public EndingStageId Id { get; }

        public float DurationSeconds { get; }

        /// <summary>为 true 时阶段不按时长自动退出,由权威回调(如 EndingFightCompleted)显式完成。</summary>
        public bool WaitForExternalCompletion { get; }

        /// <summary>等待外部完成时的兜底超时秒数;0 表示无限等待。</summary>
        public float TimeoutSeconds { get; }
    }

    public readonly struct EndingStageEvent
    {
        private EndingStageEvent(EndingStageId stage, EndingStageEventKind kind, EndingStageExitReason exitReason)
        {
            Stage = stage;
            Kind = kind;
            ExitReason = exitReason;
        }

        public EndingStageId Stage { get; }

        public EndingStageEventKind Kind { get; }

        /// <summary>仅当 Kind == Exit 时有意义。</summary>
        public EndingStageExitReason ExitReason { get; }

        public static EndingStageEvent Entered(EndingStageId stage)
        {
            return new EndingStageEvent(stage, EndingStageEventKind.Enter, EndingStageExitReason.Completed);
        }

        public static EndingStageEvent Exited(EndingStageId stage, EndingStageExitReason reason)
        {
            return new EndingStageEvent(stage, EndingStageEventKind.Exit, reason);
        }
    }
}
