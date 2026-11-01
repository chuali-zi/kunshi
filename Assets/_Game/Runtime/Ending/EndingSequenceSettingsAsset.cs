using UnityEngine;

namespace ConGame.Runtime.Ending
{

    [CreateAssetMenu(menuName = "ConGame/Config/Ending Sequence", fileName = "CFG_EndingSequence")]
    public sealed class EndingSequenceSettingsAsset : ScriptableObject
    {
        [SerializeField] private EndingSequenceSettings settings = new EndingSequenceSettings();

        public EndingSequenceSettings CreateSettings()
        {
            return settings == null
                ? EndingSequenceSettings.Default
                : settings.CloneNormalized();
        }
    }
}
