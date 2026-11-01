using System;
using System.Collections.Generic;
using ConGame.Domain.Counterplay;
using ConGame.Domain.Facts;
using ConGame.Domain.Interaction;
using ConGame.Runtime.AI;
using ConGame.Runtime.Counterplay;
using ConGame.Runtime.Ending;
using ConGame.Runtime.Events;
using ConGame.Runtime.Interaction;
using ConGame.Runtime.Lighting;
using UnityEngine;

namespace ConGame.Presentation.Vfx
{
    [Serializable]
    public struct VfxPoolBinding
    {
        public VfxCueId Cue;
        public VfxPool Pool;
    }

    /// <summary>
    /// 语义 VFX 导演,模式对齐 <c>SemanticAudioDirector</c>:
    /// 消费 ImpactResolved、ShotFired、破坏状态(椅/瓶/镜/钟/灯)、电视状态和
    /// Performance 语义事件,翻译成 <see cref="VfxCueId"/> 并从 <see cref="VfxPool"/> 借出实例。
    /// 所有绑定经 Configure/Register* 显式装配,不做场景查找。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SemanticVfxDirector : MonoBehaviour
    {
        [SerializeField] private GameplayEventHub events;
        [SerializeField] private KillerDirector killer;
        [SerializeField] private TelevisionController television;
        [SerializeField] private CounterplayPerformanceRuntimeAdapter performanceAdapter;
        [SerializeField] private Transform muzzleAnchor;
        [SerializeField] private Transform performanceAnchor;
        [SerializeField] private VfxPoolBinding[] pools;

        private readonly Dictionary<VfxCueId, VfxPool> _poolByCue = new Dictionary<VfxCueId, VfxPool>();
        private readonly Dictionary<Component, VfxCueId> _receiverCues = new Dictionary<Component, VfxCueId>();
        private readonly Dictionary<StatefulWorldItem, VfxCueId> _breakableItems =
            new Dictionary<StatefulWorldItem, VfxCueId>();
        private readonly Dictionary<StatefulWorldItem, Action<ItemPhysicalState>> _breakableHandlers =
            new Dictionary<StatefulWorldItem, Action<ItemPhysicalState>>();
        private readonly Dictionary<BreakableLight, VfxCueId> _breakableLights =
            new Dictionary<BreakableLight, VfxCueId>();
        private readonly Dictionary<string, VfxCueId> _performanceCues =
            new Dictionary<string, VfxCueId>(StringComparer.OrdinalIgnoreCase);

        public void Configure(
            GameplayEventHub gameplayEvents,
            KillerDirector killerDirector,
            TelevisionController televisionController,
            CounterplayPerformanceRuntimeAdapter adapter,
            Transform muzzle,
            Transform performance)
        {
            events = gameplayEvents;
            killer = killerDirector;
            television = televisionController;
            performanceAdapter = adapter;
            muzzleAnchor = muzzle;
            performanceAnchor = performance;
            if (isActiveAndEnabled)
            {
                UnsubscribeAll();
                SubscribeAll();
            }
        }

        public void RegisterPool(VfxCueId cue, VfxPool pool)
        {
            if (pool == null)
            {
                return;
            }

            _poolByCue[cue] = pool;
        }

        /// <summary>命中接收者 → 语义 cue(例:玩家身体 → BloodImpact,墙面 → WallDust)。</summary>
        public void MapReceiver(Component receiver, VfxCueId cue)
        {
            if (receiver != null)
            {
                _receiverCues[receiver] = cue;
            }
        }

        /// <summary>易碎物(椅/瓶/镜/钟)→ 破坏碎片 cue。</summary>
        public void RegisterBreakableItem(StatefulWorldItem item, VfxCueId cue)
        {
            if (item == null)
            {
                return;
            }

            if (!_breakableItems.ContainsKey(item))
            {
                _breakableItems.Add(item, cue);
                Action<ItemPhysicalState> handler = state => OnBreakableItemStateChanged(item, state);
                _breakableHandlers.Add(item, handler);
                if (_subscribed)
                {
                    item.StateChanged += handler;
                }
            }
        }

        /// <summary>可破坏灯(顶灯/台灯)→ 玻璃爆裂 cue。</summary>
        public void RegisterBreakableLight(BreakableLight lightState, VfxCueId cue)
        {
            if (lightState != null)
            {
                _breakableLights[lightState] = cue;
            }
        }

        /// <summary>Performance 语义轨:outcomeId → cue,播放在 performanceAnchor。</summary>
        public void MapPerformanceOutcome(string outcomeId, VfxCueId cue)
        {
            if (!string.IsNullOrEmpty(outcomeId))
            {
                _performanceCues[outcomeId] = cue;
            }
        }

        public bool PlayCueAt(VfxCueId cue, Vector3 position, Vector3 normal)
        {
            EnsurePoolTable();
            if (!_poolByCue.TryGetValue(cue, out VfxPool pool) || pool == null)
            {
                return false;
            }

            Quaternion rotation = normal.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(normal)
                : Quaternion.identity;
            return pool.Acquire(position, rotation) != null;
        }

        /// <summary>公开射击语义入口,KillerDirector.ShotFired 之外也可由验证代码调用。</summary>
        public void NotifyShotFired()
        {
            if (muzzleAnchor != null)
            {
                PlayCueAt(VfxCueId.MuzzleFlash, muzzleAnchor.position, muzzleAnchor.forward);
            }
        }

        public void NotifyShotFired(Transform weaponOrMuzzle)
        {
            Transform muzzle = FindShotMuzzle(weaponOrMuzzle);
            if (muzzle != null)
            {
                PlayCueAt(VfxCueId.MuzzleFlash, muzzle.position, muzzle.forward);
            }
            else if (weaponOrMuzzle != null)
            {
                PlayCueAt(VfxCueId.MuzzleFlash, weaponOrMuzzle.position, weaponOrMuzzle.forward);
            }
            else
            {
                NotifyShotFired();
            }
        }

        public void OnD22ShotCueRequested(bool terminalShot, Transform source, int loopEpoch)
        {
            if (terminalShot && source != null)
            {
                source = FindActiveNamedChild(source, "PRP_PistolBackup_Holstered") ?? source;
            }

            NotifyShotFired(source);
        }

        private static Transform FindActiveNamedChild(Transform root, string targetName)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.gameObject.activeInHierarchy && string.Equals(child.name, targetName,
                    StringComparison.Ordinal))
                {
                    return child;
                }

