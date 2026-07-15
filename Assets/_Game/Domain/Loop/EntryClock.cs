using System;

namespace ConGame.Domain.Loop
{

    [Flags]
    public enum EntryClockSignal
    {
        None = 0,
        Warning = 1 << 0,
        Enter = 1 << 1
    }

    public readonly struct EntryClockSettings
    {
        public EntryClockSettings(float idleDeadline, float activeDeadline, float loudNoiseDelay, float warningLead)
        {
            if (idleDeadline <= 0f || activeDeadline <= 0f || loudNoiseDelay <= 0f || warningLead < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(idleDeadline), "Entry clock values must be positive.");
            }

            IdleDeadline = idleDeadline;
            ActiveDeadline = activeDeadline;
            LoudNoiseDelay = loudNoiseDelay;
            WarningLead = warningLead;
        }

        public float IdleDeadline { get; }

        public float ActiveDeadline { get; }

        public float LoudNoiseDelay { get; }

        public float WarningLead { get; }

        public static EntryClockSettings Default => new EntryClockSettings(45f, 30f, 8f, 0.5f);
    }

    public sealed class EntryClock
    {
        private readonly EntryClockSettings _settings;
        private bool _warningIssued;
        private bool _entryIssued;

        public EntryClock(EntryClockSettings settings)
        {
            _settings = settings;
            Reset();
        }

        public float Elapsed { get; private set; }

        public float Deadline { get; private set; }

        public bool HasMeaningfulInput { get; private set; }

        public bool EntryIssued => _entryIssued;

        public void Reset()
        {
            Elapsed = 0f;
            Deadline = _settings.IdleDeadline;
            HasMeaningfulInput = false;
            _warningIssued = false;
            _entryIssued = false;
        }

        public EntryClockSignal MarkMeaningfulAction()
        {
            if (HasMeaningfulInput || _entryIssued)
            {
                return EntryClockSignal.None;
            }

            HasMeaningfulInput = true;
            if (Elapsed >= _settings.ActiveDeadline)
            {
                Deadline = Elapsed + _settings.WarningLead;
                return IssueWarning();
            }

            Deadline = Math.Min(Deadline, _settings.ActiveDeadline);
            return EvaluateSignals();
        }

        public EntryClockSignal MarkLoudNoise()
        {
            if (_entryIssued)
            {
                return EntryClockSignal.None;
            }

            Deadline = Math.Min(Deadline, Elapsed + _settings.LoudNoiseDelay);
            return EvaluateSignals();
        }

        public EntryClockSignal Advance(float deltaTime, bool paused = false)
        {
            if (deltaTime < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            }

            if (_entryIssued || paused)
            {
                return EntryClockSignal.None;
            }

            Elapsed += deltaTime;
            return EvaluateSignals();
        }

        private EntryClockSignal EvaluateSignals()
        {
            EntryClockSignal signal = EntryClockSignal.None;
            if (!_warningIssued && Elapsed >= Deadline - _settings.WarningLead)
            {
                signal |= IssueWarning();
            }

            if (!_entryIssued && Elapsed >= Deadline)
            {
                _entryIssued = true;
                signal |= EntryClockSignal.Enter;
            }

            return signal;
        }

        private EntryClockSignal IssueWarning()
        {
            if (_warningIssued)
            {
                return EntryClockSignal.None;
            }

            _warningIssued = true;
            return EntryClockSignal.Warning;
        }
    }
}
