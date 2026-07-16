using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SensCalibr8.Editor
{
    public static class ProductionBuild
    {
        public const string BootstrapScenePath = "Assets/SensCalibr8/Scenes/ProductionBootstrap.unity";

        public static void PrepareProject()
        {
            EnsureBootstrapScene();
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(BootstrapScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("SensCalibr8 production project foundation prepared.");
        }

        public static void BuildWindows()
        {
            PrepareProject();
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
                ?? throw new InvalidOperationException("Unity project root is unavailable.");
            string outputPath = Environment.GetEnvironmentVariable("SENSCALIBR8_BUILD_PATH");
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                outputPath = Path.Combine(projectRoot, "Builds", "Windows", "SensCalibr8.exe");
            }

            string outputDirectory = Path.GetDirectoryName(outputPath)
                ?? throw new InvalidOperationException("Build output directory is unavailable.");
            Directory.CreateDirectory(outputDirectory);
            var options = new BuildPlayerOptions
            {
                scenes = new[] { BootstrapScenePath },
                locationPathName = outputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.StrictMode
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Production foundation build failed: {report.summary.result} ({report.summary.totalErrors} errors).");
            }

            Debug.Log($"Production foundation build passed: {outputPath}");
        }

        private static void EnsureBootstrapScene()
        {
            if (File.Exists(BootstrapScenePath))
            {
                return;
            }

            string directory = Path.GetDirectoryName(BootstrapScenePath)
                ?? throw new InvalidOperationException("Bootstrap scene directory is unavailable.");
            Directory.CreateDirectory(directory);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("SensCalibr8 Production Bootstrap");
            SceneManager.MoveGameObjectToScene(root, scene);
            if (!EditorSceneManager.SaveScene(scene, BootstrapScenePath))
            {
                throw new InvalidOperationException("Failed to save the production bootstrap scene.");
            }
        }
    }
}
