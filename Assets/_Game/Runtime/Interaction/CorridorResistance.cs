using UnityEngine;

namespace ConGame.Runtime.Interaction
{
    public static class CorridorResistance
    {
        public static Vector3 FilterMove(Vector3 planarMove, Vector3 inwardDirection, float inwardScale, float pushback)
        {
            Vector3 inward = inwardDirection;
            inward.y = 0f;
            if (inward.sqrMagnitude < 0.0001f)
            {
                return planarMove;
            }

            inward.Normalize();
            float along = Vector3.Dot(planarMove, inward);
            Vector3 lateral = planarMove - inward * along;
            float inwardMotion = along > 0f ? along * inwardScale : along;
            return lateral + inward * inwardMotion - inward * pushback;
        }
    }
}
