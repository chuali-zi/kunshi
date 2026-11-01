using System;

namespace ConGame.Domain.Choreography
{
    /// <summary>
    /// Pure-data pose used by paired (player + killer) performances. Yaw is stored in
    /// degrees so this type stays free of any engine math dependency.
    /// </summary>
    [Serializable]
    public readonly struct PairedAnchorPose : IEquatable<PairedAnchorPose>
    {
        public PairedAnchorPose(float x, float y, float z, float yawDegrees)
        {
            X = x;
            Y = y;
            Z = z;
            YawDegrees = yawDegrees;
        }

        public float X { get; }
        public float Y { get; }
        public float Z { get; }
        public float YawDegrees { get; }

        public static float Distance(in PairedAnchorPose a, in PairedAnchorPose b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            float dz = a.Z - b.Z;
            return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        /// <summary>Shortest wrapped absolute yaw delta in degrees, range [0, 180].</summary>
        public static float YawDeltaDegrees(float a, float b)
        {
            float delta = (a - b) % 360f;
            if (delta > 180f)
            {
                delta -= 360f;
            }
            else if (delta < -180f)
            {
                delta += 360f;
            }

            return Math.Abs(delta);
        }

        public bool Equals(PairedAnchorPose other)
        {
            return X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z) &&
                   YawDegrees.Equals(other.YawDegrees);
        }

        public override bool Equals(object obj)
        {
            return obj is PairedAnchorPose other && Equals(other);
        }

        public override int GetHashCode()
        {
            return X.GetHashCode() ^ (Y.GetHashCode() << 2) ^ (Z.GetHashCode() >> 2) ^
                   (YawDegrees.GetHashCode() >> 1);
        }
    }

    /// <summary>
    /// Launch tolerances from gameplay_logic_spec §12: a paired performance may only
    /// start when both participants are within 0.15m and 10° of the authored anchor.
    /// </summary>
    [Serializable]
    public readonly struct PairedAnchorTolerance
    {
        public PairedAnchorTolerance(float maxPositionError, float maxYawErrorDegrees, float maxSeparationError)
        {
            MaxPositionError = Math.Max(0f, maxPositionError);
            MaxYawErrorDegrees = Math.Max(0f, maxYawErrorDegrees);
            MaxSeparationError = Math.Max(0f, maxSeparationError);
        }

        public float MaxPositionError { get; }
        public float MaxYawErrorDegrees { get; }
        public float MaxSeparationError { get; }

        public static PairedAnchorTolerance Default => new PairedAnchorTolerance(0.15f, 10f, 0.15f);
    }

    public enum PairedAnchorFailure
    {
        None = 0,
        PlayerPosition = 1,
        PlayerYaw = 2,
        KillerPosition = 3,
        KillerYaw = 4,
        Separation = 5
    }

    public readonly struct PairedAnchorValidationResult
    {
        public PairedAnchorValidationResult(
            PairedAnchorFailure failure,
            float playerPositionError,
            float playerYawError,
            float killerPositionError,
            float killerYawError,
            float separationError)
        {
            Failure = failure;
            PlayerPositionError = playerPositionError;
            PlayerYawError = playerYawError;
            KillerPositionError = killerPositionError;
            KillerYawError = killerYawError;
            SeparationError = separationError;
        }

        public PairedAnchorFailure Failure { get; }
        public float PlayerPositionError { get; }
        public float PlayerYawError { get; }
        public float KillerPositionError { get; }
        public float KillerYawError { get; }
        public float SeparationError { get; }
        public bool IsWithinTolerance => Failure == PairedAnchorFailure.None;
    }

    /// <summary>
    /// Validates both participants against the two authored anchor poses before a
    /// paired performance is allowed to start. Pure math; the presentation layer
    /// supplies world poses sampled from scene transforms.
    /// </summary>
    public static class PairedAnchorValidator
    {
        public static PairedAnchorValidationResult Validate(
            in PairedAnchorPose expectedPlayer,
            in PairedAnchorPose expectedKiller,
            in PairedAnchorPose actualPlayer,
            in PairedAnchorPose actualKiller,
            in PairedAnchorTolerance tolerance)
        {
            float playerPositionError = PairedAnchorPose.Distance(expectedPlayer, actualPlayer);
            float playerYawError = PairedAnchorPose.YawDeltaDegrees(
                expectedPlayer.YawDegrees, actualPlayer.YawDegrees);
            float killerPositionError = PairedAnchorPose.Distance(expectedKiller, actualKiller);
            float killerYawError = PairedAnchorPose.YawDeltaDegrees(
                expectedKiller.YawDegrees, actualKiller.YawDegrees);
            float expectedSeparation = PairedAnchorPose.Distance(expectedPlayer, expectedKiller);
            float actualSeparation = PairedAnchorPose.Distance(actualPlayer, actualKiller);
            float separationError = Math.Abs(actualSeparation - expectedSeparation);

            PairedAnchorFailure failure = PairedAnchorFailure.None;
            if (playerPositionError > tolerance.MaxPositionError)
            {
                failure = PairedAnchorFailure.PlayerPosition;
            }
            else if (playerYawError > tolerance.MaxYawErrorDegrees)
            {
                failure = PairedAnchorFailure.PlayerYaw;
            }
            else if (killerPositionError > tolerance.MaxPositionError)
            {
                failure = PairedAnchorFailure.KillerPosition;
            }
            else if (killerYawError > tolerance.MaxYawErrorDegrees)
            {
                failure = PairedAnchorFailure.KillerYaw;
            }
            else if (separationError > tolerance.MaxSeparationError)
            {
                failure = PairedAnchorFailure.Separation;
            }

            return new PairedAnchorValidationResult(
                failure,
                playerPositionError,
                playerYawError,
                killerPositionError,
                killerYawError,
                separationError);
        }
    }
}
