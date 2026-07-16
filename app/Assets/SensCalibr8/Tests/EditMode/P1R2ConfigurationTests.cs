using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using SensCalibr8.Core.Configuration;
using SensCalibr8.Core.Domain;
using SensCalibr8.Services.Configuration;
using UnityEngine;

namespace SensCalibr8.Tests
{
    public sealed class P1R2ConfigurationTests
    {
        [Test]
        public void LoadsTheAcceptedCompleteProjection()
        {
            FrozenCalibrationConfiguration configuration = FrozenCalibrationConfigurationLoader.LoadFromRepository(RepositoryRoot());
            ParityManifest parity = LoadJson<ParityManifest>("config/production-config-parity-v1.json");
            Assert.That(configuration.ConfigVersion.Value, Is.EqualTo(parity.config_version));
            Assert.That(configuration.FormulaVersion.Value, Is.EqualTo(parity.formula_version));
            Assert.That(configuration.Sha256, Is.EqualTo(parity.config_sha256));
            Assert.That(configuration.SourceContracts, Has.Count.EqualTo(6));
            Assert.That(typeof(CalibrationConfigurationRecord).GetProperties(), Has.Length.EqualTo(parity.record_field_count));
            Assert.That(configuration.Record.NormalizationVersion, Is.EqualTo(parity.normalization_version));
            Assert.That(configuration.Record.SignalPipelineVersion, Is.EqualTo(parity.signal_pipeline_version));
            Assert.That(configuration.Record.TestGeometryVersion, Is.EqualTo(parity.test_geometry_version));
        }

        [Test]
        public void ImmutableContractsExposeNoPublicSetters()
        {
            AssertNoPublicSetters(typeof(FrozenCalibrationConfiguration));
            AssertNoPublicSetters(typeof(CalibrationConfigurationRecord));
            AssertNoPublicSetters(typeof(SourceContract));
        }

        [Test]
        public void LoaderRejectsAConfigurationWithTamperedIdentityEvenWhenTheEnvelopeHashIsUpdated()
        {
            string root = RepositoryRoot();
            string tempRoot = Path.Combine(Path.GetTempPath(), "senscalibr8-p1r2-" + Guid.NewGuid().ToString("N"));
            try
            {
                string plans = Path.Combine(tempRoot, "calibration", "plans");
                Directory.CreateDirectory(plans);
                string config = File.ReadAllText(Path.Combine(root, FrozenCalibrationConfigurationLoader.AcceptedConfigurationPath));
                config = config.Replace("\"status\": \"accepted\"", "\"status\": \"draft\"");
                File.WriteAllText(Path.Combine(plans, "calibration-config-v1.json"), config);
                string envelope = File.ReadAllText(Path.Combine(root, FrozenCalibrationConfigurationLoader.AcceptanceEnvelopePath));
                envelope = envelope.Replace("c618a3e50473b072b107d2e2926f4d05e7bbafa33bc04af8beb5eb5f775b3b2e", Sha256(Path.Combine(plans, "calibration-config-v1.json")));
                File.WriteAllText(Path.Combine(plans, "p0-r7-calibration-config-accepted-v1.json"), envelope);
                Assert.That(() => FrozenCalibrationConfigurationLoader.LoadFromRepository(tempRoot), Throws.TypeOf<InvalidDataException>().With.Message.Contains("Configuration identity mismatch: status"));
            }
            finally
            {
                if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
            }
        }

        [Test]
        public void DomainContractsHaveStableNamesWithoutUnityDependencies()
        {
            Assert.That(Enum.GetNames(typeof(TestMode)), Is.EquivalentTo(new[] { "FlickClose", "FlickFar", "Tracking", "MicroCorrection" }));
            Assert.That(Enum.GetNames(typeof(ProtocolPhase)), Is.EquivalentTo(new[] { "PhaseOne", "PhaseTwo", "PhaseThree" }));
            Assert.That(Enum.GetNames(typeof(PerformanceGrade)), Is.EquivalentTo(new[] { "S", "A", "B", "C", "D" }));
            Assert.That(typeof(TestMode).Assembly.GetName().Name, Is.EqualTo("SensCalibr8.Core"));
        }

        [Test]
        public void GeneralResearchConstantsAreTypedAndImmutable()
        {
            ResearchConstants constants = ResearchConstantsLoader.LoadFromRepository(RepositoryRoot());
            Assert.That(constants.PsaBaselineEdpi, Is.EqualTo(280d));
            Assert.That(constants.EdpiFloor, Is.EqualTo(160d));
            AssertNoPublicSetters(typeof(ResearchConstants));
        }

        private static void AssertNoPublicSetters(Type type)
        {
            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
                Assert.That(property.SetMethod, Is.Null, type.Name + "." + property.Name);
        }

        private static T LoadJson<T>(string relativePath) => JsonUtility.FromJson<T>(File.ReadAllText(Path.Combine(RepositoryRoot(), relativePath)));
        private static string RepositoryRoot() => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

        private static string Sha256(string path)
        {
            using System.Security.Cryptography.SHA256 algorithm = System.Security.Cryptography.SHA256.Create();
            return BitConverter.ToString(algorithm.ComputeHash(File.ReadAllBytes(path))).Replace("-", string.Empty).ToLowerInvariant();
        }

        [Serializable]
        private sealed class ParityManifest
        {
            public string config_version;
            public string formula_version;
            public string config_sha256;
            public int record_field_count;
            public string normalization_version;
            public string signal_pipeline_version;
            public string test_geometry_version;
        }
    }
}
