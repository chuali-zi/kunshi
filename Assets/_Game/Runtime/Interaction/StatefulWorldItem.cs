using System;
using System.Collections;
using System.Collections.Generic;
using ConGame.Domain.Interaction;
using ConGame.Runtime.Loop;
using UnityEngine;

namespace ConGame.Runtime.Interaction
{
    [DisallowMultipleComponent]
    public sealed class StatefulWorldItem : MonoBehaviour, ILoopResettable
    {
        [SerializeField] private Rigidbody body;
        [SerializeField] private ItemPhysicalState initialState = ItemPhysicalState.Placed;

        private readonly List<CollisionPair> _ignoredPlayerCollisions = new List<CollisionPair>();
        private Transform _initialParent;
        private Vector3 _initialLocalPosition;
        private Quaternion _initialLocalRotation;
        private Vector3 _initialLocalScale;
        private bool _initialActive;
        private bool _initialIsKinematic;
        private bool _initialUseGravity;
        private Collider[] _colliders;
        private bool[] _initialColliderEnabled;
        private Coroutine _collisionRestoreRoutine;
        private bool _captured;

        public event Action<ItemPhysicalState> StateChanged;

        public int ResetOrder => 90;
        public ItemPhysicalState CurrentState { get; private set; } = ItemPhysicalState.Placed;
        public Rigidbody Body => body;
        public bool AllowsOrdinaryInteraction => CurrentState != ItemPhysicalState.Scripted;

        public void Configure(Rigidbody targetBody, ItemPhysicalState state = ItemPhysicalState.Placed)
        {
            body = targetBody;
            initialState = state;
            CurrentState = state;
            _captured = false;
        }

        private void Awake()
        {
            EnsureComponents();
            CurrentState = initialState;
        }

        public bool BeginHeld(Transform socket, Collider[] playerColliders)
        {
            if (socket == null || CurrentState == ItemPhysicalState.Scripted)
            {
                return false;
            }

            EnsureCaptured();
            CancelCollisionRestore();
            RestoreIgnoredPlayerCollisions();
            IgnorePlayerCollisions(playerColliders);

            if (body != null)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.isKinematic = true;
                body.useGravity = false;
            }

            transform.SetParent(socket, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            SetState(ItemPhysicalState.Held);
            return true;
        }

        public void Release(Vector3 velocity, Vector3 angularVelocity)
        {
            if (CurrentState != ItemPhysicalState.Held && CurrentState != ItemPhysicalState.Scripted)
            {
                return;
            }

            transform.SetParent(null, true);
            if (body != null)
            {
                body.isKinematic = false;
                body.useGravity = true;
                body.linearVelocity = velocity;
                body.angularVelocity = angularVelocity;
            }

            SetState(ItemPhysicalState.Flying);
            if (isActiveAndEnabled)
            {
                _collisionRestoreRoutine = StartCoroutine(RestorePlayerCollisionsAfterFixedStep());
            }
            else
            {
                RestoreIgnoredPlayerCollisions();
            }
        }

        public bool PlaceAt(PlacementSocket socket)
        {
            if (socket == null || !socket.CanAccept(GetComponent<InteractableEntity>()))
            {
                return false;
            }

            CancelCollisionRestore();
            RestoreIgnoredPlayerCollisions();
            Transform anchor = socket.Anchor;
            transform.SetParent(anchor, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            if (body != null)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.isKinematic = true;
                body.useGravity = false;
            }

            SetState(ItemPhysicalState.Placed);
            return true;
        }

        public void EnterScriptedState()
        {
            CancelCollisionRestore();
            RestoreIgnoredPlayerCollisions();
            if (body != null)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.isKinematic = true;
            }

            SetState(ItemPhysicalState.Scripted);
        }

        public void MarkBroken()
        {
            CancelCollisionRestore();
            RestoreIgnoredPlayerCollisions();
            SetState(ItemPhysicalState.Broken);
        }

        public void MarkPlaced()
        {
            CancelCollisionRestore();
            RestoreIgnoredPlayerCollisions();
            SetState(ItemPhysicalState.Placed);
        }

