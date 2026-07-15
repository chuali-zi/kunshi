using UnityEngine;

namespace ConGame.Runtime.Loop
{

    [DisallowMultipleComponent]
    public sealed class TransformResettable : MonoBehaviour, ILoopResettable
    {
        [SerializeField] private int resetOrder = 100;

        private Transform _parent;
        private Vector3 _localPosition;
        private Quaternion _localRotation;
        private Vector3 _localScale;
        private bool _active;

        public int ResetOrder => resetOrder;

        public void CaptureInitialState()
        {
            Transform cachedTransform = transform;
            _parent = cachedTransform.parent;
            _localPosition = cachedTransform.localPosition;
            _localRotation = cachedTransform.localRotation;
            _localScale = cachedTransform.localScale;
            _active = gameObject.activeSelf;
        }

        public void Quiesce(in LoopResetContext context)
        {
        }

        public void Restore(in LoopResetContext context)
        {
            Transform cachedTransform = transform;
            cachedTransform.SetParent(_parent, false);
            cachedTransform.localPosition = _localPosition;
            cachedTransform.localRotation = _localRotation;
            cachedTransform.localScale = _localScale;
            gameObject.SetActive(_active);
        }

        public void Resume(in LoopResetContext context)
        {
        }
    }
}
