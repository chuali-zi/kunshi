using ConGame.Runtime.Loop;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace ConGame.Presentation.Animation.Rigging
{
    /// <summary>
    /// 脚底约束:固定机位验收用,把左右脚 TwoBoneIK 目标对齐到地面,
    /// 吸收贴地类剪辑(NIUA 髋部站立基准)与地面的高度差。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FootPlantRigController : MonoBehaviour, ILoopResettable
    {
        [SerializeField] private Rig rig;
        [SerializeField] private TwoBoneIKConstraint leftFootConstraint;
        [SerializeField] private TwoBoneIKConstraint rightFootConstraint;
        [SerializeField] private Transform leftFootTarget;
        [SerializeField] private Transform rightFootTarget;
        [SerializeField] private AnimationRigSettingsAsset settings;
        [SerializeField] private LayerMask groundMask = 1; // Default 层
        [SerializeField, Min(0.05f)] private float raycastUp = 0.4f;
        [SerializeField, Min(0.1f)] private float raycastDown = 1.2f;

        private readonly WeightBlender _weight = new WeightBlender();

        public int ResetOrder => 80;
        public float CurrentWeight => _weight.Current;
        public bool IsPlanted => _weight.Target > 0f;

        public void Configure(
            Rig footRig,
            TwoBoneIKConstraint leftFoot,
            TwoBoneIKConstraint rightFoot,
            Transform leftTarget,
            Transform rightTarget,
            AnimationRigSettingsAsset rigSettings,
            LayerMask ground)
        {
            rig = footRig;
            leftFootConstraint = leftFoot;
            rightFootConstraint = rightFoot;
            leftFootTarget = leftTarget;
            rightFootTarget = rightTarget;
            settings = rigSettings;
            groundMask = ground;
            _weight.SnapTo(0f);
            ApplyWeight(0f);
        }

        public Transform EnsureTarget(bool left)
        {
            TwoBoneIKConstraint constraint = left ? leftFootConstraint : rightFootConstraint;
            Transform target = left ? leftFootTarget : rightFootTarget;
            if (target == null)
            {
                GameObject targetObject = new GameObject(left ? "LeftFootTarget" : "RightFootTarget");
                target = targetObject.transform;
                target.SetParent(rig == null ? transform : rig.transform, false);
                if (left)
                {
                    leftFootTarget = target;
                }
                else
                {
                    rightFootTarget = target;
                }
            }

            if (constraint != null && constraint.data.target == null)
            {
                TwoBoneIKConstraintData data = constraint.data;
                data.target = target;
                constraint.data = data;
            }

            return target;
        }

        /// <summary>从当前脚骨位置向下探测地面并贴合。任一脚未命中地面则整体失败。</summary>
        public bool PlantFeet()
        {
            bool leftOk = PlantOneFoot(true);
            bool rightOk = PlantOneFoot(false);
            if (!leftOk || !rightOk)
            {
                ReleaseFeet();
                return false;
            }

            float blend = UpperBodyGunLayerController.BlendOrDefault(
                settings == null ? 0f : settings.footPlantBlendSeconds, 0.15f);
            _weight.SetTarget(1f, blend);
            return true;
        }

        public void ReleaseFeet()
        {
            float blend = UpperBodyGunLayerController.BlendOrDefault(
                settings == null ? 0f : settings.footPlantBlendSeconds, 0.15f);
            _weight.SetTarget(0f, blend);
        }

        public void CaptureInitialState()
        {
            _weight.SnapTo(0f);
            ApplyWeight(0f);
        }

        public void Quiesce(in LoopResetContext context)
        {
        }

        public void Restore(in LoopResetContext context)
        {
            _weight.SnapTo(0f);
            ApplyWeight(0f);
        }

        public void Resume(in LoopResetContext context)
        {
        }

        private void LateUpdate()
        {
            if (rig != null && !_weight.IsSettled)
            {
                ApplyWeight(_weight.Tick(Time.deltaTime));
            }
        }

        private bool PlantOneFoot(bool left)
        {
            TwoBoneIKConstraint constraint = left ? leftFootConstraint : rightFootConstraint;
            if (constraint == null || constraint.data.tip == null)
            {
                return false;
            }

            Transform target = EnsureTarget(left);
            Vector3 origin = constraint.data.tip.position + Vector3.up * raycastUp;
            if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, raycastUp + raycastDown,
                                 groundMask, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            target.position = hit.point;
            target.rotation = Quaternion.Euler(0f, constraint.data.tip.eulerAngles.y, 0f);
            return true;
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
