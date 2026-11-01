using System;
using UnityEngine;

namespace ConGame.Runtime.AI
{
    /// <summary>
    /// Aim/Fire 三段语义的可绑定槽位。只按语义引用剪辑键,
    /// 不直接引用具体 AnimationClip;实际资产由美术并行交付后
    /// 在 Presentation 侧绑定到 AN_Killer_AimEnter/AimHold/Fire。
    /// </summary>
    [Serializable]
    public struct KillerAimFirePresentationSettings
    {
        public const string DefaultAimEnterClipKey = "AN_Killer_AimEnter";
        public const string DefaultAimHoldClipKey = "AN_Killer_AimHold";
        public const string DefaultFireClipKey = "AN_Killer_Fire";

        public string AimEnterClipKey;
        public string AimHoldClipKey;
        public string FireClipKey;

        /// <summary>
        /// AimEnter 段无动画/无标记时的确定性时长回退;超过该时长进入 AimHold 语义。
        /// </summary>
        [Min(0f)] public float AimEnterFallbackSeconds;

        public static KillerAimFirePresentationSettings Default
        {
            get
            {
                return new KillerAimFirePresentationSettings
                {
                    AimEnterClipKey = DefaultAimEnterClipKey,
                    AimHoldClipKey = DefaultAimHoldClipKey,
                    FireClipKey = DefaultFireClipKey,
                    AimEnterFallbackSeconds = 0.35f
                };
            }
        }

        public KillerAimFirePresentationSettings Validated()
        {
            KillerAimFirePresentationSettings fallback = Default;
            AimEnterClipKey = string.IsNullOrWhiteSpace(AimEnterClipKey)
                ? fallback.AimEnterClipKey
                : AimEnterClipKey;
            AimHoldClipKey = string.IsNullOrWhiteSpace(AimHoldClipKey)
                ? fallback.AimHoldClipKey
                : AimHoldClipKey;
            FireClipKey = string.IsNullOrWhiteSpace(FireClipKey) ? fallback.FireClipKey : FireClipKey;
            AimEnterFallbackSeconds = AimEnterFallbackSeconds < 0f
                ? fallback.AimEnterFallbackSeconds
                : AimEnterFallbackSeconds;
            return this;
        }
    }
}
