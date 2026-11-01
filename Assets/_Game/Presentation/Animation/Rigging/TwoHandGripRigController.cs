using ConGame.Runtime.Loop;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace ConGame.Presentation.Animation.Rigging
{
    /// <summary>
    /// 双手持物 IK:玩家搬运大物件(木椅/抽屉柜/球棍双手位)时,
    /// 用 TwoBoneIKConstraint 把另一只手贴合到物件上的握点。
    /// 握点由交互/演出层给出世界位置,本组件只负责权重与目标。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TwoHandGripRigController : MonoBehaviour, ILoopResettable
    {
        [SerializeField] private Rig rig;
        [SerializeField] private TwoBoneIKConstraint offHandConstraint;
        [SerializeField] private Transform gripTarget;
        [SerializeField] private AnimationRigSettingsAsset settings;

        private readonly WeightBlender _weight = new WeightBlender();
        private Transform _followGrip;

        public int ResetOrder => 80;
        public float CurrentWeight => _weight.Current;
        public bool HasGrip => _followGrip != null || _weight.Target > 0f;
        public Transform GripTarget => gripTarget;

        public void Configure(
            Rig gripRig,
            TwoBoneIKConstraint constraint,
            Transform target,
            AnimationRigSettingsAsset rigSettings)
        {
            rig = gripRig;
            offHandConstraint = constraint;
            gripTarget = target;
            settings = rigSettings;
            _followGrip = null;
            _weight.SnapTo(0f);
            ApplyWeight(0f);
        }

        public Transform EnsureTarget()
        {
            if (gripTarget == null)
            {
                GameObject targetObject = new GameObject("OffHandGripTarget");
                gripTarget = targetObject.transform;
                gripTarget.SetParent(rig == null ? transform : rig.transform, false);
            }

            if (offHandConstraint != null && offHandConstraint.data.target == null)
            {
                TwoBoneIKConstraintData data = offHandConstraint.data;
                data.target = gripTarget;
                offHandConstraint.data = data;
            }

            return gripTarget;
        }

        /// <summary>把副手贴合到世界握点(如物件上的 GripSocket)。</summary>
        public void SetGrip(Vector3 worldPosition, Quaternion worldRotation)
        {
            EnsureTarget();
            _followGrip = null;
            gripTarget.SetPositionAndRotation(worldPosition, worldRotation);
            BlendIn();
        }

        /// <summary>贴合一个会随手部/物件移动的握点 Transform。</summary>
        public void SetGrip(Transform followGrip)
        {
            EnsureTarget();
            _followGrip = followGrip;
            if (followGrip != null)
            {
                gripTarget.SetPositionAndRotation(followGrip.position, followGrip.rotation);
                BlendIn();
            }
            else
            {
                ReleaseGrip();
            }
        }

        public void ReleaseGrip()
        {
            _followGrip = null;
            float blend = UpperBodyGunLayerController.BlendOrDefault(
                settings == null ? 0f : settings.gripBlendSeconds, 0.2f);
            _weight.SetTarget(0f, blend);
        }

        private void LateUpdate()
        {
            if (_followGrip != null && gripTarget != null)
            {
                gripTarget.SetPositionAndRotation(_followGrip.position, _followGrip.rotation);
            }

            if (rig != null && !_weight.IsSettled)
            {
                ApplyWeight(_weight.Tick(Time.deltaTime));
            }
        }

        public void CaptureInitialState()
        {
            _followGrip = null;
            _weight.SnapTo(0f);
            ApplyWeight(0f);
        }

        public void Quiesce(in LoopResetContext context)
        {
        }

        public void Restore(in LoopResetContext context)
        {
            _followGrip = null;
            _weight.SnapTo(0f);
            ApplyWeight(0f);
        }

        public void Resume(in LoopResetContext context)
        {
        }

        private void BlendIn()
        {
            float blend = UpperBodyGunLayerController.BlendOrDefault(
                settings == null ? 0f : settings.gripBlendSeconds, 0.2f);
            _weight.SetTarget(1f, blend);
        }

        private void ApplyWeight(float value)
        {
            if (rig != null)
            {
                rig.weight = value;
            }
        }
    }
}
