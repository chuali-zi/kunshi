using System;
using UnityEngine;

namespace ConGame.Runtime.Settings
{
    [Serializable]
    public sealed class GameSettingsV1
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public float masterVolume = 1f;
        public float lookSensitivity = 1f;
        public bool invertLookY;
        public float qteThresholdScale = 1f;
        public bool reduceFlashing;
        public float crtIntensity = 1f;

        public void Normalize()
        {
            schemaVersion = CurrentSchemaVersion;
            masterVolume = Mathf.Clamp01(masterVolume);
            lookSensitivity = Mathf.Clamp(lookSensitivity, 0.25f, 3f);
            qteThresholdScale = Mathf.Clamp(qteThresholdScale, 0.5f, 1f);
            crtIntensity = Mathf.Clamp01(crtIntensity);
        }

        public int AdjustQteThreshold(int configuredThreshold)
        {
            return Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(1, configuredThreshold) * qteThresholdScale));
        }

        public Vector2 ApplyLookSettings(Vector2 rawLook)
        {
            float y = invertLookY ? -rawLook.y : rawLook.y;
            return new Vector2(rawLook.x, y) * lookSensitivity;
        }
    }
}
