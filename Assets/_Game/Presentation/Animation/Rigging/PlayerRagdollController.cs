using ConGame.Runtime.Loop;
using UnityEngine;

namespace ConGame.Presentation.Animation.Rigging
{
    /// <summary>
    /// 玩家 Ragdoll 接管:HitFall/死亡时先播完中弹反应窗口(≤0.5s),
    /// 再把 Animator 切换为 Ragdoll 物理;LoopReset 复原绑姿与 Animator,切换无毛刺。
    /// 装配经 Configure 显式传入 Animator 与 Ragdoll 刚体数组,不做场景查找。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerRagdollController : MonoBehaviour, ILoopResettable
    {
        [SerializeField] private Animator animator;
        [SerializeField] private AnimationRigSettingsAsset settings;
        [SerializeField] private Rigidbody[] ragdollBodies;
        [SerializeField] private CharacterController playerController;

        private readonly RagdollPhaseController _phase = new RagdollPhaseController();
        private Vector3[] _bindLocalPositions;
        private Quaternion[] _bindLocalRotations;
        private Vector3 _pendingImpulse;
        private Rigidbody _impulseBody;
        private bool _captured;

        public int ResetOrder => 110;
        public RagdollPhase Phase => _phase.Phase;
        public bool IsRagdollActive => _phase.Phase == RagdollPhase.Ragdoll;

        public void Configure(
            Animator targetAnimator,
            Rigidbody[] bodies,
            AnimationRigSettingsAsset rigSettings,
            CharacterController controller = null)
        {
            animator = targetAnimator;
            ragdollBodies = bodies ?? new Rigidbody[0];
            settings = rigSettings;
            playerController = controller;
            _phase.Configure(rigSettings == null ? 0.35f : rigSettings.ragdollHitReactSeconds);
            _phase.Reset();
            _captured = false;
        }

        /// <summary>HitFall/死亡请求 Ragdoll 接管;命中冲量在切换当帧施加。</summary>
        public bool EngageRagdoll(Vector3 impulse, Rigidbody hitBody = null)
        {
            if (!_phase.BeginHit())
            {
                return false;
            }

            _pendingImpulse = impulse;
            _impulseBody = hitBody;
            return true;
        }

        /// <summary>未走 LoopReset 的主动恢复(如验证代码):回绑姿并恢复 Animator。</summary>
        public void RecoverToAnimated()
        {
            SetBodiesKinematic(true);
            RestoreBindPose();
            if (animator != null)
            {
                animator.enabled = true;
            }

            if (playerController != null)
            {
                playerController.enabled = true;
            }

            _phase.Reset();
        }

        private void Update()
        {
            if (_phase.Tick(Time.deltaTime))
            {
                EnterRagdollPhysics();
            }
        }

        public void CaptureInitialState()
        {
            EnsureBindPoseCaptured();
            SetBodiesKinematic(true);
            _phase.Configure(settings == null ? 0.35f : settings.ragdollHitReactSeconds);
            _phase.Reset();
        }

        public void Quiesce(in LoopResetContext context)
        {
            // 重置序:先停物理,避免 Restore 期间刚体继续模拟。
            StopBodyMotion();
            SetBodiesKinematic(true);
        }

        public void Restore(in LoopResetContext context)
        {
            RecoverToAnimated();
        }

        public void Resume(in LoopResetContext context)
        {
        }

        private void EnterRagdollPhysics()
        {
            EnsureBindPoseCaptured();
            if (playerController != null)
            {
                playerController.enabled = false;
            }

            SetBodiesKinematic(false);
            Rigidbody target = _impulseBody != null ? _impulseBody : (ragdollBodies.Length > 0 ? ragdollBodies[0] : null);
            if (target != null && _pendingImpulse.sqrMagnitude > 0.0001f)
            {
                target.AddForce(_pendingImpulse, ForceMode.Impulse);
            }

            _pendingImpulse = Vector3.zero;
            _impulseBody = null;
            if (animator != null)
            {
                // 先切换刚体再关 Animator,同帧完成,避免姿态回跳。
                animator.enabled = false;
            }
        }

        private void EnsureBindPoseCaptured()
        {
            if (_captured)
            {
                return;
            }

            ragdollBodies ??= new Rigidbody[0];
            _bindLocalPositions = new Vector3[ragdollBodies.Length];
            _bindLocalRotations = new Quaternion[ragdollBodies.Length];
            for (int i = 0; i < ragdollBodies.Length; i++)
            {
                Transform bone = ragdollBodies[i] == null ? null : ragdollBodies[i].transform;
                _bindLocalPositions[i] = bone == null ? Vector3.zero : bone.localPosition;
                _bindLocalRotations[i] = bone == null ? Quaternion.identity : bone.localRotation;
            }

            _captured = true;
        }

        private void RestoreBindPose()
        {
            EnsureBindPoseCaptured();
            for (int i = 0; i < ragdollBodies.Length; i++)
            {
                if (ragdollBodies[i] == null)
                {
                    continue;
                }

                ragdollBodies[i].transform.localPosition = _bindLocalPositions[i];
                ragdollBodies[i].transform.localRotation = _bindLocalRotations[i];
            }
        }

        private void StopBodyMotion()
        {
            for (int i = 0; i < ragdollBodies.Length; i++)
            {
                Rigidbody body = ragdollBodies[i];
                if (body != null && !body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
            }
        }

        private void SetBodiesKinematic(bool kinematic)
        {
            for (int i = 0; i < ragdollBodies.Length; i++)
            {
                if (ragdollBodies[i] != null)
                {
                    ragdollBodies[i].isKinematic = kinematic;
                }
            }
        }
    }
}
