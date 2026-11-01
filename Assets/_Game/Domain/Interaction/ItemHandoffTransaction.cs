using System;

namespace ConGame.Domain.Interaction
{

    public enum HandoffKind
    {
        PickUp = 0,
        Carry = 1,
        PutDown = 2,
        Throw = 3,
        Drop = 4
    }

    public enum HandoffPhase
    {
        Idle = 0,
        AttachPending = 1,
        DetachPending = 2
    }

    public enum HandoffCompletion
    {
        None = 0,
        CompletedByMarker = 1,
        CompletedByFallback = 2,
        Cancelled = 3
    }

    // Authoritative state machine for the Pickup/PutDown/Throw contract:
    // Started -> Attach/DetachRequested -> Completed/Cancelled. The real
    // reparenting only happens when a completion is observed (animation
    // marker or deterministic timer fallback); cancelling before completion
    // leaves item ownership untouched.
    public sealed class ItemHandoffTransaction
    {
        public HandoffPhase Phase { get; private set; } = HandoffPhase.Idle;
        public HandoffKind Kind { get; private set; } = HandoffKind.PickUp;
        public float StartedAt { get; private set; }
        public float FallbackDeadline { get; private set; }
        public bool IsPending => Phase != HandoffPhase.Idle;

        public static HandoffPhase PhaseFor(HandoffKind kind)
        {
            return kind is HandoffKind.PickUp or HandoffKind.Carry
                ? HandoffPhase.AttachPending
                : HandoffPhase.DetachPending;
        }

        public bool TryBegin(HandoffKind kind, float fallbackSeconds, float now)
        {
            if (IsPending)
            {
                return false;
            }

            Kind = kind;
            Phase = PhaseFor(kind);
            StartedAt = now;
            FallbackDeadline = now + Math.Max(0f, fallbackSeconds);
            return true;
        }

        public HandoffCompletion NotifyAttachMarker()
        {
            return Phase == HandoffPhase.AttachPending ? Complete(HandoffCompletion.CompletedByMarker)
                : HandoffCompletion.None;
        }

        public HandoffCompletion NotifyDetachMarker()
        {
            return Phase == HandoffPhase.DetachPending ? Complete(HandoffCompletion.CompletedByMarker)
                : HandoffCompletion.None;
        }

        public HandoffCompletion Advance(float now)
        {
            if (!IsPending || now < FallbackDeadline)
            {
                return HandoffCompletion.None;
            }

            return Complete(HandoffCompletion.CompletedByFallback);
        }

        public HandoffCompletion Cancel()
        {
            if (!IsPending)
            {
                return HandoffCompletion.None;
            }

            Reset();
            return HandoffCompletion.Cancelled;
        }

        private HandoffCompletion Complete(HandoffCompletion completion)
        {
            Reset();
            return completion;
        }

        private void Reset()
        {
            Phase = HandoffPhase.Idle;
            StartedAt = 0f;
            FallbackDeadline = 0f;
        }
    }
}
