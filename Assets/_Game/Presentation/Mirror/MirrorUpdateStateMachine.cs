using System;

namespace ConGame.Presentation.Mirror
{
    /// <summary>
    /// 镜面反射更新状态。
    /// </summary>
    public enum MirrorUpdateState
    {
        /// <summary>完整且对固定机位可见：本帧更新反射。</summary>
        Active = 0,
        /// <summary>完整但对固定机位不可见：停更。</summary>
        Hidden = 1,
        /// <summary>已破碎：停更并切破裂占位材质。</summary>
        Broken = 2
    }

    /// <summary>
    /// 镜面更新决策状态机（纯 C#，可单测，不引用 UnityEngine）。
    /// 可见性与破坏是两条独立输入，破碎永远优先于可见性。
    /// </summary>
    public sealed class MirrorUpdateStateMachine
    {
        public event Action<MirrorUpdateState> StateChanged;

        public MirrorUpdateState State { get; private set; } = MirrorUpdateState.Hidden;
        public bool IsBroken { get; private set; }
        public bool IsVisible { get; private set; }
        public bool ShouldRender => State == MirrorUpdateState.Active;
        public bool UseBrokenMaterial => IsBroken;

        public void SetVisible(bool visible)
        {
            if (IsVisible == visible)
            {
                return;
            }

            IsVisible = visible;
            Recompute();
        }

        public void SetBroken(bool broken)
        {
            if (IsBroken == broken)
            {
                return;
            }

            IsBroken = broken;
            Recompute();
        }

        /// <summary>
        /// LoopReset：恢复完整材质并回到 Hidden，可见性由下一帧重新评估。
        /// </summary>
        public void ResetMirror()
        {
            IsBroken = false;
            IsVisible = false;
            Recompute();
        }

        private void Recompute()
        {
            MirrorUpdateState next = IsBroken
                ? MirrorUpdateState.Broken
                : IsVisible ? MirrorUpdateState.Active : MirrorUpdateState.Hidden;
            if (State == next)
            {
                return;
            }

            State = next;
            StateChanged?.Invoke(State);
        }
    }
}
