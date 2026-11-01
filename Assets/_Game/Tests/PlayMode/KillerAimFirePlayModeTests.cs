using System.Collections;
using ConGame.Bootstrap;
using ConGame.Domain.Flow;
using ConGame.Presentation.Animation;
using ConGame.Runtime.AI;
using ConGame.Runtime.Events;
using ConGame.Runtime.Facts;
using ConGame.Runtime.Flow;
using ConGame.Runtime.Input;
using ConGame.Runtime.Loop;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ConGame.Tests.PlayMode
{
    /// <summary>
    /// 黑衣人开枪(AimFire)动作:驱动层把开火触发到 Animator,并在 E3 结局枪杀时播放。
    /// </summary>
    public sealed class KillerAimFirePlayModeTests
    {
        private static readonly int AimFireHash = Animator.StringToHash("AimFire");

        [UnityTest]
        public IEnumerator KillerRoleSetsAimFireTriggerOnPlayAimFire()
        {
#if UNITY_EDITOR
            GameObject go = new GameObject("killer_driver");
            try
            {
                Animator animator = go.AddComponent<Animator>();
                animator.runtimeAnimatorController = BuildAimFireController();
                CharacterAnimatorDriver driver = go.AddComponent<CharacterAnimatorDriver>();
                driver.Configure(CharacterAnimatorDriver.DriverRole.Killer, animator, null, null, null, null);

                yield return null; // 让 Animator 初始化并进入默认 Idle

                driver.PlayAimFire();

                // 触发已设置、尚未被下一帧的状态机评估消费
                Assert.That(animator.GetBool(AimFireHash), Is.True);
            }
            finally
            {
                Object.Destroy(go);
            }
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator AimCancellationReturnsKillerAnimatorToIdle()
        {
#if UNITY_EDITOR
            GameObject go = new GameObject("killer_cancel_driver");
            try
            {
                Animator animator = go.AddComponent<Animator>();
                animator.runtimeAnimatorController = BuildAimFireController();
                CharacterAnimatorDriver driver = go.AddComponent<CharacterAnimatorDriver>();
                driver.Configure(CharacterAnimatorDriver.DriverRole.Killer, animator, null, null, null, null);
                yield return null;

                driver.PlayAimFire();
                yield return null;
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("AimFire"), Is.True);

                var cancel = typeof(CharacterAnimatorDriver).GetMethod(
                    "OnKillerAimCancelled",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                Assert.That(cancel, Is.Not.Null);
                cancel.Invoke(driver, new object[] { 1 });
                yield return new WaitForSeconds(0.12f);

                Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"), Is.True,
                    "Losing the firing solution must visibly cancel AimFire.");
            }
            finally
            {
                Object.Destroy(go);
            }
#else
            yield break;
#endif
        }


        [UnityTest]
        public IEnumerator PlayerRolePlayAimFireIsNoOp()
        {
#if UNITY_EDITOR
            GameObject go = new GameObject("player_driver");
            try
            {
                Animator animator = go.AddComponent<Animator>();
                animator.runtimeAnimatorController = BuildAimFireController();
                CharacterAnimatorDriver driver = go.AddComponent<CharacterAnimatorDriver>();
                driver.Configure(CharacterAnimatorDriver.DriverRole.Player, animator, null, null, null, null);

                yield return null;

                driver.PlayAimFire();

                // 玩家角色不持枪,PlayAimFire 应当无效
                Assert.That(animator.GetBool(AimFireHash), Is.False);
            }
            finally
            {
                Object.Destroy(go);
            }
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator InitialAwakeningTriggersPlayerWakeUp()
        {
#if UNITY_EDITOR
            GameObject go = new GameObject("player_wakeup_driver");
            GameObject systems = new GameObject("flow");
            try
            {
                GameFlowController flow = systems.AddComponent<GameFlowController>();
                flow.Initialize(GamePhase.Awakening);
                Animator animator = go.AddComponent<Animator>();
                animator.runtimeAnimatorController = BuildWakeUpController();
                CharacterAnimatorDriver driver = go.AddComponent<CharacterAnimatorDriver>();
                driver.Configure(CharacterAnimatorDriver.DriverRole.Player, animator, null, null, flow, null);

                yield return null;

                Assert.That(animator.GetBool(Animator.StringToHash("WakeUp")), Is.True);
            }
            finally
            {
                Object.Destroy(go);
                Object.Destroy(systems);
            }
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator PlayerAnimatorFollowsSittingAndStandingFacts()
        {
#if UNITY_EDITOR
            GameObject go = new GameObject("player_seated_pose_driver");
            try
            {
                GameplayFactService facts = go.AddComponent<GameplayFactService>();
                facts.CaptureInitialState();
                facts.SetPlayerPose(ConGame.Domain.Facts.PlayerPose.Sitting);

                Animator animator = go.AddComponent<Animator>();
                animator.runtimeAnimatorController = BuildSeatedPoseController();
                CharacterAnimatorDriver driver = go.AddComponent<CharacterAnimatorDriver>();
                driver.Configure(CharacterAnimatorDriver.DriverRole.Player, animator, null, null, null, facts);

                // CharacterAnimatorDriver cross-fades the pose; allow its authored transition
                // time to complete before asserting the destination state.
                yield return new WaitForSeconds(0.1f);
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("SitChair"), Is.True,
                    "Sitting facts should present the authored chair pose.");

                facts.SetPlayerPose(ConGame.Domain.Facts.PlayerPose.Standing);
                yield return new WaitForSeconds(0.15f);
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"), Is.True,
                    "A standing pose should exit the chair pose.");
            }
            finally
            {
                Object.Destroy(go);
            }
#else
            yield break;
#endif
        }

        [Test]
        public void FirstMeaningfulActionStandsPlayerWithoutWaitingForAwakeningTimer()
        {
            GameObject root = new GameObject("seated_awakening_flow");
            try
            {
                GameFlowController flow = root.AddComponent<GameFlowController>();
                GameplayFactService facts = root.AddComponent<GameplayFactService>();
                facts.CaptureInitialState();
                GameplayEventHub events = root.AddComponent<GameplayEventHub>();
                PlayerInputRouter input = root.AddComponent<PlayerInputRouter>();
                LoopDirector loop = root.AddComponent<LoopDirector>();
                loop.Configure(null, flow, null, facts, events, input, null, null);
                loop.Initialize();
                facts.SetPlayerPose(ConGame.Domain.Facts.PlayerPose.Sitting);

                Assert.That(facts.CaptureSnapshot().PlayerPose, Is.EqualTo(ConGame.Domain.Facts.PlayerPose.Sitting));
                loop.NotifyMeaningfulAction();

                Assert.That(facts.CaptureSnapshot().PlayerPose, Is.EqualTo(ConGame.Domain.Facts.PlayerPose.Standing));
                Assert.That(flow.CommitPending(), Is.True);
                Assert.That(flow.CurrentPhase, Is.EqualTo(GamePhase.FreeRoam));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [UnityTest]
        public IEnumerator EndingShotPlaysKillerAimFire()
        {
#if UNITY_EDITOR
            GameObject systems = new GameObject("ending_systems");
            GameObject killerObject = new GameObject("Killer");
            try
            {
                GameFlowController flow = systems.AddComponent<GameFlowController>();
                flow.Initialize(GamePhase.FreeRoam);
                LoopDirector loop = systems.AddComponent<LoopDirector>();

                Animator animator = killerObject.AddComponent<Animator>();
                animator.runtimeAnimatorController = BuildAimFireController();
                KillerDirector killer = killerObject.AddComponent<KillerDirector>();
                killer.CaptureInitialState();
                CharacterAnimatorDriver driver = killerObject.AddComponent<CharacterAnimatorDriver>();
                driver.Configure(CharacterAnimatorDriver.DriverRole.Killer, animator, null, null, flow, null);

                EndingDirector ending = systems.AddComponent<EndingDirector>();
                ending.enabled = false;
                ending.Configure(flow, loop, null, null, null, killer, null, null, null, null, string.Empty);
                ending.enabled = true;

                bool aimFireSetAtShot = false;
                ending.EndingShotFired += () => aimFireSetAtShot = animator.GetBool(AimFireHash);

                Assert.That(flow.Request(GamePhase.EndingE1, TransitionPriority.EndingCommitted, "tv"), Is.True);
                Assert.That(flow.CommitPending(), Is.True);

                float elapsed = 0f;
                while (!ending.ShotFired && elapsed < 30f)
                {
                    elapsed += Time.deltaTime;
                    yield return null;
                }

                Assert.That(ending.ShotFired, Is.True, "结局枪杀应当触发");
                Assert.That(aimFireSetAtShot, Is.True, "结局枪杀时黑衣人应当播放 AimFire 开枪动作");
            }
            finally
            {
                Object.Destroy(systems);
                Object.Destroy(killerObject);
            }
#else
            yield break;
#endif
        }

#if UNITY_EDITOR
        // 运行时无法创建 AnimatorController,借编辑器 API 在内存中搭一个最小控制器:
        // Speed(float)+AimFire(trigger),AnyState -> AimFire。
        private static RuntimeAnimatorController BuildAimFireController()
        {
            var controller = new UnityEditor.Animations.AnimatorController();
            controller.name = "AC_KillerAimFireTest";
            controller.AddLayer("Base");
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("AimFire", AnimatorControllerParameterType.Trigger);

            UnityEditor.Animations.AnimatorStateMachine sm = controller.layers[0].stateMachine;
            UnityEditor.Animations.AnimatorState idle = sm.AddState("Idle");
            UnityEditor.Animations.AnimatorState fire = sm.AddState("AimFire");
            sm.defaultState = idle;

            UnityEditor.Animations.AnimatorStateTransition entry = sm.AddAnyStateTransition(fire);
            entry.AddCondition(UnityEditor.Animations.AnimatorConditionMode.If, 0f, "AimFire");
            entry.hasExitTime = false;
            entry.duration = 0f;
            return controller;
        }

        private static RuntimeAnimatorController BuildWakeUpController()
        {
            var controller = new UnityEditor.Animations.AnimatorController();
            controller.name = "AC_PlayerWakeUpTest";
            controller.AddLayer("Base");
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("WakeUp", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Swing", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("HitFall", AnimatorControllerParameterType.Trigger);
            controller.layers[0].stateMachine.defaultState =
                controller.layers[0].stateMachine.AddState("Idle");
            return controller;
        }

        private static RuntimeAnimatorController BuildSeatedPoseController()
        {
            var controller = new UnityEditor.Animations.AnimatorController();
            controller.name = "AC_PlayerSeatedPoseTest";
            controller.AddLayer("Base");
            UnityEditor.Animations.AnimatorStateMachine sm = controller.layers[0].stateMachine;
            UnityEditor.Animations.AnimatorState idle = sm.AddState("Idle");
            sm.AddState("SitChair");
            sm.defaultState = idle;
            return controller;
        }
#endif
    }
}
