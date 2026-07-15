using System;
using System.Collections.Generic;
using ConGame.Domain.Counterplay;
using ConGame.Domain.Facts;
using UnityEngine;

namespace ConGame.Runtime.Counterplay
{

    [CreateAssetMenu(menuName = "ConGame/Content/Counterplay Catalog", fileName = "CAT_Counterplays")]
    public sealed class CounterplayCatalogAsset : ScriptableObject
    {
        [SerializeField]
        private CounterplayDefinitionAsset[] definitions;

        private CounterplayRule[] _rules;
        private readonly CounterplayEvaluator _evaluator = new CounterplayEvaluator();

        public IReadOnlyList<CounterplayDefinitionAsset> Definitions =>
            definitions ?? Array.Empty<CounterplayDefinitionAsset>();

        public CounterplayDefinitionAsset Select(CounterplayTriggerMode trigger, in FactSnapshot snapshot)
        {
            EnsureRules();
            CounterplayRule selected = _evaluator.Select(_rules, trigger, snapshot);
            if (selected == null)
            {
                return null;
            }

            for (int i = 0; i < definitions.Length; i++)
            {
                if (definitions[i] != null &&
                    string.Equals(definitions[i].OutcomeId, selected.Id, StringComparison.Ordinal))
                {
                    return definitions[i];
                }
            }

            return null;
        }

        public void Configure(CounterplayDefinitionAsset[] entries)
        {
            definitions = entries;
            _rules = null;
        }

        private void EnsureRules()
        {
            if (_rules != null)
            {
                return;
            }

            if (definitions == null)
            {
                _rules = Array.Empty<CounterplayRule>();
                return;
            }

            List<CounterplayRule> implemented = new List<CounterplayRule>(definitions.Length);
            for (int i = 0; i < definitions.Length; i++)
            {
                CounterplayDefinitionAsset definition = definitions[i];
                if (definition != null &&
                    definition.ContentStatus == ConGame.Domain.Interaction.ContentStatus.Implemented)
                {
                    implemented.Add(definition.ToRule());
                }
            }

            _rules = implemented.ToArray();
        }
    }
}
