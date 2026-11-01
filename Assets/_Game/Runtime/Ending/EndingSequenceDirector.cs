using System;
using System.Collections.Generic;
using ConGame.Domain.Facts;
using ConGame.Domain.Flow;
using ConGame.Runtime.AI;
using ConGame.Runtime.Flow;
using ConGame.Runtime.Input;
using ConGame.Runtime.Interaction;
using ConGame.Runtime.Loop;
using ConGame.Runtime.Player;
using UnityEngine;

namespace ConGame.Runtime.Ending
{
    public enum EndingSequenceKind
    {
        None = 0,
        E2 = 1,
        E3 = 2
    }

    /// <summary>
    /// E2/E3 统一结局阶段编排器。阶段时间线由纯 C# 的 EndingSequenceTimeline 驱动,
    /// 本组件只负责把 Enter/Exit 事件映射到运行时系统与表现事件。
    /// 取消边界与 GameFlowController 的 FlowTransitionRequest 优先级对齐:
    /// EndingCommitted(重入 EndingE1)可取消一切进行中的结局阶段;普通 Death 不能。
    /// 结局不触发 LoopReset;E3 标题拍结束后提交 Finished 并干净收束。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EndingSequenceDirector : MonoBehaviour
    {
        [SerializeField] private GameFlowController flow;
        [SerializeField] private LoopDirector loop;
        [SerializeField] private LoopResetRegistry resetRegistry;
        [SerializeField] private PlayerInputRouter input;
        [SerializeField] private PlayerMotor player;
        [SerializeField] private KillerDirector killer;
        [SerializeField] private TelevisionController television;
        [SerializeField] private EndingWallClockSecondHand wallClock;
        [SerializeField] private StatefulWorldItem backupPistol;
        [SerializeField] private EndingSequenceSettingsAsset settingsAsset;
        [SerializeField] private Transform firstPersonViewTransform;
        [SerializeField] private Transform endingChairAnchor;
        [SerializeField] private Transform endingKillerAnchor;

        private readonly List<EndingStageEvent> _eventBuffer = new List<EndingStageEvent>();
        private EndingSequenceTimeline _timeline;
        private EndingSequenceKind _sequence = EndingSequenceKind.None;
        private EndingSequenceSettings _settings;
        private int _epoch;
        private int _clockTicksIssued;
        private bool _e2Completed;
        private bool _e3Started;
        private string _e2OutcomeId = string.Empty;

        // ---- 表现层消费事件(Presentation 只订阅,不被 Runtime 引用) ----

        /// <summary>第一人称/固定机位硬切(true = 第一人称)。零时长切换,由相机表现执行。</summary>
        public event Action<bool> FirstPersonModeRequested;
        public event Action<bool> LookEnabledRequested;
        /// <summary>&lt;=0 表示清除偏航限制。</summary>
        public event Action<float> YawLimitRequested;
        public event Action LookResetRequested;
        public event Action<Vector3> LookAtRequested;
        public event Action<float> BlackoutRequested;
        public event Action ScreenPulseRequested;
        public event Action<float> TitleAmountRequested;
        /// <summary>E2 倒地期间每帧推送程序化相机姿态。</summary>
        public event Action<EndingFallPose> FallPoseUpdated;
        /// <summary>倒地拍结束(含取消),表现层恢复基准姿态。</summary>
        public event Action FallSequenceFinished;
        /// <summary>E3 神秘人进门/站定/瞄准拍开始,参数为总时长。</summary>
        public event Action<float> KillerApproachRequested;
        /// <summary>E3 备用枪开枪表现语义,全结局只发一次;与权威 ShotFired 的对齐由枪击表现管线负责。</summary>
        public event Action BackupPistolShotPerformed;
        /// <summary>E3 标题拍结束、Finished 已提交。</summary>
        public event Action SequenceFinished;

        public EndingSequenceKind ActiveSequence => _sequence;
        public bool IsRunning => _timeline != null && _timeline.State == EndingSequenceState.Running;
        public EndingStageId CurrentStage => _timeline == null ? EndingStageId.None : _timeline.CurrentStage;
        public bool ShotPerformed { get; private set; }
        public bool ClockAdvanced { get; private set; }
        public int PassResetCount { get; private set; }
        public string E2OutcomeId => _e2OutcomeId;
        public EndingSequenceSettings Settings => _settings ?? EndingSequenceSettings.Default;

        public void Configure(
            GameFlowController flowController,
            LoopDirector loopDirector,
            LoopResetRegistry registry,
            PlayerInputRouter inputRouter,
            PlayerMotor playerMotor,
            KillerDirector killerDirector,
            TelevisionController televisionController,
            Transform firstPersonView,
            Transform chairAnchor,
            Transform killerAnchor,
            EndingSequenceSettingsAsset settings = null,
            EndingWallClockSecondHand clock = null,
            StatefulWorldItem backupPistolItem = null)
        {
            Unsubscribe();
            flow = flowController;
            loop = loopDirector;
            resetRegistry = registry;
            input = inputRouter;
            player = playerMotor;
            killer = killerDirector;
            television = televisionController;
            firstPersonViewTransform = firstPersonView;
            endingChairAnchor = chairAnchor;
            endingKillerAnchor = killerAnchor;
            settingsAsset = settings;
            wallClock = clock;
            backupPistol = backupPistolItem;
            _settings = settingsAsset == null ? EndingSequenceSettings.Default : settingsAsset.CreateSettings();
            if (isActiveAndEnabled)
            {
                Subscribe();
            }
        }

        private void OnEnable()
        {
            _settings ??= settingsAsset == null ? EndingSequenceSettings.Default : settingsAsset.CreateSettings();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (flow != null)
            {
                flow.PhaseChanged -= OnPhaseChanged;
                flow.PhaseChanged += OnPhaseChanged;
            }
        }

        private void Unsubscribe()
        {
            if (flow != null)
            {
                flow.PhaseChanged -= OnPhaseChanged;
            }
        }

        /// <summary>幂等:E2 已在运行或已完成时返回 false。</summary>
        public bool TryBeginE2()
        {
            if (_sequence != EndingSequenceKind.None || _e2Completed)
            {
                return false;
            }

            BeginSequence(EndingSequenceKind.E2, EndingStageCatalog.BuildE2Stages(Settings));
            return true;
        }

        /// <summary>幂等:任何序列进行中或 E3 已开始时返回 false。</summary>
        public bool TryBeginE3()
        {
            if (_sequence != EndingSequenceKind.None || _e3Started)
            {
                return false;
            }

            _e3Started = true;
            BeginSequence(EndingSequenceKind.E3, EndingStageCatalog.BuildE3Stages(Settings));
            return true;
        }

        /// <summary>EndingCommitted 级取消:中断当前阶段并阻止后续阶段进入。幂等。</summary>
        public bool CancelSequence(string reason)
        {
            if (!IsRunning)
            {
                return false;
            }

            if (!_timeline.TryCancel(TransitionPriority.EndingCommitted, reason))
            {
                return false;
            }

            DrainAndDispatch();
            CleanupAfterCancel();
            _sequence = EndingSequenceKind.None;
            return true;
        }

        private void BeginSequence(EndingSequenceKind kind, EndingStageDefinition[] stages)
        {
            _sequence = kind;
            _epoch = loop == null ? 0 : loop.LoopEpoch;
            _clockTicksIssued = 0;
            if (kind == EndingSequenceKind.E2)
            {
                _e2OutcomeId = string.Empty;
            }

            _timeline = new EndingSequenceTimeline(stages);
            _timeline.Begin();
            DrainAndDispatch();
        }

        private void Update()
        {
            if (!IsRunning)
            {
                return;
            }

            if (loop != null && loop.LoopEpoch != _epoch)
            {
                CancelSequence("Loop epoch changed during ending sequence.");
                return;
            }

            _timeline.Advance(Time.deltaTime);
            TickStageFrameLogic();
            DrainAndDispatch();
        }

        private void TickStageFrameLogic()
        {
            if (!IsRunning)
            {
                return;
            }

            if (_timeline.CurrentStage == EndingStageId.E2Fall)
            {
                float duration = Mathf.Max(0.01f, Settings.E2FallSeconds);
                float t = Mathf.Clamp01(_timeline.CurrentStageElapsed / duration);
                FallPoseUpdated?.Invoke(EndingFallMotion.Sample(t, Settings));
            }
            else if (_timeline.CurrentStage == EndingStageId.E3ClockTick && wallClock != null)
            {
                int tickCount = Mathf.Max(0, Settings.E3ClockTickCount);
                float duration = Mathf.Max(0.01f, Settings.E3ClockTickSeconds);
                while (_clockTicksIssued < tickCount &&
                       _timeline.CurrentStageElapsed >= duration * (_clockTicksIssued + 1) / (tickCount + 1))
                {
                    wallClock.TickOnce();
                    _clockTicksIssued++;
                }
            }
        }

        private void DrainAndDispatch()
        {
            if (_timeline == null)
            {
                return;
            }

            _timeline.DrainEvents(_eventBuffer);
            for (int i = 0; i < _eventBuffer.Count; i++)
            {
                EndingStageEvent stageEvent = _eventBuffer[i];
                if (stageEvent.Kind == EndingStageEventKind.Enter)
                {
                    OnStageEntered(stageEvent.Stage);
                }
                else if (stageEvent.ExitReason != EndingStageExitReason.Cancelled)
                {
                    OnStageExited(stageEvent.Stage, stageEvent.ExitReason);
                }
            }

            _eventBuffer.Clear();

            if (_timeline != null && _timeline.State == EndingSequenceState.Completed)
            {
                FinalizeSequence();
            }
        }

        private void FinalizeSequence()
        {
            if (_sequence == EndingSequenceKind.E2)
            {
                _e2Completed = true;
            }
            else if (_sequence == EndingSequenceKind.E3)
            {
                SequenceFinished?.Invoke();
            }

            _sequence = EndingSequenceKind.None;
        }

        private void OnStageEntered(EndingStageId stage)
        {
            switch (stage)
            {
                case EndingStageId.E2FirstPersonTakeover:
                    FirstPersonModeRequested?.Invoke(true);
                    LookResetRequested?.Invoke();
                    if (television != null)
                    {
                        LookAtRequested?.Invoke(television.transform.position);
                    }

                    YawLimitRequested?.Invoke(0f);
                    LookEnabledRequested?.Invoke(true);
                    player?.SetMovementReference(firstPersonViewTransform, true);
                    input?.SetGameplayEnabled(true);
                    player?.SetInputLocked(false);
                    break;
                case EndingStageId.E2HuntToDeath:
                    if (killer == null)
                    {
                        _e2OutcomeId = "D01";
                        _timeline.CompleteCurrentStage();
                        break;
                    }

                    killer.SetEndingMode(true);
                    killer.gameObject.SetActive(true);
                    killer.BeginEntry(_epoch, OnKillerKillConfirmed);
                    break;
                case EndingStageId.E2Fall:
                    LockPlayer();
                    LookEnabledRequested?.Invoke(false);
                    ScreenPulseRequested?.Invoke();
                    killer?.SetEndingMode(false);
                    killer?.Quiesce(new LoopResetContext(_epoch, _epoch));
                    break;
                case EndingStageId.E2Blackout:
                    BlackoutRequested?.Invoke(1f);
                    FallSequenceFinished?.Invoke();
                    break;
                case EndingStageId.E3SitChair:
                    RestoreEndingPass();
                    FirstPersonModeRequested?.Invoke(true);
                    player?.SetMovementReference(firstPersonViewTransform, true);
                    player?.SetInputLocked(true);
                    input?.SetLookOnlyEnabled(true);
                    LookResetRequested?.Invoke();
                    if (endingKillerAnchor != null)
                    {
                        LookAtRequested?.Invoke(endingKillerAnchor.position);
                    }

                    YawLimitRequested?.Invoke(Settings.E3YawLimitDegrees);
                    LookEnabledRequested?.Invoke(true);
                    backupPistol?.EnterScriptedState();
                    BlackoutRequested?.Invoke(0f);
                    break;
                case EndingStageId.E3KillerApproach:
                    if (killer != null)
                    {
                        killer.gameObject.SetActive(true);
                        killer.SetEndingMode(true);
                    }

                    KillerApproachRequested?.Invoke(Settings.E3KillerApproachSeconds);
                    break;
                case EndingStageId.E3ClockTick:
                    _clockTicksIssued = 0;
                    wallClock?.ResetSequence();
                    break;
                case EndingStageId.E3BackupShot:
                    FireEndingShot();
                    break;
                case EndingStageId.E3Title:
                    loop?.MarkEndingCompleted();
                    TitleAmountRequested?.Invoke(1f);
                    BlackoutRequested?.Invoke(0.92f);
                    input?.SetGameplayEnabled(false);
                    LookEnabledRequested?.Invoke(false);
                    break;
            }
        }

        private void OnStageExited(EndingStageId stage, EndingStageExitReason reason)
        {
            switch (stage)
            {
                case EndingStageId.E2HuntToDeath:
                    if (reason == EndingStageExitReason.Timeout)
                    {
                        killer?.ForceImmediateKill("D01");
                        if (string.IsNullOrEmpty(_e2OutcomeId))
                        {
                            _e2OutcomeId = "D01";
                        }
                    }

                    break;
                case EndingStageId.E2Blackout:
                    flow?.Request(
                        GamePhase.EndingE3,
                        TransitionPriority.Normal,
                        string.IsNullOrEmpty(_e2OutcomeId) ? "First-person death completed." : _e2OutcomeId);
                    break;
                case EndingStageId.E3ClockTick:
                    if (wallClock != null)
                    {
                        wallClock.AdvancePastMark();
                    }

                    ClockAdvanced = true;
                    break;
                case EndingStageId.E3Title:
                    flow?.Request(
                        GamePhase.Finished,
                        TransitionPriority.Normal,
                        "The clock advanced and the final shot landed.");
                    break;
            }
        }

        private void OnKillerKillConfirmed(string outcomeId)
        {
            if (!IsRunning || _sequence != EndingSequenceKind.E2 ||
                _timeline.CurrentStage != EndingStageId.E2HuntToDeath)
            {
                return;
            }

            _e2OutcomeId = IsEndingOutcome(outcomeId) ? outcomeId : "D01";
            _timeline.CompleteCurrentStage();
            DrainAndDispatch();
        }

        private void OnPhaseChanged(GamePhase previous, GamePhase current, string reason)
        {
            switch (current)
            {
                case GamePhase.EndingE1:
                    // EndingCommitted 重入 E1:取消一切进行中的结局阶段。
                    CancelSequence("EndingE1 re-committed.");
                    break;
                case GamePhase.EndingE2:
                    TryBeginE2();
                    break;
                case GamePhase.EndingE3:
                    TryBeginE3();
                    break;
                case GamePhase.Finished:
                    if (IsRunning)
                    {
                        CancelSequence("Finished reached while ending sequence was running.");
                    }

                    break;
                case GamePhase.LoopReset:
                case GamePhase.Awakening:
                case GamePhase.Menu:
                    if (IsRunning)
                    {
                        CancelSequence($"Flow entered {current} during ending sequence.");
                    }

                    break;
            }
        }

        private void RestoreEndingPass()
        {
            if (resetRegistry != null)
            {
                resetRegistry.ResetAll(_epoch, _epoch);
                PassResetCount++;
            }

            television?.SetEndingDestroyed();
            killer?.SetEndingMode(false);
            killer?.Quiesce(new LoopResetContext(_epoch, _epoch));
            LockPlayer();

            if (player == null || endingChairAnchor == null)
            {
                return;
            }

            Vector3 position = endingChairAnchor.position;
            Quaternion rotation = endingChairAnchor.rotation;
            if (endingKillerAnchor != null)
            {
                Vector3 direction = endingKillerAnchor.position - position;
                direction.y = 0f;
                if (direction.sqrMagnitude > 0.0001f)
                {
                    rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
                }
            }

            player.Teleport(position, rotation);
            player.GetComponent<PlayerInteractor>()?.SetPose(PlayerPose.Sitting);
        }

        private void FireEndingShot()
        {
            if (ShotPerformed)
            {
                return;
            }

            ShotPerformed = true;
            BackupPistolShotPerformed?.Invoke();
            BlackoutRequested?.Invoke(1f);
        }

        private void CleanupAfterCancel()
        {
            LockPlayer();
            LookEnabledRequested?.Invoke(false);
            FallSequenceFinished?.Invoke();
            killer?.SetEndingMode(false);
            killer?.Quiesce(new LoopResetContext(_epoch, _epoch));
        }

        private void LockPlayer()
        {
            input?.SetGameplayEnabled(false);
            player?.SetInputLocked(true);
        }

        private static bool IsEndingOutcome(string outcomeId)
        {
            return string.Equals(outcomeId, "D01", StringComparison.Ordinal) ||
                   string.Equals(outcomeId, "D07", StringComparison.Ordinal) ||
                   string.Equals(outcomeId, "D10", StringComparison.Ordinal);
        }
    }
}
