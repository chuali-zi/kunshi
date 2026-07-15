using ConGame.Domain.Interaction;
using UnityEngine;

namespace ConGame.Runtime.Data
{

    [CreateAssetMenu(menuName = "ConGame/Content/Item Definition", fileName = "ITEM_New")]
    public sealed class ItemDefinitionAsset : ScriptableObject
    {
        [SerializeField] private string itemId;
        [SerializeField] private string displayName;
        [SerializeField] private InteractionVerb supportedVerbs;
        [SerializeField] private ItemCategory categories;
        [SerializeField] private ContentStatus contentStatus;

        public string ItemId => itemId;
        public string DisplayName => displayName;
        public InteractionVerb SupportedVerbs => supportedVerbs;
        public ItemCategory Categories => categories;
        public ContentStatus ContentStatus => contentStatus;

        public void Configure(
            string id,
            string name,
            InteractionVerb verbs,
            ItemCategory itemCategories,
            ContentStatus status)
        {
            itemId = id;
            displayName = name;
            supportedVerbs = verbs;
            categories = itemCategories;
            contentStatus = status;
        }
    }
}
