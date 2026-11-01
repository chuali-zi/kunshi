using ConGame.Runtime.Loop;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace ConGame.Presentation.Animation.Rigging
{
    /// <summary>
    /// 上身持枪叠加层:Killer 瞄准/持枪时把上身 Rig(如 MultiAim/Override 链)
    /// 权重平滑切入/切出。复用于 Killer 与玩家(D22 持枪)。
    /// 绑定经 Configure 显式传入,不做场景查找。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UpperBodyGunLayerController : MonoBehaviour, ILoopResettable
    {
        [SerializeField] private Rig rig;
        [SerializeField] private AnimationRigSettingsAsset settings;

        private readonly WeightBlender _weight = new WeightBlender();

        public int ResetOrder => 80;
        public float CurrentWeight => _weight.Current;
        public bool IsUpperBodyActive { get; private set; }

        public void Configure(Rig upperBodyRig, AnimationRigSettingsAsset rigSettings)
        {
            rig = upperBodyRig;
            settings = rigSettings;
            _weight.SnapTo(0f);
            IsUpperBodyActive = false;
            ApplyWeight(0f);
        }

        public void SetUpperBodyActive(bool active)
        {
            IsUpperBodyActive = active;
            float blend = active
                ? BlendOrDefault(settings == null ? 0f : settings.upperBodyBlendInSeconds, 0.15f)
                : BlendOrDefault(settings == null ? 0f : settings.upperBodyBlendOutSeconds, 0.2f);
            _weight.SetTarget(active ? 1f : 0f, blend);
        }

        private void LateUpdate()
        {
            if (rig != null && !_weight.IsSettled)
            {
                ApplyWeight(_weight.Tick(Time.deltaTime));
            }
        }

        public void CaptureInitialState()
        {
            _weight.SnapTo(0f);
            IsUpperBodyActive = false;
            ApplyWeight(0f);
        }

        public void Quiesce(in LoopResetContext context)
        {
        }

        public void Restore(in LoopResetContext context)
        {
            _weight.SnapTo(0f);
            IsUpperBodyActive = false;
            ApplyWeight(0f);
        }

        public void Resume(in LoopResetContext context)
        {
        }

        private void ApplyWeight(float value)
        {
            if (rig != null)
            {
                rig.weight = value;
            }
        }

        internal static float BlendOrDefault(float configured, float fallback)
        {
            return configured > 0f ? configured : fallback;
        }
    }
}
