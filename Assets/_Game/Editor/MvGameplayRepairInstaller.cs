using System.IO;
using ConGame.Bootstrap;
using ConGame.Presentation.Camera;
using ConGame.Presentation.Lighting;
using ConGame.Runtime.Lighting;
using ConGame.Runtime.Ending;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ConGame.Editor
{
    public static class MvGameplayRepairInstaller
    {
        [MenuItem("ConGame/Apply MV Gameplay Repairs")]
        public static void Apply()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.name != "Room_M1")
                throw new System.InvalidOperationException("Open Room_M1 in Edit Mode first.");
            const string backup = "Logs/mv_repair_20260912/Room_M1.before.unity";
            Directory.CreateDirectory(Path.GetDirectoryName(backup));
            if (!File.Exists(backup)) File.Copy(scene.path, backup);

            // Use the authored low ambient color. A skybox probe otherwise keeps
            // lighting the room after both gameplay light sources are destroyed.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            WireBrokenLamp("PRP_TableLamp");
            WireBrokenLamp("PRP_CeilingLamp");

            var cameraMode = Object.FindAnyObjectByType<CameraModeController>();
            var screen = GameObject.Find("PRP_CRTTV")?.transform.Find("CameraScreenAnchor");
            cameraMode?.AlignFixedView(screen);
            foreach (var component in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (component == null || component.GetType().Name != "CinemachineVirtualCamera") continue;
                var serialized = new SerializedObject(component);
                var near = serialized.FindProperty("m_Lens.NearClipPlane");
                if (near != null) near.floatValue = 0.03f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            var ending = Object.FindAnyObjectByType<EndingDirector>();
            if (ending != null)
            {
                var serialized = new SerializedObject(ending);
                serialized.FindProperty("endingSettingsAsset").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<EndingSequenceSettingsAsset>("Assets/_Game/Content/Timelines/CFG_EndingSequence.asset");
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        private static void WireBrokenLamp(string name)
        {
            GameObject lamp = GameObject.Find(name);
            Transform intact = lamp == null ? null : lamp.transform.Find(name + "_Intact_Visual");
            if (intact == null) return;
            Transform broken = lamp.transform.Find(name + "_Broken_Visual");
            if (broken == null)
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Art/Props/" + name + "_Broken.fbx");
                if (model == null) return;
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, lamp.transform);
                instance.name = name + "_Broken_Visual";
                broken = instance.transform;
                broken.localPosition = intact.localPosition;
                broken.localRotation = intact.localRotation;
                broken.localScale = intact.localScale;
            }
            var presenter = lamp.GetComponent<BreakableLightPresenter>() ?? lamp.AddComponent<BreakableLightPresenter>();
            presenter.Configure(lamp.GetComponent<BreakableLight>(), intact.gameObject, broken.gameObject);
            EditorUtility.SetDirty(presenter);
        }
    }
}
