using System;
using UnityEngine;

namespace ConGame.Runtime.AI
{
    /// <summary>
    /// 区分「候选反制」与「已确认进入演出」的确认来源。
    /// 只有这三种来源允许锁入 PerformanceLocked 并启动正式 Performance。
    /// </summary>
    public enum PerformanceConfirmationKind
    {
        None = 0,
        AttackCommit = 1,
        TrapTrigger = 2,
        ConfirmedContact = 3
    }

    [Serializable]
    public struct PerformanceAnchorPose
    {
        public Vector3 Position;
        public Quaternion Rotation;
    }

    /// <summary>
    /// 双人演出的启动锚点对。确认型接触保留当帧双方的位置与朝向；
    /// 消费方(Presentation/M1 双人编排)负责把双方对齐到锚点,
    /// 启动误差超过 0.15m/10° 时按架构约定禁止播放并回退 D01。
    /// </summary>
    [Serializable]
    public struct PerformanceAnchorPair
    {
        public const float DefaultMaxPositionError = 0.15f;
        public const float DefaultMaxAngleErrorDegrees = 10f;

        public PerformanceAnchorPose Killer;
        public PerformanceAnchorPose Player;
        public float MaxPositionError;
        public float MaxAngleErrorDegrees;

        public static PerformanceAnchorPair CaptureCurrent(Transform killer, Transform player)
        {
            return new PerformanceAnchorPair
            {
                Killer = killer == null ? default : new PerformanceAnchorPose
                {
                    Position = killer.position,
                    Rotation = killer.rotation
                },
                Player = player == null ? default : new PerformanceAnchorPose
                {
                    Position = player.position,
                    Rotation = player.rotation
                },
                MaxPositionError = DefaultMaxPositionError,
                MaxAngleErrorDegrees = DefaultMaxAngleErrorDegrees
            };
        }

        public static PerformanceAnchorPair CreateFacing(
            Transform killer,
            Transform player,
            float maxPositionError = DefaultMaxPositionError,
            float maxAngleErrorDegrees = DefaultMaxAngleErrorDegrees)
        {
            PerformanceAnchorPair pair = new PerformanceAnchorPair
            {
                MaxPositionError = maxPositionError > 0f ? maxPositionError : DefaultMaxPositionError,
                MaxAngleErrorDegrees = maxAngleErrorDegrees > 0f
                    ? maxAngleErrorDegrees
                    : DefaultMaxAngleErrorDegrees
            };
            if (killer == null || player == null)
            {
                return pair;
            }

            Vector3 killerForward = HorizontalDirection(player.position - killer.position, killer.forward);
            Vector3 playerForward = HorizontalDirection(killer.position - player.position, player.forward);
            pair.Killer = new PerformanceAnchorPose
            {
                Position = killer.position,
                Rotation = Quaternion.LookRotation(killerForward, Vector3.up)
            };
            pair.Player = new PerformanceAnchorPose
            {
                Position = player.position,
                Rotation = Quaternion.LookRotation(playerForward, Vector3.up)
            };
            return pair;
        }

        public bool IsWithinLaunchTolerance(Transform killer, Transform player)
        {
            if (killer == null || player == null)
            {
                return false;
            }

            float positionTolerance = MaxPositionError > 0f ? MaxPositionError : DefaultMaxPositionError;
            float angleTolerance = MaxAngleErrorDegrees > 0f
                ? MaxAngleErrorDegrees
                : DefaultMaxAngleErrorDegrees;
            return HorizontalDistance(killer.position, Killer.Position) <= positionTolerance &&
                   HorizontalDistance(player.position, Player.Position) <= positionTolerance &&
                   Quaternion.Angle(killer.rotation, Killer.Rotation) <= angleTolerance &&
                   Quaternion.Angle(player.rotation, Player.Rotation) <= angleTolerance;
        }

        private static Vector3 HorizontalDirection(Vector3 offset, Vector3 fallbackForward)
        {
            offset.y = 0f;
            if (offset.sqrMagnitude < 0.0001f)
            {
                offset = fallbackForward;
                offset.y = 0f;
            }

            return offset.sqrMagnitude < 0.0001f ? Vector3.forward : offset.normalized;
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }

    /// <summary>
    /// 已确认演出的启动契约。携带原子冻结的 outcome、创建时 loopEpoch
    /// 和双方锚点;消费方在执行任何延时/编排前必须校验 LoopEpoch。
    /// </summary>
    public readonly struct ConfirmedPerformanceIntent
    {
        public readonly string OutcomeId;
        public readonly int LoopEpoch;
        public readonly PerformanceConfirmationKind Kind;
        public readonly PerformanceAnchorPair Anchors;

        public ConfirmedPerformanceIntent(
            string outcomeId,
            int loopEpoch,
            PerformanceConfirmationKind kind,
            in PerformanceAnchorPair anchors)
        {
            OutcomeId = outcomeId;
            LoopEpoch = loopEpoch;
            Kind = kind;
            Anchors = anchors;
        }

        public bool IsValid =>
            Kind != PerformanceConfirmationKind.None && !string.IsNullOrWhiteSpace(OutcomeId);
    }

    /// <summary>
    /// 正式 Performance 启动入口。由 Presentation/M1 的双人编排器实现;
    /// 返回 false 表示锚点超差或资产缺失,消费方自行回退 D01。
    /// </summary>
    public interface IPerformanceLaunchRequest
    {
        bool TryLaunch(in ConfirmedPerformanceIntent intent);

        void NotifyLoopEpoch(int loopEpoch);
    }

    public interface IPerformanceCancellable
    {
        void CancelAllForLoop(int loopEpoch);
    }
}
