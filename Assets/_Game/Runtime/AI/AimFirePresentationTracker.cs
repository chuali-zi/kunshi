namespace ConGame.Runtime.AI
{
    public enum AimFirePresentationStage
    {
        Idle = 0,
        AimEnter = 1,
        AimHold = 2,
        Fire = 3
    }

    /// <summary>
    /// AimEnter → AimHold → Fire 语义入口的幂等守卫(纯 C#,不依赖场景)。
    /// 同一次瞄准会话内 AimEnter/Hold/Fire 各自最多放行一次,
    /// AimCancelled 只在瞄准会话存活时放行;E3 或重试路径的重复调用被吞掉。
    /// </summary>
    public sealed class AimFirePresentationTracker
    {
        public AimFirePresentationStage Stage { get; private set; } = AimFirePresentationStage.Idle;
        public int Epoch { get; private set; }

        public bool IsAimSessionActive =>
            Stage == AimFirePresentationStage.AimEnter || Stage == AimFirePresentationStage.AimHold;

        public bool TryBeginAim(int loopEpoch)
        {
            if (IsAimSessionActive)
            {
                return false;
            }

            Stage = AimFirePresentationStage.AimEnter;
            Epoch = loopEpoch;
            return true;
        }

        public bool TryEnterHold(int loopEpoch)
        {
            if (Stage != AimFirePresentationStage.AimEnter || loopEpoch != Epoch)
            {
                return false;
            }

            Stage = AimFirePresentationStage.AimHold;
            return true;
        }

        public bool TryCommitFire(int loopEpoch)
        {
            if (!IsAimSessionActive || loopEpoch != Epoch)
            {
                return false;
            }

            Stage = AimFirePresentationStage.Fire;
            return true;
        }

        public bool TryCancelAim(int loopEpoch)
        {
            if (!IsAimSessionActive || loopEpoch != Epoch)
            {
                return false;
            }

            Stage = AimFirePresentationStage.Idle;
            return true;
        }

        public void Reset()
        {
            Stage = AimFirePresentationStage.Idle;
        }
    }
}
