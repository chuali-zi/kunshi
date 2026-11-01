
using ConGame.Runtime.AI;
using ConGame.Domain.Flow;
using ConGame.Runtime.Facts;
using ConGame.Runtime.Flow;
using ConGame.Runtime.Player;
using ConGame.Domain.Facts;
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
        private static readonly int LocomotionRateHash = Animator.StringToHash("LocomotionRate");
        private static readonly int WakeUpHash = Animator.StringToHash("WakeUp");
        private static readonly int SwingHash = Animator.StringToHash("Swing");
        private static readonly int HitFallHash = Animator.StringToHash("HitFall");
        private static readonly int AimFireHash = Animator.StringToHash("AimFire");
        private static readonly int DodgeHash = Animator.StringToHash("Dodge");
        private static readonly int ClimbHash = Animator.StringToHash("Climb");
        private static readonly int ClimbRateHash = Animator.StringToHash("ClimbRate");

        // AN_Player_Climb 剪辑长度 2.0s,按实际攀爬时长缩放播放速率
        private const float ClimbClipSeconds = 2.0f;

        [SerializeField] private DriverRole role;
        [SerializeField] private Animator animator;
        [SerializeField] private CharacterController characterController;
        [SerializeField] private NavMeshAgent agent;
        [SerializeField] private GameFlowController flow;

        [SerializeField] private KillerDirector killerDirector;
