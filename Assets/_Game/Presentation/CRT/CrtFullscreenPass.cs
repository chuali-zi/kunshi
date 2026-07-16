using UnityEngine;
using UnityEngine.Rendering;

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

        public Material Material => material;
        public float Blackout => _blackout;
        public float TitleAmount => _titleAmount;

        public void Configure(CrtSignalController signalController, Material overlayMaterial,
                              UnityEngine.Camera camera = null)
        {
            signal = signalController;
            material = overlayMaterial;
            targetCamera = camera;
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
            RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
        }

        private void OnDisable()
        {
            RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
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

        private void OnEndCameraRendering(ScriptableRenderContext context, UnityEngine.Camera camera)
        {
            if (material == null)
            {
                return;
            }

            if (targetCamera != null && camera != targetCamera)
            {
                return;
            }

            if (camera.cameraType != UnityEngine.CameraType.Game)
            {
                return;
            }

            var cmd = new CommandBuffer { name = "ConGameCRT" };
            cmd.Blit(BuiltinRenderTextureType.CurrentActive, BuiltinRenderTextureType.CurrentActive, material);
            context.ExecuteCommandBuffer(cmd);
            cmd.Release();
        }

        private void ClearReverseShot()
        {
            _reverseShot = 0f;
        }
    }
}
