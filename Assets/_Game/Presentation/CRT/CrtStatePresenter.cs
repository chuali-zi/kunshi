using ConGame.Domain.Facts;
using ConGame.Runtime.Ending;
using ConGame.Runtime.Loop;
using UnityEngine;

namespace ConGame.Presentation.CRT
{
    [DisallowMultipleComponent]
    public sealed class CrtStatePresenter : MonoBehaviour, ILoopResettable
    {
        private static readonly int CrackAmountId = Shader.PropertyToID("_CrackAmount");
        private static readonly int PulseId = Shader.PropertyToID("_PulseAmount");

        [SerializeField] private TelevisionController television;
        [SerializeField] private CrtSignalController signal;
        [SerializeField] private CrtFullscreenPass fullscreenPass;
        [SerializeField] private Material fullScreenMaterial;
        [SerializeField] private GameObject intactVisual;
        [SerializeField] private GameObject crackedVisual;
        [SerializeField] private GameObject destroyedVisual;

        public int ResetOrder => 70;

        public void Configure(
            TelevisionController controller,
            CrtSignalController signalController = null,
            CrtFullscreenPass pass = null,
            Material material = null,
            GameObject intact = null,
            GameObject cracked = null,
            GameObject destroyed = null)
        {
            Unsubscribe();
            television = controller;
            signal = signalController;
            fullscreenPass = pass;
            fullScreenMaterial = material != null ? material : (pass == null ? null : pass.Material);
            intactVisual = intact;
            crackedVisual = cracked;
            destroyedVisual = destroyed;

            if (isActiveAndEnabled)
            {
                Subscribe();
            }

            ApplyDamageState(television == null ? TelevisionDamageState.Intact : television.DamageState, false);
        }

        public void CaptureInitialState()
        {
            ApplyCurrentDamageState();
        }

        public void Quiesce(in LoopResetContext context)
        {
        }

        public void Restore(in LoopResetContext context)
        {
            ApplyCurrentDamageState();
        }

        public void Resume(in LoopResetContext context)
        {
            ApplyCurrentDamageState();
        }

        private void OnEnable()
        {
            Subscribe();
            ApplyCurrentDamageState();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnDamageChanged(TelevisionDamageState state)
        {
            ApplyDamageState(state, true);
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

        private void Subscribe()
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

        private void Unsubscribe()
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

        private void ApplyCurrentDamageState()
        {
            ApplyDamageState(television == null ? TelevisionDamageState.Intact : television.DamageState, false);
        }

        private void ApplyDamageState(TelevisionDamageState state, bool playEffects)
        {
            if (intactVisual != null)
            {
                intactVisual.SetActive(state == TelevisionDamageState.Intact);
            }

            if (crackedVisual != null)
            {
                crackedVisual.SetActive(state == TelevisionDamageState.Cracked);
            }

            if (destroyedVisual != null)
            {
                destroyedVisual.SetActive(state == TelevisionDamageState.Destroyed);
            }

            float crack = state == TelevisionDamageState.Intact ? 0f : 1f;
            if (fullScreenMaterial != null && fullScreenMaterial.HasProperty(CrackAmountId))
            {
                fullScreenMaterial.SetFloat(CrackAmountId, crack);
            }

            if (playEffects && state == TelevisionDamageState.Destroyed)
            {
                fullscreenPass?.Pulse(1f);
                fullscreenPass?.SetBlackout(0.45f);
            }
        }
    }
}
