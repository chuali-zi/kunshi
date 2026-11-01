using System;
using UnityEngine;

namespace ConGame.Runtime.AI
{
    /// <summary>
    /// 踹门语义帧的确定性定时回退。门只在 KickContact(动画事件或
    /// 回退计时到达)之后才打开;KickWindup 期间只播放晃动,不驱动开门。
    /// </summary>
    [Serializable]
    public struct BreachKickTimingSettings
    {
        /// <summary>踢击前摇(晃动)时长;超过总时长一半时按一半收敛。</summary>
        [Min(0.01f)] public float WindupSeconds;

        /// <summary>无动画/无接触标记时,KickContact 的确定性回退触发时刻。</summary>
        [Min(0.01f)] public float ContactFallbackSeconds;

        public static BreachKickTimingSettings Default
        {
            get
            {
                return new BreachKickTimingSettings
                {
                    WindupSeconds = 0.28f,
                    ContactFallbackSeconds = 0.4f
                };
            }
        }

        public BreachKickTimingSettings Validated()
        {
            BreachKickTimingSettings fallback = Default;
            WindupSeconds = WindupSeconds <= 0f ? fallback.WindupSeconds : WindupSeconds;
            ContactFallbackSeconds = ContactFallbackSeconds <= 0f
                ? fallback.ContactFallbackSeconds
                : ContactFallbackSeconds;
            return this;
        }
    }
}
