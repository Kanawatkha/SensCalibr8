using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using NUnit.Framework;
using SensCalibr8.Core;
using SensCalibr8.Data;
using SensCalibr8.Services;
using SensCalibr8.TestLogic;
using SensCalibr8.UI;
using UnityEngine;

namespace SensCalibr8.Tests
{
    public sealed class ProductionFoundationTests
    {
        [Test]
        public void EnvironmentManifestPinsTheSupportedToolchain()
        {
            EnvironmentManifest manifest = LoadJson<EnvironmentManifest>("config/production-environment-v1.json");
            Assert.That(manifest.schema_version, Is.EqualTo("sc8-production-environment-v1"));
            Assert.That(manifest.status, Is.EqualTo("pinned"));
            Assert.That(manifest.offline_runtime_required, Is.True);
            Assert.That(manifest.unity.editor_version, Is.EqualTo("6000.5.3f1"));
            Assert.That(manifest.python.version, Is.EqualTo("3.12.13"));
        }

        [Test]
        public void UnityProjectAndInputSystemVersionsMatchTheManifest()
        {
            EnvironmentManifest environment = LoadJson<EnvironmentManifest>("config/production-environment-v1.json");
            UnityPackageManifest packages = LoadJson<UnityPackageManifest>("app/Packages/manifest.json");
            string projectVersion = File.ReadAllText(AtRoot("app/ProjectSettings/ProjectVersion.txt"));
            string playerSettings = File.ReadAllText(AtRoot("app/ProjectSettings/ProjectSettings.asset"));
            Assert.That(projectVersion, Does.Contain("m_EditorVersion: " + environment.unity.editor_version));
            Assert.That(packages.dependencies.com_unity_inputsystem, Is.EqualTo(environment.unity.input_system_version));
            Assert.That(packages.dependencies.com_unity_test_framework, Is.EqualTo(environment.unity.test_framework_version));
            Assert.That(playerSettings, Does.Contain("activeInputHandler: 1"));
        }

        [Test]
        public void FrozenCalibrationConfigMatchesThePinnedHash()
        {
            EnvironmentManifest environment = LoadJson<EnvironmentManifest>("config/production-environment-v1.json");
            string configPath = AtRoot(environment.calibration.config_path);
            Assert.That(File.Exists(configPath), Is.True, configPath);
            Assert.That(Sha256(configPath), Is.EqualTo(environment.calibration.config_sha256));
        }

        [Test]
        public void LayerAssembliesCompileAsDistinctBoundaries()
        {
            var assemblies = new HashSet<string>
            {
                typeof(CoreAssemblyMarker).Assembly.GetName().Name,
                typeof(DataAssemblyMarker).Assembly.GetName().Name,
                typeof(ServicesAssemblyMarker).Assembly.GetName().Name,
                typeof(TestLogicAssemblyMarker).Assembly.GetName().Name,
                typeof(UiAssemblyMarker).Assembly.GetName().Name
            };
            Assert.That(assemblies, Has.Count.EqualTo(5));
        }

        [Test]
        public void AssemblyDefinitionsEnforceApprovedDependencyDirection()
        {
            AssertReferences("Core", Array.Empty<string>(), true);
            AssertReferences("Data", new[] { "SensCalibr8.Core" }, true);
            AssertReferences("Services", new[] { "SensCalibr8.Core", "SensCalibr8.Data" }, true);
            AssertReferences("TestLogic", new[] { "SensCalibr8.Core", "SensCalibr8.Services", "Unity.InputSystem" }, false);
            AssertReferences("Integration", new[] { "SensCalibr8.Core", "SensCalibr8.Data", "SensCalibr8.Services", "SensCalibr8.TestLogic" }, false);
            AssertReferences("UI", new[] { "SensCalibr8.Core", "SensCalibr8.Services" }, false);
        }

