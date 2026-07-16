using ConGame.Runtime.Loop;
using UnityEngine;

namespace ConGame.Presentation.CRT
{
    [DisallowMultipleComponent]
    public sealed class AwakeningPresenter : MonoBehaviour
    {
        [SerializeField] private LoopDirector loop;
        [SerializeField] private CrtFullscreenPass crtPass;

        private float _blackRemaining;
        private float _crackEchoRemaining;

        public bool IsInitialBlackActive => _blackRemaining > 0f;
        public bool IsCrackEchoActive => _crackEchoRemaining > 0f;

        public void Configure(LoopDirector loopDirector, CrtFullscreenPass fullscreenPass = null)
        {
            loop = loopDirector;
            crtPass = fullscreenPass;
        }

        private void OnEnable()
        {
            if (loop != null)
            {
                loop.AwakeningStarted += OnAwakeningStarted;
                loop.TelevisionCrackEchoRequested += OnCrackEcho;
            }
        }

        private void OnDisable()
        {
            if (loop != null)
            {
                loop.AwakeningStarted -= OnAwakeningStarted;
                loop.TelevisionCrackEchoRequested -= OnCrackEcho;
            }
        }

        private void Update()
        {
            _blackRemaining = Mathf.Max(0f, _blackRemaining - Time.deltaTime);
            _crackEchoRemaining = Mathf.Max(0f, _crackEchoRemaining - Time.deltaTime);
            if (crtPass == null)
            {
                return;
            }

            float black = _blackRemaining > 0f ? 1f : 0f;
            float echo = _crackEchoRemaining > 0f ? Mathf.Clamp01(_crackEchoRemaining / 0.15f) * 0.35f : 0f;
            crtPass.SetBlackout(Mathf.Max(black, echo > 0f ? 0f : black));
            if (echo > 0f)
            {
                crtPass.Pulse(echo);
            }
        }

        private void OnAwakeningStarted(bool firstLaunch)
        {
            int loopIndex = loop == null || loop.Progress == null ? 1 : loop.Progress.loopIndex;
            _blackRemaining = firstLaunch ? 2f : Mathf.Clamp(0.35f - (loopIndex - 2) * 0.05f, 0f, 0.35f);
            if (loop != null && loop.Progress != null && loop.Progress.d21Seen)
            {
                crtPass?.TriggerReverseShot(0.1f);
            }
        }

        private void OnCrackEcho()
        {
            _crackEchoRemaining = 0.15f;
            crtPass?.Pulse(0.8f);
        }
    }
}