        public void CaptureInitialState()
        {
            EnsureComponents();
            _initialParent = transform.parent;
            _initialLocalPosition = transform.localPosition;
            _initialLocalRotation = transform.localRotation;
            _initialLocalScale = transform.localScale;
            _initialActive = gameObject.activeSelf;
            _initialIsKinematic = body != null && body.isKinematic;
            _initialUseGravity = body != null && body.useGravity;
            _initialColliderEnabled = new bool[_colliders.Length];
            for (int i = 0; i < _colliders.Length; i++)
            {
                _initialColliderEnabled[i] = _colliders[i] != null && _colliders[i].enabled;
            }

            CurrentState = initialState;
            _captured = true;
        }

        public void Quiesce(in LoopResetContext context)
        {
            CancelCollisionRestore();
            RestoreIgnoredPlayerCollisions();
            if (body != null)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.isKinematic = true;
            }
        }

        public void Restore(in LoopResetContext context)
        {
            EnsureCaptured();
            gameObject.SetActive(_initialActive);
            transform.SetParent(_initialParent, false);
            transform.localPosition = _initialLocalPosition;
            transform.localRotation = _initialLocalRotation;
            transform.localScale = _initialLocalScale;

            if (body != null)
            {
                body.position = transform.position;
                body.rotation = transform.rotation;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.useGravity = _initialUseGravity;
            }

            for (int i = 0; i < _colliders.Length; i++)
            {
                if (_colliders[i] != null)
                {
                    _colliders[i].enabled = _initialColliderEnabled[i];
                }
            }

            SetState(initialState);
        }

        public void Resume(in LoopResetContext context)
        {
            if (body != null)
            {
                body.isKinematic = _initialIsKinematic;
            }
        }

        private IEnumerator RestorePlayerCollisionsAfterFixedStep()
        {
            yield return new WaitForFixedUpdate();
            _collisionRestoreRoutine = null;
            RestoreIgnoredPlayerCollisions();
        }

        private void IgnorePlayerCollisions(Collider[] playerColliders)
        {
            if (playerColliders == null)
            {
                return;
            }

            for (int i = 0; i < _colliders.Length; i++)
            {
                Collider itemCollider = _colliders[i];
                if (itemCollider == null)
                {
                    continue;
                }

                for (int j = 0; j < playerColliders.Length; j++)
                {
                    Collider playerCollider = playerColliders[j];
                    if (playerCollider == null || playerCollider == itemCollider)
                    {
                        continue;
                    }

                    Physics.IgnoreCollision(itemCollider, playerCollider, true);
                    _ignoredPlayerCollisions.Add(new CollisionPair(itemCollider, playerCollider));
                }
            }
        }

        private void RestoreIgnoredPlayerCollisions()
        {
            for (int i = 0; i < _ignoredPlayerCollisions.Count; i++)
            {
                CollisionPair pair = _ignoredPlayerCollisions[i];
                if (pair.Item != null && pair.Player != null)
                {
                    Physics.IgnoreCollision(pair.Item, pair.Player, false);
                }
            }

            _ignoredPlayerCollisions.Clear();
        }

        private void CancelCollisionRestore()
        {
            if (_collisionRestoreRoutine == null)
            {
                return;
            }

            StopCoroutine(_collisionRestoreRoutine);
            _collisionRestoreRoutine = null;
        }

        private void EnsureCaptured()
        {
            if (!_captured)
            {
                CaptureInitialState();
            }
        }

        private void EnsureComponents()
        {
            body ??= GetComponent<Rigidbody>();
            _colliders ??= GetComponentsInChildren<Collider>(true);
        }

        private void SetState(ItemPhysicalState state)
        {
            if (CurrentState == state)
            {
                return;
            }

            CurrentState = state;
            StateChanged?.Invoke(state);
        }

        private readonly struct CollisionPair
        {
            public CollisionPair(Collider item, Collider player)
            {
                Item = item;
                Player = player;
            }

            public Collider Item { get; }
            public Collider Player { get; }
        }
    }
}
