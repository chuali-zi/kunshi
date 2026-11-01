using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ConGame.Editor
{
    /// <summary>Importer event times are normalized; AnimationClip event times are seconds.</summary>
    public static class ConGameAnimationEventRepair
    {
        private static readonly Dictionary<string, AnimationEvent[]> Markers = new()
        {
            { "AN_Player_Swing", new[] { Event("HitStart", 0.45f), Event("HitEnd", 0.6f) } },
            { "AN_Player_Pickup", new[] { Event("Attach", 0.5f), Event("Detach", 0.5f) } },
            { "AN_Player_Throw", new[] { Event("Detach", 0.5f) } },
            { "AN_Player_PutDownLarge", new[] { Event("Detach", 0.7f) } },
            { "AN_Killer_AimFire", new[] { Event("AimHold", 0.25f), Event("Muzzle", 0.5f), Event("Fire", 0.5f) } },
            { "AN_Killer_KickDoor", new[] { Event("KickContact", 0.5f) } },
            { "AN_Killer_Stumble", new[] { Event("StumbleLightEnd", 0.4f), Event("StumbleHeavyEnd", 0.7f), Event("RecoverEnd", 0.9f) } },
            { "AN_Killer_BlockLight", new[] { Event("BlockStart", 0.15f), Event("HardStunEnd", 0.9f) } },
            { "AN_Killer_DrawBackupFire", new[] { Event("Attach", 0.35f), Event("Muzzle", 0.625f), Event("Fire", 0.625f) } },
            { "AN_Killer_Fall", new[] { Event("FallContact", 0.5f), Event("PairStart", 0.9f) } },
            { "PAIR_D08_PlayerSide", new[] { Event("PairStart", 0f), Event("PairContact", 0.1f), Event("PairRelease", 0.85f) } },
            { "PAIR_D08_KillerSide", new[] { Event("PairStart", 0f), Event("PairContact", 0.1f), Event("PairRelease", 0.85f) } },
            { "PAIR_D11_PlayerSide", new[] { Event("PairStart", 0f), Event("PairContact", 0.15f), Event("PairRelease", 0.9f) } },
            { "PAIR_D11_KillerSide", new[] { Event("PairStart", 0f), Event("PairContact", 0.15f), Event("PairRelease", 0.9f) } },
            { "PAIR_D21_PlayerSide", new[] { Event("PairStart", 0f), Event("PairContact", 0.1f), Event("PairLoopStart", 0.25f), Event("PairLoopEnd", 0.6f), Event("Detach", 0.7f), Event("BlockStart", 0.8f), Event("DrawStart", 0.9f) } },
            { "PAIR_D21_KillerSide", new[] { Event("PairStart", 0f), Event("PairContact", 0.1f), Event("PairLoopStart", 0.25f), Event("PairLoopEnd", 0.6f), Event("Detach", 0.7f), Event("BlockStart", 0.8f), Event("DrawStart", 0.9f) } },
        };

        [MenuItem("ConGame/Characters/Repair Imported Animation Event Times")]
        public static void Repair()
        {
            int repaired = 0;
            foreach (var entry in Markers)
            {
                string path = "Assets/_Game/Art/Animations/" + entry.Key + ".fbx";
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) throw new InvalidOperationException("Missing animation: " + path);
                var clips = importer.clipAnimations;
                bool changed = false;
                foreach (var clip in clips)
                {
                    // Preserve later hand-tuned normalized markers. Replace only missing
                    // markers or the legacy seconds-written-as-normalized table.
                    if (clip.events.Length > 0 && clip.events.All(e => e.time >= 0f && e.time <= 1f)) continue;
                    clip.events = entry.Value;
                    changed = true;
                }
                if (!changed) continue;
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
                repaired++;
            }
            Debug.Log($"Repaired normalized animation event times on {repaired} imported clips.");
        }

        private static AnimationEvent Event(string function, float normalizedTime) =>
            new AnimationEvent { functionName = function, time = normalizedTime };
    }
}
