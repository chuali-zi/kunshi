using System;

using ConGame.Runtime.Interaction;
using ConGame.Domain.Facts;
using UnityEngine;

namespace ConGame.Runtime.Events
{

    public enum NoiseLevel
    {
        Normal = 0,
        Loud = 1
    }


    public readonly struct ImpactEvent
    {
        public ImpactEvent(in HitInfo hit, HitResult result, Component receiver)
        {
            Hit = hit;
            Result = result;
            Receiver = receiver;
        }

        public HitInfo Hit { get; }
        public HitResult Result { get; }
        public Component Receiver { get; }
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

        public event Action<ImpactEvent> ImpactResolved;
public event Action<NoiseEvent> NoiseEmitted;
        public event Action<KillerStimulus> KillerStimulusEmitted;

        public void EmitNoise(NoiseLevel level, Vector3 position, string sourceId)
        {
            NoiseEmitted?.Invoke(new NoiseEvent(level, position, sourceId));
        }

public void EmitImpact(in HitInfo hit, HitResult result, Component receiver)
        {
            if (result != HitResult.Ignored)
            {
                ImpactResolved?.Invoke(new ImpactEvent(hit, result, receiver));
            }
        }


        public void EmitStimulus(KillerStimulusType type, string sourceId = "")
        {
            KillerStimulusEmitted?.Invoke(new KillerStimulus(type, sourceId));
        }
    }
}
