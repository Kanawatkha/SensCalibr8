using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SensCalibr8.Calibration.Editor
{
    public static class P0R3StandaloneBuild
    {
        private const string SceneFolder = "Assets/SensCalibr8/Calibration/Standalone";
        private const string ScenePath = SceneFolder + "/P0R3Capture.unity";
        private const string ExecutableName = "SensCalibr8Calibration.exe";

        [MenuItem("SensCalibr8/Calibration/Build P0-R3 Windows Standalone")]
        public static void BuildWindows()
        {
            EnsureStandaloneScene();
            ConfigurePlayer();

            string repositoryRoot = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", "..", ".."));
            string outputDirectory = Path.Combine(
                repositoryRoot,
                "calibration",
                "builds",
                "p0-r3");
            Directory.CreateDirectory(outputDirectory);
            string executablePath = Path.Combine(outputDirectory, ExecutableName);

            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = executablePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    "P0-R3 standalone build failed: " + report.summary.result);
            }

            string runtimeAssemblyName = "SensCalibr8.Calibration.Runtime.dll";
            string runtimeAssemblyPath = Path.Combine(
                outputDirectory,
                "SensCalibr8Calibration_Data",
                "Managed",
                runtimeAssemblyName);
            if (!File.Exists(runtimeAssemblyPath))
            {
                throw new FileNotFoundException(
                    "Built calibration runtime assembly was not found.",
                    runtimeAssemblyPath);
            }

            StandaloneBuildManifest manifest = new StandaloneBuildManifest
            {
                BuildId = "sc8-p0-r3-windows-standalone-v3",
                HarnessVersion = CalibrationHarnessMetadata.HarnessVersion,
                UnityVersion = Application.unityVersion,
                BuildTarget = report.summary.platform.ToString(),
                ExecutableName = ExecutableName,
                ExecutableSha256 = CalibrationFileSystem.ComputeSha256(executablePath),
                RuntimeAssemblyName = runtimeAssemblyName,
                RuntimeAssemblySha256 = CalibrationFileSystem.ComputeSha256(runtimeAssemblyPath),
                TotalBytes = report.summary.totalSize,
                BuiltUtc = DateTime.UtcNow.ToString("o"),
                StartupMode = "960x540-windowed",
                CaptureMode = "native-borderless-fullscreen",
                NetworkRequirement = "offline"
            };
            File.WriteAllText(
                Path.Combine(outputDirectory, "p0-r3-standalone-build-manifest.json"),
                JsonUtility.ToJson(manifest, true));

            Debug.Log("P0-R3 standalone build completed: " + executablePath);
        }

        private static void EnsureStandaloneScene()
        {
            if (!AssetDatabase.IsValidFolder(SceneFolder))
            {
                AssetDatabase.CreateFolder(
                    "Assets/SensCalibr8/Calibration",
                    "Standalone");
            }

            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                throw new IOException("Unable to save P0-R3 standalone scene.");
            }
        }

        private static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "SensCalibr8";
            PlayerSettings.productName = "SensCalibr8 P0-R3 Calibration";
            PlayerSettings.defaultScreenWidth = 960;
            PlayerSettings.defaultScreenHeight = 540;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = false;
            PlayerSettings.allowFullscreenSwitch = true;
            PlayerSettings.usePlayerLog = true;
        }

        [Serializable]
        private sealed class StandaloneBuildManifest
        {
            public string BuildId;
            public string HarnessVersion;
            public string UnityVersion;
            public string BuildTarget;
            public string ExecutableName;
            public string ExecutableSha256;
            public string RuntimeAssemblyName;
            public string RuntimeAssemblySha256;
            public ulong TotalBytes;
            public string BuiltUtc;
            public string StartupMode;
            public string CaptureMode;
            public string NetworkRequirement;
        }
    }
}
