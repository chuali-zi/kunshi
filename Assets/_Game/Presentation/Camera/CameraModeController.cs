using ConGame.Runtime.Loop;
using UnityEngine;

namespace ConGame.Presentation.Camera
{

    [DisallowMultipleComponent]
    public sealed class CameraModeController : MonoBehaviour, ILoopResettable
    {
        [SerializeField] private GameObject fixedCameraRig;
        [SerializeField] private GameObject firstPersonCameraRig;

        private Vector3 _fixedPosition;
        private Quaternion _fixedRotation;

        public int ResetOrder => 20;

        public bool IsFirstPerson { get; private set; }

        public void Configure(GameObject fixedRig, GameObject firstPersonRig)
        {
            fixedCameraRig = fixedRig;
            firstPersonCameraRig = firstPersonRig;
        }

        private void LateUpdate()
        {
            if (!IsFirstPerson && fixedCameraRig != null)
            {
                fixedCameraRig.transform.SetPositionAndRotation(_fixedPosition, _fixedRotation);
            }
        }

        public void SwitchToFixed()
        {
            IsFirstPerson = false;
            if (fixedCameraRig != null)
            {
                fixedCameraRig.SetActive(true);
            }

            if (firstPersonCameraRig != null)
            {
                firstPersonCameraRig.SetActive(false);
            }
        }

        public void SwitchToFirstPerson()
        {
            IsFirstPerson = true;
            if (fixedCameraRig != null)
            {
                fixedCameraRig.SetActive(false);
            }

            if (firstPersonCameraRig != null)
            {
                firstPersonCameraRig.SetActive(true);
            }
        }

        public void CaptureInitialState()
        {
            if (fixedCameraRig != null)
            {
                _fixedPosition = fixedCameraRig.transform.position;
                _fixedRotation = fixedCameraRig.transform.rotation;
            }

            SwitchToFixed();
        }

        public void Quiesce(in LoopResetContext context)
        {
        }

        public void Restore(in LoopResetContext context)
        {
            if (fixedCameraRig != null)
            {
                fixedCameraRig.transform.SetPositionAndRotation(_fixedPosition, _fixedRotation);
            }

            SwitchToFixed();
        }

        public void Resume(in LoopResetContext context)
        {
        }
    }
}
