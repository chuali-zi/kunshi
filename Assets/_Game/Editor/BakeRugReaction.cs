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
            { "Left Forearm Twist In-Out", -.31f },
            { "Left Hand Down-Up", -.235f },
            { "LeftHand.Thumb.1 Stretched", -.7f },
            { "LeftHand.Thumb.Spread", -.45f },
            { "LeftHand.Index.1 Stretched", .53f },
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
            { "Left Forearm Twist In-Out", CatchCurve(.39f, -.15f, -.20f) },
            // Flex the wrist as the fingertips slide off the bound edge onto the carpet.
            { "Left Hand Down-Up", new AnimationCurve(new Keyframe(.52f, .27f),
                new Keyframe(.70f, -.11f, -2f, -2f), new Keyframe(.76f, -.23f, -1f, -1f),
                new Keyframe(.85f, -.27f)) },
            { "Left Hand In-Out", CatchCurve(0f, .16f, 0f) },
            // Open the thumb as the wrist turns palm-down. The suit's four fingers
            // move together on the index chain and uncurl as they take weight.
            { "LeftHand.Thumb.1 Stretched", CatchCurve(-1.4f, -.6f, -.3f) },
            { "LeftHand.Thumb.Spread", CatchCurve(-.7f, -.45f, -.45f) },
            { "LeftHand.Index.1 Stretched", CatchCurve(.32f, .48f, .60f) },
            { "LeftHand.Index.2 Stretched", CatchCurve(.48f, .92f, .92f) },
            { "LeftHand.Index.3 Stretched", CatchCurve(.38f, .9f, .9f) },
        };

        private static AnimationCurve CatchCurve(float reach, float contact, float settle)
        {
            return new AnimationCurve(new Keyframe(.52f, reach), new Keyframe(.70f, contact),
                new Keyframe(.85f, settle));
        }

        // Let the pelvis lead the turn as the supporting shoe slips, then give the
        // authored shoulder and hand catch full control by the time the palm lands.
        private static readonly AnimationCurve HipLead = new(
            new Keyframe(.25f, 0f), new Keyframe(.435f, .46f),
            new Keyframe(.55f, .18f, -1.8f, -1.8f),
            new Keyframe(.64f, .10f, -.8f, -.8f), new Keyframe(.71f, 0f));

        // Lower the far heel as the player lands, easing off as the shoulder takes weight.
        // Values are metres below the source ankle path at normalized clip times.
        private static readonly AnimationCurve FarHeelDrop = new(
            new Keyframe(.44f, 0f), new Keyframe(.64f, .043f, .20f, .20f),
            new Keyframe(.74f, .066f), new Keyframe(1f, .008f));

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
                Quaternion rollStart = Quaternion.identity;
                const int buckleFrame = 5;
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
                    // Once the knees buckle, carry the pelvis onto its side in one arc.
                    // The source fall pitches forward again halfway through this turn.
                    if (i == buckleFrame) rollStart = rotation;
                    rotation = Quaternion.Slerp(rotation, restingRotation, land);
                    float lead = HipLead.Evaluate(impact);
                    if (lead > 0f)
                    {
                        float roll = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(
                            buckleFrame / (float)Frames, .87f, impact));
                        Quaternion leadingHip = Quaternion.Slerp(rollStart, restingRotation, roll);
                        rotation = Quaternion.Slerp(rotation, leadingHip, lead);
                    }
                    float heelSlide = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.30f, .78f, impact));
                    float nearLegWeight = Mathf.Lerp(land, heelSlide, lead);
                    if (Quaternion.Dot(rotation, previous) < 0f)
                        rotation = new Quaternion(-rotation.x, -rotation.y, -rotation.z, -rotation.w);
                    previous = rotation;
                    for (int j = 0; j < 3; j++) Key(curves, "RootT." + "xyz"[j], time, body[j]);
                    for (int j = 0; j < 4; j++) Key(curves, "RootQ." + "xyzw"[j], time, rotation[j]);
                    float catchWeight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.28f, .52f, impact))
                        * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.85f, .98f, impact)));
                    // The far boot folds back beneath the bent knee while the near heel
                    // slides forward. Foot goals are stored in the rotating body's space.
                    float tuck = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.44f, .68f, impact));
                    Vector3 farFootOffset = Quaternion.Inverse(rotation)
                        * new Vector3(-.15f * tuck, -FarHeelDrop.Evaluate(impact), 0f) / bodyScale;
                    foreach (var entry in sourceCurves)
                    {
                        float value = entry.Value.Evaluate((float)hit.GetTime());
                        if (RestingChannels.TryGetValue(entry.Key, out float resting))
                        {
                            // Slide the near heel out while the far knee still folds under the hip.
                            float weight = entry.Key.StartsWith("LeftFootT.") || entry.Key.StartsWith("Left Upper Leg")
                                || entry.Key == "Left Lower Leg Stretch" ? nearLegWeight : land;
                            if (entry.Key.Contains("Chest") || entry.Key.Contains("Shoulder"))
                                weight = shoulder;
                            if (entry.Key.Contains("Forearm") || entry.Key.StartsWith("Neck") || entry.Key.StartsWith("Head"))
                                weight = release;
                            value = Mathf.Lerp(value, resting, weight);
                        }
                        if (entry.Key.StartsWith("RightFootT."))
                            value += farFootOffset[entry.Key[^1] - 'x'];
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
                // A heel or seam can be the lowest point between two 30 Hz pose keys.
                const int contactSubsteps = 4;
                var vertical = new AnimationCurve();
                for (int i = 0; i <= Frames * contactSubsteps; i++)
                {
                    float time = i / (float)(Rate * contactSubsteps);
                    baked.SetTime(time);
                    graph.Evaluate(0);
                    skin.BakeMesh(mesh, true);
                    mesh.GetVertices(vertices);
                    float lowest = float.PositiveInfinity;
                    foreach (Vector3 vertex in vertices)
                        lowest = Mathf.Min(lowest, skin.transform.TransformPoint(vertex).y);
                    float authoredHeight = curves["RootT.y"].Evaluate(time);
                    vertical.AddKey(time, authoredHeight + (.008f - lowest) / bodyScale);
                }
                for (int i = 0; i <= Frames * contactSubsteps; i++) vertical.SmoothTangents(i, 0f);
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
