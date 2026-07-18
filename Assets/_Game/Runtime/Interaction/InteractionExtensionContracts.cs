using UnityEngine;
using ConGame.Runtime.Facts;

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

    internal sealed class GlareLifetime : MonoBehaviour
    {
        private GameplayFactService _facts;
        private float _expiresAt;

        public static void Activate(GameplayFactService facts, float durationSeconds)
        {
            if (facts == null)
            {
                return;
            }

            GlareLifetime lifetime = facts.GetComponent<GlareLifetime>();
            if (lifetime == null)
            {
                lifetime = facts.gameObject.AddComponent<GlareLifetime>();
            }

            lifetime._facts = facts;
            lifetime._expiresAt = Mathf.Max(lifetime._expiresAt, Time.time + Mathf.Max(0.05f, durationSeconds));
            lifetime.enabled = true;
            facts.SetGlareActive(true);
        }

        private void Update()
        {
            if (_facts == null || Time.time < _expiresAt)
            {
                return;
            }

            _facts.SetGlareActive(false);
            enabled = false;
        }

        private void OnDisable()
        {
            if (_facts != null && Time.time >= _expiresAt)
            {
                _facts.SetGlareActive(false);
            }
        }

        private void OnEnable()
        {
            if (_facts != null && Time.time >= _expiresAt)
            {
                _expiresAt = Time.time + 0.05f;
            }
        }
    }
}
