using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace ConGame.Editor
{
    public static class ConGameBuildPipeline
    {
        private const string DevelopmentPath = "Builds/WindowsDev/ConGame.exe";
        private const string ReleasePath = "Builds/WindowsRelease/ConGame.exe";

        [MenuItem("ConGame/Build/Windows Development (Mono)", priority = 200)]
        public static void BuildWindowsDevelopment()
        {
            ConfigurePlayer(ScriptingImplementation.Mono2x);
            Build(DevelopmentPath, BuildOptions.Development | BuildOptions.AllowDebugging);
        }

        [MenuItem("ConGame/Build/Windows Release (IL2CPP)", priority = 201)]
        public static void BuildWindowsRelease()
        {
            ConfigurePlayer(ScriptingImplementation.IL2CPP);
            Build(ReleasePath, BuildOptions.CleanBuildCache);
        }

        public static void ConfigureReleaseSettings()
        {
            ConfigurePlayer(ScriptingImplementation.IL2CPP);
            AssetDatabase.SaveAssets();
            Debug.Log("[ConGame] Windows x64 Release settings configured for IL2CPP and DX11.");
        }

        private static void ConfigurePlayer(ScriptingImplementation backend)
        {
            PlayerSettings.companyName = "ConGame";
            PlayerSettings.productName = "困室";
            PlayerSettings.bundleVersion = "1.0.0";
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.runInBackground = false;
            PlayerSettings.forceSingleInstance = true;
            PlayerSettings.usePlayerLog = true;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, backend);
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(
                BuildTarget.StandaloneWindows64,
                new[] { GraphicsDeviceType.Direct3D11 });
        }

        private static void Build(string relativeOutputPath, BuildOptions options)
        {
            int validationErrors = ConGameProjectValidator.ValidateProject(true);
            if (validationErrors != 0)
            {
                throw new BuildFailedException($"ConGame validation failed with {validationErrors} error(s).");
            }

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Environment.CurrentDirectory;
            string outputPath = Path.GetFullPath(Path.Combine(projectRoot, relativeOutputPath));
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? projectRoot);

            string[] scenes = Array.ConvertAll(
                EditorBuildSettings.scenes,
                scene => scene.enabled ? scene.path : string.Empty);
            scenes = Array.FindAll(scenes, path => !string.IsNullOrWhiteSpace(path));

            BuildPlayerOptions buildOptions = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = options
            };

            BuildReport report = BuildPipeline.BuildPlayer(buildOptions);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new BuildFailedException(
                    $"ConGame build failed: {report.summary.result}, {report.summary.totalErrors} error(s).");
            }

            Debug.Log(
                $"[ConGame] Build succeeded: {outputPath} ({report.summary.totalSize} bytes, " +
                $"{report.summary.totalTime}).");
        }
    }
}
