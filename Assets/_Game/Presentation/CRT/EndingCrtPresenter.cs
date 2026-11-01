using ConGame.Presentation.Audio;
using ConGame.Runtime.Ending;
using UnityEngine;

namespace ConGame.Presentation.CRT
{

    /// <summary>
    /// 消费 EndingSequenceDirector 的全屏表现事件,走现有 CRT 后处理路径:
    /// 黑场淡入(_Blackout)、闪帧脉冲(_PulseAmount)、标题浮现(_TitleAmount),
    /// 以及 E3 备用枪枪响的音效/脉冲。只消费 Runtime 状态。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EndingCrtPresenter : MonoBehaviour
    {
        [SerializeField] private EndingSequenceDirector director;
        [SerializeField] private CrtFullscreenPass fullscreenPass;
        [SerializeField] private SemanticAudioDirector audioDirector;

        public void Configure(
            EndingSequenceDirector sequenceDirector,
            CrtFullscreenPass pass,
            SemanticAudioDirector audio = null)
        {
            Unsubscribe();
            director = sequenceDirector;
            fullscreenPass = pass;
            audioDirector = audio;
            if (isActiveAndEnabled)
            {
                Subscribe();
            }
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (director == null)
            {
                return;
            }

            director.BlackoutRequested += OnBlackoutRequested;
            director.ScreenPulseRequested += OnScreenPulseRequested;
            director.TitleAmountRequested += OnTitleAmountRequested;
            director.BackupPistolShotPerformed += OnBackupPistolShotPerformed;
        }

        private void Unsubscribe()
        {
            if (director == null)
            {
                return;
            }

            director.BlackoutRequested -= OnBlackoutRequested;
            director.ScreenPulseRequested -= OnScreenPulseRequested;
            director.TitleAmountRequested -= OnTitleAmountRequested;
            director.BackupPistolShotPerformed -= OnBackupPistolShotPerformed;
        }

        private void OnBlackoutRequested(float amount)
        {
            fullscreenPass?.SetBlackout(amount);
        }

        private void OnScreenPulseRequested()
        {
            fullscreenPass?.Pulse(1f);
        }

        private void OnTitleAmountRequested(float amount)
        {
            fullscreenPass?.SetTitleAmount(amount);
        }

        private void OnBackupPistolShotPerformed()
        {
            fullscreenPass?.Pulse(1f);
            audioDirector?.Play(AudioCueId.Gunshot);
        }
    }
}
