using UnityEngine;

namespace ConGame.Runtime.Loop
{

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class RigidbodyResettable : MonoBehaviour, ILoopResettable
    {
        [SerializeField] private int resetOrder = 110;

        private Rigidbody _body;
        private Transform _parent;
        private Vector3 _localPosition;
        private Quaternion _localRotation;
        private Vector3 _localScale;
        private bool _isKinematic;
        private bool _useGravity;
        private bool _active;

        public int ResetOrder => resetOrder;

        private void Awake()
        {
            _body = GetComponent<Rigidbody>();
        }

        public void CaptureInitialState()
        {
            _body ??= GetComponent<Rigidbody>();
            _parent = transform.parent;
            _localPosition = transform.localPosition;
            _localRotation = transform.localRotation;
            _localScale = transform.localScale;
            _isKinematic = _body.isKinematic;
            _useGravity = _body.useGravity;
            _active = gameObject.activeSelf;
        }

        public void Quiesce(in LoopResetContext context)
        {
            if (_body == null)
            {
                return;
            }

            _body.linearVelocity = Vector3.zero;
            _body.angularVelocity = Vector3.zero;
            _body.isKinematic = true;
        }

        public void Restore(in LoopResetContext context)
        {
            gameObject.SetActive(_active);
            transform.SetParent(_parent, false);
            transform.localPosition = _localPosition;
            transform.localRotation = _localRotation;
            transform.localScale = _localScale;
            _body.position = transform.position;
            _body.rotation = transform.rotation;
            _body.linearVelocity = Vector3.zero;
            _body.angularVelocity = Vector3.zero;
            _body.useGravity = _useGravity;
        }

        public void Resume(in LoopResetContext context)
        {
            if (_body != null)
            {
                _body.isKinematic = _isKinematic;
            }
        }
    }
}
