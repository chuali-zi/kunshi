using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ConGame.Editor
{
    public static class MvArtUpgrade
    {
        const string Room = "Assets/_Game/Scenes/Room_M1.unity";
        const string Shots = "art/previews/mv_20260911";

        [MenuItem("ConGame/Art/Apply Abuku MV Art")]
        public static void Run()
        {
            MvRoomPresentationUpgrade.Run();
        }

        public static void Capture()
        {
            EditorSceneManager.OpenScene(Room, OpenSceneMode.Single);
            Directory.CreateDirectory(Shots);
            // These are actual scene renders. Hide actors only for the room overview.
            var actors = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .Where(t => t.name == "Player" || t.name == "Killer").Select(t => t.gameObject).ToArray();
            foreach (var a in actors) a.SetActive(false);
            Shoot("room_fixed.png",new Vector3(0,1.15f,-2.14f),new Vector3(0,1.10f,1.0f),62);
            Shoot("room_west.png",new Vector3(1.55f,1.45f,-.4f),new Vector3(-2.05f,1.05f,1.55f),62);
            Shoot("room_door.png",new Vector3(-.8f,1.4f,-1.1f),new Vector3(2.45f,1.0f,1.45f),58);
            Shoot("room_tv.png",new Vector3(0,1.35f,1.4f),new Vector3(0,1.0f,-2.35f),62);
            foreach (var a in actors) a.SetActive(true);
            Shoot("room_with_actors.png",new Vector3(0,1.15f,-2.14f),new Vector3(0,1.0f,.4f),62);
            // Changes made for capture are intentionally not saved.
            EditorSceneManager.OpenScene(Room, OpenSceneMode.Single);
            Debug.Log("MV_ART_CAPTURE_DONE");
        }

        static void Shoot(string name, Vector3 pos, Vector3 target, float fov)
        {
            var go = new GameObject("MV_ScreenshotCamera");
            var camera = go.AddComponent<Camera>();
            go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(target-pos));
            camera.fieldOfView=fov;
            camera.nearClipPlane=.03f;
            camera.farClipPlane=35;
            camera.clearFlags=CameraClearFlags.SolidColor;
            camera.backgroundColor=Color.black;
            var rt=new RenderTexture(1600,900,24);
            var previous=RenderTexture.active;
            try
            {
                camera.targetTexture=rt;
                camera.Render();
                RenderTexture.active=rt;
                var tex=new Texture2D(1600,900,TextureFormat.RGB24,false);
                tex.ReadPixels(new Rect(0,0,1600,900),0,0); tex.Apply();
                File.WriteAllBytes(Path.Combine(Shots,name),tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
            }
            finally
            {
                RenderTexture.active=previous;
                camera.targetTexture=null;
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
