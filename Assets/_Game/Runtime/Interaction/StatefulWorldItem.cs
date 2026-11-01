using System;
using System.Collections;
using System.Collections.Generic;
using ConGame.Domain.Interaction;
using ConGame.Domain.Facts;
using ConGame.Runtime.AI;
using ConGame.Runtime.Events;
using ConGame.Runtime.Loop;
using UnityEngine;

namespace ConGame.Runtime.Interaction
{
    [DisallowMultipleComponent]
    public sealed class StatefulWorldItem : MonoBehaviour, ILoopResettable
    {
        [SerializeField] private Rigidbody body;
        [SerializeField] private ItemPhysicalState initialState = ItemPhysicalState.Placed;
        [SerializeField] private ImpactKind flyingImpactKind = ImpactKind.Projectile;

        private readonly List<CollisionPair> _ignoredPlayerCollisions = new List<CollisionPair>();
        private Transform _initialParent;
        private Vector3 _initialLocalPosition;
        private Quaternion _initialLocalRotation;
        private Vector3 _initialLocalScale;
        private bool _initialActive;
        private bool _initialIsKinematic;
        private bool _initialUseGravity;
        private CollisionDetectionMode _initialCollisionDetectionMode;
        private Collider[] _colliders;
        private bool[] _initialColliderEnabled;
        private Coroutine _collisionRestoreRoutine;
        private bool _captured;
        private PlacementSocket _initialSocket;
        private GameplayEventHub _throwEvents;
        private Vector3 _flightDirection;
        private bool _flightImpactConsumed;

        public event Action<ItemPhysicalState> StateChanged;

        public int ResetOrder => 90;
        public ItemPhysicalState CurrentState { get; private set; } = ItemPhysicalState.Placed;
        public Rigidbody Body => body;
        public bool AllowsOrdinaryInteraction => CurrentState != ItemPhysicalState.Scripted;
        public PlacementSocket CurrentSocket { get; private set; }

