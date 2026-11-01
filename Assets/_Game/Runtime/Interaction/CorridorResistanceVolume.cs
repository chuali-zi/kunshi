using ConGame.Domain.Interaction;
using ConGame.Runtime.Player;
using UnityEngine;

namespace ConGame.Runtime.Interaction
{
    [DisallowMultipleComponent]
    public sealed class CorridorResistanceVolume : MonoBehaviour
    {
        [SerializeField] private Vector3 inwardDirection = Vector3.right;
        [SerializeField, Range(0f, 1f)] private float inwardScale;
        [SerializeField, Min(0f)] private float pushback = 1.4f;

        public void Configure(Vector3 inward, float inwardMotionScale = 0f, float pushStrength = 1.4f)
        {
            inwardDirection = inward;
            inwardScale = inwardMotionScale;
            pushback = pushStrength;
        }

        private void OnTriggerStay(Collider other)
        {
            PlayerMotor motor = other == null ? null : other.GetComponentInParent<PlayerMotor>();
            motor?.ApplyCorridorResistance(inwardDirection, inwardScale, pushback);
        }
    }
}
