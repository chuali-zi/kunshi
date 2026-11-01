using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ConGame.Editor
{
    /// <summary>
    /// 程序化生成占位 VFX prefab(枪口火光、弹着火花/血迹、木屑、陶瓷碎片、
    /// 镜裂、墙灰、灯玻璃、电视爆裂、手电可见光锥),全部用内置粒子/网格,
    /// 粒子 StopAction=None,可被 <c>VfxPool</c> 池化。
    /// 幂等:已存在的同名资产先删除再重建,重复执行结果一致。
    /// </summary>
    public static class GeneratePlaceholderVfxPrefabs
    {
        public const string OutputFolder = "Assets/_Game/Art/VFX/Placeholder";

        private const string MaterialFolder = OutputFolder + "/Materials";

        public static GameObject EnsureMuzzleFlashPrefab()
        {
            string path = OutputFolder + "/VFX_MuzzleFlash.prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            EnsureFolder("Assets/_Game/Art", "VFX");
            EnsureFolder("Assets/_Game/Art/VFX", "Placeholder");
            EnsureFolder(OutputFolder, "Materials");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/M_VFX_Additive.mat") ??
                CreateMaterial("M_VFX_Additive", new Color(1f, 0.85f, 0.5f, 1f), true);
            CreateParticlePrefab("VFX_MuzzleFlash", material, new ParticleSpec
            {
                Duration = 0.08f, Lifetime = 0.06f, Speed = 0.4f, Size = 0.22f,
                BurstCount = 6, Color = new Color(1f, 0.8f, 0.35f, 1f),
                ShapeAngle = 18f, Gravity = 0f
            });
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        public static void RepairMuzzleFlashVisual()
        {
            GameObject prefab = EnsureMuzzleFlashPrefab();
            string path = AssetDatabase.GetAssetPath(prefab);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                ConfigureMuzzleFlashVisual(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
        }

        private static void ConfigureMuzzleFlashVisual(GameObject root)
        {
            const string meshPath = OutputFolder + "/MuzzleFlashBurst.asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (mesh == null)
            {
                mesh = new Mesh { name = "MuzzleFlashBurst" };
                var vertices = new List<Vector3> { Vector3.zero };
                var triangles = new List<int>();
                var colors = new List<Color> { Color.white };
                const int points = 16;
                for (int i = 0; i < points; i++)
                {
                    float angle = i * Mathf.PI * 2f / points;
                    float radius = i % 2 == 0 ? (i % 4 == 0 ? 0.5f : 0.32f) : 0.12f;
                    vertices.Add(new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
                    colors.Add(new Color(1f, 0.5f, 0.08f, 0f));
                    triangles.Add(0); triangles.Add(i + 1); triangles.Add((i + 1) % points + 1);
                }
                mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.SetColors(colors);
                mesh.RecalculateNormals(); mesh.RecalculateBounds();
                AssetDatabase.CreateAsset(mesh, meshPath);
            }
            const string materialPath = MaterialFolder + "/M_MuzzleFlash.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = "M_MuzzleFlash" };
                AssetDatabase.CreateAsset(material, materialPath);
            }
            material.SetColor("_BaseColor", new Color(3f, 2.1f, 0.8f, 1f));
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 2f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cull", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(material);

            ParticleSystem particles = root.GetComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.startLifetime = 0.055f; main.startSpeed = 0f; main.startSize = 0.16f;
            main.startColor = Color.white;
            var emission = particles.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            var shape = particles.shape; shape.enabled = false;
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.mesh = mesh; renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        [MenuItem("ConGame/Generate/Placeholder VFX Prefabs")]
        public static void GenerateAllMenu()
        {
            GenerateAll();
        }

        /// <summary>batchmode 入口:-executeMethod ConGame.Editor.GeneratePlaceholderVfxPrefabs.GenerateAll</summary>
        public static void GenerateAll()
        {
            EnsureFolder("Assets/_Game/Art", "VFX");
            EnsureFolder("Assets/_Game/Art/VFX", "Placeholder");
            EnsureFolder(OutputFolder, "Materials");

            Material additive = CreateMaterial("M_VFX_Additive", new Color(1f, 0.85f, 0.5f, 1f), true);
            Material alpha = CreateMaterial("M_VFX_Alpha", new Color(1f, 1f, 1f, 1f), false);
            Material cone = CreateMaterial("M_VFX_FlashlightCone", new Color(1f, 0.95f, 0.6f, 0.16f), false);

            CreateParticlePrefab("VFX_MuzzleFlash", additive, new ParticleSpec
            {
                Duration = 0.08f, Lifetime = 0.06f, Speed = 0.4f, Size = 0.22f,
                BurstCount = 6, Color = new Color(1f, 0.8f, 0.35f, 1f),
                ShapeAngle = 18f, Gravity = 0f
            });
            CreateParticlePrefab("VFX_ImpactSpark", additive, new ParticleSpec
            {
                Duration = 0.3f, Lifetime = 0.25f, Speed = 3f, Size = 0.03f,
                BurstCount = 12, Color = new Color(1f, 0.92f, 0.6f, 1f),
                ShapeAngle = 35f, Gravity = 4f
            });
            CreateParticlePrefab("VFX_BloodImpact", alpha, new ParticleSpec
            {
                Duration = 0.4f, Lifetime = 0.35f, Speed = 1.8f, Size = 0.05f,
                BurstCount = 16, Color = new Color(0.45f, 0.05f, 0.04f, 1f),
                ShapeAngle = 40f, Gravity = 6f
            });
            CreateParticlePrefab("VFX_WoodDebris", alpha, new ParticleSpec
            {
                Duration = 0.5f, Lifetime = 0.5f, Speed = 2.5f, Size = 0.06f,
                BurstCount = 20, Color = new Color(0.45f, 0.3f, 0.15f, 1f),
                ShapeAngle = 45f, Gravity = 9f
            });
            CreateParticlePrefab("VFX_CeramicShards", alpha, new ParticleSpec
            {
                Duration = 0.5f, Lifetime = 0.45f, Speed = 2.2f, Size = 0.05f,
                BurstCount = 18, Color = new Color(0.9f, 0.88f, 0.82f, 1f),
                ShapeAngle = 50f, Gravity = 9f
            });
            CreateParticlePrefab("VFX_MirrorCrack", additive, new ParticleSpec
            {
                Duration = 0.45f, Lifetime = 0.4f, Speed = 2f, Size = 0.04f,
                BurstCount = 24, Color = new Color(0.8f, 0.9f, 1f, 1f),
                ShapeAngle = 55f, Gravity = 7f
            });
            CreateParticlePrefab("VFX_WallDust", alpha, new ParticleSpec
            {
                Duration = 0.7f, Lifetime = 0.6f, Speed = 0.8f, Size = 0.15f,
                BurstCount = 10, Color = new Color(0.6f, 0.58f, 0.55f, 0.7f),
                ShapeAngle = 30f, Gravity = 0.2f
            });
            CreateParticlePrefab("VFX_LampGlassBurst", additive, new ParticleSpec
            {
                Duration = 0.45f, Lifetime = 0.4f, Speed = 2.6f, Size = 0.045f,
                BurstCount = 20, Color = new Color(1f, 0.85f, 0.5f, 1f),
                ShapeAngle = 60f, Gravity = 7f
            });
            CreateParticlePrefab("VFX_TelevisionBurst", additive, new ParticleSpec
            {
                Duration = 0.6f, Lifetime = 0.5f, Speed = 4f, Size = 0.05f,
                BurstCount = 30, Color = new Color(0.7f, 0.95f, 1f, 1f),
                ShapeAngle = 70f, Gravity = 5f
            });
            CreateFlashlightConePrefab(cone);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[GeneratePlaceholderVfxPrefabs] 占位 VFX prefab 已生成/覆盖于 " + OutputFolder);
        }

        private struct ParticleSpec
        {
            public float Duration;
            public float Lifetime;
            public float Speed;
            public float Size;
            public int BurstCount;
            public Color Color;
            public float ShapeAngle;
            public float Gravity;
        }

        private static void CreateParticlePrefab(string name, Material material, ParticleSpec spec)
        {
            GameObject root = new GameObject(name);
            try
            {
                ParticleSystem particles = root.AddComponent<ParticleSystem>();
                ParticleSystem.MainModule main = particles.main;
                main.duration = spec.Duration;
                main.loop = false;
                main.playOnAwake = false;
                main.stopAction = ParticleSystemStopAction.None;
                main.startLifetime = spec.Lifetime;
                main.startSpeed = spec.Speed;
                main.startSize = spec.Size;
                main.startColor = spec.Color;
                main.gravityModifier = spec.Gravity;
                main.maxParticles = Mathf.Max(32, spec.BurstCount * 2);
                main.simulationSpace = ParticleSystemSimulationSpace.World;

                ParticleSystem.EmissionModule emission = particles.emission;
                emission.rateOverTime = 0f;
                emission.SetBursts(new[]
                {
                    new ParticleSystem.Burst(0f, (short)spec.BurstCount)
                });

                ParticleSystem.ShapeModule shape = particles.shape;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = spec.ShapeAngle;
                shape.radius = 0.02f;

                ParticleSystemRenderer renderer = root.GetComponent<ParticleSystemRenderer>();
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                if (material != null)
                {
                    renderer.sharedMaterial = material;
                }

                if (name == "VFX_MuzzleFlash") ConfigureMuzzleFlashVisual(root);
                SavePrefabOverwriting(root, name);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void CreateFlashlightConePrefab(Material material)
        {
            GameObject root = new GameObject("VFX_FlashlightCone");
            try
            {
                Mesh coneMesh = CreateConeMesh(0.65f, 3.2f, 24);
                SaveAssetOverwriting(coneMesh, "VFX_FlashlightConeMesh", OutputFolder);

                MeshFilter filter = root.AddComponent<MeshFilter>();
                filter.sharedMesh = coneMesh;
                MeshRenderer renderer = root.AddComponent<MeshRenderer>();
                if (material != null)
                {
                    renderer.sharedMaterial = material;
                }

                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                SavePrefabOverwriting(root, "VFX_FlashlightCone");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>沿 +Z 轴的张开光锥网格,apex 在原点。</summary>
        private static Mesh CreateConeMesh(float radius, float length, int segments)
        {
            List<Vector3> vertices = new List<Vector3> { Vector3.zero };
            List<int> triangles = new List<int>();
            for (int i = 0; i < segments; i++)
            {
                float angle = (i / (float)segments) * Mathf.PI * 2f;
                vertices.Add(new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, length));
            }

            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                triangles.Add(0);
                triangles.Add(1 + next);
                triangles.Add(1 + i);
            }

            Mesh mesh = new Mesh { name = "VFX_FlashlightConeMesh" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Material CreateMaterial(string name, Color color, bool additive)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            Material material = new Material(shader) { name = name };
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            else if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f); // Transparent
            }

            if (additive && material.HasProperty("_Blend"))
            {
                material.SetFloat("_Blend", 1f); // Additive
            }

            SaveAssetOverwriting(material, name, MaterialFolder);
            return AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/" + name + ".mat");
        }

        private static void SavePrefabOverwriting(GameObject root, string name)
        {
            string path = OutputFolder + "/" + name + ".prefab";
            AssetDatabase.DeleteAsset(path);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }

        private static void SaveAssetOverwriting(Object asset, string name, string folder)
        {
            string extension = asset is Mesh ? ".asset" : ".mat";
            string path = folder + "/" + name + extension;
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(asset, path);
        }

        private static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + child))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }
    }
}
