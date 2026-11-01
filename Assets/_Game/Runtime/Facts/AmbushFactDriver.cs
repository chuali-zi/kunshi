using ConGame.Domain.Facts;
using ConGame.Domain.Interaction;
using UnityEngine;

namespace ConGame.Runtime.Facts
{
    [DisallowMultipleComponent]
    public sealed class AmbushFactDriver : MonoBehaviour
    {
        [SerializeField] private GameplayFactService facts;

        public void Configure(GameplayFactService factService)
        {
            facts = factService;
        }

        private void Update()
        {
            if (facts == null)
            {
                return;
            }

            FactSnapshot snapshot = facts.CaptureSnapshot();
            bool ambushing = AmbushFactDerivation.IsAmbushing(
                snapshot.PlayerZone, snapshot.PlayerPose, snapshot.IsMoving);
            if (snapshot.IsAmbushing != ambushing)
            {
                facts.SetAmbushing(ambushing);
            }
        }
    }
}
