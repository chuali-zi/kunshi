using ConGame.Domain.Interaction;
using ConGame.Runtime.Interaction;
using ConGame.Runtime.Loop;
using ConGame.Runtime.Player;
using UnityEngine;

namespace ConGame.Presentation.Props
{
    /// <summary>The dropped beam follows the rolling prop; a held beam follows player aim.</summary>
    [DisallowMultipleComponent]
    public sealed class DroppedFlashlightPresenter : MonoBehaviour, ILoopResettable
    {
        [SerializeField] private StatefulWorldItem item;
        [SerializeField] private PlayerInteractor player;
        [SerializeField] private Light beam;
        [SerializeField] private float lensOffset = 0.13f;

        private bool _illuminated;
        private bool _subscribed;

        public int ResetOrder => 97;
        public Light BeamLight => beam;

        public void Configure(StatefulWorldItem worldItem, PlayerInteractor interactor, Light beamLight)
        {
            Unsubscribe();
            item = worldItem;
            player = interactor;
            beam = beamLight;
            if (isActiveAndEnabled) Subscribe();
            ApplyBeamPose();
        }

        private void OnEnable()
        {
            Subscribe();
            ApplyBeamPose();
        }

        private void OnDisable()
        {
            Unsubscribe();
            SetLight(false);
        }

        private void LateUpdate() => ApplyBeamPose();

        private void OnStateChanged(ItemPhysicalState state)
        {
            if (state == ItemPhysicalState.Flying || state == ItemPhysicalState.Held)
                _illuminated = true;
            ApplyBeamPose();
        }

        private void ApplyBeamPose()
        {
            if (beam == null || item == null) return;
            bool visible = _illuminated && item.gameObject.activeInHierarchy &&
                           item.CurrentState != ItemPhysicalState.Broken &&
                           item.CurrentState != ItemPhysicalState.Scripted;
            SetLight(visible);
            if (!visible) return;

            if (item.CurrentState == ItemPhysicalState.Held && player != null)
            {
                // Match the authoritative Use ray. A rolling beam only lights the room;
                // it never invokes glare, a mirror branch, or a successful counterplay.
                beam.transform.SetPositionAndRotation(player.transform.position + Vector3.up * 1.4f,
                    Quaternion.LookRotation(player.AimForward, Vector3.up));
            }
            else
            {
                beam.transform.SetPositionAndRotation(item.transform.position + item.transform.forward * lensOffset,
                    item.transform.rotation);
            }
        }

        public void CaptureInitialState()
        {
            _illuminated = false;
            SetLight(false);
        }

        public void Quiesce(in LoopResetContext context)
        {
            _illuminated = false;
            SetLight(false);
        }

        public void Restore(in LoopResetContext context)
        {
            _illuminated = false;
            SetLight(false);
        }

        public void Resume(in LoopResetContext context) => ApplyBeamPose();

        private void Subscribe()
        {
            if (_subscribed || item == null) return;
            item.StateChanged += OnStateChanged;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            if (item != null) item.StateChanged -= OnStateChanged;
            _subscribed = false;
        }

        private void SetLight(bool enabled)
        {
            if (beam != null) beam.enabled = enabled;
        }
    }
}
