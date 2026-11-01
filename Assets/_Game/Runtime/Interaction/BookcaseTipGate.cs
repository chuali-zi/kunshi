using UnityEngine;

namespace ConGame.Runtime.Interaction
{
    public static class BookcaseTipGate
    {
        public static bool BlocksDoor(Vector3 bookcasePosition, Vector3 doorPosition, Vector3 tipWorldDirection)
        {
            Vector3 toDoor = doorPosition - bookcasePosition;
            toDoor.y = 0f;
            Vector3 tip = tipWorldDirection;
            tip.y = 0f;
            if (toDoor.sqrMagnitude < 0.0001f || tip.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            return Vector3.Dot(tip.normalized, toDoor.normalized) > 0.25f;
        }
    }
}
