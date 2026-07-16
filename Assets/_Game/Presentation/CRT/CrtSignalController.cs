using System;
using ConGame.Domain.Facts;
using ConGame.Runtime.Ending;
using ConGame.Runtime.Loop;
using ConGame.Runtime.Settings;
using UnityEngine;

namespace ConGame.Presentation.CRT
{
    [DisallowMultipleComponent]
    public sealed class CrtSignalController : MonoBehaviour
    {
        [SerializeField] private TelevisionController television;
        [SerializeField] private LoopDirector loop;

        public event Action SignalChanged;

        public int LightImpactFramesRemaining { get; private set; }

        public int ReverseShotFramesRemaining { get; private set; }

        public float CrackAmount { get; private set; }

        public float NoiseStrength
        {
            get
            {
                int loopIndex = loop == null || loop.Progress == null ? 1 : loop.Progress.loopIndex;
                bool d21Seen = loop != null && loop.Progress != null && loop.Progress.d21Seen;
                float intensity = new GameSettingsStore().Load().crtIntensity;
                return Mathf.Clamp01((0.08f + (loopIndex - 1) * 0.012f + (d21Seen ? 0.22f : 0f)) * intensity);
            }
        }

        public bool ReverseShotEnabled => loop != null && loop.Progress != null && loop.Progress.d21Seen;

        public void Configure(TelevisionController televisionController, LoopDirector loopDirector)
        {
            television = televisionController;
            loop = loopDirector;
        }

        private void OnEnable()
        {
            if (television != null)
            {
                television.DamageChanged += OnDamageChanged;
                television.LightImpactPulse += OnLightImpact;
            }
        }

        private void OnDisable()
        {
            if (television != null)
            {
                television.DamageChanged -= OnDamageChanged;
                television.LightImpactPulse -= OnLightImpact;
            }
        }

        private void LateUpdate()
        {
            bool changed = false;
            if (LightImpactFramesRemaining > 0)
            {
                LightImpactFramesRemaining--;
                changed = true;
                if (LightImpactFramesRemaining == 0 && (television == null ||
                                                        television.DamageState == TelevisionDamageState.Intact))
                {
                    CrackAmount = 0f;
                }
            }

            if (ReverseShotFramesRemaining > 0)
            {
                ReverseShotFramesRemaining--;
                changed = true;
            }

            if (changed)
            {
                SignalChanged?.Invoke();
            }
        }

        private void OnLightImpact()
        {
            GameSettingsV1 settings = new GameSettingsStore().Load();
            LightImpactFramesRemaining = settings.reduceFlashing ? 1 : 3;
            CrackAmount = 1f;
            if (ReverseShotEnabled)
            {
                ReverseShotFramesRemaining = settings.reduceFlashing ? 1 : 2;
            }

            SignalChanged?.Invoke();
        }

        private void OnDamageChanged(TelevisionDamageState state)
        {
            CrackAmount = state == TelevisionDamageState.Intact ? 0f : 1f;
            SignalChanged?.Invoke();
        }
    }
}
