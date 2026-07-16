using UnityEngine;

namespace ConGame.Runtime.Interaction
{
    public readonly struct FlashlightHit
    {
        public FlashlightHit(string sourceId, Vector3 origin, Vector3 direction, float exposureSeconds, float intensity = 1f)
        {
            SourceId = sourceId ?? string.Empty;
            Origin = origin;
            Direction = direction.sqrMagnitude > 0f ? direction.normalized : Vector3.forward;
            ExposureSeconds = Mathf.Max(0f, exposureSeconds);
            Intensity = Mathf.Max(0f, intensity);
        }

        public string SourceId { get; }
        public Vector3 Origin { get; }
        public Vector3 Direction { get; }
        public float ExposureSeconds { get; }
        public float Intensity { get; }
    }

    public interface IFlashlightReactive
    {
        void React(in FlashlightHit hit);
    }

    public interface IUseHandler
    {
        bool CanUse(in InteractionContext context);
        InteractionResult Use(in InteractionContext context);
    }
}
