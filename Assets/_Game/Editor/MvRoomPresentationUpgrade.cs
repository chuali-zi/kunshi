using System;
using System.IO;
using System.Linq;
using ConGame.Presentation.Lighting;
using ConGame.Runtime.Lighting;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ConGame.Editor
{
    /// <summary>Updates the live room's presentation without replacing its gameplay wiring.</summary>
    public static class MvRoomPresentationUpgrade
    {
        const string Room = "Assets/_Game/Scenes/Room_M1.unity";
        const string Art = "Assets/_Game/Art/";
        const string Config = "Assets/_Game/Content/Config/";

        [MenuItem("ConGame/Art/Upgrade Playable MV Room")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Leave Play Mode before applying room art.");
            const string backup = "Logs/mv_optimization_20260922/before/Room_M1.unity";
            Directory.CreateDirectory(Path.GetDirectoryName(backup));
            if (!File.Exists(backup)) File.Copy(Room, backup);
            ArtAcceptance.PrepareAssets();
            var scene = EditorSceneManager.OpenScene(Room);
            ApplyToCurrentScene();
            ConGameArtInstaller.RebuildNavMesh();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("MV_ROOM_UPGRADE: materials, camera, practical shadows, linked bounce, coat and navigation saved.");
        }

        public static void ApplyToCurrentScene()
        {
            foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var source = PrefabUtility.GetCorrespondingObjectFromSource(r);
                if (source == null) continue;
                var path = AssetDatabase.GetAssetPath(source);
                if (path.StartsWith(Art + "Environment/") || path.StartsWith(Art + "Props/"))
                    r.sharedMaterials = source.sharedMaterials;
            }
            var chair = Find("FUR_Armchair");
            if (chair != null) chair.position = new Vector3(-1.8f, .575f, 1.5f);
            var rear = Find("Zone_SofaRear");
            if (rear != null) rear.position = new Vector3(-2.07f, .8f, 2.23f);
            InstallCoat();
            ConfigureCamera();
            ConfigureRenderer();
            ConfigureGrade();
            var ceiling = ConfigurePractical("PRP_CeilingLamp", new Vector3(0, -.7f, 0), .38f, 7f);
            var table = ConfigurePractical("PRP_TableLamp", new Vector3(0, .11f, 0), .35f, 4.5f);
            ConfigureBounce(ceiling, "CeilingBounce", new Vector3(0, .05f, 1.15f), 1.4f, 6f, new Vector3(-35, 115, 0));
            ConfigureBounce(ceiling, "RoomBounce", new Vector3(0, -.65f, -1.65f), 2f, 7f, new Vector3(60, -25, 0));
            ConfigureBounce(table, "TableBounce", new Vector3(.25f, .45f, 0), .3f, 3f);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.075f, .065f, .050f);
            RenderSettings.reflectionIntensity = .25f;
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }

        public static void InstallCoat()
        {
            var host = Find("PRP_KillerCoat");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Art + "Props/PRP_KillerCoat.fbx");
            if (host == null || prefab == null) return;
            var collider = host.GetComponent<BoxCollider>();
            if (host.localScale != Vector3.one)
            {
                var scale = host.localScale;
                if (collider != null)
                {
                    collider.size = Vector3.Scale(collider.size, scale);
                    collider.center = Vector3.Scale(collider.center, scale);
                }
                foreach (Transform child in host)
                {
                    child.localPosition = Vector3.Scale(child.localPosition, scale);
                    child.localScale = Vector3.Scale(child.localScale, scale);
                }
                host.localScale = Vector3.one;
            }
            var placeholder = host.GetComponent<Renderer>();
            if (placeholder != null) placeholder.enabled = false;
            host.position = new Vector3(3.04f, 1.25f, -.43f);
            if (collider != null)
            {
                collider.center = Vector3.zero;
                collider.size = new Vector3(.16f, 1.3f, .87f);
            }
            var visual = host.Find("MV_CoatVisual");
            if (visual == null)
            {
                visual = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, host)).transform;
                visual.name = "MV_CoatVisual";
            }
            visual.localPosition = Vector3.zero;
            visual.localRotation = Quaternion.Euler(0, 90, 0);
            visual.localScale = Vector3.one;
            var renderers = visual.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            visual.localScale *= 1.3f / bounds.size.y;
            bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            visual.position += host.position - bounds.center;
            var materialPath = Art + "Materials/MAT_Coat_Dark.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, materialPath);
            }
            material.SetColor("_BaseColor", new Color(.075f, .066f, .055f));
            material.SetFloat("_Smoothness", .22f);
            foreach (var renderer in renderers)
                renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray();
            EditorUtility.SetDirty(material);
        }

        static Light ConfigurePractical(string hostName, Vector3 localPosition, float intensity, float range)
        {
            var host = Find(hostName);
            if (host == null) return null;
            var legacy = host.GetComponent<Light>();
            if (legacy != null) legacy.enabled = false;
            var emitter = Child(host, "PracticalEmitter");
            emitter.localPosition = localPosition;
            var light = emitter.GetComponent<Light>();
            if (light == null) light = emitter.gameObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.enabled = true;
            light.color = new Color(1f, .86f, .69f);
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = .65f;
            light.shadowBias = .035f;
            light.shadowNormalBias = .12f;
            light.shadowNearPlane = .08f;
            var lightData = emitter.GetComponent<UniversalAdditionalLightData>();
            if (lightData == null) lightData = emitter.gameObject.AddComponent<UniversalAdditionalLightData>();
            var shadowSettings = new SerializedObject(lightData);
            shadowSettings.FindProperty("m_AdditionalLightsShadowResolutionTier").intValue = 1;
            shadowSettings.ApplyModifiedPropertiesWithoutUndo();
            lightData.softShadowQuality = SoftShadowQuality.High;
            var state = host.GetComponent<BreakableLight>();
            if (state != null) { state.Configure(light); EditorUtility.SetDirty(state); }
            foreach (var r in host.GetComponentsInChildren<Renderer>(true))
                if (r.name.Contains("Bulb") || (hostName.Contains("Table") && r.name.Contains("Shade")))
                    r.shadowCastingMode = ShadowCastingMode.Off;
            EditorUtility.SetDirty(light);
            return light;
        }

        static void ConfigureBounce(Light source, string name, Vector3 localPosition, float ratio, float range, Vector3? direction = null)
        {
            if (source == null) return;
            var child = Child(source.transform.parent, name);
            child.localPosition = localPosition;
            var light = child.GetComponent<Light>();
            if (light == null) light = child.gameObject.AddComponent<Light>();
            // Broad, inexpensive room bounce; every fill still follows its practical lamp.
            light.type = direction.HasValue ? LightType.Directional : LightType.Point;
            if (direction.HasValue) child.rotation = Quaternion.Euler(direction.Value);
            light.range = range;
            light.shadows = LightShadows.None;
            var link = child.GetComponent<PracticalLightBounce>();
            if (link == null) link = child.gameObject.AddComponent<PracticalLightBounce>();
            link.Configure(source, light, ratio);
        }

        static void ConfigureCamera()
        {
            foreach (var camera in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (camera.name != "Main Camera") continue;
                camera.fieldOfView = 57.3835f;
                camera.allowHDR = true;
                var data = camera.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = true;
                data.volumeLayerMask = 1;
                data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
                camera.transform.rotation = Quaternion.Euler(.912f, 0, 0);
                EditorUtility.SetDirty(camera);
                EditorUtility.SetDirty(data);
            }
            var fixedRig = Find("CM Fixed Television Camera");
            if (fixedRig == null) return;
            fixedRig.rotation = Quaternion.Euler(.912f, 0, 0);
            foreach (var component in fixedRig.GetComponents<Component>())
            {
                if (component.GetType().Name != "CinemachineVirtualCamera") continue;
                var so = new SerializedObject(component);
                so.FindProperty("m_Lens.FieldOfView").floatValue = 57.3835f;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        static void ConfigureRenderer()
        {
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline == null) return;
            var settings = new SerializedObject(pipeline);
            settings.FindProperty("m_AdditionalLightShadowsSupported").boolValue = true;
            settings.FindProperty("m_SoftShadowsSupported").boolValue = true;
            settings.FindProperty("m_AdditionalLightsRenderingMode").intValue = 1;
            settings.FindProperty("m_AdditionalLightsShadowResolutionTierMedium").intValue = 512;
            settings.ApplyModifiedPropertiesWithoutUndo();
            pipeline.additionalLightsShadowmapResolution = 2048;
            pipeline.shadowDistance = 20f;
            pipeline.msaaSampleCount = 4;
            pipeline.supportsHDR = true;
            EditorUtility.SetDirty(pipeline);
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(Config + "RP_ConGame_Renderer.asset");
            if (renderer == null) return;
            var ao = renderer.rendererFeatures.OfType<ScreenSpaceAmbientOcclusion>().FirstOrDefault();
            if (ao == null)
            {
                ao = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();
                ao.name = "MV Contact Shadows";
                AssetDatabase.AddObjectToAsset(ao, renderer);
                renderer.rendererFeatures.Add(ao);
            }
            var serialized = new SerializedObject(ao);
            serialized.FindProperty("m_Settings.AOMethod").intValue = 1;
            serialized.FindProperty("m_Settings.Downsample").boolValue = true;
            serialized.FindProperty("m_Settings.Intensity").floatValue = 1.3f;
            serialized.FindProperty("m_Settings.DirectLightingStrength").floatValue = .7f;
            serialized.FindProperty("m_Settings.Radius").floatValue = .22f;
            serialized.FindProperty("m_Settings.Falloff").floatValue = 12f;
            serialized.FindProperty("m_Settings.BlurQuality").intValue = 1;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            ao.SetActive(true);
            ao.Create();
            renderer.SetDirty();
            EditorUtility.SetDirty(renderer);
        }

        static void ConfigureGrade()
        {
            var path = Config + "VP_MvRoom.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }
            Get<Tonemapping>(profile).mode.Override(TonemappingMode.ACES);
            var color = Get<ColorAdjustments>(profile);
            color.postExposure.Override(.15f);
            color.contrast.Override(6f);
            color.saturation.Override(-8f);
            Get<WhiteBalance>(profile).temperature.Override(3f);
            var vignette = Get<Vignette>(profile);
            vignette.intensity.Override(.16f);
            vignette.smoothness.Override(.55f);
            var bloom = Get<Bloom>(profile);
            bloom.intensity.Override(.08f);
            bloom.threshold.Override(1.4f);
            foreach (var component in profile.components) EditorUtility.SetDirty(component);
            EditorUtility.SetDirty(profile);
            var go = Find("MV Room Grade");
            if (go == null) go = new GameObject("MV Room Grade").transform;
            go.gameObject.layer = 0;
            var volume = go.GetComponent<Volume>();
            if (volume == null) volume = go.gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 0;
            volume.weight = 1f;
            volume.sharedProfile = profile;
        }

        static T Get<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet<T>(out var value)) return value;
            value = profile.Add<T>(true);
            AssetDatabase.AddObjectToAsset(value, profile);
            return value;
        }
        static Transform Find(string name) => UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(t => t.name == name && t.gameObject.scene.IsValid());
        static Transform Child(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child != null) return child;
            child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }
    }
}
