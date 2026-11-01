using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ConGame.Editor
{
    /// <summary>
    /// B08→Unity 美术资产验收：导入 FBX/贴图、建 URP 材质、按名字重映射、
    /// 摆验收场景、固定机位截图。批处理入口: -executeMethod ConGame.Editor.ArtAcceptance.Run
    /// </summary>
    public static class ArtAcceptance
    {
        const string ArtRoot = "Assets/_Game/Art";
        const string MatRoot = ArtRoot + "/Materials";
        const string TexRoot = ArtRoot + "/Textures";
        const string ScenePath = "Assets/_Game/Scenes/ArtAcceptance.unity";
        const string OutDir = @"D:\game\con_game\art\previews\unity";

        // 名字 -> (texBaseName 或 null, baseColor, metallic, smoothness, emission或null)
        static readonly Dictionary<string, (string tex, Color col, float metal, float smooth, Color? emit)> Mats =
            new Dictionary<string, (string, Color, float, float, Color?)>
        {
            ["MAT_Wallpaper_FloralAged"] = ("TEX_MAT_Wallpaper_FloralAged", Color.white, 0f, 0.1f, null),
            ["MAT_Wallpaper_Damage"] = ("TEX_MAT_Wallpaper_FloralAged", new Color(0.8f, 0.75f, 0.68f), 0f, 0.1f, null),
            ["MAT_Floor_DarkGreen"] = ("TEX_MAT_Floor_DarkGreen", Color.white, 0f, 0.05f, null),
            ["MAT_Floor_DarkGreen_Rug"] = ("TEX_MAT_Fabric_Rug", Color.white, 0f, 0.02f, null),
            ["MAT_MV_Skirting"] = (null, new Color(0.30f, 0.28f, 0.21f), 0f, 0.2f, null),
            ["MAT_MV_ClockFace"] = (null, new Color(0.89f, 0.88f, 0.83f), 0f, 0.1f, null),
            ["MAT_MV_Ink"] = (null, new Color(0.12f, 0.12f, 0.10f), 0f, 0.2f, null),
            ["MAT_MV_EnamelBlack"] = (null, new Color(0.11f, 0.13f, 0.11f), 0f, 0.4f, null),
            ["MAT_MV_LampShadeOuter"] = (null, new Color(.30f, .31f, .27f), 0f, .25f, null),
            ["MAT_MV_Book_0"] = (null, new Color(0.39f, 0.48f, 0.44f), 0f, 0.1f, null),
            ["MAT_MV_Book_1"] = (null, new Color(0.51f, 0.32f, 0.26f), 0f, 0.1f, null),
            ["MAT_MV_Book_2"] = (null, new Color(0.58f, 0.53f, 0.41f), 0f, 0.1f, null),
            ["MAT_MV_Book_3"] = (null, new Color(0.35f, 0.40f, 0.46f), 0f, 0.1f, null),
            ["MAT_Wood_Walnut"] = ("TEX_MAT_Wood_Walnut", Color.white, 0f, 0.5f, null),
            ["MAT_Wood_Worn"] = ("TEX_MAT_Wood_Worn", Color.white, 0f, 0.4f, null),
            ["MAT_Leather_Burgundy"] = ("TEX_MAT_Leather_Burgundy", Color.white, 0f, 0.45f, null),
            ["MAT_Fabric_Bedding"] = ("TEX_MAT_Fabric_Bedding", Color.white, 0f, 0.15f, null),
            ["MAT_Metal_Dark"] = ("TEX_MAT_Metal_Dark", Color.white, 0.85f, 0.55f, null),
            ["MAT_Metal_Dark_Backup"] = ("TEX_MAT_Metal_Dark", new Color(0.9f, 0.9f, 0.95f), 0.85f, 0.65f, null),
            ["MAT_Props_Small_Atlas"] = ("TEX_MAT_Props_Small_Atlas", Color.white, 0f, 0.3f, null),
            ["MAT_Props_Small_Atlas_Rose"] = ("TEX_MAT_Props_Small_Atlas", Color.white, 0f, 0.4f, null),
            ["MAT_Corridor_Black"] = (null, new Color(0.008f, 0.008f, 0.01f), 0f, 0.2f, null),
            ["MAT_Ceiling_White"] = (null, new Color(0.82f, 0.80f, 0.75f), 0f, 0.2f, null),
            ["MAT_Brick_Aged"] = (null, new Color(0.35f, 0.20f, 0.16f), 0f, 0.1f, null),
            ["MAT_Glass_Set"] = (null, new Color(0.85f, 0.88f, 0.90f, 0.35f), 0.1f, 0.85f, null),
            ["MAT_Glass_Set_Porcelain"] = (null, new Color(0.92f, 0.90f, 0.86f), 0f, 0.75f, null),
            ["MAT_Mirror_Surface"] = (null, new Color(0.75f, 0.80f, 0.85f), 0.9f, 0.95f, null),
            ["MAT_CRT_Inner"] = (null, new Color(0.06f, 0.05f, 0.04f), 0.3f, 0.4f, null),
            ["MAT_LampShade_Inner"] = (null, new Color(0.85f, 0.80f, 0.70f), 0f, 0.5f, null),
            ["MAT_Lamp_Emissive"] = (null, new Color(0.95f, 0.88f, 0.70f), 0f, 0.3f, new Color(0.95f, 0.88f, 0.70f)),
            ["MAT_Flashlight_Emissive"] = (null, new Color(0.9f, 0.85f, 0.7f), 0f, 0.3f, new Color(0.9f, 0.85f, 0.7f)),
        };

        struct Placement
        {
            public string fbx; public Vector3 pos; public float rotY; public string socketParent; public string socketName;
            public Placement(string f, Vector3 p, float r = 0f, string sp = null, string sn = null)
            { fbx = f; pos = p; rotY = r; socketParent = sp; socketName = sn; }
        }

        static readonly Placement[] EnvPlacements =
        {
            new Placement("Environment/ENV_RoomShell", Vector3.zero),
            new Placement("Environment/ENV_DoorSet", Vector3.zero),
            new Placement("Environment/ENV_BlackCorridor", Vector3.zero),
            new Placement("Environment/FUR_Bookcase", new Vector3(-2.925f, 0, 1.8f), 90f),
            new Placement("Environment/FUR_WoodChair", new Vector3(0, 0, 2.35f), 180f),
            // 扶手椅: 不靠墙(椅背↔北墙≈0.7m 蹲行通道)、与书柜留≈0.5m 空隙、微侧向门 (items_interactions §一)
            new Placement("Environment/FUR_Armchair", new Vector3(-1.8f, 0, 1.5f), 160f),
            new Placement("Environment/FUR_Bed", new Vector3(1.3f, 0, 2.1f), 180f),
            new Placement("Environment/FUR_TVCabinet", new Vector3(0, 0, -2.375f)),
            new Placement("Environment/FUR_Dresser", new Vector3(-2.86f, 0, 0.3f), 90f),
            new Placement("Environment/FUR_SideTable", new Vector3(2.45f, 0, 1.35f)),
            new Placement("Environment/ENV_BoardedWindow", new Vector3(-1.2f, 0.9f, 2.61f)),
            new Placement("Props/PRP_CRTTV_Intact", new Vector3(0, 0.65f, -2.438f)),
            new Placement("Props/PRP_Blanket_Crumpled", new Vector3(1.15f, 0.62f, 2.1f)),
            // 吊灯: 参考图罩体完整入画; §2 相机下 2.8m 安装点只剩灯泡底, 降到 2.55 (杆顶出画不穿帮)
            new Placement("Props/PRP_CeilingLamp_Intact", new Vector3(0, 2.55f, 0)),
            new Placement("Props/PRP_TableLamp_Intact", new Vector3(-2.86f, 0.85f, -0.02f), 90f),
            // 地毯: 西端对齐扶手椅(新位)前脚, 东端留门口 1m 铺被空地 (items_interactions §一)
            new Placement("Props/PRP_Rug", new Vector3(0f, 0, 0.85f), 16f),
            new Placement("Props/PRP_Remote", Vector3.zero, 90f, "FUR_Dresser", "SOCKET_RemoteTop"),
            new Placement("Props/PRP_Books_Set", Vector3.zero, 90f, "FUR_Bookcase", "SOCKET_Shelf_02"),
            new Placement("Props/PRP_Vase", Vector3.zero, 0f, "FUR_SideTable", "SOCKET_Vase"),
            new Placement("Props/PRP_WallClock", new Vector3(1.3f, 1.85f, 2.56f), 180f),
            new Placement("Props/PRP_Mirror", new Vector3(3.0f, 0.5f, -1.0f), -78f),
            new Placement("Props/PRP_DrawerClutter", new Vector3(-2.45f, 0, -0.05f), -40f),
        };

        public static void Run()
        {
            try
            {
                RunInner();
                Debug.Log("ART_ACCEPTANCE_DONE");
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError("ART_ACCEPTANCE_FAIL " + e);
                EditorApplication.Exit(1);
            }
        }

        static void RunInner()
        {
            PrepareAssets();
            BuildSceneAndShoot();
        }

        public static void PrepareAssets()
        {
            SyncGeneratedArt();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ConfigureTextureImporters();
            CreateMaterials();
            RemapAllFbx();
        }

        static void SyncGeneratedArt()
        {
            CopyGeneratedFiles(@"D:\game\con_game\art\exports\textures", TexRoot, "*.png");
            CopyGeneratedFiles(@"D:\game\con_game\art\exports\environment", ArtRoot + "/Environment", "*.fbx");
            CopyGeneratedFiles(@"D:\game\con_game\art\exports\props", ArtRoot + "/Props", "*.fbx");
        }

        static void CopyGeneratedFiles(string sourceDirectory, string assetDirectory, string pattern)
        {
            Directory.CreateDirectory(assetDirectory);
            foreach (string source in Directory.GetFiles(sourceDirectory, pattern))
            {
                File.Copy(source, Path.Combine(assetDirectory, Path.GetFileName(source)), true);
            }
        }

        static void ConfigureTextureImporters()
        {
            foreach (string file in Directory.GetFiles(TexRoot, "*.png"))
            {
                string path = file.Replace('\\', '/');
                var ti = (TextureImporter)AssetImporter.GetAtPath(path);
                if (ti == null) continue;
                bool dirty = false;
                if (path.EndsWith("_N.png") && ti.textureType != TextureImporterType.NormalMap)
                { ti.textureType = TextureImporterType.NormalMap; dirty = true; }
                if ((path.EndsWith("_MRA.png") || path.EndsWith("_Mask.png")) && ti.sRGBTexture)
                { ti.sRGBTexture = false; dirty = true; }
                if (dirty) ti.SaveAndReimport();
            }
            Debug.Log("ACCEPT textures configured");
        }

        static void CreateMaterials()
        {
            if (!AssetDatabase.IsValidFolder(MatRoot))
                AssetDatabase.CreateFolder(ArtRoot, "Materials");
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            foreach (var kv in Mats)
            {
                string path = MatRoot + "/" + kv.Key + ".mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null)
                {
                    m = new Material(lit);
                    AssetDatabase.CreateAsset(m, path);
                }
                m.shader = lit;
                var v = kv.Value;
                m.SetColor("_BaseColor", v.col);
                m.SetFloat("_Metallic", v.metal);
                m.SetFloat("_Smoothness", v.smooth);
                m.SetFloat("_BumpScale", 0.22f);
                if (v.tex != null)
                {
                    var bc = AssetDatabase.LoadAssetAtPath<Texture2D>(TexRoot + "/" + v.tex + "_BC.png");
                    var n = AssetDatabase.LoadAssetAtPath<Texture2D>(TexRoot + "/" + v.tex + "_N.png");
                    var mask = AssetDatabase.LoadAssetAtPath<Texture2D>(TexRoot + "/" + v.tex + "_Mask.png");
                    if (bc != null) m.SetTexture("_BaseMap", bc);
                    if (n != null) { m.SetTexture("_BumpMap", n); m.EnableKeyword("_NORMALMAP"); }
                    if (mask != null)
                    {
                        m.SetTexture("_MetallicGlossMap", mask);
                        m.SetTexture("_OcclusionMap", mask);
                        m.EnableKeyword("_METALLICSPECGLOSSMAP");
                    }
                }
                if (v.emit.HasValue)
                {
                    m.EnableKeyword("_EMISSION");
                    m.SetColor("_EmissionColor", v.emit.Value * 2f);
                    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }
                EditorUtility.SetDirty(m);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("ACCEPT materials created: " + Mats.Count);
        }

        static void RemapAllFbx()
        {
            int remapped = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { ArtRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)) continue;
                var mi = AssetImporter.GetAtPath(path) as ModelImporter;
                if (mi == null) continue;
                try
                {
                    mi.materialLocation = ModelImporterMaterialLocation.InPrefab;
                    foreach (var kv in Mats)
                    {
                        var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), kv.Key);
                        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatRoot + "/" + kv.Key + ".mat");
                        if (mat != null) { mi.AddRemap(id, mat); remapped++; }
                    }
                    mi.SaveAndReimport();
                }
                catch (Exception e)
                {
                    Debug.LogWarning("ACCEPT remap fallback " + path + " : " + e.Message);
                }
            }
            Debug.Log("ACCEPT fbx remapped slots: " + remapped);
        }

        static void SwapMaterialsByName(GameObject root)
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    string key = mats[i].name.Replace(" (Instance)", "");
                    var m = AssetDatabase.LoadAssetAtPath<Material>(MatRoot + "/" + key + ".mat");
                    if (m != null) mats[i] = m;
                    else Debug.LogWarning("ACCEPT unmat " + key + " on " + r.name);
                }
                r.sharedMaterials = mats;
            }
        }

        static Transform FindDeep(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        static void BuildSceneAndShoot()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 中性材质审查灯，不额外向相机正面补光。
            var sunGo = new GameObject("Sun");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 0.45f;
            sun.shadows = LightShadows.Soft;
            sunGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var lampGo = new GameObject("CeilingLight");
            var pl = lampGo.AddComponent<Light>();
            pl.type = LightType.Point;
            pl.intensity = 0.8f;
            pl.shadows = LightShadows.Soft;
            pl.range = 8f;
            pl.color = new Color(1f, 0.92f, 0.78f);
            lampGo.transform.position = new Vector3(0, 2.3f, 0);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.06f, 0.055f, 0.05f);

            // 摆放
            var placed = new Dictionary<string, GameObject>();
            foreach (var p in EnvPlacements)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArtRoot + "/" + p.fbx + ".fbx");
                if (prefab == null) { Debug.LogWarning("ACCEPT missing fbx " + p.fbx); continue; }
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                string name = Path.GetFileName(p.fbx);
                go.name = name;
                SwapMaterialsByName(go);
                if (p.socketParent != null && placed.TryGetValue(p.socketParent, out var host))
                {
                    var sock = FindDeep(host.transform, p.socketName);
                    if (sock != null) go.transform.position = sock.position;
                }
                else
                {
                    go.transform.position = p.pos;
                }
                go.transform.rotation = Quaternion.Euler(0, p.rotY, 0);
                placed[name] = go;
            }
            Debug.Log("ACCEPT placed: " + placed.Count);

            // 相机与截图
            Directory.CreateDirectory(OutDir);
            Shoot("B08U_fixed_camera.png", new Vector3(0, 1.15f, -2.14f), new Vector3(0, 1.0f, 0.4f), 62f, false, 0f);
            // 顶视: 隐藏天花
            var shell = placed.ContainsKey("ENV_RoomShell") ? placed["ENV_RoomShell"] : null;
            var ceil = shell != null ? FindDeep(shell.transform, "Shell_Ceiling") : null;
            if (ceil != null) ceil.gameObject.SetActive(false);
            Shoot("B08U_topdown.png", new Vector3(0, 12f, 0), Vector3.zero, 0f, true, 4.5f);
            if (ceil != null) ceil.gameObject.SetActive(true);
            Shoot("B08U_mirror.png", new Vector3(-1.0f, 1.3f, -1.5f), new Vector3(3.0f, 1.1f, -1.0f), 45f, false, 0f);
            Shoot("B08U_vase_dresser.png", new Vector3(1.6f, 1.1f, 0.3f), new Vector3(2.45f, 0.7f, 1.35f), 40f, false, 0f);
            Shoot("B08U_clock.png", new Vector3(1.3f, 1.85f, 1.2f), new Vector3(1.3f, 1.85f, 2.56f), 30f, false, 0f);
            // 地毯特写: 验证 3.5×0.9 平铺朝向与卷翘角
            Shoot("B08U_rug.png", new Vector3(0.4f, 1.7f, -1.4f), new Vector3(0.4f, 0f, 1.3f), 45f, false, 0f);

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("ACCEPT scene saved " + ScenePath);
        }

        static void Shoot(string file, Vector3 pos, Vector3 target, float fov, bool ortho, float orthoSize)
        {
            var go = new GameObject("ShotCam");
            var cam = go.AddComponent<Camera>();
            go.transform.position = pos;
            go.transform.rotation = Quaternion.LookRotation(target - pos, Vector3.up);
            if (ortho) { cam.orthographic = true; cam.orthographicSize = orthoSize; go.transform.rotation = Quaternion.Euler(90, 0, 0); }
            else cam.fieldOfView = fov;
            cam.nearClipPlane = 0.03f;
            var rt = new RenderTexture(960, 540, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(960, 540, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(OutDir, file), tex.EncodeToPNG());
            RenderTexture.active = null;
            cam.targetTexture = null;
            rt.Release();
            UnityEngine.Object.DestroyImmediate(tex);
            UnityEngine.Object.DestroyImmediate(go);
            Debug.Log("ACCEPT shot " + file);
        }
    }
}
