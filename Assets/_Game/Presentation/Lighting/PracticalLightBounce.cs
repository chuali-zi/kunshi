using UnityEngine;

namespace ConGame.Presentation.Lighting
{
    /// <summary>
    /// Keeps a dedicated fill light in step with a real light without replacing
    /// the fill light's own transform, range, or shadow settings.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PracticalLightBounce : MonoBehaviour
    {
        [SerializeField] private Light sourceLight;
        [SerializeField] private Light targetLight;
        [SerializeField, Min(0f)] private float intensityRatio = 0.25f;

        public Light SourceLight => sourceLight;
        public Light TargetLight => targetLight;
        public float IntensityRatio
        {
            get => intensityRatio;
            set => intensityRatio = Mathf.Max(0f, value);
        }

        /// <summary>
        /// Binds the real source light and the dedicated fill light.
        /// </summary>
        public void Configure(Light source, Light target)
        {
            Configure(source, target, intensityRatio);
        }

        /// <summary>
        /// Binds the real source light, dedicated fill light, and fill strength ratio.
        /// A source and target reference to the same Light is ignored by the sync
        /// path so configuring a mistaken self-reference cannot turn the real light off.
        /// </summary>
        public void Configure(Light source, Light target, float ratio)
        {
            sourceLight = source;
            targetLight = target;
            intensityRatio = Mathf.Max(0f, ratio);
            SyncTarget();
        }

        private void OnEnable()
        {
            SyncTarget();
        }

        private void OnDisable()
        {
            DisableTargetIfDedicated();
        }

        private void LateUpdate()
        {
            SyncTarget();
        }

        private void SyncTarget()
        {
            if (targetLight == null)
            {
                return;
            }

            // Never modify a source when a wiring mistake points both references
            // at the same Light. The dedicated fill must be a separate component.
            if (sourceLight == targetLight)
            {
                return;
            }

            bool sourceEmits = sourceLight != null && sourceLight.isActiveAndEnabled;
            targetLight.enabled = sourceEmits;
            if (!sourceEmits)
            {
                return;
            }

            targetLight.color = sourceLight.color;
            targetLight.intensity = sourceLight.intensity * intensityRatio;
        }

        private void DisableTargetIfDedicated()
        {
            if (targetLight != null && targetLight != sourceLight)
            {
                targetLight.enabled = false;
            }
        }
    }
}
