using System;
using System.Collections.Generic;
using ConGame.Domain.Flow;

namespace ConGame.Runtime.Ending
{

    /// <summary>
    /// 结局阶段时间线状态机。纯 C#、无 UnityEngine 依赖,供 EditMode 测试直接驱动。
    /// 阶段按列表顺序执行,每阶段有 Enter/Exit 事件;外部权威回调可完成门控阶段;
    /// 取消语义与 GameFlowMachine 的 FlowTransitionRequest 优先级对齐:
    /// 只有 EndingCommitted 能取消进行中的结局阶段,Normal/Death 一律拒绝。
    /// </summary>
    public sealed class EndingSequenceTimeline
    {
        private readonly EndingStageDefinition[] _stages;
        private readonly List<EndingStageEvent> _pendingEvents = new List<EndingStageEvent>();
        private int _index = -1;
        private float _elapsed;

        public EndingSequenceTimeline(IReadOnlyList<EndingStageDefinition> stages)
        {
            if (stages == null || stages.Count == 0)
            {
                throw new ArgumentException("Ending sequence requires at least one stage.", nameof(stages));
            }

            _stages = new EndingStageDefinition[stages.Count];
            for (int i = 0; i < stages.Count; i++)
            {
                if (stages[i].Id == EndingStageId.None)
                {
                    throw new ArgumentException("Ending stage id None is not allowed.", nameof(stages));
                }

                for (int j = 0; j < i; j++)
                {
                    if (_stages[j].Id == stages[i].Id)
                    {
                        throw new ArgumentException(
                            $"Duplicate ending stage id {stages[i].Id}.", nameof(stages));
                    }
                }

                _stages[i] = stages[i];
            }

            State = EndingSequenceState.Idle;
        }

        public EndingSequenceState State { get; private set; }

        public string CancelReason { get; private set; } = string.Empty;

        public int CurrentStageIndex => _index;

        public float CurrentStageElapsed => _elapsed;

        public EndingStageId CurrentStage =>
            State == EndingSequenceState.Running && _index >= 0 && _index < _stages.Length
                ? _stages[_index].Id
                : EndingStageId.None;

        /// <summary>幂等:只有 Idle 状态能开始,重复调用返回 false 且不产生事件。</summary>
        public bool Begin()
        {
            if (State != EndingSequenceState.Idle)
            {
                return false;
            }

            State = EndingSequenceState.Running;
            EnterStage(0);
            return true;
        }

        public void Advance(float deltaTime)
        {
            if (State != EndingSequenceState.Running || deltaTime <= 0f)
            {
                return;
            }

            _elapsed += deltaTime;
            EndingStageDefinition stage = _stages[_index];
            if (stage.WaitForExternalCompletion)
            {
                if (stage.TimeoutSeconds > 0f && _elapsed >= stage.TimeoutSeconds)
                {
                    ExitCurrentStage(EndingStageExitReason.Timeout);
                }

                return;
            }

            if (_elapsed >= stage.DurationSeconds)
            {
                ExitCurrentStage(EndingStageExitReason.Completed);
            }
        }

        /// <summary>权威回调(如 E2 击杀完成)显式结束当前门控阶段;非门控阶段拒绝。</summary>
        public bool CompleteCurrentStage()
        {
            if (State != EndingSequenceState.Running || !_stages[_index].WaitForExternalCompletion)
            {
                return false;
            }

            ExitCurrentStage(EndingStageExitReason.Completed);
            return true;
        }

        /// <summary>
        /// 取消边界:仅 EndingCommitted 可取消进行中的时间线(与 GameFlowMachine 优先级一致)。
        /// 幂等:非 Running 状态或低优先级请求返回 false。
        /// </summary>
        public bool TryCancel(TransitionPriority priority, string reason)
        {
            if (State != EndingSequenceState.Running || priority < TransitionPriority.EndingCommitted)
            {
                return false;
            }

            CancelReason = reason ?? string.Empty;
            ExitCurrentStage(EndingStageExitReason.Cancelled);
            return true;
        }

        /// <summary>取走自上次调用以来累积的 Enter/Exit 事件。</summary>
        public int DrainEvents(List<EndingStageEvent> output)
        {
            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            int count = _pendingEvents.Count;
            output.AddRange(_pendingEvents);
            _pendingEvents.Clear();
            return count;
        }

        private void EnterStage(int index)
        {
            _index = index;
            _elapsed = 0f;
            _pendingEvents.Add(EndingStageEvent.Entered(_stages[index].Id));
        }

        private void ExitCurrentStage(EndingStageExitReason reason)
        {
            _pendingEvents.Add(EndingStageEvent.Exited(_stages[_index].Id, reason));
            if (reason == EndingStageExitReason.Cancelled)
            {
                State = EndingSequenceState.Cancelled;
                return;
            }

            int next = _index + 1;
            if (next >= _stages.Length)
            {
                State = EndingSequenceState.Completed;
                return;
            }

            EnterStage(next);
        }
    }
}
