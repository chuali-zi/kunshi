using System;
using ConGame.Domain.Interaction;
using UnityEngine;

namespace ConGame.Runtime.Interaction
{

    // One authoritative breakage commit. Published after StatefulWorldItem
    // enters Broken so audio, shard-zone and ambient-loop consumers react to
    // the same source of truth.
    public readonly struct BreakageEvent
    {
        public BreakageEvent(
            string itemId,
            Vector3 position,
            ImpactKind kind,
            ShardSlowZoneSpec? shardZone,
            bool silencesAmbientLoop)
        {
            ItemId = itemId ?? string.Empty;
            Position = position;
            Kind = kind;
            ShardZone = shardZone;
            SilencesAmbientLoop = silencesAmbientLoop;
        }

        public string ItemId { get; }
        public Vector3 Position { get; }
        public ImpactKind Kind { get; }
        public ShardSlowZoneSpec? ShardZone { get; }
        public bool SilencesAmbientLoop { get; }
    }

    // Lightweight semantic channel for interaction-layer events that do not
    // belong on GameplayEventHub (noise/impact/stimulus). Presentation and
    // audio consumers subscribe; nothing here reaches back into Runtime.
    [DisallowMultipleComponent]
    public sealed class InteractionEventChannel : MonoBehaviour
    {
        public event Action<BreakageEvent> BreakageResolved;

        public void EmitBreakage(in BreakageEvent breakage)
        {
            BreakageResolved?.Invoke(breakage);
        }
    }
}
