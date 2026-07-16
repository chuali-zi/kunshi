using ConGame.Domain.Facts;
using ConGame.Runtime.Ending;
using UnityEngine;

namespace ConGame.Presentation.CRT
{
    [DisallowMultipleComponent]
    public sealed class CrtStatePresenter : MonoBehaviour
    {
        private static readonly int CrackAmountId = Shader.PropertyToID("_CrackAmount");
        private static readonly int PulseId = Shader.PropertyToID("_PulseAmount");

        [SerializeField] private TelevisionController television;
        [SerializeField] private CrtSignalController signal;
        [SerializeField] private CrtFullscreenPass fullscreenPass;
        [SerializeField] private Material fullScreenMaterial;

        public void Configure(
            TelevisionController controller,
            CrtSignalController signalController = null,
            CrtFullscreenPass pass = null,
            Material material = null)
        {
            television = controller;
            signal = signalController;
            fullscreenPass = pass;
            fullScreenMaterial = material != null ? material : (pass == null ? null : pass.Material);
        }

        private void OnEnable()
        {
            if (television != null)
            {
                television.DamageChanged += OnDamageChanged;
                television.LightImpactPulse += OnLightImpactPulse;
            }

            if (signal != null)
            {
                signal.SignalChanged += OnSignalChanged;
            }
        }

        private void OnDisable()
        {
            if (television != null)
            {
                television.DamageChanged -= OnDamageChanged;
                television.LightImpactPulse -= OnLightImpactPulse;
            }

            if (signal != null)
            {
                signal.SignalChanged -= OnSignalChanged;
            }
        }

        private void OnDamageChanged(TelevisionDamageState state)
        {
            float crack = state == TelevisionDamageState.Intact ? 0f : 1f;
            if (fullScreenMaterial != null && fullScreenMaterial.HasProperty(CrackAmountId))
            {
                fullScreenMaterial.SetFloat(CrackAmountId, crack);
            }

            if (state == TelevisionDamageState.Destroyed)
            {
                fullscreenPass?.Pulse(1f);
                fullscreenPass?.SetBlackout(0.45f);
            }
        }

        private void OnLightImpactPulse()
        {
            fullscreenPass?.Pulse(1f);
            if (fullScreenMaterial != null && fullScreenMaterial.HasProperty(PulseId))
            {
                fullScreenMaterial.SetFloat(PulseId, 1f);
            }
        }

        private void OnSignalChanged()
        {
            if (fullScreenMaterial == null || signal == null)
            {
                return;
            }

            if (fullScreenMaterial.HasProperty(CrackAmountId))
            {
                fullScreenMaterial.SetFloat(CrackAmountId, signal.CrackAmount);
            }
        }
    }
}
