using ConGame.Runtime.Interaction;
using UnityEngine;

namespace ConGame.Presentation.Animation.Rigging
{
    [DisallowMultipleComponent]
    public sealed class InspectFocusLookDriver : MonoBehaviour
    {
        [SerializeField] private InteractionScanner scanner;
        [SerializeField] private LookAimRigController look;

        private InteractableEntity _driven;

        public void Configure(InteractionScanner interactionScanner, LookAimRigController lookAim)
        {
            scanner = interactionScanner;
            look = lookAim;
        }

        private void LateUpdate()
        {
            if (look == null || scanner == null)
            {
                return;
            }

            InteractableEntity focus = scanner.HasInspectFocusDwell ? scanner.Current : null;
            if (focus == _driven)
            {
                return;
            }

            _driven = focus;
            if (focus == null)
            {
                look.ClearTarget();
            }
            else
            {
                look.SetFollowTarget(focus.transform);
            }
        }
    }
}
