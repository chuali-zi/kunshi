using UnityEngine;

namespace ConGame.Runtime.Config
{

    [CreateAssetMenu(menuName = "ConGame/Config/Dark Adaptation", fileName = "CFG_DarkAdaptation")]
    public sealed class DarkAdaptationConfigAsset : ScriptableObject
    {
        [SerializeField] private float[] ambientIntensity =
        {
            0.002f,
            0.0045f,
            0.0075f,
            0.011f,
            0.015f,
            0.02f
        };

        public float GetIntensity(int experience)
        {
            if (ambientIntensity == null || ambientIntensity.Length == 0)
            {
                return 0.002f;
            }

            return ambientIntensity[Mathf.Clamp(experience, 0, ambientIntensity.Length - 1)];
        }
    }
}
