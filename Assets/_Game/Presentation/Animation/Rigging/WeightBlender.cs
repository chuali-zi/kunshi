namespace ConGame.Presentation.Animation.Rigging
{
    /// <summary>
    /// 纯 C# 权重平滑器:按配置化 blend 时间把当前权重移向目标值。
    /// 不引用 UnityEngine,EditMode 可直接单测。blend 时间为 0 时瞬时切换。
    /// </summary>
    public sealed class WeightBlender
    {
        private float _ratePerSecond = -1f;

        public float Current { get; private set; }
        public float Target { get; private set; }
        public bool IsSettled => Current == Target;

        public void SetTarget(float target, float blendSeconds)
        {
            Target = Clamp01(target);
            _ratePerSecond = blendSeconds <= 0f ? -1f : 1f / blendSeconds;
        }

        public float Tick(float deltaTime)
        {
            if (Current == Target)
            {
                return Current;
            }

            if (_ratePerSecond <= 0f || deltaTime <= 0f)
            {
                if (_ratePerSecond <= 0f)
                {
                    Current = Target;
                }

                return Current;
            }

            float step = _ratePerSecond * deltaTime;
            float delta = Target - Current;
            Current = delta > 0f
                ? (delta <= step ? Target : Current + step)
                : (-delta <= step ? Target : Current - step);
            return Current;
        }

        public void SnapTo(float value)
        {
            Current = Clamp01(value);
            Target = Current;
            _ratePerSecond = -1f;
        }

        private static float Clamp01(float value)
        {
            if (value < 0f)
            {
                return 0f;
            }

            return value > 1f ? 1f : value;
        }
    }
}
