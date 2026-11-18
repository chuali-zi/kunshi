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

        // End pose on the production suit. Open the tucked elbows, stretch the
        // lower leg along the runner and keep the other knee folded.
        private static readonly Dictionary<string, float> RestingChannels = new()
        {
            { "Spine Front-Back", .88f },
            { "Spine Left-Right", -.98f },
            { "Spine Twist Left-Right", -.6f },
            { "Chest Front-Back", -.53f },
            { "Chest Left-Right", -.98f },
            { "Chest Twist Left-Right", .15f },
            { "UpperChest Front-Back", -.01f },
            { "UpperChest Left-Right", -.29f },
            { "UpperChest Twist Left-Right", .14f },
            { "Neck Nod Down-Up", .28f },
            { "Neck Turn Left-Right", .2f },
            { "Head Nod Down-Up", -.93f },
            { "Head Tilt Left-Right", .98f },
            { "Head Turn Left-Right", .9f },
            { "Left Upper Leg Front-Back", .98f },
            { "Left Upper Leg In-Out", -.98f },
            { "Left Upper Leg Twist In-Out", .98f },
            { "Left Lower Leg Stretch", .32f },
            { "Right Upper Leg Front-Back", .98f },
            { "Right Upper Leg Twist In-Out", -.21f },
            { "Right Lower Leg Stretch", .27f },
            { "Left Shoulder Down-Up", -.71f },
            { "Left Shoulder Front-Back", .3f },
            { "Left Arm Down-Up", .22f },
            { "Left Arm Front-Back", .43f },
            { "Left Arm Twist In-Out", -.63f },
            { "Left Forearm Stretch", .96f },
            { "Left Forearm Twist In-Out", -.47f },
            { "Left Hand Down-Up", .02f },
            { "Right Shoulder Down-Up", -.3f },
            { "Right Arm Down-Up", -1.06f },
            { "Right Arm Front-Back", .25f },
            { "Right Arm Twist In-Out", -.87f },
            { "Right Forearm Stretch", -.01f },
            { "Right Forearm Twist In-Out", -1.35f },
            { "Right Hand Down-Up", -.15f },
            // Keep the heels at the runner as the upper body rolls across the hips.
            { "LeftFootT.x", .11f },
            { "LeftFootT.y", -.75f },
            { "LeftFootT.z", -.19f },
            { "RightFootT.x", .24f },
            { "RightFootT.y", -.38f },
            { "RightFootT.z", -.19f },
        };

        // The near arm clears the folding shin, bends under the descending shoulder,
        // then slides out beside the hip. Times are fractions of the landing clip.
        private static readonly Dictionary<string, AnimationCurve> CatchingArm = new()
        {
            { "Left Arm Down-Up", CatchCurve(-.89f, 0f, .44f) },
            { "Left Arm Front-Back", CatchCurve(.23f, .45f, .45f) },
            { "Left Arm Twist In-Out", CatchCurve(.17f, -.61f, -.81f) },
            { "Left Forearm Stretch", CatchCurve(.72f, .41f, .54f) },
            { "Left Forearm Twist In-Out", CatchCurve(.39f, -1f, -1f) },
            { "Left Hand Down-Up", CatchCurve(.27f, -.03f, .01f) },
            { "Left Hand In-Out", CatchCurve(0f, .16f, 0f) },
        };

        private static AnimationCurve CatchCurve(float reach, float contact, float settle)
        {
            return new AnimationCurve(new Keyframe(.52f, reach), new Keyframe(.70f, contact),
                new Keyframe(.85f, settle));
        }

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
                // Supine, with the long axis of the body across the runner.
                var restingRotation = Quaternion.LookRotation(new Vector3(-0.259f, 0.958f, 0.125f),
                    new Vector3(-0.699f, -0.097f, -0.708f));
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
                    float land = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.42f, .90f, impact));
                    // The hips take the impact first. Let the upper back and collar roll onto
                    // the runner before the head and forearms finish dropping.
                    float shoulder = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.54f, .96f, impact));
                    float release = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.63f, 1f, impact));
                    // Carry the hip toward the front edge so the far leg clears the wingback.
                    body += new Vector3(.09f, 0f, .26f) * (land / bodyScale);
                    rotation = Quaternion.Slerp(rotation, restingRotation, land);
                    if (Quaternion.Dot(rotation, previous) < 0f)
                        rotation = new Quaternion(-rotation.x, -rotation.y, -rotation.z, -rotation.w);
                    previous = rotation;
                    for (int j = 0; j < 3; j++) Key(curves, "RootT." + "xyz"[j], time, body[j]);
                    for (int j = 0; j < 4; j++) Key(curves, "RootQ." + "xyzw"[j], time, rotation[j]);
                    float catchWeight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.28f, .52f, impact))
                        * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.85f, .98f, impact)));
                    foreach (var entry in sourceCurves)
                    {
                        float value = entry.Value.Evaluate((float)hit.GetTime());
                        if (RestingChannels.TryGetValue(entry.Key, out float resting))
                        {
                            float weight = land;
                            if (entry.Key.Contains("Chest") || entry.Key.Contains("Shoulder"))
                                weight = shoulder;
                            if (entry.Key.Contains("Forearm") || entry.Key.StartsWith("Neck") || entry.Key.StartsWith("Head"))
                                weight = release;
                            value = Mathf.Lerp(value, resting, weight);
                        }
                        if (CatchingArm.TryGetValue(entry.Key, out var catchCurve))
                            value = Mathf.Lerp(value, catchCurve.Evaluate(impact), catchWeight);
                        Key(curves, entry.Key, time, value);
                    }
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
