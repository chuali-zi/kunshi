using System;
using System.Collections.Generic;
using ConGame.Domain.Facts;
using ConGame.Domain.Interaction;

namespace ConGame.Domain.Counterplay
{

    [Flags]
    public enum CounterplayTriggerMode
    {
        None = 0,
        Entry = 1 << 0,
        Reactive = 1 << 1,
        Continuous = 1 << 2,
        Fallback = 1 << 3
    }

    public sealed class CounterplayRule
    {
        public CounterplayRule(
            string id,
            int sortOrder,
            CounterplayTriggerMode triggerMode,
            IReadOnlyList<FactCondition> conditions,
            ContentStatus contentStatus = ContentStatus.Specified)
        {
            Id = id ?? string.Empty;
            SortOrder = sortOrder;
            TriggerMode = triggerMode;
            Conditions = conditions ?? Array.Empty<FactCondition>();
            ContentStatus = contentStatus;
        }

        public string Id { get; }

        public int SortOrder { get; }

        public CounterplayTriggerMode TriggerMode { get; }

        public IReadOnlyList<FactCondition> Conditions { get; }

        public ContentStatus ContentStatus { get; }

        public bool Matches(CounterplayTriggerMode trigger, in FactSnapshot snapshot)
        {
            if ((TriggerMode & trigger) == 0)
            {
                return false;
            }

            for (int i = 0; i < Conditions.Count; i++)
            {
                if (!Conditions[i].Matches(snapshot))
                {
                    return false;
                }
            }

            return true;
        }
    }

    public sealed class CounterplayEvaluator
    {
        public CounterplayRule Select(
            IReadOnlyList<CounterplayRule> rules,
            CounterplayTriggerMode trigger,
            in FactSnapshot snapshot)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            CounterplayRule best = null;
            CounterplayRule fallback = null;

            for (int i = 0; i < rules.Count; i++)
            {
                CounterplayRule rule = rules[i];
                if (rule == null)
                {
                    continue;
                }

                if ((rule.TriggerMode & CounterplayTriggerMode.Fallback) != 0)
                {
                    if (fallback == null || rule.SortOrder < fallback.SortOrder)
                    {
                        fallback = rule;
                    }

                    continue;
                }

                if (rule.Matches(trigger, snapshot) && (best == null || rule.SortOrder < best.SortOrder))
                {
                    best = rule;
                }
            }

            return best ?? fallback;
        }
    }
}
