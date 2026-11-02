using System.Collections.Generic;
using UnityEngine;

namespace ConGame.Presentation.CRT
{
    [DisallowMultipleComponent]
    public sealed class CrtFullscreenPass : MonoBehaviour
    {
        private static readonly int NoiseId = Shader.PropertyToID("_NoiseStrength");
        private static readonly int CrackId = Shader.PropertyToID("_CrackAmount");
        private static readonly int PulseId = Shader.PropertyToID("_PulseAmount");
        private static readonly int BlackoutId = Shader.PropertyToID("_Blackout");
        private static readonly int TitleId = Shader.PropertyToID("_TitleAmount");
        private static readonly int ReverseId = Shader.PropertyToID("_ReverseShot");

        [SerializeField] private CrtSignalController signal;
        [SerializeField] private Material material;
        [SerializeField] private UnityEngine.Camera targetCamera;

        private float _blackout;
        private float _titleAmount;
        private float _reverseShot;
        private float _forcedPulse;
        private Material _ownedMaterial;
        private static readonly HashSet<CrtFullscreenPass> Active = new HashSet<CrtFullscreenPass>();

        public Material Material => material;
        public float Blackout => _blackout;
        public float TitleAmount => _titleAmount;

        private void Awake()
        {
            if (Application.isPlaying && material != null)
            {
                _ownedMaterial = new Material(material);
                material = _ownedMaterial;
            }
        }

        private void OnDestroy()
        {
            if (_ownedMaterial != null) Destroy(_ownedMaterial);
        }

        public void ConfigureSignatureArt(Texture title, Texture cutIn, Texture glass)
        {
            if (material == null) return;
            material.SetTexture("_TitleTex", title);
            material.SetTexture("_CutInTex", cutIn);
            material.SetTexture("_GlassTex", glass);
        }

        public void SetSignatureState(float dark, float title, float glass, float cutIn)
        {
            if (material == null) return;
            material.SetFloat("_DarkAmount", Mathf.Clamp01(dark));
            material.SetFloat("_DeathTitle", Mathf.Clamp01(title));
            material.SetFloat("_ScreenCrack", Mathf.Clamp01(glass));
            material.SetFloat("_CutInAmount", Mathf.Clamp01(cutIn));
        }

        public void Configure(CrtSignalController signalController, Material overlayMaterial,
                              UnityEngine.Camera camera = null)
        {
            signal = signalController;
            material = overlayMaterial;
            targetCamera = camera;
            if (isActiveAndEnabled) Active.Add(this);
        }

        public void SetBlackout(float amount)
        {
            _blackout = Mathf.Clamp01(amount);
        }

        public void SetTitleAmount(float amount)
        {
            _titleAmount = Mathf.Clamp01(amount);
        }

        public void TriggerReverseShot(float durationSeconds = 0.08f)
        {
            _reverseShot = 1f;
            CancelInvoke(nameof(ClearReverseShot));
            Invoke(nameof(ClearReverseShot), Mathf.Max(0.02f, durationSeconds));
        }

        public void Pulse(float amount = 1f)
        {
            _forcedPulse = Mathf.Clamp01(amount);
        }

        private void OnEnable()
        {
            Active.Add(this);
        }

        private void OnDisable()
        {
            Active.Remove(this);
        }

        private void LateUpdate()
        {
            if (material == null)
            {
                return;
            }

            float noise = signal == null ? 0.08f : signal.NoiseStrength;
            float crack = signal == null ? 0f : signal.CrackAmount;
            float pulse = _forcedPulse;
            if (signal != null && signal.LightImpactFramesRemaining > 0)
            {
                pulse = Mathf.Max(pulse, 1f);
            }

            material.SetFloat(NoiseId, noise);
            material.SetFloat(CrackId, crack);
            material.SetFloat(PulseId, pulse);
            material.SetFloat(BlackoutId, _blackout);
            material.SetFloat(TitleId, _titleAmount);
            float reverse = Mathf.Max(_reverseShot, signal != null && signal.ReverseShotFramesRemaining > 0 ? 1f : 0f);
            material.SetFloat(ReverseId, reverse);
            _forcedPulse = Mathf.MoveTowards(_forcedPulse, 0f, Time.deltaTime * 8f);
        }

        internal static Material ForCamera(UnityEngine.Camera camera)
        {
            foreach (var pass in Active)
                if (pass != null && pass.isActiveAndEnabled && pass.material != null &&
                    (pass.targetCamera == null || pass.targetCamera == camera)) return pass.material;
            return null;
        }

        private void ClearReverseShot()
        {
            _reverseShot = 0f;
        }
    }
}
