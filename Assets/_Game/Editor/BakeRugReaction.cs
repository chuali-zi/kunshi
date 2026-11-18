using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace ConGame.Editor
{
    public static class BakeRugReaction
    {
        private const string Folder = "Assets/_Game/Art/Animations/";
        private const string Path = Folder + "AN_Player_RugLanding.anim";
        private const int Rate = 30;
        private const int Frames = 20;

        [MenuItem("ConGame/Characters/Bake D16 player landing")]
        public static void Bake()
        {
            var actor = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Game/Art/Characters/CH_Player_rigged.fbx"));
            var animator = actor.GetComponent<Animator>();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            actor.transform.localScale = Vector3.one * 1.89f;
            actor.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var graph = PlayableGraph.Create("Bake rug contact");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var mesh = new Mesh();
            var skin = actor.GetComponentInChildren<SkinnedMeshRenderer>();
            var vertices = new List<Vector3>();
            var result = new AnimationClip { name = "AN_Player_RugLanding", frameRate = Rate };
            float bodyScale = animator.humanScale * actor.transform.lossyScale.y;
            try
            {
                AnimationClip fall = AssetDatabase.LoadAllAssetsAtPath(Folder + "AN_Player_HitFall.fbx")
                    .OfType<AnimationClip>().First(clip => !clip.name.StartsWith("__preview__"));
                var sourceCurves = AnimationUtility.GetCurveBindings(fall)
                    .Where(b => !b.propertyName.StartsWith("Root"))
                    .ToDictionary(b => b.propertyName, b => AnimationUtility.GetEditorCurve(fall, b));

                var hit = AnimationClipPlayable.Create(graph, fall);
                hit.SetApplyFootIK(false);
                var output = AnimationPlayableOutput.Create(graph, "Player", animator);
                output.SetSourcePlayable(hit);
                graph.Play();

                var curves = new Dictionary<string, AnimationCurve>();
                var pose = new HumanPose();
                using var handler = new HumanPoseHandler(animator.avatar, actor.transform);
                Quaternion previous = Quaternion.identity;
                for (int i = 0; i <= Frames; i++)
                {
                    float time = i / (float)Rate;
                    float impact = i / (float)Frames;
                    hit.SetTime(Mathf.Lerp(.22f, .91f, impact) * fall.length);
                    graph.Evaluate(0);
                    handler.GetHumanPose(ref pose);
                    // Turn the roll across the camera as the knees give and the shoulder settles.
                    float settle = Mathf.SmoothStep(0f, 1f, impact);
                    Vector3 body = pose.bodyPosition + Vector3.back * (.14f * settle / bodyScale);
                    var rollDirection = Quaternion.AngleAxis(58f * settle, Vector3.up);
                    body = rollDirection * body;
                    Quaternion rotation = rollDirection * pose.bodyRotation;
                    if (Quaternion.Dot(rotation, previous) < 0f)
                        rotation = new Quaternion(-rotation.x, -rotation.y, -rotation.z, -rotation.w);
                    previous = rotation;
                    for (int j = 0; j < 3; j++) Key(curves, "RootT." + "xyz"[j], time, body[j]);
                    for (int j = 0; j < 4; j++) Key(curves, "RootQ." + "xyzw"[j], time, rotation[j]);
                    foreach (var entry in sourceCurves)
                        Key(curves, entry.Key, time, entry.Value.Evaluate((float)hit.GetTime()));
                }
                var settings = AnimationUtility.GetAnimationClipSettings(result);
                settings.loopBlendOrientation = true;
                settings.loopBlendPositionY = true;
                settings.loopBlendPositionXZ = true;
                settings.keepOriginalOrientation = true;
                settings.keepOriginalPositionY = true;
                settings.keepOriginalPositionXZ = true;
                settings.stopTime = Frames / (float)Rate;
                AnimationUtility.SetAnimationClipSettings(result, settings);
                foreach (var entry in curves)
                {
                    var curve = entry.Value;
                    if (entry.Key != "RootT.y" && curve.keys.All(key => Mathf.Abs(key.value - curve[0].value) < .0001f))
                        curve = AnimationCurve.Constant(0f, settings.stopTime, curve[0].value);
                    AnimationUtility.SetEditorCurve(result, EditorCurveBinding.FloatCurve("", typeof(Animator), entry.Key), curve);
                }

                // Retargeting the muscle curves can change which part of the suit touches first.
                // Fit the authored vertical curve to the output played on the actual suit.
                var baked = AnimationClipPlayable.Create(graph, result);
                output.SetSourcePlayable(baked);
                var vertical = curves["RootT.y"];
                for (int i = 0; i <= Frames; i++)
                {
                    float time = i / (float)Rate;
                    baked.SetTime(time);
                    graph.Evaluate(0);
                    skin.BakeMesh(mesh, true);
                    mesh.GetVertices(vertices);
                    float lowest = float.PositiveInfinity;
                    foreach (Vector3 vertex in vertices)
                        lowest = Mathf.Min(lowest, skin.transform.TransformPoint(vertex).y);
                    var key = vertical[i];
                    key.value += (.008f - lowest) / bodyScale;
                    vertical.MoveKey(i, key);
                }
                for (int i = 0; i <= Frames; i++) vertical.SmoothTangents(i, 0f);
                AnimationUtility.SetEditorCurve(result, EditorCurveBinding.FloatCurve("", typeof(Animator), "RootT.y"), vertical);
                var old = AssetDatabase.LoadAssetAtPath<AnimationClip>(Path);
                if (old == null)
                    AssetDatabase.CreateAsset(result, Path);
                else
                {
                    EditorUtility.CopySerialized(result, old);
                    EditorUtility.SetDirty(old);
                }
                AssetDatabase.SaveAssets();
                Debug.Log($"Baked {Path}");
            }
            finally
            {
                graph.Destroy();
                if (!EditorUtility.IsPersistent(result)) Object.DestroyImmediate(result);
                Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(actor);
            }
        }

        private static void Key(Dictionary<string, AnimationCurve> curves, string name, float time, float value)
        {
            if (!curves.TryGetValue(name, out var curve)) curves.Add(name, curve = new AnimationCurve());
            curve.AddKey(time, value);
        }
    }
}
