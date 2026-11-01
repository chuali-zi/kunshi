using ConGame.Domain.Facts;
using ConGame.Domain.Interaction;
using ConGame.Runtime.AI;
using ConGame.Runtime.Events;
using ConGame.Runtime.Facts;
using ConGame.Runtime.Loop;
using UnityEngine;

namespace ConGame.Runtime.Interaction
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class TripTrapController : MonoBehaviour, IUseHandler, ILoopResettable
    {
        [SerializeField] private GameplayFactService facts;
        [SerializeField] private GameplayEventHub events;
        [SerializeField] private TrapState armedState = TrapState.RugArmed;
        [SerializeField] private bool armOnUse = true;
        [SerializeField, Min(0.1f)] private float pullDistance = 1.6f;
        [SerializeField] private PlacementSocket trapSocket;

        private int _occupantCount;

        public int ResetOrder => 74;
        public bool OccupantPresent => _occupantCount > 0;

        private void Awake()
        {
            // The nav-driven killer has no Rigidbody. A kinematic trigger body
            // lets PhysX report real foot contact without moving the trap.
            Rigidbody body = GetComponent<Rigidbody>() ?? gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            BoxCollider trigger = GetComponent<BoxCollider>();
            float scaleY = Mathf.Abs(transform.lossyScale.y);
            if (trigger != null && trigger.isTrigger && scaleY > 0.0001f)
            {
                // Keep the authored footprint and floor height, but let the
                // contact volume reach the killer's capsule above the floor.
                float extraHeight = Mathf.Max(0f, 0.4f / scaleY - trigger.size.y);
                trigger.size += Vector3.up * extraHeight;
                trigger.center += Vector3.up * (extraHeight * 0.5f);
            }
        }

        public void Configure(
            GameplayFactService factService,
            GameplayEventHub eventHub,
            TrapState trapState,
            bool useArmsTrap)
        {
            facts = factService;
            events = eventHub;
            armedState = trapState;
            armOnUse = useArmsTrap;
        }

        public void ConfigureSocket(PlacementSocket socket)
        {
            trapSocket = socket;
        }

        public bool CanUse(in InteractionContext context)
        {
            if (facts == null)
            {
                return false;
            }

            FactSnapshot snapshot = facts.CaptureSnapshot();
            if (armOnUse && snapshot.Trap == TrapState.None)
            {
                return true;
            }

            return CanPull(context);
        }

        public bool CanPull(in InteractionContext context)
        {
            if (facts == null)
            {
                return false;
            }

            FactSnapshot snapshot = facts.CaptureSnapshot();
            return snapshot.Trap == armedState &&
                   TripTrapTiming.CanPull(IsOccupied, armedState, snapshot.TrapPulled) &&
                   CanPullFromPlayer(context);
        }

        public InteractionResult Use(in InteractionContext context)
        {
            if (facts == null)
            {
                return InteractionResult.Rejected;
            }

            if (armOnUse && facts.CaptureSnapshot().Trap == TrapState.None)
            {
                facts.SetTrap(armedState);
                facts.SetTrapPulled(false);
                return InteractionResult.Accepted;
            }

            if (!CanPull(context))
            {
                return InteractionResult.Rejected;
            }

            CommitPull();
            return InteractionResult.Accepted;
        }

        public void CaptureInitialState()
        {
            _occupantCount = 0;
        }

        public void Quiesce(in LoopResetContext context)
        {
            _occupantCount = 0;
        }

        public void Restore(in LoopResetContext context)
        {
            _occupantCount = 0;
        }

        public void Resume(in LoopResetContext context)
        {
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsKiller(other))
            {
                return;
            }

            _occupantCount++;
        }

        private void OnTriggerExit(Collider other)
        {
            if (IsKiller(other))
            {
                _occupantCount = Mathf.Max(0, _occupantCount - 1);
            }
        }

        private void CommitPull()
        {
            facts.SetTrap(armedState);
            facts.SetTrapPulled(true);
            events?.EmitStimulus(KillerStimulusType.TrapPull, armedState == TrapState.RugArmed ? "rug" : "blanket");
        }

        private bool IsOccupied => OccupantPresent && (trapSocket == null || trapSocket.Occupant != null);

        private bool CanPullFromPlayer(in InteractionContext context)
        {
            if (context.Player == null)
            {
                return false;
            }

            Vector3 toTrap = transform.position - context.Player.transform.position;
            toTrap.y = 0f;
            if (toTrap.sqrMagnitude > pullDistance * pullDistance)
            {
                return false;
            }

            return toTrap.sqrMagnitude < 0.0001f ||
                   Vector3.Dot(context.Player.transform.forward, toTrap.normalized) >= -0.15f;
        }

        private static bool IsKiller(Collider other)
        {
            return other != null && other.GetComponentInParent<KillerDirector>() != null;
        }
    }
}
