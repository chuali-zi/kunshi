using System;
using UnityEngine;

namespace ConGame.Runtime.Interaction
{

    // Deterministic timer fallbacks for the handoff transaction when an
    // animation clip or its Attach/Detach marker is missing. A value of 0
    // completes the transaction on the next frame.
    [Serializable]
    public struct InteractionHandoffSettings
    {
        [Min(0f)] public float AttachFallbackSeconds;
        [Min(0f)] public float DetachFallbackSeconds;
        [Min(0f)] public float ThrowFallbackSeconds;

        public static InteractionHandoffSettings Default
        {
            get
            {
                return new InteractionHandoffSettings
                {
                    AttachFallbackSeconds = 0.35f,
                    DetachFallbackSeconds = 0.3f,
                    ThrowFallbackSeconds = 0.2f
                };
            }
        }

        public InteractionHandoffSettings Validated()
        {
            InteractionHandoffSettings fallback = Default;
            AttachFallbackSeconds = AttachFallbackSeconds < 0f ? fallback.AttachFallbackSeconds
                : AttachFallbackSeconds;
            DetachFallbackSeconds = DetachFallbackSeconds < 0f ? fallback.DetachFallbackSeconds
                : DetachFallbackSeconds;
            ThrowFallbackSeconds = ThrowFallbackSeconds < 0f ? fallback.ThrowFallbackSeconds
                : ThrowFallbackSeconds;
            return this;
        }
    }
}
