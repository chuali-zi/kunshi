using UnityEngine;
using UnityEngine.Playables;

namespace ConGame.Presentation.M1
{

    public enum M1GreyboxDeathStyle
    {
        DefaultShot = 0,
        SofaRearShot = 1,
        ArmedStandoff = 2
    }

    [CreateAssetMenu(menuName = "ConGame/M1/Greybox Performance", fileName = "PERF_M1_New")]
    public sealed class M1GreyboxPlayableAsset : PlayableAsset
    {
        [SerializeField]
        private M1GreyboxDeathStyle style;
        [SerializeField, Min(0.25f)]
        private float sequenceDuration = 1.6f;

        public M1GreyboxDeathStyle Style => style;

        public float SequenceDuration => Mathf.Max(0.25f, sequenceDuration);

        public override double duration => SequenceDuration;

        public void Configure(M1GreyboxDeathStyle deathStyle, float seconds)
        {
            style = deathStyle;
            sequenceDuration = Mathf.Max(0.25f, seconds);
        }

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            return Playable.Create(graph);
        }
    }
}
