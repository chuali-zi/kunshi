using ConGame.Runtime.Ending;
using UnityEngine;

namespace ConGame.Presentation.Camera
{

    /// <summary>
    /// 消费 EndingSequenceDirector 的相机/视线事件:第一人称硬切、视线开关与偏航限制、
    /// E2 倒地的程序化相机姿态(pitch 下压 + 高度下降 + 受限 roll)。只消费 Runtime 状态。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EndingFirstPersonPresenter : MonoBehaviour
    {
        [SerializeField] private EndingSequenceDirector director;
        [SerializeField] private CameraModeController cameraMode;
        [SerializeField] private FirstPersonLookController look;
        [SerializeField] private Transform fallPivot;

        private Vector3 _fallPivotBaseLocalPosition;
        private Quaternion _fallPivotBaseLocalRotation;
        private bool _fallBaseCaptured;

        public void Configure(
            EndingSequenceDirector sequenceDirector,
            CameraModeController cameraController,
            FirstPersonLookController lookController,
            Transform pivot)
        {
            Unsubscribe();
            director = sequenceDirector;
            cameraMode = cameraController;
            look = lookController;
            fallPivot = pivot;
            CaptureFallBase();
            if (isActiveAndEnabled)
            {
                Subscribe();
            }
        }

        private void OnEnable()
        {
            CaptureFallBase();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (director == null)
            {
                return;
            }

            director.FirstPersonModeRequested += OnFirstPersonModeRequested;
            director.LookEnabledRequested += OnLookEnabledRequested;
            director.YawLimitRequested += OnYawLimitRequested;
            director.LookResetRequested += OnLookResetRequested;
            director.LookAtRequested += OnLookAtRequested;
            director.FallPoseUpdated += OnFallPoseUpdated;
            director.FallSequenceFinished += OnFallSequenceFinished;
        }

        private void Unsubscribe()
        {
            if (director == null)
            {
                return;
            }

            director.FirstPersonModeRequested -= OnFirstPersonModeRequested;
            director.LookEnabledRequested -= OnLookEnabledRequested;
            director.YawLimitRequested -= OnYawLimitRequested;
            director.LookResetRequested -= OnLookResetRequested;
            director.LookAtRequested -= OnLookAtRequested;
            director.FallPoseUpdated -= OnFallPoseUpdated;
            director.FallSequenceFinished -= OnFallSequenceFinished;
        }

        private void OnFirstPersonModeRequested(bool firstPerson)
        {
            if (cameraMode == null)
            {
                return;
            }

            if (firstPerson)
            {
                cameraMode.SwitchToFirstPerson();
            }
            else
            {
                cameraMode.SwitchToFixed();
            }
        }

        private void OnLookEnabledRequested(bool enabled)
        {
            if (look != null)
            {
                look.LookEnabled = enabled;
            }
        }

        private void OnYawLimitRequested(float maxDegrees)
        {
            if (look == null)
            {
                return;
            }

            if (maxDegrees > 0f)
            {
                look.SetYawLimit(maxDegrees);
            }
            else
            {
                look.ClearYawLimit();
            }
        }

        private void OnLookResetRequested()
        {
            look?.ResetLook();
        }

        private void OnLookAtRequested(Vector3 worldTarget)
        {
            look?.LookAt(worldTarget);
        }

        private void OnFallPoseUpdated(EndingFallPose pose)
        {
            if (fallPivot == null)
            {
                return;
            }

            CaptureFallBase();
            Vector3 position = _fallPivotBaseLocalPosition;
            position.y -= pose.HeightDropMeters;
            fallPivot.localPosition = position;
            fallPivot.localRotation =
                _fallPivotBaseLocalRotation * Quaternion.Euler(pose.PitchDownDegrees, 0f, pose.RollDegrees);
        }

        private void OnFallSequenceFinished()
        {
            if (fallPivot == null || !_fallBaseCaptured)
            {
                return;
            }

            fallPivot.localPosition = _fallPivotBaseLocalPosition;
            fallPivot.localRotation = _fallPivotBaseLocalRotation;
        }

        private void CaptureFallBase()
        {
            if (fallPivot == null || _fallBaseCaptured)
            {
                return;
            }

            _fallPivotBaseLocalPosition = fallPivot.localPosition;
            _fallPivotBaseLocalRotation = fallPivot.localRotation;
            _fallBaseCaptured = true;
        }
    }
}
