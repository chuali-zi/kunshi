using System;
using System.Collections.Generic;

namespace ConGame.Domain.Counterplay
{
    public enum PerformanceHeldItemPolicy
    {
        Preserve = 0,
        DropAtStart = 1,
        LockToPerformance = 2,
        ReturnToInitialState = 3
    }

    public enum CounterplayNarrativeOutcome
    {
        ScriptedFailure = 0
    }

    [Serializable]
    public struct QteWindowDefinition
    {
        public QteWindowDefinition(float durationSeconds, int requiredPresses)
        {
            DurationSeconds = durationSeconds;
            RequiredPresses = requiredPresses;
        }

        public float DurationSeconds;
        public int RequiredPresses;

        public float SafeDurationSeconds => Math.Max(0.01f, DurationSeconds);

        public int GetScaledRequiredPresses(float accessibilityThresholdScale)
        {
            float scale = Math.Max(0.1f, Math.Min(1f, accessibilityThresholdScale));
            return Math.Max(1, (int)Math.Ceiling(Math.Max(1, RequiredPresses) * scale));
        }
    }

    [Serializable]
    public struct PerformanceBranchDefinition
    {
        public PerformanceBranchDefinition(
            string branchId,
            bool requiresAllQteWindows,
            string nextOutcomeId,
            bool marksD21Seen = false)
        {
            BranchId = branchId ?? string.Empty;
            RequiresAllQteWindows = requiresAllQteWindows;
            NextOutcomeId = nextOutcomeId ?? string.Empty;
            MarksD21Seen = marksD21Seen;
        }

        public string BranchId;
        public bool RequiresAllQteWindows;
        public string NextOutcomeId;
        public bool MarksD21Seen;
    }

    public readonly struct QteWindowResult
    {
        public QteWindowResult(int index, int pressCount, int requiredPresses)
        {
            Index = index;
            PressCount = pressCount;
            RequiredPresses = requiredPresses;
        }

        public int Index { get; }
        public int PressCount { get; }
        public int RequiredPresses { get; }
        public bool Succeeded => PressCount >= RequiredPresses;
    }

    public readonly struct QteSequenceSnapshot
    {
        public QteSequenceSnapshot(
            int currentWindowIndex,
            int currentPressCount,
            float currentWindowRemaining,
            bool isComplete,
            bool allWindowsSucceeded)
        {
            CurrentWindowIndex = currentWindowIndex;
            CurrentPressCount = currentPressCount;
            CurrentWindowRemaining = currentWindowRemaining;
            IsComplete = isComplete;
            AllWindowsSucceeded = allWindowsSucceeded;
        }

        public int CurrentWindowIndex { get; }
        public int CurrentPressCount { get; }
        public float CurrentWindowRemaining { get; }
        public bool IsComplete { get; }
        public bool AllWindowsSucceeded { get; }
        public CounterplayNarrativeOutcome NarrativeOutcome => CounterplayNarrativeOutcome.ScriptedFailure;
    }

    /// <summary>
    /// Pure-domain QTE counter. It counts rising edges only; holding a button never
    /// produces repeated presses. Mechanical success is deliberately separate from
    /// the immutable scripted-failure narrative outcome.
    /// </summary>
    public sealed class QteSequenceCounter
    {
        private readonly QteWindowDefinition[] _windows;
        private readonly QteWindowResult[] _results;
        private readonly float _accessibilityThresholdScale;
        private int _currentWindowIndex;
        private int _currentPressCount;
        private float _currentWindowRemaining;
        private bool _wasPressed;
        private bool _allWindowsSucceeded = true;

        public QteSequenceCounter(
            IReadOnlyList<QteWindowDefinition> windows,
            float accessibilityThresholdScale = 1f)
        {
            if (windows == null)
            {
                throw new ArgumentNullException(nameof(windows));
            }

            _windows = new QteWindowDefinition[windows.Count];
            for (int i = 0; i < windows.Count; i++)
            {
                _windows[i] = windows[i];
            }

            _results = new QteWindowResult[_windows.Length];
            _accessibilityThresholdScale = Math.Max(0.1f, Math.Min(1f, accessibilityThresholdScale));
            Reset();
        }

        public int WindowCount => _windows.Length;
        public int CurrentWindowIndex => _currentWindowIndex;
        public bool IsComplete => _currentWindowIndex >= _windows.Length;
        public bool AllWindowsSucceeded => IsComplete && _allWindowsSucceeded;
        public CounterplayNarrativeOutcome NarrativeOutcome => CounterplayNarrativeOutcome.ScriptedFailure;
        public IReadOnlyList<QteWindowResult> Results => _results;

        public void Reset()
        {
            _currentWindowIndex = 0;
            _currentPressCount = 0;
            _wasPressed = false;
            _allWindowsSucceeded = true;
            _currentWindowRemaining = _windows.Length == 0 ? 0f : _windows[0].SafeDurationSeconds;
            Array.Clear(_results, 0, _results.Length);
        }

        public QteSequenceSnapshot Advance(float deltaTime, bool interactIsPressed)
        {
            if (deltaTime < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            }

            if (IsComplete)
            {
                _wasPressed = interactIsPressed;
                return CaptureSnapshot();
            }

            if (interactIsPressed && !_wasPressed)
            {
                _currentPressCount++;
            }

            _wasPressed = interactIsPressed;
            float remainingDelta = deltaTime;
            while (!IsComplete && remainingDelta >= _currentWindowRemaining)
            {
                remainingDelta -= _currentWindowRemaining;
                CompleteCurrentWindow();
            }

            if (!IsComplete)
            {
                _currentWindowRemaining -= remainingDelta;
            }

            return CaptureSnapshot();
        }

        private void CompleteCurrentWindow()
        {
            int required = _windows[_currentWindowIndex].GetScaledRequiredPresses(_accessibilityThresholdScale);
            QteWindowResult result = new QteWindowResult(_currentWindowIndex, _currentPressCount, required);
            _results[_currentWindowIndex] = result;
            _allWindowsSucceeded &= result.Succeeded;
            _currentWindowIndex++;
            _currentPressCount = 0;
            _currentWindowRemaining = IsComplete ? 0f : _windows[_currentWindowIndex].SafeDurationSeconds;
        }

        private QteSequenceSnapshot CaptureSnapshot()
        {
            return new QteSequenceSnapshot(
                _currentWindowIndex,
                _currentPressCount,
                _currentWindowRemaining,
                IsComplete,
                AllWindowsSucceeded);
        }
    }
}
