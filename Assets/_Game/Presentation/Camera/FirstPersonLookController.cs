using ConGame.Runtime.Input;
using UnityEngine;

namespace ConGame.Presentation.Camera
{
    [DisallowMultipleComponent]
    public sealed class FirstPersonLookController : MonoBehaviour
    {
        [SerializeField] private PlayerInputRouter input;
        [SerializeField] private Transform yawPivot;
        [SerializeField] private Transform pitchPivot;
        [SerializeField] private float sensitivity = 0.12f;
        [SerializeField] private float minPitch = -75f;
        [SerializeField] private float maxPitch = 75f;
        [SerializeField] private bool lookEnabled = true;

        private float _yaw;
        private float _pitch;

        public bool LookEnabled
        {
            get => lookEnabled;
            set => lookEnabled = value;
        }

        public void Configure(PlayerInputRouter inputRouter, Transform yaw, Transform pitch)
        {
            input = inputRouter;
            yawPivot = yaw;
            pitchPivot = pitch;
            if (yawPivot != null)
            {
                _yaw = yawPivot.localEulerAngles.y;
            }

            if (pitchPivot != null)
            {
                _pitch = NormalizePitch(pitchPivot.localEulerAngles.x);
            }
        }

        private void LateUpdate()
        {
            if (!lookEnabled || input == null || !input.GameplayEnabled || yawPivot == null || pitchPivot == null)
            {
                return;
            }

            Vector2 look = input.Look;
            _yaw += look.x * sensitivity;
            _pitch = Mathf.Clamp(_pitch - look.y * sensitivity, minPitch, maxPitch);
            yawPivot.localRotation = Quaternion.Euler(0f, _yaw, 0f);
            pitchPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        public void ResetLook()
        {
            _yaw = 0f;
            _pitch = 0f;
            if (yawPivot != null)
            {
                yawPivot.localRotation = Quaternion.identity;
            }

            if (pitchPivot != null)
            {
                pitchPivot.localRotation = Quaternion.identity;
            }
        }

        private static float NormalizePitch(float eulerX)
        {
            if (eulerX > 180f)
            {
                eulerX -= 360f;
            }

            return eulerX;
        }
    }
}
