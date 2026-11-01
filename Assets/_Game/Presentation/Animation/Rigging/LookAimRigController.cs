using ConGame.Runtime.Loop;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace ConGame.Presentation.Animation.Rigging
{
    /// <summary>
    /// Look/Aim Rig:驱动 MultiAimConstraint 的目标点,让头部/脊椎朝向目标。
    /// Killer 瞄准(含 D19/D20 手电扫描)与玩家焦点注视复用同一组件。
    /// 目标 Transform 由本组件持有并移动,外部只给世界坐标或跟踪对象。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LookAimRigController : MonoBehaviour, ILoopResettable
    {
        [SerializeField] private Rig rig;
        [SerializeField] private MultiAimConstraint aimConstraint;
        [SerializeField] private Transform aimTarget;
        [SerializeField] private AnimationRigSettingsAsset settings;

        private readonly WeightBlender _weight = new WeightBlender();
        private Transform _followTarget;

        public int ResetOrder => 80;
        public float CurrentWeight => _weight.Current;
        public bool HasTarget => _followTarget != null || _weight.Target > 0f;
        public Transform AimTarget => aimTarget;

        public void Configure(
            Rig lookRig,
            MultiAimConstraint constraint,
            Transform target,
            AnimationRigSettingsAsset rigSettings)
        {
            rig = lookRig;
            aimConstraint = constraint;
            aimTarget = target;
            settings = rigSettings;
            _followTarget = null;
            _weight.SnapTo(0f);
            ApplyWeight(0f);
        }

        /// <summary>没有场景目标时在 Rig 下创建并接入约束,保持组件可程序化装配。</summary>
        public Transform EnsureTarget()
        {
            if (aimTarget == null)
            {
                GameObject targetObject = new GameObject("AimTarget");
                aimTarget = targetObject.transform;
                aimTarget.SetParent(rig == null ? transform : rig.transform, false);
            }

            if (aimConstraint != null)
            {
                MultiAimConstraintData data = aimConstraint.data;
                if (data.sourceObjects.Count == 0)
                {
                    data.sourceObjects.Add(new WeightedTransform(aimTarget, 1f));
                }
                else if (data.sourceObjects.GetTransform(0) == null)
                {
                    data.sourceObjects.SetTransform(0, aimTarget);
                }

                aimConstraint.data = data;
            }

            return aimTarget;
        }

        public void SetWorldTarget(Vector3 worldPosition)
        {
            EnsureTarget();
            _followTarget = null;
            aimTarget.position = worldPosition;
            BlendIn();
        }

        public void SetFollowTarget(Transform follow)
        {
            EnsureTarget();
            _followTarget = follow;
            if (follow != null)
            {
                aimTarget.position = follow.position;
                BlendIn();
            }
            else
            {
                ClearTarget();
            }
        }

        public void ClearTarget()
        {
            _followTarget = null;
            float blend = UpperBodyGunLayerController.BlendOrDefault(
                settings == null ? 0f : settings.aimBlendOutSeconds, 0.25f);
            _weight.SetTarget(0f, blend);
        }

        private void LateUpdate()
        {
            if (_followTarget != null && aimTarget != null)
            {
                aimTarget.position = _followTarget.position;
            }

            if (rig != null && !_weight.IsSettled)
            {
                ApplyWeight(_weight.Tick(Time.deltaTime));
            }
        }

        public void CaptureInitialState()
        {
            _followTarget = null;
            _weight.SnapTo(0f);
            ApplyWeight(0f);
        }

        public void Quiesce(in LoopResetContext context)
        {
        }

        public void Restore(in LoopResetContext context)
        {
            _followTarget = null;
            _weight.SnapTo(0f);
            ApplyWeight(0f);
        }

        public void Resume(in LoopResetContext context)
        {
        }

        private void BlendIn()
        {
            float blend = UpperBodyGunLayerController.BlendOrDefault(
                settings == null ? 0f : settings.aimBlendInSeconds, 0.18f);
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
