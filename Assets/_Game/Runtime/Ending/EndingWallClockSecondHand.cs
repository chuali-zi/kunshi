using System;
using ConGame.Domain.Interaction;
using ConGame.Runtime.Interaction;
using UnityEngine;

namespace ConGame.Runtime.Ending
{

    /// <summary>
    /// E3 挂钟秒针:编排器驱动若干次嗒声小步进,最后越过原格(全游戏唯一一次时间前进)。
    /// 挂钟若已被破坏(ItemPhysicalState.Broken)或缺秒针引用,降级为只发语义事件、不旋转。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EndingWallClockSecondHand : MonoBehaviour
    {
        [SerializeField] private Transform secondHand;
        [SerializeField] private StatefulWorldItem clockItem;
        [SerializeField] private float tickDegrees = 1.5f;
        [SerializeField] private float markAdvanceDegrees = 6f;

        public event Action Ticked;
        public event Action MarkPassed;

        public int TickCount { get; private set; }
        public bool MarkAdvanced { get; private set; }
        public bool Degraded { get; private set; }

        public void Configure(Transform hand, StatefulWorldItem item)
        {
            secondHand = hand;
            clockItem = item;
        }

        public void ResetSequence()
        {
            TickCount = 0;
            MarkAdvanced = false;
            Degraded = false;
        }

        public bool TickOnce()
        {
            if (IsUnavailable())
            {
                Degraded = true;
                return false;
            }

            secondHand.localRotation *= Quaternion.Euler(0f, 0f, -tickDegrees);
            TickCount++;
            Ticked?.Invoke();
            return true;
        }

        /// <summary>秒针越过原格。幂等:重复调用不再旋转、不重复发事件。</summary>
        public bool AdvancePastMark()
        {
            if (MarkAdvanced)
            {
                return false;
            }

            MarkAdvanced = true;
            if (IsUnavailable())
            {
                Degraded = true;
                MarkPassed?.Invoke();
                return false;
            }

            secondHand.localRotation *= Quaternion.Euler(0f, 0f, -markAdvanceDegrees);
            MarkPassed?.Invoke();
            return true;
        }

        private bool IsUnavailable()
        {
            return secondHand == null ||
                   (clockItem != null && clockItem.CurrentState == ItemPhysicalState.Broken);
        }
    }
}