        public void Configure(
            Rigidbody targetBody,
            ItemPhysicalState state = ItemPhysicalState.Placed,
            ImpactKind impactKind = ImpactKind.Projectile)
        {
            body = targetBody;
            initialState = state;
            flyingImpactKind = impactKind;
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
            ReleaseCurrentSocket();
            IgnorePlayerCollisions(playerColliders);

            if (body != null)
            {
                StopBodyMotion();
                body.isKinematic = true;
                body.useGravity = false;
            }

            transform.SetParent(socket, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            SetState(ItemPhysicalState.Held);
            return true;
        }

        public void Release(
            Vector3 velocity,
            Vector3 angularVelocity,
            GameplayEventHub throwEvents = null)
        {
            if (CurrentState != ItemPhysicalState.Held && CurrentState != ItemPhysicalState.Scripted)
            {
                return;
            }

            ReleaseCurrentSocket();
            transform.SetParent(null, true);
            if (body != null)
            {
                body.isKinematic = false;
                body.useGravity = true;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                body.linearVelocity = velocity;
                body.angularVelocity = angularVelocity;
            }

            _throwEvents = throwEvents;
            _flightDirection = velocity.sqrMagnitude > 0.001f ? velocity.normalized : transform.forward;
            _flightImpactConsumed = false;
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

        /// <summary>
        /// Launches an unheld placed item from a scripted world position. Staged
        /// drops use the regular rigidbody and reset path without player-throw cues.
        /// </summary>
        public bool TossIntoWorld(
            Vector3 position,
            Quaternion rotation,
            Vector3 velocity,
            Vector3 angularVelocity,
            Collider[] initiallyIgnoredColliders = null)
        {
            if (CurrentState != ItemPhysicalState.Placed || body == null)
            {
                return false;
            }

            EnsureCaptured();
            CancelCollisionRestore();
            RestoreIgnoredPlayerCollisions();
            ReleaseCurrentSocket();
            transform.SetParent(null, true);
            transform.SetPositionAndRotation(position, rotation);
            Physics.SyncTransforms();
            IgnorePlayerCollisions(initiallyIgnoredColliders);

            StopBodyMotion();
            body.isKinematic = false;
            body.useGravity = true;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.position = position;
            body.rotation = rotation;
            body.linearVelocity = velocity;
            body.angularVelocity = angularVelocity;

            _throwEvents = null;
            _flightDirection = velocity.sqrMagnitude > 0.001f ? velocity.normalized : transform.forward;
            _flightImpactConsumed = false;
            SetState(ItemPhysicalState.Flying);
            if (isActiveAndEnabled)
            {
                _collisionRestoreRoutine = StartCoroutine(RestoreIgnoredCollisionsAfterD21Toss());
            }
            else
            {
                RestoreIgnoredPlayerCollisions();
            }

            return true;
        }

        /// <summary>Applies a small follow-up nudge to an item already in flight.</summary>
        public bool AddFlightImpulse(Vector3 velocityChange, Vector3 angularVelocityChange)
        {
            if (CurrentState != ItemPhysicalState.Flying || body == null || body.isKinematic)
            {
                return false;
            }

            body.linearVelocity += velocityChange;
            body.angularVelocity += angularVelocityChange;
            if (velocityChange.sqrMagnitude > 0.001f)
            {
                _flightDirection = velocityChange.normalized;
            }

            return true;
        }

        public bool PlaceAt(PlacementSocket socket)
        {
            if (socket == null || !socket.CanAccept(GetComponent<InteractableEntity>()))
            {
                return false;
            }

            CancelCollisionRestore();
            RestoreIgnoredPlayerCollisions();
            if (CurrentSocket != null && CurrentSocket != socket)
            {
                ReleaseCurrentSocket();
            }

            Transform anchor = socket.Anchor;
            transform.SetParent(anchor, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            if (body != null)
            {
                StopBodyMotion();
                body.isKinematic = true;
                body.useGravity = false;
            }

            CurrentSocket = socket;
            ClearFlightContext();
            SetState(ItemPhysicalState.Placed);
            return true;
        }

        public bool PlaceInWorld(Vector3 groundPosition, Quaternion rotation)
        {
            if (CurrentState != ItemPhysicalState.Held)
            {
                return false;
            }

            EnsureComponents();
            CancelCollisionRestore();
            RestoreIgnoredPlayerCollisions();
            ReleaseCurrentSocket();
            transform.SetParent(null, true);
            transform.SetPositionAndRotation(groundPosition, rotation);
            Physics.SyncTransforms();

            float lowestPoint = transform.position.y;
            for (int i = 0; i < _colliders.Length; i++)
            {
                Collider itemCollider = _colliders[i];
                if (itemCollider != null && itemCollider.enabled)
                {
                    lowestPoint = Mathf.Min(lowestPoint, itemCollider.bounds.min.y);
                }
            }

            transform.position += Vector3.up * (groundPosition.y - lowestPoint);
            if (body != null)
            {
                InteractableEntity entity = GetComponent<InteractableEntity>();
                bool isHeavy = entity != null &&
                               ((entity.Categories & ItemCategory.Heavy) != 0 ||
                                entity.GetComponent<CarryCapability>() != null);
                body.isKinematic = false;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.collisionDetectionMode = CollisionDetectionMode.Discrete;
                body.useGravity = !isHeavy;
                body.isKinematic = isHeavy;
                body.position = transform.position;
                body.rotation = transform.rotation;
            }

            ClearFlightContext();
            SetState(ItemPhysicalState.Placed);
            return true;
        }

        public void EnterScriptedState()
        {
            CancelCollisionRestore();
            RestoreIgnoredPlayerCollisions();
            ReleaseCurrentSocket();
            if (body != null)
            {
                StopBodyMotion();
                body.isKinematic = true;
            }

            SetState(ItemPhysicalState.Scripted);
        }

        public void MarkBroken()
        {
            CancelCollisionRestore();
            RestoreIgnoredPlayerCollisions();
            ReleaseCurrentSocket();
            ClearFlightContext();
            SetState(ItemPhysicalState.Broken);
        }

        public void MarkPlaced()
        {
            CancelCollisionRestore();
            RestoreIgnoredPlayerCollisions();
            ClearFlightContext();
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
            _initialCollisionDetectionMode = body == null
                ? CollisionDetectionMode.Discrete
                : body.collisionDetectionMode;
            CurrentSocket ??= GetComponentInParent<PlacementSocket>();
            _initialSocket = CurrentSocket;
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
                StopBodyMotion();
                body.isKinematic = true;
            }

            ClearFlightContext();
        }

        public void Restore(in LoopResetContext context)
        {
            EnsureCaptured();
            if (CurrentSocket != _initialSocket)
            {
                ReleaseCurrentSocket();
            }
            gameObject.SetActive(_initialActive);
            transform.SetParent(_initialParent, false);
            transform.localPosition = _initialLocalPosition;
            transform.localRotation = _initialLocalRotation;
            transform.localScale = _initialLocalScale;

            if (body != null)
            {
                body.position = transform.position;
                body.rotation = transform.rotation;
                StopBodyMotion();
                body.useGravity = _initialUseGravity;
                body.collisionDetectionMode = _initialCollisionDetectionMode;
            }

            for (int i = 0; i < _colliders.Length; i++)
            {
                if (_colliders[i] != null)
                {
                    _colliders[i].enabled = _initialColliderEnabled[i];
                }
            }

            CurrentSocket = _initialSocket;
            ClearFlightContext();
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

        private IEnumerator RestoreIgnoredCollisionsAfterD21Toss()
        {
            // D21 drops begin at a hand/holster position that can overlap a character collider.
            yield return new WaitForSeconds(0.2f);
            _collisionRestoreRoutine = null;
            RestoreIgnoredPlayerCollisions();
        }

        private void OnCollisionEnter(Collision collision)
        {
            // Scripted falls and ordinary drops still collide physically. Only an
            // intentional player throw may invoke gameplay hit receivers.
            if (_throwEvents == null || CurrentState != ItemPhysicalState.Flying || _flightImpactConsumed || collision == null ||
                collision.collider == null || collision.transform.IsChildOf(transform))
            {
                return;
            }

            _flightImpactConsumed = true;
            Vector3 direction = _flightDirection.sqrMagnitude > 0.001f
                                    ? _flightDirection
                                    : -collision.relativeVelocity.normalized;
            float mass = body == null ? 1f : Mathf.Max(0.01f, body.mass);
            float force = Mathf.Max(collision.impulse.magnitude, collision.relativeVelocity.magnitude * mass);
            Vector3 point = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;

            MonoBehaviour[] behaviours = collision.collider.GetComponentsInParent<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IHitReceiver receiver)
                {
                    HitInfo hit = new HitInfo(
                        flyingImpactKind,
                        ResolveSourceId(),
                        point,
                        direction,
                        force);
                    HitResult result = receiver.ReceiveHit(hit);
                    _throwEvents?.EmitImpact(hit, result, behaviours[i]);
                    if (result != HitResult.Ignored)
                    {
                        _throwEvents?.EmitNoise(force >= 6f ? NoiseLevel.Loud : NoiseLevel.Normal, point,
                            ResolveSourceId());
                    }
                    break;
                }
            }

            if (_throwEvents != null && collision.collider.GetComponentInParent<KillerDirector>() != null)
            {
                _throwEvents.EmitStimulus(KillerStimulusType.IncomingProjectile, ResolveSourceId());
            }

            _throwEvents = null;
            _flightDirection = Vector3.zero;
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

        private void ReleaseCurrentSocket()
        {
            if (CurrentSocket == null)
            {
                return;
            }

            PlacementSocket previous = CurrentSocket;
            CurrentSocket = null;
            previous.Release(GetComponent<InteractableEntity>());
        }

        private void StopBodyMotion()
        {
            if (body == null || body.isKinematic)
            {
                return;
            }

            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        private void ClearFlightContext()
        {
            _throwEvents = null;
            _flightDirection = Vector3.zero;
            _flightImpactConsumed = false;
        }

        private string ResolveSourceId()
        {
            InteractableEntity entity = GetComponent<InteractableEntity>();
            return entity == null || string.IsNullOrEmpty(entity.ItemId) ? name : entity.ItemId;
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
