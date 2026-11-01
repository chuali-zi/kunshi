using UnityEngine;

namespace ConGame.Presentation.Animation.Rigging
{
    /// <summary>
    /// Animation Rigging / Ragdoll 的权重平滑与接管时间配置。
    /// 独立于 GameConfigAsset,由 Bootstrap 或场景生成器分发给各 Rig 控制器。
    /// </summary>
    [CreateAssetMenu(menuName = "ConGame/Animation/Rig Settings", fileName = "AnimationRigSettings")]
    public sealed class AnimationRigSettingsAsset : ScriptableObject
    {
        [Header("权重平滑(秒)")]
        [Min(0f)] public float aimBlendInSeconds = 0.18f;
        [Min(0f)] public float aimBlendOutSeconds = 0.25f;
        [Min(0f)] public float upperBodyBlendInSeconds = 0.15f;
        [Min(0f)] public float upperBodyBlendOutSeconds = 0.2f;
        [Min(0f)] public float gripBlendSeconds = 0.2f;
        [Min(0f)] public float footPlantBlendSeconds = 0.15f;

        [Header("Ragdoll")]
        [Tooltip("中弹反应到 Ragdoll 接管的延迟,manifest 要求 ≤0.5s")]
        [Min(0f)] public float ragdollHitReactSeconds = 0.35f;

        public void Normalize()
        {
            aimBlendInSeconds = ClampBlend(aimBlendInSeconds);
            aimBlendOutSeconds = ClampBlend(aimBlendOutSeconds);
            upperBodyBlendInSeconds = ClampBlend(upperBodyBlendInSeconds);
            upperBodyBlendOutSeconds = ClampBlend(upperBodyBlendOutSeconds);
            gripBlendSeconds = ClampBlend(gripBlendSeconds);
            footPlantBlendSeconds = ClampBlend(footPlantBlendSeconds);
            ragdollHitReactSeconds = Mathf.Clamp(ragdollHitReactSeconds, 0.05f, 0.5f);
        }

        private static float ClampBlend(float seconds)
        {
            return Mathf.Clamp(seconds, 0.01f, 2f);
        }
    }
}
