using UnityEditor;
using UnityEngine;

namespace ConGame.Editor
{
    // Shape the delivered clothing in bind pose. Skin weights, UVs, avatar and
    // attachment points remain those of the original textured character.
    internal sealed class TailorCharacterMesh : AssetPostprocessor
    {
        private const string Characters = "Assets/_Game/Art/Characters/CH_";

        public override uint GetVersion() => 1;

        private void OnPostprocessModel(GameObject model)
        {
            bool suit = assetPath == Characters + "Player_rigged.fbx";
            if (!suit && assetPath != Characters + "Killer_rigged.fbx") return;

            foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                Mesh mesh = skin.sharedMesh;
                Matrix4x4 toModel = model.transform.worldToLocalMatrix * skin.transform.localToWorldMatrix;
                ShapeClothing(mesh, skin.bones, toModel, suit);
            }
        }

        private static void ShapeClothing(Mesh mesh, Transform[] bones, Matrix4x4 toModel, bool suit)
        {
            // The textured FBXs are single meshes. Use the same blended weights
            // as skinning to end each alteration smoothly at a joint.
            var cloth = new float[bones.Length];
            float shinX = 0f;
            var bind = mesh.bindposes;
            for (int i = 0; i < bones.Length; i++)
            {
                string name = bones[i].name;
                if (name.EndsWith("LeftLeg"))
                    shinX = Mathf.Abs(toModel.MultiplyPoint3x4(bind[i].inverse.MultiplyPoint3x4(Vector3.zero)).x);
                if (name.EndsWith("Hips") || name.Contains("Spine") || name.EndsWith("Leg"))
                    cloth[i] = 1f;
            }

            var points = mesh.vertices;
            var weights = mesh.boneWeights;
            float bottom = float.PositiveInfinity;
            float top = float.NegativeInfinity;
            foreach (Vector3 point in points)
            {
                float y = toModel.MultiplyPoint3x4(point).y;
                bottom = Mathf.Min(bottom, y);
                top = Mathf.Max(top, y);
            }

            Matrix4x4 toMesh = toModel.inverse;
            for (int i = 0; i < points.Length; i++)
            {
                BoneWeight b = weights[i];
                float influence = b.weight0 * cloth[b.boneIndex0] + b.weight1 * cloth[b.boneIndex1]
                                + b.weight2 * cloth[b.boneIndex2] + b.weight3 * cloth[b.boneIndex3];
                Vector3 p = toModel.MultiplyPoint3x4(points[i]);
                float height = Mathf.InverseLerp(bottom, top, p.y);
                float waist = Band(height, .40f, .55f, .78f) * influence;
                p.x *= 1f - waist * (suit ? .19f : .13f);
                p.z *= 1f - waist * .12f;
                if (suit)
                {
                    // Taper the trouser break around each shin, keeping the leather shoes full.
                    float cuff = Band(height, .055f, .16f, .43f) * influence;
                    float creaseX = Mathf.Sign(p.x) * shinX;
                    p.x -= (p.x - creaseX) * .24f * cuff;
                    p.z *= 1f - .14f * cuff;
                }
                else
                {
                    // A heavier skirt flares below the coat belt and gives way to the boots.
                    float skirt = Band(height, .14f, .31f, .51f) * influence;
                    p.x *= 1f + .24f * skirt;
                    p.z *= 1f + .15f * skirt;
                }
                points[i] = toMesh.MultiplyPoint3x4(p);
            }
            mesh.vertices = points;
            mesh.RecalculateBounds();
        }

        private static float Band(float height, float start, float peak, float end)
        {
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(start, peak, height))
                 * Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(peak, end, height));
        }
    }
}
