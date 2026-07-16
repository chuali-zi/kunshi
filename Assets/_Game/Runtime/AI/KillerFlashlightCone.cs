using ConGame.Domain.Facts;
using ConGame.Runtime.AI;
using ConGame.Runtime.Facts;
using ConGame.Runtime.Loop;
using UnityEngine;

namespace ConGame.Runtime.AI
{
    [DisallowMultipleComponent]
    public sealed class KillerFlashlightCone : MonoBehaviour, ILoopResettable
    {
        [SerializeField] private Transform playerTarget;
        [SerializeField] private GameplayFactService facts;
        [SerializeField] private KillerDirector killer;
        [SerializeField] private Light spotLight;
        [SerializeField] private float exposeSecondsToKill = 0.8f;
        [SerializeField] private float maxRange = 8f;
        [SerializeField] private float coneAngle = 28f;

        private float _exposedSeconds;
        private bool _initialLightEnabled;

        public int ResetOrder => 35;
        public float ExposedSeconds => _exposedSeconds;

        public void Configure(Transform target, GameplayFactService factService, KillerDirector killerDirector,
                              Light light = null)
        {
            playerTarget = target;
            facts = factService;
            killer = killerDirector;
            spotLight = light;
        }

        private void Awake()
        {
            if (spotLight == null)
            {
                spotLight = GetComponentInChildren<Light>(true);
            }
        }

        private void Update()
        {
            if (killer == null || killer.State == KillerState.Dormant || playerTarget == null || facts == null)
            {
                SetLightActive(false);
                return;
            }

            FactSnapshot snapshot = facts.CaptureSnapshot();
            bool darkAmbush = snapshot.RoomLight == RoomLightState.PersistentBlack &&
                              (snapshot.IsAmbushing ||
                               string.Equals(killer.SelectedOutcomeId, "D20", System.StringComparison.Ordinal) ||
                               string.Equals(killer.SelectedOutcomeId, "D19", System.StringComparison.Ordinal));
            SetLightActive(darkAmbush);
            if (!darkAmbush)
            {
                _exposedSeconds = 0f;
                return;
            }

            if (IsPlayerInCone())
            {
                _exposedSeconds += Time.deltaTime;
                if (_exposedSeconds >= exposeSecondsToKill &&
                    string.Equals(killer.SelectedOutcomeId, "D20", System.StringComparison.Ordinal))
                {
                    killer.ForceImmediateKill("D20");
                    _exposedSeconds = 0f;
                }
            }
            else
            {
                _exposedSeconds = Mathf.Max(0f, _exposedSeconds - Time.deltaTime * 0.5f);
            }
        }

        public void CaptureInitialState()
        {
            _initialLightEnabled = spotLight != null && spotLight.enabled;
            _exposedSeconds = 0f;
        }

        public void Quiesce(in LoopResetContext context)
        {
            _exposedSeconds = 0f;
            SetLightActive(false);
        }

        public void Restore(in LoopResetContext context)
        {
            _exposedSeconds = 0f;
            SetLightActive(_initialLightEnabled);
        }

        public void Resume(in LoopResetContext context)
        {
        }

        private bool IsPlayerInCone()
        {
            Vector3 origin = spotLight != null ? spotLight.transform.position : transform.position;
            Vector3 forward = spotLight != null ? spotLight.transform.forward : transform.forward;
            Vector3 toPlayer = playerTarget.position + Vector3.up * 1.2f - origin;
            float distance = toPlayer.magnitude;
            if (distance > maxRange || distance < 0.05f)
            {
                return false;
            }

            float angle = Vector3.Angle(forward, toPlayer);
            if (angle > coneAngle)
            {
                return false;
            }

            if (Physics.Raycast(origin, toPlayer / distance, out RaycastHit hit, distance,
                                ~0, QueryTriggerInteraction.Ignore))
            {
                return hit.transform == playerTarget || hit.transform.IsChildOf(playerTarget);
            }

            return true;
        }

        private void SetLightActive(bool active)
        {
            if (spotLight != null)
            {
                spotLight.enabled = active;
            }
        }
    }
}
