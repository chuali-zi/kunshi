using ConGame.Domain.Facts;
using ConGame.Runtime.Ending;
using UnityEngine;

namespace ConGame.Presentation.CRT
{

    [DisallowMultipleComponent]
    public sealed class CrtStatePresenter : MonoBehaviour
    {
        private static readonly int CrackAmountId = Shader.PropertyToID("_CrackAmount");

        [SerializeField] private TelevisionController television;
        [SerializeField] private Material fullScreenMaterial;

        public void Configure(TelevisionController controller, Material material = null)
        {
            television = controller;
            fullScreenMaterial = material;
        }

        private void OnEnable()
        {
            if (television != null)
            {
                television.DamageChanged += OnDamageChanged;
                television.LightImpactPulse += OnLightImpactPulse;
            }
        }

        private void OnDisable()
        {
            if (television != null)
            {
                television.DamageChanged -= OnDamageChanged;
                television.LightImpactPulse -= OnLightImpactPulse;
            }
        }

        private void OnDamageChanged(TelevisionDamageState state)
        {
            if (fullScreenMaterial != null && fullScreenMaterial.HasProperty(CrackAmountId))
            {
                fullScreenMaterial.SetFloat(CrackAmountId, state == TelevisionDamageState.Intact ? 0f : 1f);
            }
        }

        private void OnLightImpactPulse()
        {
            // The production renderer replaces this with a three-frame URP Full Screen Pass pulse.
            Debug.Log("[ConGame] TV light-impact crack pulse.");
        }
    }
}
