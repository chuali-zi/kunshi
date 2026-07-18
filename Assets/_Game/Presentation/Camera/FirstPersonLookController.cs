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
        private bool _yawLimited;
        private float _yawCenter;
        private float _maxYawDelta;

        public bool LookEnabled
        {
            get => lookEnabled;
            set => lookEnabled = value;
        }

        public Transform ViewTransform => pitchPivot != null ? pitchPivot : yawPivot;

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
            if (_yawLimited)
            {
                _yaw = Mathf.Clamp(_yaw, _yawCenter - _maxYawDelta, _yawCenter + _maxYawDelta);
            }
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

        public void LookAt(Vector3 worldTarget)
        {
            if (yawPivot == null || pitchPivot == null)
            {
                return;
            }

            Vector3 direction = worldTarget - pitchPivot.position;
            if (direction.sqrMagnitude < 0.0001f)
            {
                return;
            }

            Vector3 planar = Vector3.ProjectOnPlane(direction, Vector3.up);
            if (planar.sqrMagnitude > 0.0001f)
            {
                yawPivot.rotation = Quaternion.LookRotation(planar.normalized, Vector3.up);
                _yaw = yawPivot.localEulerAngles.y;
            }

            float horizontalDistance = Mathf.Max(0.0001f, planar.magnitude);
            _pitch = Mathf.Clamp(-Mathf.Atan2(direction.y, horizontalDistance) * Mathf.Rad2Deg, minPitch, maxPitch);
            pitchPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        public void SetYawLimit(float maxDegrees)
        {
            _yawCenter = _yaw;
            _maxYawDelta = Mathf.Max(0f, maxDegrees);
            _yawLimited = true;
        }

        public void ClearYawLimit()
        {
            _yawLimited = false;
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
