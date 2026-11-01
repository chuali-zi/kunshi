namespace ConGame.Presentation.Animation.Rigging
{
    public enum RagdollPhase
    {
        Animated = 0,
        HitReact = 1,
        Ragdoll = 2
    }

    /// <summary>
    /// Ragdoll 接管时序的纯 C# 状态核:Animated → HitReact(中弹反应窗口) → Ragdoll。
    /// 不引用 UnityEngine,EditMode 可直接单测;MonoBehaviour 驱动层只翻译状态。
    /// </summary>
    public sealed class RagdollPhaseController
    {
        private float _hitReactSeconds = 0.35f;
        private float _timer;

        public RagdollPhase Phase { get; private set; } = RagdollPhase.Animated;
        public float HitReactSeconds => _hitReactSeconds;

        public void Configure(float hitReactSeconds)
        {
            _hitReactSeconds = hitReactSeconds < 0f ? 0f : hitReactSeconds;
            if (Phase == RagdollPhase.Animated)
            {
                _timer = 0f;
            }
        }

        /// <summary>请求接管(HitFall/死亡)。已在接管流程中返回 false,不重复触发。</summary>
        public bool BeginHit()
        {
            if (Phase != RagdollPhase.Animated)
            {
                return false;
            }

            Phase = RagdollPhase.HitReact;
            _timer = 0f;
            return true;
        }

        /// <summary>推进计时;返回 true 的当帧表示应切入 Ragdoll 物理。</summary>
        public bool Tick(float deltaTime)
        {
            if (Phase != RagdollPhase.HitReact)
            {
                return false;
            }

            _timer += deltaTime < 0f ? 0f : deltaTime;
            if (_timer < _hitReactSeconds)
            {
                return false;
            }

            Phase = RagdollPhase.Ragdoll;
            return true;
        }

        public void Reset()
        {
            Phase = RagdollPhase.Animated;
            _timer = 0f;
        }
    }
}
