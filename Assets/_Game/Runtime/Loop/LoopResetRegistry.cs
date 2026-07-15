using System;
using System.Collections.Generic;
using UnityEngine;

namespace ConGame.Runtime.Loop
{

    [DisallowMultipleComponent]
    public sealed class LoopResetRegistry : MonoBehaviour
    {
        [SerializeField] private Transform registrationRoot;

        private readonly List<Entry> _entries = new List<Entry>();
        private bool _captured;

        public int RegisteredCount => _entries.Count;

        public void Configure(Transform root)
        {
            registrationRoot = root;
        }

        public void CaptureInitialState()
        {
            _entries.Clear();
            Transform root = registrationRoot == null ? transform.root : registrationRoot;
            MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
            int registrationIndex = 0;
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is ILoopResettable resettable)
                {
                    _entries.Add(new Entry(resettable, registrationIndex++));
                }
            }

            _entries.Sort(Entry.Compare);
            for (int i = 0; i < _entries.Count; i++)
            {
                InvokeSafely(_entries[i].Target.CaptureInitialState, "capture", _entries[i].Target);
            }

            _captured = true;
        }

        public void ResetAll(int previousEpoch, int nextEpoch)
        {
            if (!_captured)
            {
                CaptureInitialState();
            }

            LoopResetContext context = new LoopResetContext(previousEpoch, nextEpoch);
            for (int i = 0; i < _entries.Count; i++)
            {
                ILoopResettable target = _entries[i].Target;
                InvokeSafely(() => target.Quiesce(context), "quiesce", target);
            }

            for (int i = 0; i < _entries.Count; i++)
            {
                ILoopResettable target = _entries[i].Target;
                InvokeSafely(() => target.Restore(context), "restore", target);
            }

            Physics.SyncTransforms();

            for (int i = 0; i < _entries.Count; i++)
            {
                ILoopResettable target = _entries[i].Target;
                InvokeSafely(() => target.Resume(context), "resume", target);
            }
        }

        private static void InvokeSafely(Action action, string stage, ILoopResettable target)
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                Debug.LogException(new InvalidOperationException(
                    $"Loop reset {stage} failed for {target.GetType().Name}.", exception));
            }
        }

        private readonly struct Entry
        {
            public Entry(ILoopResettable target, int registrationIndex)
            {
                Target = target;
                RegistrationIndex = registrationIndex;
            }

            public ILoopResettable Target { get; }
            private int RegistrationIndex { get; }

            public static int Compare(Entry left, Entry right)
            {
                int order = left.Target.ResetOrder.CompareTo(right.Target.ResetOrder);
                return order != 0 ? order : left.RegistrationIndex.CompareTo(right.RegistrationIndex);
            }
        }
    }
}
