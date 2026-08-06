using System;
using System.Collections.Generic;
using System.IO;
using ConGame.Presentation.Audio;
using UnityEditor;
using UnityEngine;

namespace ConGame.Editor
{
    /// <summary>
    /// NIUA 音效接入:把 art/exports/audio 的 SFX_*.wav 复制进
    /// Assets/_Game/Art/Audio,并按 SFX_&lt;AudioCueId&gt;.wav 命名约定
    /// 自动填充场景内 SemanticAudioDirector 的 bindings。
    /// </summary>
    public static class AudioCueAutoBinder
    {
        const string AudioAssetDir = "Assets/_Game/Art/Audio";
        static readonly string ExportDir =
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "art", "exports", "audio"));

        [MenuItem("ConGame/Art/Import SFX From art-exports")]
        public static void ImportSfx()
        {
            if (!Directory.Exists(ExportDir))
            {
                Debug.LogError($"[AudioCueAutoBinder] 导出目录不存在: {ExportDir}(先跑 art/pipeline/niua/gen_sfx.py)");
                return;
            }

            Directory.CreateDirectory(AudioAssetDir);
            int copied = 0;
            foreach (string src in Directory.GetFiles(ExportDir, "SFX_*.wav"))
            {
                string dst = Path.Combine(AudioAssetDir, Path.GetFileName(src));
                File.Copy(src, dst, overwrite: true);
                copied++;
            }

            AssetDatabase.Refresh();
            Debug.Log($"[AudioCueAutoBinder] 已复制 {copied} 个 wav 到 {AudioAssetDir}");
        }

        [MenuItem("ConGame/Art/Bind SFX Audio Cues In Open Scene")]
        public static void BindCues()
        {
            SemanticAudioDirector director = UnityEngine.Object.FindFirstObjectByType<SemanticAudioDirector>(FindObjectsInactive.Include);
            if (director == null)
            {
                Debug.LogError("[AudioCueAutoBinder] 当前场景没有 SemanticAudioDirector");
                return;
            }

            var bindings = new List<AudioCueBinding>();
            var missing = new List<string>();
            foreach (AudioCueId cue in Enum.GetValues(typeof(AudioCueId)))
            {
                string assetPath = $"{AudioAssetDir}/SFX_{cue}.wav";
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
                if (clip == null)
                {
                    missing.Add(cue.ToString());
                    continue;
                }

                bindings.Add(new AudioCueBinding
                {
                    Cue = cue,
                    Clip = clip,
                    // 与 EnsureProceduralBindings 的默认音量保持一致
                    Volume = cue == AudioCueId.EndingSilence ? 0.35f : 0.7f
                });
            }

            Undo.RecordObject(director, "Bind SFX Audio Cues");
            var serialized = new SerializedObject(director);
            SerializedProperty bindingsProp = serialized.FindProperty("bindings");
            bindingsProp.arraySize = bindings.Count;
            for (int i = 0; i < bindings.Count; i++)
            {
                SerializedProperty element = bindingsProp.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("Cue").enumValueIndex = (int)bindings[i].Cue;
                element.FindPropertyRelative("Clip").objectReferenceValue = bindings[i].Clip;
                element.FindPropertyRelative("Volume").floatValue = bindings[i].Volume;
            }

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(director);
            Debug.Log($"[AudioCueAutoBinder] 绑定 {bindings.Count} 个音效槽位" +
                      (missing.Count > 0 ? $";缺失: {string.Join(", ", missing)}" : ",全部命中"));
        }
    }
}
