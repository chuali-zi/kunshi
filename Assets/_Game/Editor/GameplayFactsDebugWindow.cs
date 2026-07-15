using ConGame.Domain.Facts;
using ConGame.Runtime.Facts;
using ConGame.Runtime.Loop;
using UnityEditor;
using UnityEngine;

namespace ConGame.Editor
{

    public sealed class GameplayFactsDebugWindow : EditorWindow
    {
        [MenuItem("ConGame/Debug/Gameplay Facts", priority = 200)]
        private static void Open()
        {
            GetWindow<GameplayFactsDebugWindow>("ConGame Facts");
        }

        private void OnGUI()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to inspect live gameplay facts.", MessageType.Info);
                return;
            }

            GameplayFactService facts = Object.FindAnyObjectByType<GameplayFactService>();
            LoopDirector loop = Object.FindAnyObjectByType<LoopDirector>();
            if (facts == null || loop == null)
            {
                EditorGUILayout.HelpBox("No active RoomContext was found.", MessageType.Warning);
                return;
            }

            FactSnapshot snapshot = facts.CaptureSnapshot();
            EditorGUILayout.LabelField("Loop", loop.Progress == null ? "-" : loop.Progress.loopIndex.ToString());
            EditorGUILayout.LabelField("Epoch", loop.LoopEpoch.ToString());
            EditorGUILayout.LabelField("Phase", Object.FindAnyObjectByType<ConGame.Runtime.Flow.GameFlowController>()?.CurrentPhase.ToString() ?? "-");
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Player Zone", snapshot.PlayerZone.ToString());
            EditorGUILayout.LabelField("Player Pose", snapshot.PlayerPose.ToString());
            EditorGUILayout.LabelField("Held Categories", snapshot.HeldItemCategories.ToString());
            EditorGUILayout.LabelField("Room Light", snapshot.RoomLight.ToString());
            EditorGUILayout.LabelField("Meaningful Input", snapshot.HasMeaningfulInput.ToString());
            EditorGUILayout.LabelField("TV", snapshot.TelevisionDamage.ToString());
            EditorGUILayout.LabelField("Last Outcome", loop.LastOutcomeId ?? string.Empty);

            EditorGUILayout.Space();
            if (GUILayout.Button("Mark Meaningful Action"))
            {
                loop.NotifyMeaningfulAction();
            }

            if (GUILayout.Button("Force DEV Death / Reset"))
            {
                loop.RequestKill("DEV");
            }

            Repaint();
        }
    }
}
