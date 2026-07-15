using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace SensCalibr8.Calibration.Tests
{
    public sealed class P0R7CalibrationConfigurationTests
    {
        [Test]
        public void FrozenConfigurationLoadsWithExpectedIdentityAndHash()
        {
            P0R7CalibrationSnapshot snapshot = LoadSnapshot();
            Assert.That(snapshot.ConfigVersion, Is.EqualTo("calibration_config_v1"));
            Assert.That(snapshot.FormulaVersion, Is.EqualTo("sc8-performance-score-v1"));
            Assert.That(snapshot.SignalPipelineVersion, Is.EqualTo("sc8-signal-pipeline-v1"));
            Assert.That(snapshot.TestGeometryVersion, Is.EqualTo("sc8-test-geometry-v1"));
            Assert.That(snapshot.Sha256, Has.Length.EqualTo(64));
        }

        [Test]
        public void EveryDatabaseFieldDeserializesAndScalarBindingsMatch()
        {
            P0R7CalibrationDatabaseRecord record = LoadSnapshot().Document.calibration_configs_record;
            Assert.That(record.config_version, Is.Not.Empty);
            Assert.That(record.normalization_version, Is.Not.Empty);
            Assert.That(record.signal_pipeline_version, Is.Not.Empty);
            Assert.That(record.test_geometry_version, Is.Not.Empty);
            Assert.That(record.created_date, Is.Not.Empty);
            Assert.That(record.input_sampling_rate_hz, Is.GreaterThan(0));
            Assert.That(record.resampling_tolerance_ms, Is.GreaterThan(0));
            Assert.That(record.timing_acceptance_policy, Is.Not.Empty);
            Assert.That(record.butterworth_order, Is.GreaterThan(0));
            Assert.That(record.cutoff_frequency_hz, Is.GreaterThan(0));
            Assert.That(record.submovement_start_deg_per_sec, Is.GreaterThan(0));
            Assert.That(record.submovement_end_deg_per_sec, Is.GreaterThan(0));
            Assert.That(record.refractory_period_ms, Is.GreaterThan(0));
            Assert.That(record.normalization_bounds_json, Is.Not.Empty);
            Assert.That(record.submovement_bounds_by_mode_json, Is.Not.Empty);
            Assert.That(record.consistency_tier_cutpoints_json, Is.Not.Empty);
            Assert.That(record.scoring_zero_tolerance, Is.GreaterThanOrEqualTo(0));
            Assert.That(record.target_geometry_json, Is.Not.Empty);
            Assert.That(record.tracking_contract_json, Is.Not.Empty);
            Assert.That(record.confirmatory_contract_json, Is.Not.Empty);
        }

        [Test]
        public void OwnerWaiverRemainsExplicitAndIsNotRelabeledStrictPass()
        {
            P0R7Limitations limitations = LoadSnapshot().Document.limitations;
            Assert.That(limitations.timing_acceptance, Is.EqualTo("accepted-by-project-owner-calibration-waiver"));
            Assert.That(limitations.strict_timing_confirmation_passed, Is.False);
            Assert.That(limitations.strict_candidate_v1_disposition, Is.EqualTo("rejected"));
            Assert.That(limitations.strict_candidate_v2_disposition, Is.EqualTo("rejected"));
            Assert.That(limitations.scientific_limitation, Is.Not.Empty);
        }

        [Test]
        public void SourceManifestHashesAreVerifiedDuringEveryLoad()
        {
            Assert.DoesNotThrow(() => LoadSnapshot());
            string original = LoadConfigText();
            string changed = original.Replace(
                "a60785d26869a54c89918d28189ce8f8ad4fcccd23ffdd7e94959a207be5ad56",
                new string('0', 64));
            Assert.That(changed, Is.Not.EqualTo(original));
            Assert.Throws<InvalidDataException>(() => LoadTemporary(changed));
        }

        [Test]
        public void DraftIncompleteAndMutatedConfigurationAreRejected()
        {
            string original = LoadConfigText();
            Assert.Throws<InvalidDataException>(() => LoadTemporary(original.Replace("\"status\": \"accepted\"", "\"status\": \"draft\"")));
            Assert.Throws<InvalidDataException>(() => LoadTemporary(original.Replace("\"immutable\": true", "\"immutable\": false")));
            Assert.Throws<InvalidDataException>(() => LoadTemporary(original.Replace("\"cutoff_frequency_hz\": 7.0", "\"cutoff_frequency_hz\": 8.0")));
        }

        [Test]
        public void FrozenConfigurationPreservesWorkedFormulaFixtures()
        {
            LoadSnapshot();
            Assert.That(P0R6ScoringStatisticsMath.ShotPerformanceScore(0.8, 0.9, 0.75, 0.6, 0.2), Is.EqualTo(77.0));
            Assert.That(P0R6ScoringStatisticsMath.TrackingPerformanceScore(0.8, 0.9, 0.7), Is.EqualTo(81.875));
            Assert.That(P0R6ScoringStatisticsMath.ShotPerformanceScore(0, 0, 0, 0, 1), Is.EqualTo(-10.0));
        }

        private static P0R7CalibrationSnapshot LoadSnapshot()
        {
            string root = RepositoryRoot();
            P0R7FreezeEvidence evidence = JsonUtility.FromJson<P0R7FreezeEvidence>(
                File.ReadAllText(Path.Combine(root, "calibration", "evidence", "p0-r7", "p0-r7-calibration-config-derived-v1.json")));
            return P0R7CalibrationConfigurationLoader.Load(
                Path.Combine(root, "calibration", "plans", "calibration-config-v1.json"),
                root,
                evidence.config_sha256);
        }

        private static string LoadConfigText()
        {
            return File.ReadAllText(Path.Combine(RepositoryRoot(), "calibration", "plans", "calibration-config-v1.json"));
        }

        private static void LoadTemporary(string json)
        {
            string root = RepositoryRoot();
            string path = Path.Combine(Path.GetTempPath(), "p0-r7-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                File.WriteAllText(path, json);
                string hash = Sha256(File.ReadAllBytes(path));
                P0R7CalibrationConfigurationLoader.Load(path, root, hash);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        private static string RepositoryRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."));
        }

        private static string Sha256(byte[] bytes)
        {
            using (var algorithm = System.Security.Cryptography.SHA256.Create())
            {
                return BitConverter.ToString(algorithm.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
            }
        }

        [Serializable]
        private sealed class P0R7FreezeEvidence { public string config_sha256; }
    }
}