[SerializeField] private GameplayFactService facts;

        private PlayerMotor playerMotor;

        private bool _wasSwinging;
        private bool _receivedPhaseChange;
        private bool _liveShotPresented;
        private bool _awaitingSeatedAwakening;
        private float _scriptedMotionSpeed;

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

            if (killerDirector != null)
            {
                killerDirector.AimStarted += OnKillerAimStarted;
                killerDirector.AimCancelled += OnKillerAimCancelled;
                killerDirector.ShotFired += OnKillerShotFired;
            }

            if (playerMotor != null)
            {
                playerMotor.ClimbStarted += OnPlayerClimbStarted;
            }
        }

        private void OnDisable()
        {
            if (flow != null)
            {
                flow.PhaseChanged -= OnPhaseChanged;
            }

            if (killerDirector != null)
            {
                killerDirector.AimStarted -= OnKillerAimStarted;
                killerDirector.AimCancelled -= OnKillerAimCancelled;
                killerDirector.ShotFired -= OnKillerShotFired;
            }

            if (playerMotor != null)
            {
                playerMotor.ClimbStarted -= OnPlayerClimbStarted;
            }
        }

        private void Start()
        {
            // GameFlow initializes directly into Awakening, so the first phase has no transition event.
            if (!_receivedPhaseChange && flow != null && flow.CurrentPhase == GamePhase.Awakening)
            {
                ApplyPhase(GamePhase.Awakening);
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


            if (role == DriverRole.Killer && killerDirector != null)
            {
                speed = Mathf.Max(speed, killerDirector.MotionSpeed);
            }
else if (agent != null && agent.enabled)
            {
                speed = agent.velocity.magnitude;
            }

            speed = Mathf.Max(speed, _scriptedMotionSpeed);

            animator.SetFloat(SpeedHash, speed, 0.12f, Time.deltaTime);
            float referenceSpeed = role == DriverRole.Player ? 2.8f : 1.8f;
            if (HasParameter(LocomotionRateHash))
            {
                animator.SetFloat(LocomotionRateHash, Mathf.Clamp(speed / referenceSpeed, 0.75f, 1.4f));
            }

            if (role == DriverRole.Player && facts != null)
            {
                ApplyPlayerPose(facts.CaptureSnapshot().PlayerPose);

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
            _receivedPhaseChange = true;
            ApplyPhase(current);
        }

        private void ApplyPhase(GamePhase current)
        {
            if (animator == null || !animator.isActiveAndEnabled)
            {
                return;
            }

            switch (current)
            {
                case GamePhase.Awakening:
                    _liveShotPresented = false;
                    ResetTriggers();
                    if (role == DriverRole.Player)
                    {
                        bool seated = _awaitingSeatedAwakening ||
                                      (facts != null && facts.CaptureSnapshot().PlayerPose == PlayerPose.Sitting);
                        if (!seated)
                        {
                            animator.SetTrigger(WakeUpHash);
                        }
                    }

                    _awaitingSeatedAwakening = false;
                    break;
                case GamePhase.KillImpact:
                    if (!_liveShotPresented)
                    {
                        animator.SetTrigger(role == DriverRole.Killer ? AimFireHash : HitFallHash);
                    }

                    break;
                case GamePhase.LoopReset:
                    ResetTriggers();
                    if (role == DriverRole.Player && HasState("SitChair"))
                    {
                        animator.Play("SitChair", 0, 0f);
                        _awaitingSeatedAwakening = true;
                    }
                    else
                    {
                        animator.Play("Idle", 0, 0f);
                    }
                    break;
            }
        }

        private void ApplyPlayerPose(PlayerPose pose)
        {
            // Counterplay and death own their poses, including a seated no-input
            // death. A lingering Sitting fact must not override HitFall each frame.
            if (flow != null && (flow.CurrentPhase == GamePhase.Counterplay ||
                                flow.CurrentPhase == GamePhase.KillImpact ||
                                flow.CurrentPhase == GamePhase.LoopReset))
            {
                return;
            }
            if (!HasState("SitChair"))
            {
                return;
            }

            if (pose == PlayerPose.Sitting)
            {
                if (!IsCurrentOrNextState("SitChair") && !IsCurrentOrNextState("WakeUp"))
                {
                    animator.CrossFade("SitChair", 0.08f, 0, 0f);
                }
            }
            else if ((IsCurrentOrNextState("SitChair") || IsCurrentOrNextState("WakeUp")) &&
                     !IsTransitioningToState("Idle"))
            {
                animator.ResetTrigger(WakeUpHash);
                animator.CrossFade("Idle", 0.08f, 0, 0f);
            }
        }

        private bool IsCurrentOrNextState(string stateName)
        {
            AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
            if (current.IsName(stateName))
            {
                return true;
            }

            return animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsName(stateName);
        }

        private bool IsTransitioningToState(string stateName)
        {
            return animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsName(stateName);
        }

        private bool HasState(string stateName)
        {
            return animator != null && animator.HasState(0, Animator.StringToHash(stateName));
        }

        public void PlayAimFire()
        {
            if (role == DriverRole.Killer && animator != null && animator.isActiveAndEnabled)
            {
                animator.SetTrigger(AimFireHash);
            }
        }

        public void PlayDodge()
        {
            if (role == DriverRole.Killer && animator != null && animator.isActiveAndEnabled && HasParameter(DodgeHash))
            {
                animator.SetTrigger(DodgeHash);
            }
        }

        public void NotifyScriptedShotPresented()
        {
            _liveShotPresented = true;
        }

        public void SetScriptedMotionSpeed(float speed)
        {
            _scriptedMotionSpeed = Mathf.Max(0f, speed);
        }

        private bool HasParameter(int hash)
        {
            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.nameHash == hash)
                {
                    return true;
                }
            }

            return false;
        }

        private void ResetTriggers()
        {
            // 只重置本角色控制器里存在的触发器,避免 Animator 缺参警告
            if (role == DriverRole.Player)
            {
                animator.ResetTrigger(WakeUpHash);
                animator.ResetTrigger(SwingHash);
                animator.ResetTrigger(HitFallHash);
                if (HasParameter(ClimbHash))
                {
                    animator.ResetTrigger(ClimbHash);
                }
            }
            else
            {
                animator.ResetTrigger(AimFireHash);
                if (HasParameter(DodgeHash))
                {
                    animator.ResetTrigger(DodgeHash);
                }
            }

            _wasSwinging = false;
        }


        private void Awake()
        {
            killerDirector ??= GetComponentInParent<KillerDirector>();
            if (killerDirector == null)
            {
                killerDirector = FindObjectOfType<KillerDirector>(true);
            }

            playerMotor ??= GetComponentInParent<PlayerMotor>();
        }

        private void OnPlayerClimbStarted(float duration)
        {
            if (role != DriverRole.Player || animator == null || !animator.isActiveAndEnabled ||
                !HasParameter(ClimbHash))
            {
                return;
            }

            if (HasParameter(ClimbRateHash) && duration > 0.01f)
            {
                animator.SetFloat(ClimbRateHash, ClimbClipSeconds / duration);
            }

            animator.SetTrigger(ClimbHash);
        }


        private void OnKillerAimStarted(int loopEpoch)
        {
            if (role == DriverRole.Killer && animator != null && animator.isActiveAndEnabled)
            {
                animator.SetTrigger(AimFireHash);
            }
        }

        private void OnKillerAimCancelled(int loopEpoch)
        {
            if (role != DriverRole.Killer || animator == null || !animator.isActiveAndEnabled)
            {
                return;
            }

            animator.ResetTrigger(AimFireHash);
            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            if (state.IsName("AimFire"))
            {
                animator.CrossFade("Idle", 0.08f, 0, 0f);
            }
        }


        private void OnKillerShotFired(string outcomeId, int loopEpoch)
        {
            _liveShotPresented = true;
            if (role == DriverRole.Player && animator != null && animator.isActiveAndEnabled)
            {
                animator.SetTrigger(HitFallHash);
            }
        }
}
}