                Transform nested = FindActiveNamedChild(child, targetName);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }

        private static Transform FindShotMuzzle(Transform source)
        {
            if (source == null)
            {
                return null;
            }

            for (int i = 0; i < source.childCount; i++)
            {
                Transform child = source.GetChild(i);
                if (!child.gameObject.activeInHierarchy)
                {
                    continue;
                }
                if (string.Equals(child.name, "SOCKET_Muzzle", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(child.name, "Muzzle", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(child.name, "MuzzleAnchor", StringComparison.OrdinalIgnoreCase))
                {
                    return child;
                }

                Transform nested = FindShotMuzzle(child);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }

        private void Awake()
        {
            EnsurePoolTable();
        }

        private void OnEnable()
        {
            SubscribeAll();
        }

        private void OnDisable()
        {
            UnsubscribeAll();
        }

        private bool _subscribed;

        private void SubscribeAll()
        {
            if (_subscribed)
            {
                return;
            }

            _subscribed = true;
            if (events != null)
            {
                events.ImpactResolved += OnImpactResolved;
            }

            if (killer != null)
            {
                killer.ShotFired += OnShotFired;
            }

            if (television != null)
            {
                television.DamageChanged += OnTelevisionDamageChanged;
            }

            if (performanceAdapter != null)
            {
                performanceAdapter.PerformanceStarted += OnPerformanceStarted;
            }

            foreach (KeyValuePair<StatefulWorldItem, Action<ItemPhysicalState>> pair in _breakableHandlers)
            {
                if (pair.Key != null)
                {
                    pair.Key.StateChanged += pair.Value;
                }
            }
        }

        private void UnsubscribeAll()
        {
            if (!_subscribed)
            {
                return;
            }

            _subscribed = false;
            if (events != null)
            {
                events.ImpactResolved -= OnImpactResolved;
            }

            if (killer != null)
            {
                killer.ShotFired -= OnShotFired;
            }

            if (television != null)
            {
                television.DamageChanged -= OnTelevisionDamageChanged;
            }

            if (performanceAdapter != null)
            {
                performanceAdapter.PerformanceStarted -= OnPerformanceStarted;
            }

            foreach (KeyValuePair<StatefulWorldItem, Action<ItemPhysicalState>> pair in _breakableHandlers)
            {
                if (pair.Key != null)
                {
                    pair.Key.StateChanged -= pair.Value;
                }
            }
        }

        private void OnImpactResolved(ImpactEvent impact)
        {
            VfxCueId cue;
            if (impact.Receiver != null && _receiverCues.TryGetValue(impact.Receiver, out cue))
            {
                // 显式接收者绑定优先。
            }
            else if (impact.Result == HitResult.Broke && impact.Receiver is BreakableLight brokenLight &&
                     _breakableLights.TryGetValue(brokenLight, out cue))
            {
                // 灯玻璃爆裂绑定。
            }
            else if (!VfxImpactSemantics.TryGetDefaultCue(impact.Hit.Kind, impact.Result, out cue))
            {
                return;
            }

            Vector3 normal = impact.Hit.Direction.sqrMagnitude > 0.0001f ? -impact.Hit.Direction : Vector3.up;
            PlayCueAt(cue, impact.Hit.Point, normal);
        }

        private void OnShotFired(string outcomeId, int loopEpoch)
        {
            NotifyShotFired();
        }

        private void OnBreakableItemStateChanged(StatefulWorldItem item, ItemPhysicalState state)
        {
            if (state != ItemPhysicalState.Broken || item == null ||
                !_breakableItems.TryGetValue(item, out VfxCueId cue))
            {
                return;
            }

            PlayCueAt(cue, item.transform.position, Vector3.up);
        }

        private void OnTelevisionDamageChanged(TelevisionDamageState state)
        {
            if (state != TelevisionDamageState.Destroyed || television == null)
            {
                return;
            }

            PlayCueAt(VfxCueId.TelevisionBurst, television.transform.position, Vector3.up);
        }

        private void OnPerformanceStarted(string outcomeId, int loopEpoch, PerformanceHeldItemPolicy heldItemPolicy)
        {
            if (!_performanceCues.TryGetValue(outcomeId, out VfxCueId cue))
            {
                return;
            }

            Vector3 position = performanceAnchor != null ? performanceAnchor.position : transform.position;
            PlayCueAt(cue, position, Vector3.up);
        }

        private void EnsurePoolTable()
        {
            if (pools == null)
            {
                return;
            }

            for (int i = 0; i < pools.Length; i++)
            {
                if (pools[i].Pool != null)
                {
                    _poolByCue[pools[i].Cue] = pools[i].Pool;
                }
            }
        }
    }
}
