using System;
using ConGame.Domain.Facts;
using ConGame.Domain.Interaction;
using ConGame.Runtime.Facts;
using ConGame.Runtime.Interaction;
using ConGame.Runtime.Loop;
using UnityEngine;
using ConGame.Runtime.Lighting;

namespace ConGame.Presentation.Interaction
{
    [DisallowMultipleComponent]
    public sealed class BlanketVisualPresenter : MonoBehaviour, ILoopResettable
    {
        [SerializeField] private StatefulWorldItem item;
        [SerializeField] private GameplayFactService facts;
        [SerializeField] private GameObject crumpledVisual;
        [SerializeField] private GameObject coveredVisual;
        [SerializeField] private GameObject flatTrapVisual;
        [SerializeField] private GameObject drapedVisual;

        private BlanketVisualState _currentState = (BlanketVisualState)(-1);
        private BreakableLight _drapedLamp;

        public int ResetOrder => 95;

        public void Configure(
            StatefulWorldItem worldItem,
            GameplayFactService factService,
            GameObject crumpled,
            GameObject covered,
            GameObject flatTrap,
            GameObject draped)
        {
            Unsubscribe();
            item = worldItem;
            facts = factService;
            crumpledVisual = crumpled;
            coveredVisual = covered;
            flatTrapVisual = flatTrap;
            drapedVisual = draped;
            _currentState = (BlanketVisualState)(-1);
            Subscribe();
            ApplyCurrentState();
        }

        public void CaptureInitialState()
        {
            ApplyCurrentState();
        }

        public void Quiesce(in LoopResetContext context)
        {
        }

        public void Restore(in LoopResetContext context)
        {
            _currentState = (BlanketVisualState)(-1);
            ApplyCurrentState();
        }

        public void Resume(in LoopResetContext context)
        {
            ApplyCurrentState();
        }

        private void OnEnable()
        {
            Subscribe();
            ApplyCurrentState();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void LateUpdate()
        {
            ApplyCurrentState();
        }

        private void Subscribe()
        {
            if (isActiveAndEnabled && item != null)
            {
                item.StateChanged -= OnItemStateChanged;
                item.StateChanged += OnItemStateChanged;
            }
        }

        private void Unsubscribe()
        {
            if (item != null)
            {
                item.StateChanged -= OnItemStateChanged;
            }
        }

        private void OnItemStateChanged(ItemPhysicalState state)
        {
            ApplyCurrentState();
        }

        private void ApplyCurrentState()
        {
            BlanketVisualState next = ResolveState();
            if (next == _currentState)
            {
                return;
            }

            _currentState = next;
            UpdateDrapedLamp(next == BlanketVisualState.Draped);
            SetActive(crumpledVisual, next == BlanketVisualState.Crumpled);
            SetActive(coveredVisual, next == BlanketVisualState.Covered);
            SetActive(flatTrapVisual, next == BlanketVisualState.FlatTrap);
            SetActive(drapedVisual, next == BlanketVisualState.Draped);
            if (next == BlanketVisualState.Draped)
            {
                AlignDrapedTopToSocket();
            }
        }

        private BlanketVisualState ResolveState()
        {
            PlacementSocket socket = item == null ? null : item.CurrentSocket;
            if (socket != null)
            {
                if (socket.SocketId.IndexOf("lamp", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return BlanketVisualState.Draped;
                }

                if (socket.SocketId.IndexOf("trap", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return BlanketVisualState.FlatTrap;
                }
            }

            if (facts != null)
            {
                FactSnapshot snapshot = facts.CaptureSnapshot();
                if (snapshot.Trap == TrapState.BlanketArmed)
                {
                    return BlanketVisualState.FlatTrap;
                }

                if (snapshot.PlayerZone == PlayerZone.Bed && snapshot.PlayerPose == PlayerPose.Hidden &&
                    item != null && item.CurrentState == ItemPhysicalState.Placed)
                {
                    return BlanketVisualState.Covered;
                }
            }

            return BlanketVisualState.Crumpled;
        }

        private static void SetActive(GameObject visual, bool active)
        {
            if (visual != null)
            {
                visual.SetActive(active);
            }
        }

        private void UpdateDrapedLamp(bool draped)
        {
            if (_drapedLamp != null && (!draped || ResolveDrapedLamp() != _drapedLamp))
            {
                _drapedLamp.SetDraped(false);
                _drapedLamp = null;
            }

            if (!draped)
            {
                return;
            }

            BreakableLight lamp = ResolveDrapedLamp();
            if (lamp != null)
            {
                lamp.SetDraped(true);
                _drapedLamp = lamp;
            }
        }

        private BreakableLight ResolveDrapedLamp()
        {
            Transform anchor = item == null || item.CurrentSocket == null ? null : item.CurrentSocket.Anchor;
            return anchor == null ? null : anchor.GetComponentInParent<BreakableLight>();
        }

        private void AlignDrapedTopToSocket()
        {
            if (drapedVisual == null || item == null || item.CurrentSocket == null ||
                item.CurrentSocket.Anchor == null)
            {
                return;
            }

            Renderer[] renderers = drapedVisual.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return;
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            // The LampDrapeSocket is the authored shade-top alignment point. The
            // FBX pivot is intentionally allowed to be offset; correct only the
            // visual child so the gameplay socket and lamp remain untouched.
            float deltaY = item.CurrentSocket.Anchor.position.y - bounds.max.y;
            if (Mathf.Abs(deltaY) > 0.0005f)
            {
                drapedVisual.transform.position += Vector3.up * deltaY;
            }
        }

        private enum BlanketVisualState
        {
            Crumpled = 0,
            Covered = 1,
            FlatTrap = 2,
            Draped = 3
        }
    }
}