        [Test]
        public void ProductionSourceDoesNotDuplicateCalibrationHarnessCode()
        {
            string assets = AtRoot("app/Assets/SensCalibr8");
            foreach (string path in Directory.GetFiles(assets, "*.cs", SearchOption.AllDirectories))
            {
                string testsSegment = Path.DirectorySeparatorChar + "Tests" + Path.DirectorySeparatorChar;
                if (path.Contains(testsSegment))
                {
                    continue;
                }
                string source = File.ReadAllText(path);
                Assert.That(source, Does.Not.Contain("SensCalibr8.Calibration"), path);
                Assert.That(source, Does.Not.Contain("calibration/harness"), path);
            }
        }

        [Test]
        public void BootstrapSceneIsPresentAndEnabledForBuild()
        {
            string scene = AtRoot("app/Assets/SensCalibr8/Scenes/ProductionBootstrap.unity");
            Assert.That(File.Exists(scene), Is.True, scene);
            string settings = File.ReadAllText(AtRoot("app/ProjectSettings/EditorBuildSettings.asset"));
            Assert.That(settings, Does.Contain("Assets/SensCalibr8/Scenes/ProductionBootstrap.unity"));
            Assert.That(settings, Does.Contain("enabled: 1"));
        }

        private static void AssertReferences(string layer, IReadOnlyCollection<string> expected, bool noEngineReferences)
        {
            string path = $"app/Assets/SensCalibr8/{layer}/SensCalibr8.{layer}.asmdef";
            AssemblyDefinition definition = LoadJson<AssemblyDefinition>(path);
            CollectionAssert.AreEquivalent(expected, definition.references ?? Array.Empty<string>(), path);
            Assert.That(definition.noEngineReferences, Is.EqualTo(noEngineReferences), path);
            Assert.That(definition.allowUnsafeCode, Is.False, path);
        }

        private static T LoadJson<T>(string relativePath)
        {
            string path = AtRoot(relativePath);
            Assert.That(File.Exists(path), Is.True, path);
            T value = JsonUtility.FromJson<T>(File.ReadAllText(path));
            Assert.That(value, Is.Not.Null, path);
            return value;
        }

        private static string AtRoot(string relativePath)
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            return Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        }

        private static string Sha256(string path)
        {
            using (SHA256 algorithm = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                return BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
            }
        }

        [Serializable]
        private sealed class EnvironmentManifest
        {
            public string schema_version;
            public string status;
            public bool offline_runtime_required;
            public UnityEnvironment unity;
            public PythonEnvironment python;
            public CalibrationEnvironment calibration;
        }

        [Serializable]
        private sealed class UnityEnvironment
        {
            public string editor_version;
            public string input_system_version;
            public string test_framework_version;
        }

        [Serializable]
        private sealed class PythonEnvironment { public string version; }

        [Serializable]
        private sealed class CalibrationEnvironment
        {
            public string config_path;
            public string config_sha256;
        }

        [Serializable]
        private sealed class UnityPackageManifest { public UnityDependencies dependencies; }

        [Serializable]
        private sealed class UnityDependencies
        {
            [SerializeField] private string com_unity_inputsystem_value;
            [SerializeField] private string com_unity_test_framework_value;

            public string com_unity_inputsystem => ReadDependency("com.unity.inputsystem", com_unity_inputsystem_value);
            public string com_unity_test_framework => ReadDependency("com.unity.test-framework", com_unity_test_framework_value);

            private string ReadDependency(string key, string fallback)
            {
                string json = File.ReadAllText(AtRoot("app/Packages/manifest.json"));
                string token = "\"" + key + "\": \"";
                int start = json.IndexOf(token, StringComparison.Ordinal);
                if (start < 0) return fallback;
                start += token.Length;
                int end = json.IndexOf('"', start);
                return end < 0 ? fallback : json.Substring(start, end - start);
            }
        }

        [Serializable]
        private sealed class AssemblyDefinition
        {
            public string[] references;
            public bool allowUnsafeCode;
            public bool noEngineReferences;
        }
    }
}
