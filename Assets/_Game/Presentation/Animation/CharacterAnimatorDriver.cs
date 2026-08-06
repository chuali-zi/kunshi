using ConGame.Domain.Flow;
using ConGame.Runtime.Facts;
using ConGame.Runtime.Flow;
using UnityEngine;
using UnityEngine.AI;

namespace ConGame.Presentation.Animation
{
    /// <summary>
    /// 把现有玩法信号(CharacterController/NavMeshAgent 速度、GamePhase、
    /// IsSwinging 事实)翻译成 Animator 参数,不侵入 PlayerMotor/KillerDirector。
    /// 参数约定:Speed(float)、WakeUp/Swing/HitFall/AimFire(trigger)。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterAnimatorDriver : MonoBehaviour
    {
        public enum DriverRole
        {
            Player = 0,
            Killer = 1
        }

        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int WakeUpHash = Animator.StringToHash("WakeUp");
        private static readonly int SwingHash = Animator.StringToHash("Swing");
        private static readonly int HitFallHash = Animator.StringToHash("HitFall");
        private static readonly int AimFireHash = Animator.StringToHash("AimFire");

        [SerializeField] private DriverRole role;
        [SerializeField] private Animator animator;
        [SerializeField] private CharacterController characterController;
        [SerializeField] private NavMeshAgent agent;
        [SerializeField] private GameFlowController flow;
        [SerializeField] private GameplayFactService facts;

        private bool _wasSwinging;

        public void Configure(
            DriverRole driverRole,
            Animator targetAnimator,
            CharacterController controller,
            NavMeshAgent navAgent,
            GameFlowController flowController,
            GameplayFactService factService)
        {
            role = driverRole;
            animator = targetAnimator;
            characterController = controller;
            agent = navAgent;
            flow = flowController;
            facts = factService;
        }

        private void OnEnable()
        {
            if (flow != null)
            {
                flow.PhaseChanged += OnPhaseChanged;
            }
        }

        private void OnDisable()
        {
            if (flow != null)
            {
                flow.PhaseChanged -= OnPhaseChanged;
            }
        }

        private void Update()
        {
            if (animator == null || !animator.isActiveAndEnabled)
            {
                return;
            }

            float speed = 0f;
            if (characterController != null && characterController.enabled)
            {
                Vector3 velocity = characterController.velocity;
                velocity.y = 0f;
                speed = velocity.magnitude;
            }
            else if (agent != null && agent.enabled)
            {
                speed = agent.velocity.magnitude;
            }

            animator.SetFloat(SpeedHash, speed, 0.12f, Time.deltaTime);

            if (role == DriverRole.Player && facts != null)
            {
                bool swinging = facts.CaptureSnapshot().IsSwinging;
                if (swinging && !_wasSwinging)
                {
                    animator.SetTrigger(SwingHash);
                }

                _wasSwinging = swinging;
            }
        }

        private void OnPhaseChanged(GamePhase previous, GamePhase current, string reason)
        {
            if (animator == null || !animator.isActiveAndEnabled)
            {
                return;
            }

            switch (current)
            {
                case GamePhase.Awakening:
                    ResetTriggers();
                    if (role == DriverRole.Player)
                    {
                        animator.SetTrigger(WakeUpHash);
                    }

                    break;
                case GamePhase.KillImpact:
                    animator.SetTrigger(role == DriverRole.Killer ? AimFireHash : HitFallHash);
                    break;
                case GamePhase.LoopReset:
                    ResetTriggers();
                    animator.Play("Idle", 0, 0f);
                    break;
            }
        }

        public void PlayAimFire()
        {
            if (role == DriverRole.Killer && animator != null && animator.isActiveAndEnabled)
            {
                animator.SetTrigger(AimFireHash);
            }
        }

        private void ResetTriggers()
        {
            // 只重置本角色控制器里存在的触发器,避免 Animator 缺参警告
            if (role == DriverRole.Player)
            {
                animator.ResetTrigger(WakeUpHash);
                animator.ResetTrigger(SwingHash);
                animator.ResetTrigger(HitFallHash);
            }
            else
            {
                animator.ResetTrigger(AimFireHash);
            }

            _wasSwinging = false;
        }
    }
}
