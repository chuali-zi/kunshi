using System;
using ConGame.Domain.Facts;
using UnityEngine;

namespace ConGame.Runtime.Events
{

    public enum NoiseLevel
    {
        Normal = 0,
        Loud = 1
    }

    public readonly struct NoiseEvent
    {
        public NoiseEvent(NoiseLevel level, Vector3 position, string sourceId)
        {
            Level = level;
            Position = position;
            SourceId = sourceId ?? string.Empty;
        }

        public NoiseLevel Level { get; }
        public Vector3 Position { get; }
        public string SourceId { get; }
    }

    [DisallowMultipleComponent]
    public sealed class GameplayEventHub : MonoBehaviour
    {
        public event Action<NoiseEvent> NoiseEmitted;
        public event Action<KillerStimulus> KillerStimulusEmitted;

        public void EmitNoise(NoiseLevel level, Vector3 position, string sourceId)
        {
            NoiseEmitted?.Invoke(new NoiseEvent(level, position, sourceId));
        }

        public void EmitStimulus(KillerStimulusType type, string sourceId = "")
        {
            KillerStimulusEmitted?.Invoke(new KillerStimulus(type, sourceId));
        }
    }
}
