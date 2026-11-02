using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace ConGame.Editor
{
    /// <summary>Reference-frame main shot tools. Never rebuilds gameplay systems.</summary>
    public static partial class MvMainShotBuilder
    {
        const string Room = "Assets/_Game/Scenes/Room_M1.unity";
        const string CapturePending = "ConGame.MvMainShot.CapturePending";
        static double captureDeadline;
        static bool standingCaptureRequested;

        [MenuItem("ConGame/Art/Capture Main Shot")]
        public static void Capture()
        {
            if (!EditorApplication.isPlaying) EditorSceneManager.OpenScene(Room, OpenSceneMode.Single);
            RenderMainCamera();
        }

        /// <summary>Batch entry point: omit -quit; captures the real awakening after 3 seconds, then exits.</summary>
        [MenuItem("ConGame/Art/Capture Main Shot in Play Mode")]
        public static void CaptureGameplay()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Start capture outside Play Mode.");
            EditorSceneManager.OpenScene(Room, OpenSceneMode.Single);
            Application.runInBackground = true;
            SessionState.SetBool(CapturePending, true);
            RestoreCaptureCallback();
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        static void RestoreCaptureCallback()
        {
            if (!SessionState.GetBool(CapturePending, false)) return;
            captureDeadline = EditorApplication.timeSinceStartup + 120;
            standingCaptureRequested=false;
            EditorApplication.update -= WaitForAwakening;
            EditorApplication.update += WaitForAwakening;
        }

        static void WaitForAwakening()
        {
            if (EditorApplication.timeSinceStartup > captureDeadline)
            {
                FinishCapture(1);
                throw new TimeoutException("Gameplay capture did not reach the awakening frame.");
            }
            if (!EditorApplication.isPlaying || Time.timeSinceLevelLoad < 3 || Time.frameCount < 8) return;
            if (Argument("-mvStanding") == "true")
            {
                if (!standingCaptureRequested)
                {
                    GameObject.Find("Player").SendMessage("OnMeaningfulAction", SendMessageOptions.RequireReceiver);
                    standingCaptureRequested=true;
                }
                if (Time.timeSinceLevelLoad < 4) return;
            }
            Application.runInBackground = true;
            try
            {
                var player=GameObject.Find("Player");
                var animator=player.GetComponentInChildren<Animator>();
                foreach(var bone in new[]{HumanBodyBones.Hips,HumanBodyBones.LeftFoot,HumanBodyBones.RightFoot})
                    Debug.Log($"MV_POSE: {bone}={animator.GetBoneTransform(bone).position:F4} root={player.transform.position:F4} visual={animator.transform.localPosition:F4}");
                foreach(var skin in player.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    var mesh=new Mesh();skin.BakeMesh(mesh,true);
                    float minY=float.PositiveInfinity;
                    foreach(var v in mesh.vertices) minY=Mathf.Min(minY,skin.transform.TransformPoint(v).y);
                    Debug.Log($"MV_SHOE_SOLE: y={minY:F4}");
                    UnityEngine.Object.DestroyImmediate(mesh);
                }
                RenderMainCamera();
                Debug.Log($"MV_GAMEPLAY_CAPTURE: frame={Time.frameCount} time={Time.timeSinceLevelLoad:F3} camera={Camera.main.transform.position:F3}");
                FinishCapture(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                FinishCapture(1);
            }
        }

        static void FinishCapture(int code)
        {
            SessionState.SetBool(CapturePending, false);
            EditorApplication.update -= WaitForAwakening;
            if (Application.isBatchMode) EditorApplication.Exit(code);
            else EditorApplication.ExitPlaymode();
        }

        internal static void RenderMainCamera(string outputPath = null)
        {
            var camera = Camera.main;
            if (camera == null) throw new InvalidOperationException("Room has no Main Camera.");
            string output = outputPath ?? Argument("-mvScreenshot") ?? "/workspace/ref/progress.png";
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
            var previous = RenderTexture.active;
            var previousTarget = camera.targetTexture;
            float previousAspect = camera.aspect;
            var target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGBHalf);
            target.Create();
            var pixels = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            try
            {
                camera.aspect = 16f / 9f;
                // StandardRequest runs the normal camera stack and updates Volumes; SingleCameraRequest skips that update.
                var request = new RenderPipeline.StandardRequest { destination = target };
                // Warm up URP/volumes before reading the actual gameplay camera, including post processing.
                RenderPipeline.SubmitRenderRequest(camera, request);
                RenderPipeline.SubmitRenderRequest(camera, request);
                var stack=VolumeManager.instance.stack;
                Debug.Log($"MV_MAIN_SHOT_GRADE: exposure={stack.GetComponent<ColorAdjustments>().postExposure.value} vignette={stack.GetComponent<Vignette>().intensity.value} grain={stack.GetComponent<FilmGrain>().intensity.value}");
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(output, pixels.EncodeToPNG());
                Debug.Log("MV_MAIN_SHOT_CAPTURE: " + output);
            }
            finally
            {
                camera.targetTexture = previousTarget;
                camera.aspect = previousAspect;
                RenderTexture.active = previous;
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(pixels);
            }
        }

        static string Argument(string key)
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, key);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
    }
}
