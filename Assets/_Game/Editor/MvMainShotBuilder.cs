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
            SessionState.SetBool(CapturePending, true);
            RestoreCaptureCallback();
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        static void RestoreCaptureCallback()
        {
            if (!SessionState.GetBool(CapturePending, false)) return;
            captureDeadline = EditorApplication.timeSinceStartup + 120;
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
            Application.runInBackground = true;
            try
            {
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

        static void RenderMainCamera()
        {
            var camera = Camera.main;
            if (camera == null) throw new InvalidOperationException("Room has no Main Camera.");
            string output = Argument("-mvScreenshot") ?? "/workspace/ref/progress.png";
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
                var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
                // Warm up URP/volumes before reading the actual gameplay camera, including post processing.
                RenderPipeline.SubmitRenderRequest(camera, request);
                RenderPipeline.SubmitRenderRequest(camera, request);
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
