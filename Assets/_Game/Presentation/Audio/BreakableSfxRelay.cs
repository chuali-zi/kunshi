using ConGame.Domain.Interaction;
using ConGame.Runtime.Interaction;
using UnityEngine;

namespace ConGame.Presentation.Audio
{
    /// <summary>
    /// 把易碎物(木椅/花瓶/镜子/挂钟)的权威 Broken 状态接到 <see cref="SemanticAudioDirector"/>,
    /// 补齐已有 SFX 的运行时语义触发路径。每项绑定经 Configure 显式装配。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BreakableSfxRelay : MonoBehaviour
    {
        [SerializeField] private SemanticAudioDirector audio;
        [SerializeField] private StatefulWorldItem item;
        [SerializeField] private AudioCueId breakCue = AudioCueId.GlassShatter;

        public void Configure(SemanticAudioDirector director, StatefulWorldItem target, AudioCueId cue)
        {
            if (isActiveAndEnabled && item != null)
            {
                item.StateChanged -= OnItemStateChanged;
            }

            audio = director;
            item = target;
            breakCue = cue;

            if (isActiveAndEnabled && item != null)
            {
                item.StateChanged += OnItemStateChanged;
            }
        }

        private void OnEnable()
        {
            if (item != null)
            {
                item.StateChanged += OnItemStateChanged;
            }
        }

        private void OnDisable()
        {
            if (item != null)
            {
                item.StateChanged -= OnItemStateChanged;
            }
        }

        private void OnItemStateChanged(ItemPhysicalState state)
        {
            if (state == ItemPhysicalState.Broken && audio != null)
            {
                audio.Play(breakCue);
            }
        }
    }
}
