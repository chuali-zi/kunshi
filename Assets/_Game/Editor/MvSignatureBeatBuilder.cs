using System;
using System.Linq;
using ConGame.Bootstrap;
using ConGame.Presentation.CRT;
using ConGame.Presentation.Vfx;
using ConGame.Runtime.AI;
using ConGame.Runtime.Ending;
using ConGame.Runtime.Events;
using ConGame.Runtime.Facts;
using ConGame.Runtime.Flow;
using ConGame.Runtime.Loop;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ConGame.Editor
{
    public static class MvSignatureBeatBuilder
    {
        [MenuItem("ConGame/Art/Install MV Signature Beats (Round 3)")]
        public static void Build()
        {
            const string folder = "Assets/_Game/Art/MvSignature/";
            foreach (string name in new[] { "TitleInk", "Realisation", "ScreenGlass" })
            {
                string texturePath = folder + name + ".png";
                AssetDatabase.ImportAsset(texturePath);
                var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 2048;
                importer.SaveAndReimport();
            }
            const string path = "Assets/_Game/Resources/MvSignatureSettings.asset";
            var settings = AssetDatabase.LoadAssetAtPath<MvSignatureSettings>(path);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<MvSignatureSettings>();
                AssetDatabase.CreateAsset(settings, path);
            }
            settings.Title = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "TitleInk.png");
            settings.Realisation = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "Realisation.png");
            settings.Glass = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "ScreenGlass.png");
            string materialPath = folder + "FlashlightAir.mat";
            var air = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (air == null)
            {
                air = new Material(Shader.Find("ConGame/MV/FlashlightAir"));
                AssetDatabase.CreateAsset(air, materialPath);
            }
            settings.FlashlightAir = air;
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/_Game/Content/Config/RP_ConGame_Renderer.asset");
            if (!renderer.rendererFeatures.OfType<CrtOverlayRendererFeature>().Any())
            {
                var feature = ScriptableObject.CreateInstance<CrtOverlayRendererFeature>();
                feature.name = "ConGame CRT and MV Signature Beats";
                AssetDatabase.AddObjectToAsset(feature, renderer);
                renderer.rendererFeatures.Add(feature);
                renderer.SetDirty();
                EditorUtility.SetDirty(renderer);
            }
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();

            var scene = EditorSceneManager.OpenScene("Assets/_Game/Scenes/Room_M1.unity");
            var room = UnityEngine.Object.FindAnyObjectByType<RoomContext>();
            if (room == null) throw new InvalidOperationException("Production RoomContext missing.");
            var presenter = room.GetComponent<MvSignaturePresenter>() ?? room.gameObject.AddComponent<MvSignaturePresenter>();
            presenter.Configure(settings, Find<GameFlowController>(), Find<LoopDirector>(), Find<GameplayFactService>(),
                Find<GameplayEventHub>(), Find<KillerDirector>(), Find<TelevisionController>(),
                Find<SemanticVfxDirector>(), Find<CrtFullscreenPass>());
            // Keep the title and glass available to the ending compositor even before runtime installation.
            EditorUtility.SetDirty(Find<CrtFullscreenPass>().Material);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("MV_SIGNATURE_BEATS: production presenter, settings, title, manga and fracture assets installed.");
        }

        private static T Find<T>() where T : UnityEngine.Object =>
            UnityEngine.Object.FindAnyObjectByType<T>(FindObjectsInactive.Include);
    }
}
