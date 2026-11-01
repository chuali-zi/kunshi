using System;
using UnityEngine;

namespace ConGame.Runtime.Interaction
{

    // Semantic marker source for the handoff transaction contract. Animation
    // clips (when delivered) call SignalAttach/SignalDetach from Animation
    // Events at the contact frame; PlayerInteractor completes the pending
    // transaction from these. If no clip/marker exists, the configured timer
    // fallback completes the transaction deterministically instead.
    [DisallowMultipleComponent]
    public sealed class HandoffSignalReceiver : MonoBehaviour
    {
        public event Action AttachSignaled;
        public event Action DetachSignaled;

        public void SignalAttach()
        {
            AttachSignaled?.Invoke();
        }

        public void SignalDetach()
        {
            DetachSignaled?.Invoke();
        }
    }
}
