using ConGame.Domain.Counterplay;
using ConGame.Domain.Facts;
using ConGame.Domain.Interaction;
using UnityEngine;
using UnityEngine.Playables;

namespace ConGame.Runtime.Counterplay
{

    [CreateAssetMenu(menuName = "ConGame/Content/Counterplay Definition", fileName = "CP_New")]
    public sealed class CounterplayDefinitionAsset : ScriptableObject
    {
        [SerializeField] private string outcomeId;
        [SerializeField] private int sortOrder;
        [SerializeField] private CounterplayTriggerMode triggerMode;
        [SerializeField] private FactCondition[] conditions;
        [SerializeField] private ContentStatus contentStatus;
        [SerializeField] private PlayableAsset performance;

        public string OutcomeId => outcomeId;
        public int SortOrder => sortOrder;
        public CounterplayTriggerMode TriggerMode => triggerMode;
        public FactCondition[] Conditions => conditions;
        public ContentStatus ContentStatus => contentStatus;
        public PlayableAsset Performance => performance;

        public CounterplayRule ToRule()
        {
            return new CounterplayRule(outcomeId, sortOrder, triggerMode, conditions, contentStatus);
        }

        public void Configure(
            string id,
            int order,
            CounterplayTriggerMode mode,
            FactCondition[] ruleConditions,
            ContentStatus status)
        {
            outcomeId = id;
            sortOrder = order;
            triggerMode = mode;
            conditions = ruleConditions;
            contentStatus = status;
        }
    }
}
